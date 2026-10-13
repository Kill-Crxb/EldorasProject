using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime + serialisable configuration for one action bar.
/// Resize() grows or shrinks the slot list while preserving existing assignments.
/// </summary>
[Serializable]
public class ActionBarConfig
{
    public string barId;
    public int    slotCount;
    public int    rowCount;
    public List<ActionBarSlotData> slots = new List<ActionBarSlotData>();

    public ActionBarConfig() { }

    public ActionBarConfig(string barId, int slotCount, int rowCount)
    {
        this.barId     = barId;
        this.slotCount = Mathf.Clamp(slotCount, 1, 12);
        this.rowCount  = Mathf.Clamp(rowCount,  1, 3);

        slots = new List<ActionBarSlotData>(this.slotCount);
        for (int i = 0; i < this.slotCount; i++)
            slots.Add(new ActionBarSlotData(barId, i));
    }

    /// <summary>
    /// Change the slot count and row count, preserving assignments that still fit.
    /// New slots are added empty at the end; slots beyond newCount are dropped.
    /// </summary>
    public void Resize(int newCount, int newRows)
    {
        newCount = Mathf.Clamp(newCount, 1, 12);
        newRows  = Mathf.Clamp(newRows,  1, 3);

        // Grow
        while (slots.Count < newCount)
            slots.Add(new ActionBarSlotData(barId, slots.Count));

        // Shrink (preserve assignments that fit)
        if (slots.Count > newCount)
            slots.RemoveRange(newCount, slots.Count - newCount);

        slotCount = newCount;
        rowCount  = newRows;

        // Re-sync slot indices in case the list was modified externally
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].barId     = barId;
            slots[i].slotIndex = i;
        }
    }

    public ActionBarSlotData GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    public int ColumnsPerRow => rowCount > 0 ? Mathf.CeilToInt((float)slotCount / rowCount) : slotCount;
}
