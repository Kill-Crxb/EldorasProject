using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Loot container component (attach to prefab).
/// </summary>
public class LootContainer : MonoBehaviour
{
    private List<ItemInstance> containedItems = new List<ItemInstance>();

    public void Initialize(List<ItemInstance> items)
    {
        containedItems = items;
    }

    public ItemInstance[] GetItems()
    {
        return containedItems.ToArray();
    }

    public bool RemoveItem(ItemInstance item)
    {
        return containedItems.Remove(item);
    }

    public int ItemCount => containedItems.Count;
}
