using NinjaGame.Stats;
using RPG.Factions;
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuardOutcome { None, Blocked, Parried, Broken }

/// <summary>
/// Passed to OnDamageIntercept listeners while the defender is blocking. The guard (GuardModule)
/// says what it made of the hit; DamageSystem then resolves the damage for that outcome.
/// </summary>
public class DamageInterceptArgs
{
    public GuardOutcome outcome;
    public readonly Vector3 attackDirection;
    public readonly ControllerBrain attacker;
    public readonly float damage;   // base damage — what a block drains from the Guard bar
    public DamageInterceptArgs(Vector3 dir, ControllerBrain attacker, float damage)
    {
        attackDirection = dir;
        this.attacker = attacker;
        this.damage = damage;
    }
}

/// <summary>
/// Handles damage calculation, damage reception, and death for any entity with a Brain.
/// Includes automatic hurtbox setup for clean damage detection.
/// </summary>
public class DamageSystem : MonoBehaviour, IBrainModule
{
    public int InitOrder => 120;

    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Header("Hurtbox Configuration")]
    [Tooltip("The collider that receives damage. Auto-created if null.")]
    [SerializeField] private Collider hurtbox;
    [SerializeField] private bool autoSetupHurtbox = true;
    [SerializeField] private float hurtboxRadius = 0.5f;
    [SerializeField] private float hurtboxHeight = 2f;
    [SerializeField] private Vector3 hurtboxCenter = new Vector3(0, 1, 0);

    [Header("Counter-hit (CF4)")]
    [Tooltip("Added to the attacker's roll when this fighter is hit during its own move's startup.")]
    [SerializeField] private int counterHitAccuracy = 2;
    [Tooltip("Added to a counter-hit's damage before the armour shield. 0 = off.")]
    [SerializeField] private int counterHitDamage = 0;

    // IBrainModule implementation
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private IStatProvider stats;
    private IHealthProvider health;
    private Blackboard blackboard;
    private GuardModule guard;
    private ArmourShieldModule shield;

    private bool isDead;
    private ControllerBrain incomingAttacker;

    // Stat_Resolution.md §4. Defense = 5 + Avoidance + armour's to-hit bonus.
    private const float BaseDefense = 5f;
    private const string AccuracyStat = "cmb.finesse";
    private const string AvoidanceStat = "def.avoidance";
    private const string ArmourDefenseStat = "atr.arm_def";

    // Public accessors
    public ControllerBrain Brain => brain;
    public Collider Hurtbox => hurtbox;
    public bool IsDead => isDead;
    // Whoever landed the killing hit. Null for deaths that came from no attacker, and for self-kills.
    public ControllerBrain Killer { get; private set; }
    // Whether the hit being taken right now (or the last one) was blocked or parried. Read by the
    // attacker's hit-state step in the same call chain; a guard that's up but flanked doesn't count (B29).
    public bool LastHitGuarded { get; private set; }

    public event Action<float> OnHealthChanged;
    public event Action<float, float> OnHealthChangedDetailed;
    public event Action OnDeath;
    // Victim side, right after OnDeath. The killer may be null.
    public event Action<ControllerBrain> OnKilledBy;
    // Attacker side — this entity landed a killing hit on the victim passed in.
    public event Action<ControllerBrain> OnKill;
    public event Action<CombatDamagePacket> OnDamageDealt;
    public event Action<CombatDamagePacket> OnDamageTaken;
    // Same moment as OnDamageTaken, with the damage actually applied — post-mitigation, post-block.
    public event Action<CombatDamagePacket, float> OnDamageApplied;
    // Same moment again, with the whole breakdown — roll, Defense, grade, soak. For logs and talents.
    public event Action<CombatDamagePacket, HitResolution> OnHitResolved;
    // Fired before damage is applied when the target is blocking. The guard sets args.outcome.
    public event Action<DamageInterceptArgs> OnDamageIntercept;

    private void Awake()
    {
        if (autoSetupHurtbox)
            SetupHurtbox();
    }

    private void SetupHurtbox()
    {
        if (hurtbox != null)
            return;

        var hurtboxGO = new GameObject("Hurtbox");
        hurtboxGO.transform.SetParent(transform);
        hurtboxGO.transform.localPosition = Vector3.zero;
        hurtboxGO.transform.localRotation = Quaternion.identity;

        // New GameObjects default to layer 0 (Default), which weapon hitLayers
        // masks don't include — the hurtbox would be invisible to strikes (StrikeHandler).
        // Use the project's Hurtbox layer, falling back to this GO's own layer.
        int hurtboxLayer = LayerMask.NameToLayer("Hurtbox");
        hurtboxGO.layer = hurtboxLayer >= 0 ? hurtboxLayer : gameObject.layer;

        var capsule = hurtboxGO.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = hurtboxRadius;
        capsule.height = hurtboxHeight;
        capsule.center = hurtboxCenter;

        hurtbox = capsule;
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        stats = brain.Stats;
        health = brain.Resources;
        blackboard = brain.GetModule<BlackboardSystem>()?.Blackboard;
        guard = brain.GetModule<GuardModule>();
        shield = brain.GetModule<ArmourShieldModule>();

        if (stats == null || health == null)
        {
            isEnabled = false;
            Debug.LogError($"[DamageSystem] Missing dependencies on {brain.EntityName} - Stats: {stats != null}, Health: {health != null}");
            return;
        }

        health.OnDeath += Die;
        health.OnHealthChanged += HandleHealthChanged;
    }

    public void UpdateModule() { }

    private void HandleHealthChanged(float current)
    {
        OnHealthChanged?.Invoke(current);
        OnHealthChangedDetailed?.Invoke(current, health.GetMaxHealth());
    }

    public CombatDamagePacket CalculateDamage(CombatAttackData attackData)
    {
        var config = DamageManager.Instance?.ActiveConfig?.GetConfig(attackData.damageType);

        // The config's attacker stats are rolled as dice (Might 6 → 1d6). A tick carries none —
        // it was paid for when the effect landed.
        float damage = attackData.baseDamage;
        if (attackData.source != DamageSource.Tick)
            damage += RollStatDice(config?.attackerStatIds, config?.attackerStatMultipliers);

        // Attack context multipliers (<= 0 treated as unset → ×1)
        float multiplier = 1f;
        if (attackData.comboMultiplier > 0f) multiplier *= attackData.comboMultiplier;
        if (attackData.weaponDamageMultiplier > 0f) multiplier *= attackData.weaponDamageMultiplier;
        if (attackData.isHeavyAttack && attackData.heavyAttackMultiplier > 0f)
            multiplier *= attackData.heavyAttackMultiplier;

        damage *= multiplier;

        float accuracy = stats != null ? stats.GetValue(AccuracyStat) : 0f;

        // Combat_Framework §3.2: a parry's riposte makes this entity's next attack a read. The attacker
        // owns it, so the packet carries it; the defender rolls twice keeping the higher. Ticks never
        // roll, so they don't spend it.
        bool advantage = attackData.source != DamageSource.Tick && guard != null && guard.TakeRiposte();

        // NOTE: no mitigation here — the ATTACKER builds this packet, so it has
        // no business reading defender stats. Mitigation is applied by the
        // defender in TakeDamage(), using the defender's own StatSystem.
        var packet = new CombatDamagePacket(
            attackData.baseDamage,
            damage,
            attackData.damageType,
            attackData.attackerTransform,
            attackData.attackerTransform?.name ?? "Unknown",
            attackData.hitPoint,
            attackData.hitNormal,
            attackData.hitPoint - (attackData.attackerTransform?.position ?? Vector3.zero),
            attackData.comboCount,
            attackData.isHeavyAttack,
            attackData.weaponId ?? "",
            attackData.source,
            accuracy,
            advantage
        );

        OnDamageDealt?.Invoke(packet);
        return packet;
    }

    /// <summary>
    /// Apply an incoming damage packet. The hit roll and soak run HERE, on the defender, using
    /// this entity's own stats. Returns the damage actually applied to health.
    /// </summary>
    public float TakeDamage(CombatDamagePacket packet)
    {
        if (!isEnabled || isDead) return 0f;
        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsInvincible)) return 0f;

        ControllerBrain attackerBrain = packet.attacker != null
            ? packet.attacker.GetComponentInParent<ControllerBrain>()
            : null;

        GuardOutcome guard = AskGuard(packet, attackerBrain);
        bool guarded = guard == GuardOutcome.Blocked || guard == GuardOutcome.Parried;
        LastHitGuarded = guarded;

        HitResolution hit = guarded ? ResolveGuardedHit(packet, guard) : ResolveHit(packet, attackerBrain);
        float dmg = hit.applied;

        // Faction damage modifier — per-relationship multiplier from the
        // FactionRelationships matrix (1.0 when factions unknown or unlisted).
        if (attackerBrain != null && attackerBrain.Faction != null && brain != null && brain.Faction != null)
            dmg *= FactionManager.GetDamageModifier(
                attackerBrain.Faction.CurrentFaction,
                brain.Faction.CurrentFaction);

        // Health raises OnDeath from inside ApplyDamage, so the attacker is parked here for Die.
        incomingAttacker = attackerBrain != brain ? attackerBrain : null;
        health.ApplyDamage(dmg);
        incomingAttacker = null;

        hit.applied = dmg;

        OnDamageTaken?.Invoke(packet);
        OnDamageApplied?.Invoke(packet, dmg);
        OnHitResolved?.Invoke(packet, hit);
        return dmg;
    }

    // The guard decides first, so a guard broken by this hit lets it through as a normal hit. Ticks
    // aren't guarded, and neither is a Perilous move: it can't be blocked or parried, only dodged.
    private GuardOutcome AskGuard(CombatDamagePacket packet, ControllerBrain attackerBrain)
    {
        if (OnDamageIntercept == null || blackboard == null) return GuardOutcome.None;
        if (packet.source == DamageSource.Tick) return GuardOutcome.None;
        if (Perilous(attackerBrain)) return GuardOutcome.None;
        if (!blackboard.GetBool(BlackboardKey.IsBlocking)) return GuardOutcome.None;

        var args = new DamageInterceptArgs(packet.attackDirection, attackerBrain, packet.finalDamage);
        OnDamageIntercept.Invoke(args);
        return args.outcome;
    }

    private static bool Perilous(ControllerBrain attackerBrain)
    {
        ICombatantState attacker = attackerBrain != null ? attackerBrain.GetProvider<ICombatantState>() : null;
        return attacker != null && attacker.CurrentAbility != null && attacker.CurrentAbility.hit.guard == GuardType.Perilous;
    }

    // Combat_Framework §3.1–3.2: no chip. A guarded hit never touches health — a block's damage went to
    // the Guard bar and posture (GuardModule), a parry's to nobody. Recorded as fully absorbed.
    private HitResolution ResolveGuardedHit(CombatDamagePacket packet, GuardOutcome guard)
    {
        return new HitResolution
        {
            grade = guard == GuardOutcome.Parried ? HitGrade.Parried : HitGrade.Blocked,
            rolled = packet.finalDamage,
            soak = packet.finalDamage,
            applied = 0f
        };
    }

    // Stat_Resolution.md §4.1. Ticks skip the roll and the shield.
    private HitResolution ResolveHit(CombatDamagePacket packet, ControllerBrain attackerBrain)
    {
        var hit = new HitResolution { grade = HitGrade.Unrolled, rolled = packet.finalDamage };

        if (packet.source == DamageSource.Tick)
        {
            hit.applied = packet.finalDamage;
            return hit;
        }

        hit.accuracy = packet.accuracy;
        hit.defense = BaseDefense + Stat(AvoidanceStat) + Stat(ArmourDefenseStat);
        hit.advantage = packet.advantage;
        hit.counter = InStartup();
        if (hit.counter) hit.accuracy += counterHitAccuracy;
        hit.roll = UnityEngine.Random.Range(1, 21);
        if (hit.advantage) hit.roll = Mathf.Max(hit.roll, UnityEngine.Random.Range(1, 21));

        bool full = hit.roll + hit.accuracy >= hit.defense;
        hit.grade = full ? HitGrade.Full : HitGrade.Glancing;
        hit.rolled = full ? packet.finalDamage : Mathf.Floor(packet.finalDamage / 2f);
        if (hit.counter) hit.rolled += counterHitDamage;

        // Contact always costs something: the hit is worth at least 1. The armour shield then takes what
        // it can of a physical hit (Combat_Framework §6.1); everything else goes past it.
        float worth = hit.rolled > 0f ? Mathf.Max(1f, hit.rolled) : 0f;
        hit.applied = AbsorbByShield(worth, packet.damageType);
        hit.soak = worth - hit.applied;
        return hit;
    }

    // Caught winding up: an attack in flight hasn't reached its first strike. Moves without frame data (a dash,
    // a spell) don't count.
    private bool InStartup()
    {
        ICombatantState self = brain != null ? brain.GetProvider<ICombatantState>() : null;
        if (self == null || self.CurrentAbility == null || !self.CurrentAbility.HasMoveData) return false;
        return self.CurrentPhase == MovePhase.Startup;
    }

    private float AbsorbByShield(float amount, DamageType type)
    {
        if (shield == null || type != DamageType.Physical) return amount;
        return shield.Absorb(amount);
    }

    // Each config stat rolled as its own die, scaled by its paired multiplier first.
    // Missing/short multiplier lists default to ×1.
    private float RollStatDice(List<string> statIds, List<float> multipliers)
    {
        if (stats == null || statIds == null) return 0f;

        float total = 0f;
        for (int i = 0; i < statIds.Count; i++)
        {
            float mult = (multipliers != null && i < multipliers.Count) ? multipliers[i] : 1f;
            total += DiceRoll.RollModifier(stats.GetValue(statIds[i]) * mult);
        }
        return total;
    }

    private float Stat(string statId) => stats != null ? stats.GetValue(statId) : 0f;

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Killer = incomingAttacker;

        OnDeath?.Invoke();
        OnKilledBy?.Invoke(Killer);
        if (Killer != null && Killer.Damage != null) Killer.Damage.RaiseKill(brain);
    }

    private void RaiseKill(ControllerBrain victim)
    {
        OnKill?.Invoke(victim);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (hurtbox != null)
            hurtbox.isTrigger = true;
    }
#endif
}
