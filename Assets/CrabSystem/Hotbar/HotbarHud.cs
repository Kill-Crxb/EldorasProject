using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HotbarHud — spawns the player's active bars into their anchors, and is the one thing that
/// knows the full set of bars that should be on screen.
///
/// That last part is the point. Before this existed, each ActionBarView was hand-placed and
/// found the player itself, so nothing anywhere could tell that a bar had no view: it simply
/// did not appear, with no error. Here, every active bar is accounted for or logged.
///
/// One of these per canvas. It also owns the AbilityPickerPanel reference, so the bar prefab
/// carries no scene references and stays a clean prefab.
/// </summary>
public class HotbarHud : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Handed to each spawned bar. Optional — bars without a picker button ignore it.")]
    [SerializeField] private AbilityPickerPanel abilityPickerPanel;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private ControllerBrain playerBrain;
    private HotbarSystem hotbarSystem;
    private SlotTransformationSystem transformSystem;

    private readonly List<ActionBarView> spawned = new List<ActionBarView>();

    void Start()
    {
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        if (hotbarSystem != null)
            hotbarSystem.OnActiveBarsChanged -= Rebuild;
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        playerBrain = PlayerBrainAccess.Find();
        if (playerBrain == null)
        {
            Debug.LogError("[HotbarHud] No player brain — no bars will be shown.");
            return;
        }

        hotbarSystem = playerBrain.GetModule<HotbarSystem>();
        transformSystem = playerBrain.GetModule<SlotTransformationSystem>();

        if (hotbarSystem == null)
        {
            Debug.LogError($"[HotbarHud] {playerBrain.name} has no HotbarSystem — no bars will be shown.");
            return;
        }

        hotbarSystem.OnActiveBarsChanged += Rebuild;
        Rebuild();
    }

    /// <summary>
    /// Tears down every spawned bar and rebuilds from the active list. A full rebuild rather
    /// than a diff because the active list changes rarely — on load, and when the player
    /// toggles a bar — and a diff would have to reason about anchor ordering anyway.
    /// </summary>
    public void Rebuild()
    {
        DespawnAll();

        if (hotbarSystem == null) return;

        foreach (var def in SortedActiveBars())
            Spawn(def);

        if (debugLogging)
            Debug.Log($"[HotbarHud] Rebuilt — {spawned.Count} bar(s) on screen.");
    }

    private List<ActionBarDefinition> SortedActiveBars()
    {
        var result = new List<ActionBarDefinition>();

        foreach (var def in hotbarSystem.GetAvailableBars())
        {
            if (def == null || !def.IsValid()) continue;
            if (!hotbarSystem.IsBarActive(def.barId)) continue;

            result.Add(def);
        }

        result.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
        return result;
    }

    private void Spawn(ActionBarDefinition def)
    {
        if (def.viewPrefab == null)
        {
            Debug.LogError($"[HotbarHud] Bar '{def.barId}' is active but has no viewPrefab — " +
                           $"it cannot be shown. Assign one on the {def.name} asset.");
            return;
        }

        var anchor = HotbarAnchorRegistry.Get(def.anchor);
        if (anchor == null)
        {
            Debug.LogError($"[HotbarHud] Bar '{def.barId}' wants anchor '{def.anchor}', which is " +
                           $"not in this scene — it cannot be shown. Add a HotbarAnchor with that id.");
            return;
        }

        var view = Instantiate(def.viewPrefab, anchor.transform);
        view.name = $"ActionBar_{def.barId}";
        view.Bind(def, hotbarSystem, transformSystem, abilityPickerPanel);

        spawned.Add(view);
    }

    private void DespawnAll()
    {
        foreach (var view in spawned)
        {
            if (view != null) Destroy(view.gameObject);
        }

        spawned.Clear();
    }
}
