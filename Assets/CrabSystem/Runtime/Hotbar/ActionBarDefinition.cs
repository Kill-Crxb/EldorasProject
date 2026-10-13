using UnityEngine;

/// <summary>
/// ActionBarDefinition — one bar, as an asset.
///
/// Everything that used to hold a bar's name now holds a reference to one of these instead:
/// HotbarSystem.availableBars, HotbarPageDefinition.suppliedBars, and the view spawned by
/// HotbarHud. A bar with no view, or a view naming a bar that does not exist, stops being a
/// reachable state.
///
/// ⚠ Nothing runtime-mutable belongs on this asset. Slot contents live in HotbarPageState;
/// whether the player has the bar switched on lives in HotbarSaveData.activeBarIds. Putting
/// either here would share one layout across every character and lose it in a build.
/// </summary>
[CreateAssetMenu(fileName = "New Action Bar", menuName = "RPG/Action Bar")]
public class ActionBarDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Save key for this bar's contents. Renaming the ASSET is free; renaming this " +
             "orphans whatever the player arranged on the bar. Treat it as write-once.")]
    public string barId;

    [Tooltip("Shown in UI and debug output.")]
    public string displayName;

    [Header("Shape")]
    [Tooltip("Slot count on a fresh character. Saves override this, not the other way round.")]
    [Range(1, 12)] public int slots = 4;

    [Tooltip("Rows is what makes a bar horizontal or vertical: the view divides slots by rows " +
             "to get its column count, so (2,1) is a horizontal pair and (3,3) a vertical stack.")]
    [Range(1, 3)] public int rows = 1;

    [Header("Presentation")]
    [Tooltip("Spawned by HotbarHud into the anchor below. Leave null and the bar logs an error " +
             "rather than quietly not appearing.")]
    public ActionBarView viewPrefab;

    [Tooltip("Which HotbarAnchor in the scene this bar spawns into.")]
    public HotbarAnchorId anchor = HotbarAnchorId.BottomCentre;

    [Tooltip("Order within the anchor when several bars share one. Low first.")]
    public int sortOrder;

    [Header("Availability")]
    [Tooltip("The player cannot switch this bar off.")]
    public bool alwaysOn;

    [Tooltip("On for a fresh character, before the player has chosen anything.")]
    public bool defaultActive = true;

    public bool IsValid() => !string.IsNullOrEmpty(barId);
}
