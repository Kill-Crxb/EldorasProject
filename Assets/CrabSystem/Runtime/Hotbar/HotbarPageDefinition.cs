using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HotbarPageDefinition — one swappable set of action bar contents, claimed by a blackboard fact.
///
/// A page is a whole loadout, not a substitution. When a page is active its bars hold their own
/// assignments; abilities on other pages are simply not present, so an armed ability can't be
/// fired bare-handed just because nobody authored an unarmed variant for it.
///
/// Activation:
///   HotbarSystem picks the highest-priority page whose activationFact is true on the entity's
///   blackboard, and falls back to the default page (the one with no fact) when none are.
///
/// Example set:
///   Page_Default   fact: (none)      priority: 0    supplies: everything
///   Page_Unarmed   fact: IsUnarmed   priority: 10   supplies: centre, bottomLeft
///   Page_Mounted   fact: IsMounted   priority: 20   supplies: centre
///
/// Adding a stance later is authoring: create the asset, name the fact, set something to write it.
/// Nothing in HotbarSystem knows what "unarmed" or "mounted" mean.
///
/// ⚠ A page decides what is IN a bar. Whether the player HAS that bar at all is
/// HotbarSystem.SetBarActive, saved per character. Do not implement "hide this bar" as a page —
/// a page supplying nothing behind an always-true fact looks like it works and then fights the
/// active list the first time both change together.
///
/// Not to be confused with SlotTransformationSystem, which morphs a single slot's ability
/// temporarily and reactively (hit procs, low-health finishers). Pages are the persistent
/// layout the player arranges; transformations are the transient overlay on top of it.
/// </summary>
[CreateAssetMenu(fileName = "New Hotbar Page", menuName = "RPG/Hotbar Page")]
public class HotbarPageDefinition : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique id, used as the save key for this page's bar contents. Renaming it orphans " +
             "whatever the player had arranged on the page.")]
    public string pageId;

    [Tooltip("Display name for UI and debugging")]
    public string displayName;

    [Header("Activation")]
    [Tooltip("Blackboard fact that claims this page while true — e.g. 'IsUnarmed'. " +
             "Leave empty to mark this the default page, which supplies every bar no other " +
             "active page has claimed.")]
    [IdRef(IdKind.Fact)] public string activationFact;

    [Tooltip("Higher wins when several pages' facts are true at once. Leave the default page at 0.")]
    public int priority = 0;

    [Header("Coverage")]
    [Tooltip("Bars this page supplies. Bars left out fall through to the default page — that is " +
             "how a consumables bar stays put across every stance. Ignored on the default page, " +
             "which always supplies everything.")]
    public List<ActionBarDefinition> suppliedBarAssets = new List<ActionBarDefinition>();

    [Tooltip("Legacy — the same list when bars were named by string. Still honoured so pages " +
             "authored before bars became assets keep working. Move entries up to " +
             "suppliedBarAssets and clear this.")]
    public List<string> suppliedBars = new List<string>();

    /// <summary>True when this page has no activation fact, making it the fallback.</summary>
    public bool IsDefault => string.IsNullOrEmpty(activationFact);

    /// <summary>
    /// Does this page provide contents for the given bar?
    ///
    /// Checks the asset list first, then the legacy string list. Keeping both means a page
    /// asset authored before this change is not silently emptied — a page that supplies nothing
    /// falls through to the default for every bar, which would quietly hand armed abilities
    /// back to an unarmed stance.
    /// </summary>
    public bool Supplies(string barId)
    {
        if (IsDefault) return true;
        if (string.IsNullOrEmpty(barId)) return false;

        if (suppliedBarAssets != null)
        {
            foreach (var bar in suppliedBarAssets)
            {
                if (bar != null && bar.barId == barId) return true;
            }
        }

        if (suppliedBars != null && suppliedBars.Contains(barId)) return true;

        return false;
    }

    public bool IsValid() => !string.IsNullOrEmpty(pageId);
}
