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

    [Tooltip("Helpful rather than harmful. Cleanse strips harmful; dispel strips helpful.")]
    public bool beneficial = true;

    [Header("Lifetime")]
    [Tooltip("Seconds. ZERO means it lasts until something removes it explicitly — a stance, " +
             "an aura you are standing in, a curse that needs cleansing.")]
    public float duration = 10f;

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

    [Header("Visuals")]
    [Tooltip("Aura spawned at the bearer's VFXSystem Aura anchor. Belongs to the PERSON, not " +
             "to whatever delivered the status — the projectile dies on impact, the glow does not.")]
    public GameObject auraVfx;

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
    public string fact;

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
    public string statId;

    [Tooltip("Added per stack. Negative is fine — armour shred is a contribution too.")]
    public float amountPerStack;
}
