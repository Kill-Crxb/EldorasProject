using UnityEngine;
using System.Collections.Generic;

// ItemInstance.cs - Runtime instance of an item (Serializable class)
[System.Serializable]
public class ItemInstance
{
    [Header("Instance Identity")]
    public string instanceId; // Unique ID for this specific item instance
    public string definitionId; // References ItemDefinition
    public long createdTimestamp;
    public int ownerPlayerId;

    [Header("Current State")]
    public ItemRarity currentTier = ItemRarity.Common;
    public int stackCount = 1;
    public float durability = 100f;
    public bool isBound = false;

    [Header("Grid Properties - Multi-Slot Support")]
    public int itemWidth = 1;    // Width in grid slots
    public int itemHeight = 1;   // Height in grid slots

    [Header("Inventory Position")]
    public int gridX = -1;       // X position in inventory grid (-1 = not placed/equipped)
    public int gridY = -1;       // Y position in inventory grid (-1 = not placed/equipped)

    [Header("Upgrades")]
    public ItemUpgrade[] upgrades = new ItemUpgrade[3];
    public int usedUpgradeSlots = 0;

    [Header("Dynamic Stats")]
    public ItemStatModifier[] calculatedModifiers; // Cached for performance

    // Constructor
    public ItemInstance(string defId, ItemRarity tier = ItemRarity.Common)
    {
        instanceId = System.Guid.NewGuid().ToString();
        definitionId = defId;
        currentTier = tier;
        createdTimestamp = System.DateTimeOffset.Now.ToUnixTimeSeconds();
        durability = 100f;

        // Initialize grid properties from definition
        InitializeGridProperties();

        RecalculateModifiers();
    }

    // Initialize grid size from ItemDefinition
    private void InitializeGridProperties()
    {
        var definition = ItemDatabase.GetDefinition(definitionId);
        if (definition != null)
        {
            itemWidth = definition.gridWidth;
            itemHeight = definition.gridHeight;
        }
    }

    // Validate grid properties match definition (for save/load safety)
    public bool ValidateGridProperties()
    {
        var definition = ItemDatabase.GetDefinition(definitionId);
        if (definition == null) return false;

        return itemWidth == definition.gridWidth && itemHeight == definition.gridHeight;
    }

    // Check if item is currently placed in inventory/equipment
    public bool IsPlaced => gridX >= 0 && gridY >= 0;

    // Remove item from grid (for moving/unequipping)
    public void RemoveFromGrid()
    {
        gridX = -1;
        gridY = -1;
    }

    // Place item at specific grid position
    public void PlaceAtPosition(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    // Get all grid slots this item occupies
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

    // Check if item would fit at a specific position
    public bool CanFitAtPosition(int x, int y, int gridWidth, int gridHeight)
    {
        // Check boundaries
        if (x < 0 || y < 0) return false;
        if (x + itemWidth > gridWidth) return false;
        if (y + itemHeight > gridHeight) return false;

        return true;
    }

    // Calculate final stats based on tier + upgrades
    public void RecalculateModifiers()
    {
        var definition = ItemDatabase.GetDefinition(definitionId);
        if (definition == null) return;

        var modifiers = new List<ItemStatModifier>();

        // Base stats from definition + tier scaling
        AddBaseStatModifiers(modifiers, definition);

        // Archetype bonuses (Health/Stamina/Mana regen)
        AddArchetypeModifiers(modifiers, definition);

        // Upgrade modifiers
        AddUpgradeModifiers(modifiers);

        calculatedModifiers = modifiers.ToArray();
    }

    private void AddBaseStatModifiers(List<ItemStatModifier> modifiers, ItemDefinition definition)
    {
        float tierMultiplier = GetTierMultiplier(currentTier);

        // Add armor/defense based on tier
        if (definition.baseStats.armor > 0)
        {
            modifiers.Add(new ItemStatModifier
            {
                statName = "armor",
                value = definition.baseStats.armor * tierMultiplier,
                source = $"{definition.displayName} (Base)"
            });
        }

        // Add other base stats similarly
        foreach (var baseStat in definition.baseStats.GetAllStats())
        {
            if (baseStat.value > 0)
            {
                modifiers.Add(new ItemStatModifier
                {
                    statName = baseStat.name,
                    value = baseStat.value * tierMultiplier,
                    source = $"{definition.displayName} (Base)"
                });
            }
        }
    }

    private void AddArchetypeModifiers(List<ItemStatModifier> modifiers, ItemDefinition definition)
    {
        float regenBonus = GetArchetypeRegenBonus(currentTier);

        switch (definition.archetype)
        {
            case ItemArchetype.Strength:
                modifiers.Add(new ItemStatModifier
                {
                    statName = "regeneration",
                    value = regenBonus,
                    source = $"{definition.displayName} (Strength Bonus)"
                });
                break;

            case ItemArchetype.Agility:
                modifiers.Add(new ItemStatModifier
                {
                    statName = "recovery", // Stamina regen
                    value = regenBonus,
                    source = $"{definition.displayName} (Agility Bonus)"
                });
                break;

            case ItemArchetype.Magic:
                modifiers.Add(new ItemStatModifier
                {
                    statName = "recollection", // Mana regen
                    value = regenBonus,
                    source = $"{definition.displayName} (Magic Bonus)"
                });
                break;
        }
    }

    private void AddUpgradeModifiers(List<ItemStatModifier> modifiers)
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
        float baseBonus = 0.5f; // Base regen bonus
        return baseBonus * GetTierMultiplier(tier);
    }

    // Integration with your existing RPGSecondaryStats system
    public void ApplyToStatsSystem(RPGSecondaryStats statsSystem)
    {
        // Remove old modifiers from this item
        statsSystem.RemoveAllModifiersFromSource(instanceId);

        // Apply new modifiers using your existing system
        foreach (var modifier in calculatedModifiers)
        {
            if (modifier.isPercentage)
            {
                statsSystem.AddPercentageModifier(modifier.statName, instanceId, modifier.value);
            }
            else
            {
                statsSystem.AddItemModifier(modifier.statName, instanceId, modifier.value);
            }
        }
    }

    // Networking/Serialization
    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public static ItemInstance FromJson(string json)
    {
        var instance = JsonUtility.FromJson<ItemInstance>(json);

        // Validate and fix grid properties on load
        if (instance != null && !instance.ValidateGridProperties())
        {
            Debug.LogWarning($"ItemInstance {instance.instanceId} grid properties don't match definition. Reinitializing.");
            instance.InitializeGridProperties();
        }

        return instance;
    }

    // Debug and validation methods
    public string GetGridInfo()
    {
        return $"Size: {itemWidth}x{itemHeight}, Position: ({gridX}, {gridY}), Placed: {IsPlaced}";
    }

    public List<ItemStatModifier> GetStatModifiers()
    {
        return new List<ItemStatModifier>(calculatedModifiers ?? new ItemStatModifier[0]);
    }
}

// Renamed to avoid conflict with your existing StatModifier class
[System.Serializable]
public class ItemStatModifier
{
    public string statName; // e.g., "meleePower", "armor", "regeneration"
    public float value;
    public string source; // For tooltip display
    public bool isPercentage = false;
}

// Supporting Data Structures
[System.Serializable]
public class BaseItemStats
{
    public float armor = 0f;
    public float health = 0f;
    public float mana = 0f;
    public float stamina = 0f;
    public float damage = 0f;
    public float attackSpeed = 0f;

    public List<NamedStat> GetAllStats()
    {
        var stats = new List<NamedStat>();
        if (armor > 0) stats.Add(new NamedStat("armor", armor));
        if (health > 0) stats.Add(new NamedStat("maxHealth", health));
        if (mana > 0) stats.Add(new NamedStat("maxMana", mana));
        if (stamina > 0) stats.Add(new NamedStat("maxStamina", stamina));
        if (damage > 0) stats.Add(new NamedStat("meleePower", damage));
        if (attackSpeed > 0) stats.Add(new NamedStat("meleeSpeed", attackSpeed));
        return stats;
    }
}

[System.Serializable]
public class NamedStat
{
    public string name;
    public float value;

    public NamedStat(string name, float value)
    {
        this.name = name;
        this.value = value;
    }
}

[System.Serializable]
public class TierScaling
{
    [Header("Stat Multipliers per Tier")]
    public float[] tierMultipliers = { 1.0f, 1.25f, 1.6f, 2.0f, 2.5f }; // White -> Orange

    [Header("Regen Bonuses per Tier")]
    public float[] regenBonuses = { 0.5f, 0.8f, 1.2f, 1.8f, 2.5f }; // Archetype bonuses
}

[System.Serializable]
public class ItemUpgrade
{
    public string upgradeId;
    public UpgradeSlotType slotType;
    public bool isActive = true;
    public ItemStatModifier[] modifiers;

    public ItemStatModifier[] GetStatModifiers()
    {
        return isActive ? modifiers : new ItemStatModifier[0];
    }
}

// Enums
public enum ItemArchetype
{
    None,
    Strength,   // Headguard - Health Regen
    Agility,    // Headband - Stamina Regen  
    Magic       // Circlet - Mana Regen
}

public enum ItemCategory
{
    Weapon,
    Armor,
    Consumable,
    Material,
    Quest,
    Misc
}

public enum UpgradeSlotType
{
    Offensive,  // Damage, crit, speed
    Defensive,  // Armor, health, resistances
    Utility     // Special effects, movement, etc.
}