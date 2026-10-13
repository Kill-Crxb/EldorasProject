using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Binds every row beneath this view to the player.
///
/// Two kinds of row, two sources. Stat rows read the stat store, which holds ceilings and
/// core values. Resource rows read the resource pools, which hold the live current values
/// and exist nowhere in the stat store. Both subscribe once and dispatch by key, so rows
/// stay passive.
/// </summary>
public class StatPanelView : UIPanelView
{
    [Header("Source")]
    [Tooltip("Leave empty to bind the player found at runtime.")]
    [SerializeField] private ControllerBrain sourceBrain;

    private readonly Dictionary<string, List<StatEntryView>> rowsByStat = new();
    private readonly Dictionary<ResourceDefinition, List<ResourceEntryView>> rowsByResource = new();

    private IStatProvider stats;
    private IResourceProvider pools;

    protected override void OnShown()
    {
        if (sourceBrain == null) sourceBrain = PlayerBrainAccess.Find();

        if (sourceBrain == null)
        {
            Debug.LogWarning($"[StatPanelView] no player brain for {name}");
            return;
        }

        if (!sourceBrain.IsInitialized)
        {
            sourceBrain.OnInitialized += HandleBrainReady;
            return;
        }

        Bind(sourceBrain.Stats, sourceBrain.Resources);
    }

    protected override void OnHidden()
    {
        if (sourceBrain != null) sourceBrain.OnInitialized -= HandleBrainReady;
        Unbind();
    }

    private void HandleBrainReady(ControllerBrain ready)
    {
        ready.OnInitialized -= HandleBrainReady;
        Bind(ready.Stats, ready.Resources);
    }

    /// <summary>Stats only. Kept for callers that predate resource rows.</summary>
    public void Bind(IStatProvider statProvider) => Bind(statProvider, null);

    public void Bind(IStatProvider statProvider, IResourceProvider resourceProvider)
    {
        Unbind();

        stats = statProvider;
        pools = resourceProvider;

        rowsByStat.Clear();
        rowsByResource.Clear();

        int missing = 0;

        foreach (var group in GetComponentsInChildren<StatGroupView>(true))
        {
            group.ApplyTitle();

            int live = 0;

            live += BindStatRows(group, ref missing);
            live += BindResourceRows(group, ref missing);

            group.SetEmpty(live == 0);
        }

        if (stats != null) stats.OnStatChanged += HandleStatChanged;
        if (pools != null) pools.OnResourceChanged += HandleResourceChanged;

        if (missing > 0)
            Debug.LogWarning($"[StatPanelView] {missing} row(s) on {name} reference stats or resources that are not loaded");
    }

    private int BindStatRows(StatGroupView group, ref int missing)
    {
        var rows = new List<StatEntryView>();
        group.CollectEntries(rows);

        int live = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrEmpty(row.StatId)) continue;

            if (row.Bind(stats)) live++;
            else missing++;

            if (!rowsByStat.TryGetValue(row.StatId, out var bucket))
                rowsByStat[row.StatId] = bucket = new List<StatEntryView>();

            bucket.Add(row);
        }

        return live;
    }

    private int BindResourceRows(StatGroupView group, ref int missing)
    {
        var rows = new List<ResourceEntryView>();
        group.CollectResourceEntries(rows);

        int live = 0;

        foreach (var row in rows)
        {
            if (row.Resource == null) continue;

            if (row.Bind(pools)) live++;
            else missing++;

            if (!rowsByResource.TryGetValue(row.Resource, out var bucket))
                rowsByResource[row.Resource] = bucket = new List<ResourceEntryView>();

            bucket.Add(row);
        }

        return live;
    }

    private void Unbind()
    {
        if (stats != null) stats.OnStatChanged -= HandleStatChanged;
        if (pools != null) pools.OnResourceChanged -= HandleResourceChanged;

        stats = null;
        pools = null;
    }

    private void HandleStatChanged(string statId, float oldValue, float newValue)
    {
        if (!rowsByStat.TryGetValue(statId, out var bucket)) return;

        for (int i = 0; i < bucket.Count; i++) bucket[i].SetValue(newValue);
    }

    /// <summary>
    /// The row re-reads both halves rather than taking the value from the event, because a
    /// ceiling change fires this too and the ratio is what the row draws.
    /// </summary>
    private void HandleResourceChanged(ResourceDefinition def, float current)
    {
        if (def == null) return;
        if (!rowsByResource.TryGetValue(def, out var bucket)) return;

        for (int i = 0; i < bucket.Count; i++) bucket[i].Refresh(pools);
    }
}
