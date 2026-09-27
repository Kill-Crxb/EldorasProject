[System.Serializable]
public class ItemTooltipData
{
    public string itemName;
    public string description;
    public string itemType;
    public ItemRarity rarity;
    public int stackCount;
    public RuntimeItemStatModifier[] statModifiers;

    public ItemTooltipData(
        string name,
        string desc,
        string type,
        ItemRarity itemRarity,
        int stack,
        RuntimeItemStatModifier[] modifiers = null)
    {
        itemName = name;
        description = desc;
        itemType = type;
        rarity = itemRarity;
        stackCount = stack;
        statModifiers = modifiers;
    }

    /// <summary>
    /// Builds the tooltip for an item. One place, so a bag slot and an equipment
    /// socket always describe the same item the same way.
    /// </summary>
    public static ItemTooltipData For(ItemInstance item)
    {
        if (item == null || item.Definition == null) return null;

        string description = item.Definition.description;

        if (item.calculatedModifiers != null && item.calculatedModifiers.Length > 0)
        {
            description += "\n\nStats:";
            foreach (var modifier in item.calculatedModifiers)
                description += $"\n+{modifier.value:F1} {modifier.statName}";
        }

        return new ItemTooltipData(
            item.Definition.displayName,
            description,
            item.Definition.category != null ? item.Definition.category.displayName : "",
            item.currentTier,
            item.stackCount,
            item.calculatedModifiers);
    }
}
