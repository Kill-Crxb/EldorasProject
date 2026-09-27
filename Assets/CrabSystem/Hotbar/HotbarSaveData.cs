using System;
using System.Collections.Generic;

/// <summary>
/// One page's bar contents as stored in the save.
///
/// Bars are a list keyed by barId rather than named fields, so adding a bar is authoring an
/// ActionBarDefinition rather than editing switch statements. JsonUtility cannot serialise a
/// dictionary but serialises a List of a [Serializable] class fine.
/// </summary>
[Serializable]
public class HotbarPageState
{
    public string pageId;
    public List<ActionBarConfig> bars = new List<ActionBarConfig>();

    // ── v2 legacy — read on load, never written ───────────────────────────
    public ActionBarConfig centreBar;
    public ActionBarConfig bottomLeftBar;
    public ActionBarConfig bottomRightBar;

    public ActionBarConfig Find(string barId)
    {
        if (bars == null || string.IsNullOrEmpty(barId)) return null;

        foreach (var bar in bars)
        {
            if (bar != null && bar.barId == barId) return bar;
        }

        return null;
    }

    /// <summary>
    /// Folds the v2 named fields into the list. Idempotent — a bar already in the list wins, and
    /// an empty slot list means the field was never written, which is what JsonUtility leaves
    /// behind for a null reference type.
    /// </summary>
    public void MigrateNamedBars()
    {
        if (bars == null) bars = new List<ActionBarConfig>();

        AdoptNamedBar(centreBar, "centre");
        AdoptNamedBar(bottomLeftBar, "bottomLeft");
        AdoptNamedBar(bottomRightBar, "bottomRight");

        centreBar = null;
        bottomLeftBar = null;
        bottomRightBar = null;
    }

    private void AdoptNamedBar(ActionBarConfig bar, string barId)
    {
        if (bar == null || bar.slots == null || bar.slots.Count == 0) return;
        if (Find(barId) != null) return;

        bar.barId = barId;
        bars.Add(bar);
    }
}

/// <summary>
/// Root object that gets JSON-serialised by HotbarSystem.
///
/// v1 stored three bars at the root. v2 stored a list of pages, each with three named bars.
/// v3 stored a list of pages, each with a list of bars. v4 adds activeBarIds — which bars the
/// player has switched on. Older shapes are kept on the class and folded forward on load.
///
/// ⚠ activeBarIds lives here and NOT on ActionBarDefinition. The asset is authored data shared by
/// every character; which bars are on is per-character runtime state.
/// </summary>
[Serializable]
public class HotbarSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;

    public List<HotbarPageState> pages = new List<HotbarPageState>();

    /// <summary>
    /// barIds the player has switched on.
    /// </summary>
    public List<string> activeBarIds = new List<string>();

    /// <summary>
    /// False on a v3 save and on a fresh character, meaning "fill from each bar's defaultActive".
    /// ⚠ This exists because an empty activeBarIds is ambiguous: it is also what you get when the
    /// player deliberately switches every optional bar off. Without this flag that choice would
    /// be silently undone on the next load.
    /// </summary>
    public bool hasChosenBars;

    // ── v1 legacy — read on load, never written ───────────────────────────
    public ActionBarConfig centreBar;
    public ActionBarConfig bottomLeftBar;
    public ActionBarConfig bottomRightBar;
}
