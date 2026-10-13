using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One vendor: what's on the shelf and what they charge. First slice of Economy_Design.md §5 —
/// likes / dislikes, faction, zone, commissions and access come later.
/// </summary>
[CreateAssetMenu(fileName = "Vendor_", menuName = "Items/Vendor Definition")]
public class VendorDefinition : ScriptableObject
{
    public string vendorId;
    public string displayName;

    [Header("Money")]
    [Tooltip("Item id paid and received. Empty = free trade, for testing — everything costs nothing and selling gives the item away.")]
    [IdRef(IdKind.Item)] public string currencyItemId;

    [Tooltip("Flat, not a percentage. Sells at value + margin, buys at value − margin (floor 1).")]
    public int margin;

    [Tooltip("Off = the shelf refuses anything the player drags onto it.")]
    public bool buysFromPlayer = true;

    [Header("Shelf")]
    public int shelfWidth = 8;
    public int shelfHeight = 6;
    public List<VendorStock> stock = new List<VendorStock>();

    public VendorStock StockFor(string itemId)
    {
        foreach (var entry in stock)
            if (entry.item != null && entry.item.itemId == itemId) return entry;
        return null;
    }
}

[Serializable]
public class VendorStock
{
    public ItemDefinition item;

    [Tooltip("A fresh copy takes the slot as soon as one is bought.")]
    public bool restocks = true;
}
