
// ItemDefinition.cs - Template/Blueprint for items (ScriptableObject - read-only)
using UnityEngine;

[CreateAssetMenu(fileName = "New Item Definition", menuName = "Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string itemId; // Unique identifier
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    public GameObject worldPrefab; // For dropped items

    [Header("Item Classification")]
    public ItemArchetype archetype; // Magic, Agility, Strength
    public EquipmentSlot equipmentSlot;
    public ItemCategory category;

    [Header("Grid Properties")]
    public int gridWidth = 1;
    public int gridHeight = 1;

    [Header("Base Stats")]
    public BaseItemStats baseStats;

    [Header("Tier Scaling")]
    public TierScaling tierScaling;

    [Header("Upgrade System")]
    public int maxUpgradeSlots = 3;
    public UpgradeSlotType[] allowedUpgradeTypes;
}