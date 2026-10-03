using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data-driven item template. Classification is by asset reference — category, subtype
/// and base type are ScriptableObjects, so new item kinds are authored, not coded.
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Basic Info")]
    [Tooltip("Unique identifier (e.g., 'steel_katana', 'iron_circlet')")]
    public string itemId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("Item description for tooltips")]
    public string description;

    [Tooltip("Item icon for UI")]
    public Sprite icon;

    [Tooltip("Prefab for dropped/world items")]
    public GameObject worldPrefab;

    [Tooltip("Prefab instantiated in character hand/socket when equipped (3D model)")]
    public GameObject equippedPrefab;

    [Header("Classification")]
    [Tooltip("Item category (Weapon, Armor, Consumable, etc.)")]
    public ItemCategory category;

    [Tooltip("Item subtype (Katana, Helmet, Potion, etc.)")]
    public ItemSubType subType;

    [Header("Grid Properties")]
    [Tooltip("Width in inventory grid cells")]
    public int gridWidth = 1;

    [Tooltip("Height in inventory grid cells")]
    public int gridHeight = 1;

    [Header("Weapon Data")]
    [Tooltip("Dice damage and combat stats for this weapon (null for non-weapons)")]
    public DiceProfile weaponData;

    [Tooltip("The weapon's default moveset — what LMB fires while it is drawn (null for non-weapons)")]
    public WeaponMoveset moveset;

    [Header("Advanced")]
    [Tooltip("Item rarity level")]
    public ItemRarity rarity = ItemRarity.Common;

    [Tooltip("Maximum stack size (1 = non-stackable)")]
    public int maxStackSize = 1;

    [Tooltip("Tags for filtering/sorting (e.g., 'legendary_set', 'fire_damage')")]
    public List<string> tags = new List<string>();

    [Tooltip("Value in currency (for buying/selling)")]
    public int baseValue;

    [Tooltip("Can this item be dropped on death?")]
    public bool dropsOnDeath = true;

    [Tooltip("Can this item be traded with other players?")]
    public bool isTradeable = true;

    [Tooltip("Can this item be destroyed/deleted?")]
    public bool isDestructible = true;

    public bool HasTag(string tag) => tags != null && tags.Contains(tag);

    public bool IsValid()
    {
        if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(displayName))
            return false;

        if (category == null || subType == null)
            return false;

        return true;
    }
}