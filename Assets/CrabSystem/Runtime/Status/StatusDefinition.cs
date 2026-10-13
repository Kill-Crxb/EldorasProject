using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a status IS. Authored once, shared by every bearer, never written to at runtime —
/// per-application state lives on StatusInstance.
///
/// A status is a NAMED, EXPIRING BUNDLE OF STAT CONTRIBUTIONS. That is the whole model, and
/// it is deliberately not an effect-scripting system. StatSystem already does keyed,
/// replace-on-re-add, exact-removal modification; the only thing missing was identity and a
/// clock, which is what this asset and StatusSystem supply.
///
/// So Stoneskin is a contribution to a mitigation stat, Wreathe is a contribution to
/// cmb.bonus_dice, Attune is a contribution to spell power. None of them need code, and gear
/// and talents reach the same stats through the same door without knowing statuses exist.
///
/// Anything genuinely reactive — Thorns answering a hit — is a small hand-written reader of
/// a stat, not a behaviour object hanging off this asset.
/// </summary>
[CreateAssetMenu(fileName = "Status_", menuName = "NinjaGame/Status/Status Definition")]
public class StatusDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique id, e.g. 'wreathe'. This is the whole identity of the status — " +
             "one instance per id per bearer, and 'status:{id}' is the stat contribution key. " +
             "Never change it after shipping; save data and authored references use it.")]
    public string id = "";

    [Tooltip("Name shown on the buff bar.")]
    public string displayName = "";

    [Tooltip("Icon for the buff bar.")]
    public Sprite icon;

    [Tooltip("What it does, in a sentence — the status bar's tooltip.")]
    [TextArea(1, 3)] public string description = "";

    [Tooltip("Helpful rather than harmful. Cleanse strips harmful; dispel strips helpful.")]
    public bool beneficial = true;

    [Tooltip("Groups talents and gear can name, e.g. control (hit states, roots). StatusFilter matches on these.")]
    public List<string> tags = new();

    public bool HasTag(string tag) => tags != null && tags.Contains(tag);

    [Header("Control and saves")]
    [Tooltip("Crowd control category (Combat_Framework §11): diminishing returns per category per target, and the " +
             "CC power vs tenacity rule. None for hit states and ordinary statuses.")]
    public ControlCategory control = ControlCategory.None;

    [Tooltip("The save the bearer rolls against it (Stat_Resolution §6): d20 + save stat vs 10 + the source's " +
             "matching stat. A success halves the duration, or swaps it for the downgrade. None = no save.")]
    public SaveType save = SaveType.None;

    [Tooltip("Applied instead on a successful save — knockdown becomes stagger, launch becomes flinch. Empty = halve.")]
    public StatusDefinition saveDowngrade;

    [Tooltip("Removed the moment the bearer takes damage (sleep, charm, pacify).")]
    public bool breaksOnDamage;

    [Header("Lifetime")]
    [Tooltip("Seconds. ZERO means it lasts until something removes it explicitly — a stance, " +
             "an aura you are standing in, a curse that needs cleansing.")]
    public float duration = 10f;

    [Tooltip("Frames at 60 fps. Above 0 it replaces duration — hit states, blockstun and anything " +
             "else authored against frame data. 0 = use seconds.")]
    public int durationFrames = 0;

    /// <summary>The lifetime every reader uses: frames when authored, otherwise seconds.</summary>
    public float Seconds => durationFrames > 0 ? durationFrames / 60f : duration;

    [Tooltip("What a second application does. See StatusStacking.")]
    public StatusStacking stacking = StatusStacking.Refresh;

    [Tooltip("Ceiling for Stack. Ignored by Refresh.")]
    public int maxStacks = 1;

    [Header("Effect")]
    [Tooltip("Stat contributions written while active, multiplied by the current stack count. " +
             "All of them are dropped together on removal — by key, so nothing subtracts a " +
             "remembered number.")]
    public List<StatContribution> contributions = new();

    [Tooltip("Blackboard facts held true while active, each from its own stack count. " +
             "Reference-counted by StatusSystem, so two statuses asserting the same fact do not " +
             "clear it out from under each other.")]
    public List<StatusFlag> blackboardFlags = new();

    [Header("Over time")]
    [Tooltip("Seconds between ticks; 0 = no ticks. Poison, burning and regeneration are statuses that tick on the " +
             "bearer, so saves, resists, immunity and cleanses reach them like any status (roadmap S14).")]
    public float tickSeconds = 0f;

    [Tooltip("Each tick deals tickDamage, rolled once per stack, as an unrolled tick hit from whoever applied it.")]
    public bool tickDealsDamage;
    public DiceRoll tickDamage = DiceRoll.D4();
    public DamageType tickDamageType = DamageType.Poison;

    [Tooltip("Each tick heals tickHeal, rolled once per stack.")]
    public bool tickHeals;
    public DiceRoll tickHeal = DiceRoll.D4();

    public bool Ticks => tickSeconds > 0f && (tickDealsDamage || tickHeals);

    [Header("Visuals")]
    [Tooltip("Aura spawned at the bearer's VFXSystem Aura anchor. Belongs to the PERSON, not " +
             "to whatever delivered the status — the projectile dies on impact, the glow does not.")]
    public GameObject auraVfx;

    [Tooltip("Where the aura sits: Aura (the body) for most; Overhead for a stun's stars.")]
    public VFXAnchor auraAnchor = VFXAnchor.Aura;

    [NonSerialized] private string cachedSourceKey;

    /// <summary>
    /// The stat contribution key for this status — "status:{id}".
    ///
    /// Cached because it is written once per contributed stat on every apply, stack change
    /// and removal, and string concatenation in that path allocates for no reason.
    /// </summary>
    public string SourceKey
    {
        get
        {
            if (string.IsNullOrEmpty(cachedSourceKey)) cachedSourceKey = "status:" + id;
            return cachedSourceKey;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // The id is half of every key this asset produces. Editing it in the inspector has to
        // invalidate the cache, or a renamed status keeps writing under its old key and its
        // contributions become unremovable.
        cachedSourceKey = null;

        if (duration < 0f) duration = 0f;
        if (tickSeconds < 0f) tickSeconds = 0f;
        if (durationFrames < 0) durationFrames = 0;
        if (maxStacks < 1) maxStacks = 1;
        if (stacking == StatusStacking.Refresh) maxStacks = 1;
    }
#endif
}

/// <summary>
/// What a second application of the same status does.
///
/// Both rules use ONE contribution key per id per bearer. Stack does arithmetic on the
/// amount rather than juggling N keys — same result, no key hygiene to get wrong.
///
/// Independent-per-source (two attackers, two separate clocks) is deliberately absent. It is
/// a damage-over-time concern, and adding it later is a key change — "status:{id}:{sourceId}"
/// — not a redesign.
/// </summary>
public enum ControlCategory { None, Hard, Root, SilenceDisarm, Snare, LossOfControl }

// Each save pairs the bearer's defence with the source's matching stat for the DC (Stat_Resolution §6).
public enum SaveType { None, Toughness, Alteration, Dexterity, Preparation }

public enum StatusStacking
{
    /// <summary>One instance. Re-applying resets the clock and changes nothing else.</summary>
    Refresh,

    /// <summary>One instance holding a count. Contributes amountPerStack x stacks, up to maxStacks.</summary>
    Stack,
}

/// <summary>One fact this status raises, once it holds enough stacks.</summary>
[Serializable]
public struct StatusFlag
{
    [IdRef(IdKind.Fact)] public string fact;

    [Tooltip("Held while the status has at least this many stacks. 1 = held whenever active. " +
             "A ladder like exhaustion raises CannotSprint at 2 and CannotAct at 5.")]
    public int minStacks;
}

/// <summary>One stat this status feeds, per stack.</summary>
[Serializable]
public struct StatContribution
{
    [Tooltip("Stat id, e.g. 'cmb.bonus_dice'. Must be loaded on the bearer — AddContribution " +
             "warns and bails for a stat whose schema the entity does not carry.")]
    [IdRef(IdKind.Stat)] public string statId;

    [Tooltip("Added per stack. Negative is fine — armour shred is a contribution too.")]
    public float amountPerStack;
}
