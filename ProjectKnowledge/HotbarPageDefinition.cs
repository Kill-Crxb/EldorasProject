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
    public string activationFact;

    [Tooltip("Higher wins when several pages' facts are true at once. Leave the default page at 0.")]
    public int priority = 0;

    [Header("Coverage")]
    [Tooltip("Bars this page supplies: centre, bottomLeft, bottomRight. Bars left out fall " +
             "through to the default page — that is how a consumables bar stays put across " +
             "every stance. Ignored on the default page, which always supplies everything.")]
    public List<string> suppliedBars = new List<string> { "centre", "bottomLeft" };

    /// <summary>True when this page has no activation fact, making it the fallback.</summary>
    public bool IsDefault => string.IsNullOrEmpty(activationFact);

    /// <summary>Does this page provide contents for the given bar?</summary>
    public bool Supplies(string barId)
    {
        if (IsDefault) return true;
        if (suppliedBars == null) return false;

        return suppliedBars.Contains(barId);
    }

    public bool IsValid() => !string.IsNullOrEmpty(pageId);
}
