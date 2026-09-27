using System;
using System.Collections.Generic;
using UnityEngine;

public enum PanelSide { Left, Right }

/// <summary>
/// One side of the screen. Owns its slots and whether the side is showing at all.
/// Left is opened by the player, right is opened by the world.
/// </summary>
public class UIPanel : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private PanelSide side = PanelSide.Left;

    [Header("Content")]
    [Tooltip("Object shown and hidden with the panel. Defaults to this object.")]
    [SerializeField] private GameObject content;
    [SerializeField] private List<UIPanelSlot> slots = new();

    public PanelSide Side => side;
    public bool IsOpen { get; private set; }
    public IReadOnlyList<UIPanelSlot> Slots => slots;

    public event Action<UIPanel> OpenChanged;

    private GameObject Root => content != null ? content : gameObject;

    private void Awake()
    {
        UIPanelRegistry.Register(this);
        Root.SetActive(false);
    }

    private void OnDestroy() => UIPanelRegistry.Unregister(this);

    public void Toggle() => SetOpen(!IsOpen);

    public void Open() => SetOpen(true);

    public void Close() => SetOpen(false);

    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;

        IsOpen = open;
        Root.SetActive(open);

        if (open) ShowDefaults();
        else HideAll();

        OpenChanged?.Invoke(this);
        UIInputRouter.Instance?.ApplyMode();
    }

    /// <summary>Shows a view in whichever slot owns it, opening the panel if needed.</summary>
    public bool Show(string viewId)
    {
        var slot = SlotOwning(viewId);
        if (slot == null) return false;

        Open();
        slot.Show(viewId);
        ApplySpan();
        return true;
    }

    public bool Show(string slotId, string viewId)
    {
        var slot = FindSlot(slotId);
        if (slot == null || !slot.Show(viewId)) return false;

        Open();
        ApplySpan();
        return true;
    }

    public UIPanelSlot FindSlot(string slotId)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].SlotId == slotId) return slots[i];
        }

        return null;
    }

    private UIPanelSlot SlotOwning(string viewId)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].Find(viewId) != null) return slots[i];
        }

        return null;
    }

    private void ShowDefaults()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null) slots[i].ShowDefault();
        }

        ApplySpan();
    }

    private void HideAll()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null) slots[i].HideAll();
        }
    }

    /// <summary>A Full view takes the panel to itself; the other slots step aside.</summary>
    private void ApplySpan()
    {
        UIPanelSlot expanded = null;

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i]?.Current == null) continue;
            if (slots[i].Current.Span != ViewSpan.Full) continue;

            expanded = slots[i];
            break;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            slots[i].gameObject.SetActive(expanded == null || slots[i] == expanded);
        }
    }
}
