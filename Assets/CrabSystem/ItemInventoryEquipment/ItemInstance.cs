using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Runtime item instance with cached definition for performance.
/// Stores state (tier, durability, position) and pre-calculated modifiers.
/// </summary>
[System.Serializable]
public class ItemInstance
{
    [Header("Instance Identity")]
    public string instanceId;
    public string definitionId;
    public long createdTimestamp;
    public int ownerPlayerId;

    [Header("Current State")]
    public ItemRarity currentTier = ItemRarity.Common;
    public int stackCount = 1;
    public float durability = 100f;
    public bool isBound = false;

    [Header("Grid Properties")]
    public int itemWidth = 1;
    public int itemHeight = 1;

    [Header("Inventory Position")]
    public int gridX = -1; // -1 = not placed
    public int gridY = -1;

    [Header("Upgrades")]
    public ItemUpgradeSlot[] upgradeSlots = new ItemUpgradeSlot[0]; // New system
    public ItemUpgrade[] upgrades = new ItemUpgrade[3]; // Legacy system
    public int usedUpgradeSlots = 0;

    [Header("Dynamic Stats")]
    public RuntimeItemStatModifier[] calculatedModifiers;

    // Cached definition - initialized once in constructor
    private ItemDefinition cachedDefinition;

    public ItemDefinition Definition => cachedDefinition;

    public ItemInstance(string defId, ItemRarity tier = ItemRarity.Common)
    {
        instanceId = System.Guid.NewGuid().ToString();
        definitionId = defId;
        currentTier = tier;
        createdTimestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
        durability = 100f;

        // Cache definition ONCE (avoid repeated lookups every property access)
        cachedDefinition = ItemManager.GetDefinition(defId);

        // Fail loud if definition missing (let it crash in dev)
        if (cachedDefinition == null)
        {
            Debug.LogError($"[ItemInstance] Invalid itemId: {defId}");
            return;
        }

        // Initialize grid properties from cached definition
        itemWidth = cachedDefinition.gridWidth;
        itemHeight = cachedDefinition.gridHeight;

        RecalculateModifiers();
    }

    public bool ValidateGridProperties()
    {
        if (cachedDefinition == null) return false;
        return itemWidth == cachedDefinition.gridWidth && itemHeight == cachedDefinition.gridHeight;
    }

    public bool IsPlaced => gridX >= 0 && gridY >= 0;

    public void RemoveFromGrid()
    {
        gridX = -1;
        gridY = -1;
    }

    public void PlaceAtPosition(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    public List<Vector2Int> GetOccupiedSlots()
    {
        var slots = new List<Vector2Int>();
        if (!IsPlaced) return slots;

        for (int x = gridX; x < gridX + itemWidth; x++)
        {
            for (int y = gridY; y < gridY + itemHeight; y++)
            {
                slots.Add(new Vector2Int(x, y));
            }
        }
        return slots;
    }

    public bool WouldOverlapWith(ItemInstance other)
    {
        if (other == null || !other.IsPlaced) return false;

        var otherSet = new HashSet<Vector2Int>(other.GetOccupiedSlots());
        foreach (var slot in GetOccupiedSlots())
        {
            if (otherSet.Contains(slot)) return true;
        }
        return false;
    }

    public void RecalculateModifiers()
    {
        // Fail fast if definition missing (caller bug)
        if (cachedDefinition == null)
        {
            calculatedModifiers = new RuntimeItemStatModifier[0];
            return;
        }

        var modifiers = new List<RuntimeItemStatModifier>();

        AddUpgradeModifiers(modifiers);

        calculatedModifiers = modifiers.ToArray();
    }

    private void AddUpgradeModifiers(List<RuntimeItemStatModifier> modifiers)
    {
        foreach (var upgrade in upgrades)
        {
            if (upgrade != null && upgrade.isActive)
            {
                modifiers.AddRange(upgrade.GetStatModifiers());
            }
        }
    }

    // Item stat modifiers are display-only in this pass: calculatedModifiers still
    // drives tooltips, but nothing writes them into the entity's stats.
}

[System.Serializable]
public class RuntimeItemStatModifier
{
    public string statName;
    public float value;
    public string source;
    public bool isPercentage;
}

[System.Serializable]
public class ItemUpgrade
{
    public string upgradeId;
    public bool isActive = false;

    public RuntimeItemStatModifier[] GetStatModifiers()
    {
        return new RuntimeItemStatModifier[0];
    }
}
