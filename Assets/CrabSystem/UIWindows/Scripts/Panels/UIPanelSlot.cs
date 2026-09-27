using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One region of a panel. Every view authored as a direct child belongs to this slot,
/// and exactly one of them is visible at a time.
/// Views are collected on first use rather than in Awake, so the slot works no matter
/// what order it and its panel are activated in.
/// </summary>
public class UIPanelSlot : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string slotId = "Slot";

    [Header("Content")]
    [Tooltip("View shown when the panel opens. Falls back to the first child view.")]
    [SerializeField] private string defaultViewId;

    private readonly List<UIPanelView> views = new();
    private bool collected;

    public string SlotId => slotId;
    public UIPanelView Current { get; private set; }

    public IReadOnlyList<UIPanelView> Views
    {
        get { Collect(); return views; }
    }

    public event Action<UIPanelSlot> ViewChanged;

    private void Collect()
    {
        if (collected) return;
        collected = true;

        foreach (Transform child in transform)
        {
            var view = Resolve(child);
            if (view == null) continue;

            views.Add(view);
            view.Hide();
        }

        if (views.Count == 0)
            Debug.LogWarning($"[UIPanelSlot] '{slotId}' on {name} has no UIPanelView children");
    }

    /// <summary>
    /// A specialised view (StatPanelView, InventoryPanelView) already is a UIPanelView,
    /// so an object carrying both has two. Take the specialised one and say so.
    /// </summary>
    private static UIPanelView Resolve(Transform child)
    {
        var found = child.GetComponents<UIPanelView>();
        if (found.Length == 0) return null;
        if (found.Length == 1) return found[0];

        var chosen = found[0];

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i].GetType() != typeof(UIPanelView)) chosen = found[i];
        }

        Debug.LogWarning($"[UIPanelSlot] {child.name} has {found.Length} UIPanelView components. " +
                         $"Using {chosen.GetType().Name}; remove the plain UIPanelView.");

        return chosen;
    }

    public UIPanelView Find(string viewId)
    {
        if (string.IsNullOrEmpty(viewId)) return null;

        Collect();

        for (int i = 0; i < views.Count; i++)
        {
            if (views[i].ViewId == viewId) return views[i];
        }

        return null;
    }

    public bool Show(string viewId)
    {
        var view = Find(viewId);
        if (view == null) return false;

        Show(view);
        return true;
    }

    public void Show(UIPanelView view)
    {
        if (view == null || Current == view) return;

        Collect();

        for (int i = 0; i < views.Count; i++)
        {
            if (views[i] != view) views[i].Hide();
        }

        Current = view;
        view.Show();
        ViewChanged?.Invoke(this);
    }

    public void ShowDefault()
    {
        Collect();

        var view = Find(defaultViewId);

        if (view == null && !string.IsNullOrEmpty(defaultViewId))
            Debug.LogWarning($"[UIPanelSlot] '{slotId}' has no view called '{defaultViewId}'");

        if (view == null && views.Count > 0) view = views[0];
        if (view == null) return;

        Show(view);
    }

    public void HideAll()
    {
        Collect();

        for (int i = 0; i < views.Count; i++)
            views[i].Hide();

        Current = null;
    }
}
