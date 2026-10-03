using NinjaGame.Stats;
using RPG.Factions;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mutable args passed to OnDamageIntercept listeners so they can modify incoming damage.
/// AbilitySystem subscribes while a defensive ability is active.
/// </summary>
public class DamageInterceptArgs
{
    public float damage;
    public readonly Vector3 attackDirection;
    public readonly ControllerBrain attacker;
    public DamageInterceptArgs(float damage, Vector3 dir, ControllerBrain attacker)
    {
        this.damage = damage;
        attackDirection = dir;
        this.attacker = attacker;
    }
}

/// <summary>
/// Handles damage calculation, damage reception, and death for any entity with a Brain.
/// Includes automatic hurtbox setup for clean damage detection.
/// </summary>
public class DamageSystem : MonoBehaviour, IBrainModule
{
    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Header("Hurtbox Configuration")]
    [Tooltip("The collider that receives damage. Auto-created if null.")]
    [SerializeField] private Collider hurtbox;
    [SerializeField] private bool autoSetupHurtbox = true;
    [SerializeField] private float hurtboxRadius = 0.5f;
    [SerializeField] private float hurtboxHeight = 2f;
    [SerializeField] private Vector3 hurtboxCenter = new Vector3(0, 1, 0);

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

    private bool isDead;
    private ControllerBrain incomingAttacker;

    // Stat_Resolution.md §4. Defense = 5 + Avoidance + armour's to-hit bonus.
    private const float BaseDefense = 5f;
    private const string AccuracyStat = "cmb.finesse";
    private const string CunningStat = "cmb.cunning";
    private const string AvoidanceStat = "def.avoidance";
    private const string ArmourDefenseStat = "atr.arm_def";
    private const string ArmourDiceStat = "atr.arm_dice";
    private const string ArmourStat = "atr.arm";
    private const string GuardBrokenStatusId = "guardbroken";

    // Public accessors
    public ControllerBrain Brain => brain;
    public Collider Hurtbox => hurtbox;
    public bool IsDead => isDead;
    // Whoever landed the killing hit. Null for deaths that came from no attacker, and for self-kills.
    public ControllerBrain Killer { get; private set; }

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
    // Fired before damage is applied when the target is blocking. Listeners may reduce args.damage.
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
        // masks don't include — the hurtbox would be invisible to WeaponHitbox.
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
        health = brain.ResourceSys;
        blackboard = brain.GetModule<BlackboardSystem>()?.Blackboard;

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

        // A crit is an explosion. Each one adds a Cunning die, in the exploded part.
        float explosion = attackData.explosionDamage + RollCunningDice(attackData.explosions);

        // Attack context multipliers (<= 0 treated as unset → ×1)
        float multiplier = 1f;
        if (attackData.comboMultiplier > 0f) multiplier *= attackData.comboMultiplier;
        if (attackData.weaponDamageMultiplier > 0f) multiplier *= attackData.weaponDamageMultiplier;
        if (attackData.isHeavyAttack && attackData.heavyAttackMultiplier > 0f)
            multiplier *= attackData.heavyAttackMultiplier;

        damage *= multiplier;
        explosion *= multiplier;

        bool crit = attackData.explosions > 0;
        float critMult = 1f;
        float accuracy = stats != null ? stats.GetValue(AccuracyStat) : 0f;

        // NOTE: no mitigation here — the ATTACKER builds this packet, so it has
        // no business reading defender stats. Mitigation is applied by the
        // defender in TakeDamage(), using the defender's own StatSystem.
        var packet = new CombatDamagePacket(
            attackData.baseDamage,
            damage,
            crit,
            critMult,
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
            explosion,
            attackData.explosions,
            accuracy
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

        HitResolution hit = ResolveHit(packet, attackerBrain);
        float dmg = hit.applied;

        // Faction damage modifier — per-relationship multiplier from the
        // FactionRelationships matrix (1.0 when factions unknown or unlisted).
        if (attackerBrain != null && attackerBrain.Faction != null && brain != null && brain.Faction != null)
            dmg *= FactionManager.GetDamageModifier(
                attackerBrain.Faction.CurrentFaction,
                brain.Faction.CurrentFaction);

        if (OnDamageIntercept != null && blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking))
        {
            var args = new DamageInterceptArgs(dmg, packet.attackDirection, attackerBrain);
            OnDamageIntercept.Invoke(args);
            dmg = args.damage;
        }

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

    // Stat_Resolution.md §4, steps 2 and 4. Ticks skip both: no roll, no soak.
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
        hit.advantage = HasAdvantage(attackerBrain);
        hit.roll = UnityEngine.Random.Range(1, 21);
        if (hit.advantage) hit.roll = Mathf.Max(hit.roll, UnityEngine.Random.Range(1, 21));

        bool full = hit.roll + hit.accuracy >= hit.defense;
        hit.grade = full ? HitGrade.Full : HitGrade.Glancing;
        hit.rolled = full
            ? packet.finalDamage + packet.explosionDamage
            : Mathf.Floor(packet.finalDamage / 2f);

        // Contact always costs something: the floor is 1. Negative soak (Sunder) adds damage.
        hit.soak = RollSoak(packet.damageType);
        hit.applied = hit.rolled > 0f ? Mathf.Max(1f, hit.rolled - hit.soak) : 0f;
        return hit;
    }

    // (arm_dice)d4 + arm flat. Physical takes all of it, True none, everything else half.
    private float RollSoak(DamageType type)
    {
        if (type == DamageType.True) return 0f;

        float soak = Stat(ArmourStat);
        int dice = Mathf.FloorToInt(Stat(ArmourDiceStat));
        for (int i = 0; i < dice; i++) soak += UnityEngine.Random.Range(1, 5);

        if (type == DamageType.Physical) return soak;
        return Mathf.Floor(soak / 2f);
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

    private float RollCunningDice(int explosions)
    {
        float total = 0f;
        for (int i = 0; i < explosions; i++) total += DiceRoll.RollModifier(Stat(CunningStat));
        return total;
    }

    private float Stat(string statId) => stats != null ? stats.GetValue(statId) : 0f;

    // Combat_Framework §3–4: a guard-broken target is open (the deathblow), and a parry's riposte
    // makes the defender's next hit a read. Either rolls the d20 twice and keeps the higher.
    private bool HasAdvantage(ControllerBrain attackerBrain)
    {
        StatusSystem statuses = brain != null ? brain.GetModule<StatusSystem>() : null;
        if (statuses != null && statuses.Has(GuardBrokenStatusId)) return true;

        AbilitySystem attackerAbilities = attackerBrain != null ? attackerBrain.GetModule<AbilitySystem>() : null;
        return attackerAbilities != null && attackerAbilities.TakeRiposte();
    }

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
