using UnityEngine;

/// <summary>
/// ItemCategory - Top-level item organization (Weapon, Armor, Consumable, etc.)
/// 
/// Purpose: Replace hardcoded ItemCategory enum with data-driven system
/// 
/// Usage:
/// - Create assets: Right-click → Items/Category
/// - Examples: "Weapon", "Armor", "Consumable", "Material", "Quest"
/// - Reference in ItemDefinition to classify items
/// 
/// Benefits:
/// - Add new categories without code changes
/// - Designer-friendly category management
/// - Visual customization per category
/// </summary>
[CreateAssetMenu(fileName = "New Item Category", menuName = "Items/Category")]
public class ItemCategory : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier for this category (e.g., 'weapon', 'armor', 'consumable')")]
    public string categoryId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [Header("Visual")]
    [Tooltip("Icon representing this category in UI")]
    public Sprite categoryIcon;

    [Tooltip("Color tint for category in UI")]
    public Color categoryColor = Color.white;

    [Header("Description")]
    [TextArea(2, 4)]
    public string description;

    [Header("Behavior")]
    [Tooltip("Can items in this category be equipped?")]
    public bool isEquippable = false;

    [Tooltip("Can items in this category be consumed/used?")]
    public bool isConsumable = false;

    [Tooltip("Can items in this category be stacked?")]
    public bool isStackable = false;

    [Tooltip("Can items in this category be traded?")]
    public bool isTradeable = true;

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(categoryId) && !string.IsNullOrEmpty(displayName);
    }
}