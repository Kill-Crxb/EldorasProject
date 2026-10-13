using System.Collections.Generic;
using UnityEngine;

// What happened to this character that a talent can answer.
public enum TriggerEvent { HitLanded, HitTaken, Kill, AbilityUsed, Evade, Parry, Block, StatusGained }

// One talent trigger: when the event happens (and the filters pass, and the d20 meets the DC, and the internal
// cooldown is clear), every effect that is set fires. Copied from a TriggerReward and held under its key.
public struct TalentTrigger
{
    public TriggerEvent Event;
    public bool FilterAbility;
    public AbilityFilter Ability;
    public StatusDefinition Status;
    public int Dc;
    public float InternalCooldown;

    public ResourceDefinition Resource;
    public float ResourceAmount;
    public StatusDefinition ApplyStatus;
    public bool StatusOnTarget;
    public AbilityFilter CooldownFilter;
    public float CooldownSeconds;
    public AbilityDefinition Proc;
    public AbilityFilter ProcSlot;
    public float ProcSeconds;
    public StatusDefinition Spread;
    public float SpreadRadius;
}

// Talent triggers — "on dodge, regain stamina", "on kill, Dash Thrust is ready sooner", "on a perfect dodge your
// light attack becomes Shadow Riposte, once". Listens to this character's own events and runs the keyed triggers
// TriggerRewards put here. A trigger can't fire from inside another trigger's effects.
public class TriggerModule : MonoBehaviour, IBrainModule
{
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;

    // Gameplay time (EntityClock).
    private float Now => brain.Clock.Now;
    private float Delta => brain.Clock.Delta;
    private AbilitySystem abilities;
    private ResourceSystem resources;
    private DamageSystem damage;
    private GuardModule guard;
    private StatusSystem statuses;
    private SlotTransformationSystem slots;

    private readonly Dictionary<string, TalentTrigger> triggers = new();
    private readonly Dictionary<string, float> readyAt = new();
    private readonly List<string> firing = new();
    private bool inEffect;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        abilities = brain.Abilities;
        resources = brain.Resources;
        damage = brain.Damage;
        guard = brain.GetModule<GuardModule>();
        statuses = brain.GetModule<StatusSystem>();
        slots = brain.GetModule<SlotTransformationSystem>();

        if (abilities != null) abilities.OnHitLanded += HandleHitLanded;
        if (abilities != null) abilities.OnAbilityUsed += HandleAbilityUsed;
        if (damage != null) damage.OnKill += HandleKill;
        if (damage != null) damage.OnDamageTaken += HandleDamageTaken;
        if (damage != null) damage.OnEvaded += HandleEvaded;
        if (guard != null) guard.OnPerfectBlock += HandleParry;
        if (guard != null) guard.OnGuardedHit += HandleBlock;
        if (statuses != null) statuses.OnStatusApplied += HandleStatusApplied;
    }

    public void UpdateModule() { }

    private void OnDestroy()
    {
        if (abilities != null) abilities.OnHitLanded -= HandleHitLanded;
        if (abilities != null) abilities.OnAbilityUsed -= HandleAbilityUsed;
        if (damage != null) damage.OnKill -= HandleKill;
        if (damage != null) damage.OnDamageTaken -= HandleDamageTaken;
        if (damage != null) damage.OnEvaded -= HandleEvaded;
        if (guard != null) guard.OnPerfectBlock -= HandleParry;
        if (guard != null) guard.OnGuardedHit -= HandleBlock;
        if (statuses != null) statuses.OnStatusApplied -= HandleStatusApplied;
    }

    // A proc ability has to be one this character can use; it isn't learned, so it stays out of the spellbook.
    public void Set(string key, TalentTrigger trigger)
    {
        triggers[key] = trigger;

        // Talents apply from TalentModule's LateInitialize, which can run before this module's.
        AbilitySystem owner = brain != null ? brain.Abilities : null;
        if (trigger.Proc != null && owner != null && owner.GetAbility(trigger.Proc.abilityId) == null)
            owner.AddAbility(trigger.Proc);
    }

    public void Remove(string key)
    {
        triggers.Remove(key);
        readyAt.Remove(key);
    }

    // ---- Events ----

    private void HandleHitLanded(AbilityDefinition ability, ControllerBrain target) => Fire(TriggerEvent.HitLanded, ability, null, target);

    private void HandleAbilityUsed(string abilityId) => Fire(TriggerEvent.AbilityUsed, abilities.GetAbility(abilityId), null, null);

    private void HandleKill(ControllerBrain victim) => Fire(TriggerEvent.Kill, abilities != null ? abilities.CurrentAbility : null, null, victim);

    private void HandleDamageTaken(CombatDamagePacket packet) => Fire(TriggerEvent.HitTaken, null, null, Attacker(packet));

    private void HandleEvaded(CombatDamagePacket packet) => Fire(TriggerEvent.Evade, null, null, Attacker(packet));

    private void HandleParry() => Fire(TriggerEvent.Parry, null, null, null);

    private void HandleBlock(int stamina, float posture, int blockstun) => Fire(TriggerEvent.Block, null, null, null);

    private void HandleStatusApplied(StatusInstance instance) => Fire(TriggerEvent.StatusGained, null, instance.Definition, null);

    private static ControllerBrain Attacker(CombatDamagePacket packet)
        => packet.attacker != null ? packet.attacker.GetComponentInParent<ControllerBrain>() : null;

    // ---- Firing ----

    private void Fire(TriggerEvent happened, AbilityDefinition ability, StatusDefinition status, ControllerBrain target)
    {
        if (!IsEnabled || inEffect || triggers.Count == 0) return;

        firing.Clear();
        foreach (KeyValuePair<string, TalentTrigger> entry in triggers)
        {
            if (Passes(entry.Key, entry.Value, happened, ability, status)) firing.Add(entry.Key);
        }

        inEffect = true;
        foreach (string key in firing)
        {
            TalentTrigger trigger = triggers[key];
            if (trigger.InternalCooldown > 0f) readyAt[key] = Now + trigger.InternalCooldown;
            Run(trigger, target);
        }
        inEffect = false;
    }

    private bool Passes(string key, TalentTrigger trigger, TriggerEvent happened, AbilityDefinition ability, StatusDefinition status)
    {
        if (trigger.Event != happened) return false;
        if (readyAt.TryGetValue(key, out float ready) && Now < ready) return false;
        if (trigger.FilterAbility && (ability == null || !trigger.Ability.Matches(ability, abilities.Modifiers))) return false;
        if (trigger.Event == TriggerEvent.StatusGained && trigger.Status != null && (status == null || status.id != trigger.Status.id)) return false;
        return trigger.Dc <= 1 || DiceRoll.Die(20) >= trigger.Dc;
    }

    private void Run(TalentTrigger trigger, ControllerBrain target)
    {
        if (trigger.Resource != null && trigger.ResourceAmount > 0f && resources != null)
            resources.RestoreResource(trigger.Resource, trigger.ResourceAmount);

        ControllerBrain bearer = trigger.StatusOnTarget ? target : brain;
        StatusSystem bearerStatuses = bearer != null ? bearer.GetModule<StatusSystem>() : null;
        if (trigger.ApplyStatus != null && bearerStatuses != null) bearerStatuses.Apply(trigger.ApplyStatus, brain);

        if (trigger.CooldownSeconds > 0f && abilities != null) abilities.ReduceCooldowns(trigger.CooldownFilter, trigger.CooldownSeconds);

        if (trigger.Proc != null && slots != null) slots.ProcOnSlot(trigger.ProcSlot, trigger.Proc, trigger.ProcSeconds);

        if (trigger.Spread != null) Spread(trigger.Spread, trigger.SpreadRadius, target);
    }

    // Spread: the other fighter (the one hit or killed) carried the status, so it jumps to whoever is nearest them —
    // not this character, not them, not the dead — within the radius.
    private void Spread(StatusDefinition status, float radius, ControllerBrain from)
    {
        StatusSystem carrier = from != null ? from.GetModule<StatusSystem>() : null;
        if (carrier == null || !carrier.HadAtDeathOrHas(status.id)) return;

        ControllerBrain nearest = null;
        float best = radius * radius;
        foreach (ControllerBrain other in FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None))
        {
            if (other == brain || other == from || other.Damage == null || other.Damage.IsDead) continue;

            float distance = (Position(other) - Position(from)).sqrMagnitude;
            if (distance > best) continue;
            best = distance;
            nearest = other;
        }

        StatusSystem next = nearest != null ? nearest.GetModule<StatusSystem>() : null;
        if (next != null) next.Apply(status, brain);
    }

    private static Vector3 Position(ControllerBrain who) => who.EntityRoot != null ? who.EntityRoot.position : who.transform.position;
}
