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

        var mySlots = GetOccupiedSlots();
        var otherSlots = other.GetOccupiedSlots();

        foreach (var mySlot in mySlots)
        {
            if (otherSlots.Contains(mySlot))
                return true;
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

        AddBaseStatModifiers(modifiers);
        AddUpgradeModifiers(modifiers);

        calculatedModifiers = modifiers.ToArray();
    }

    private void AddBaseStatModifiers(List<RuntimeItemStatModifier> modifiers)
    {
        float tierMultiplier = GetTierMultiplier(currentTier);

        // Use new statModifiers system only (Phase 3)
        // Legacy baseStats removed - all items should use ItemStatModifier[]

        // Guard clause: Skip if no archetype
        if (cachedDefinition.archetype == ItemArchetype.None) return;

        float regenBonus = GetArchetypeRegenBonus(currentTier);
        string regenStat = GetArchetypeRegenStat(cachedDefinition.archetype);

        // Guard clause: Skip if no regen stat mapping
        if (string.IsNullOrEmpty(regenStat)) return;

        modifiers.Add(new RuntimeItemStatModifier
        {
            statName = regenStat,
            value = regenBonus,
            source = $"{cachedDefinition.displayName} (Archetype)",
            isPercentage = false
        });
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

    private float GetTierMultiplier(ItemRarity tier)
    {
        switch (tier)
        {
            case ItemRarity.Common: return 1.0f;
            case ItemRarity.Uncommon: return 1.25f;
            case ItemRarity.Rare: return 1.6f;
            case ItemRarity.Epic: return 2.0f;
            case ItemRarity.Legendary: return 2.5f;
            default: return 1.0f;
        }
    }

    private float GetArchetypeRegenBonus(ItemRarity tier)
    {
        float baseBonus = 0.5f;
        return baseBonus * GetTierMultiplier(tier);
    }

    private string GetArchetypeRegenStat(ItemArchetype archetype)
    {
        switch (archetype)
        {
            case ItemArchetype.Strength: return "character.health_regen";
            case ItemArchetype.Agility: return "character.stamina_regen";
            case ItemArchetype.Magic: return "character.mana_regen";
            default: return null;
        }
    }

    public void ApplyToStatsSystem(StatSystem statSystem, ResourceSystem resourceSystem = null)
    {
        // Guard clause: Need stat system
        if (statSystem == null) return;

        // Guard clause: Need valid definition
        if (cachedDefinition == null) return;

        // Remove old modifiers first
        statSystem.Engine.RemoveAllModifiersFromSource(instanceId);

        // Apply legacy runtime modifiers
        ApplyLegacyModifiers(statSystem);

        // Apply new stat modifiers (Phase 3 system)
        ApplyNewStatModifiers(statSystem);

        // Apply resource modifiers (Phase 3 system)
        ApplyResourceModifiers(resourceSystem);
    }

    private void ApplyLegacyModifiers(StatSystem statSystem)
    {
        // Guard clause: No legacy modifiers
        if (calculatedModifiers == null) return;

        foreach (var modifier in calculatedModifiers)
        {
            // Handle percentage modifiers and skip to next
            if (modifier.isPercentage)
            {
                statSystem.Engine.AddPercentModifier(
                    modifier.statName,
                    instanceId,
                    modifier.value / 100f
                );
                continue;
            }

            // Handle flat modifiers
            statSystem.Engine.AddFlatModifier(
                modifier.statName,
                instanceId,
                modifier.value
            );
        }
    }

    private void ApplyNewStatModifiers(StatSystem statSystem)
    {
        // Guard clause: No new modifiers
        if (cachedDefinition.statModifiers == null) return;

        foreach (var modifier in cachedDefinition.statModifiers)
        {
            modifier.ApplyToStatEngine(statSystem.Engine, instanceId);
        }
    }

    private void ApplyResourceModifiers(ResourceSystem resourceSystem)
    {
        // Guard clause: No resource system provided
        if (resourceSystem == null) return;

        // Guard clause: No resource modifiers
        if (cachedDefinition.resourceModifiers == null) return;

        foreach (var modifier in cachedDefinition.resourceModifiers)
        {
            modifier.ApplyToResourceSystem(resourceSystem, instanceId);
        }
    }

    public void RemoveFromStatsSystem(StatSystem statSystem, ResourceSystem resourceSystem = null)
    {
        // Guard clause: Need stat system
        if (statSystem == null) return;

        // Remove stat modifiers
        statSystem.Engine.RemoveAllModifiersFromSource(instanceId);

        // Guard clause: No resource system
        if (resourceSystem == null) return;

        // Guard clause: No resource modifiers
        if (cachedDefinition?.resourceModifiers == null) return;

        foreach (var modifier in cachedDefinition.resourceModifiers)
        {
            modifier.RemoveFromResourceSystem(resourceSystem, instanceId);
        }
    }
}

// Legacy stat modifier structure (Phase 1.6 Days 7-8)
[System.Serializable]
public class RuntimeItemStatModifier
{
    public string statName;
    public float value;
    public string source;
    public bool isPercentage;
}

// Legacy upgrade structure
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

// Deprecated enums (Phase 3 migration)
public enum ItemArchetype
{
    None,
    Strength,
    Agility,
    Magic
}

public enum LegacyItemCategory
{
    Weapon,
    Armor,
    Consumable,
    Material,
    Quest,
    Misc
}

public enum LegacyUpgradeSlotType
{
    Offensive,
    Defensive,
    Utility
}