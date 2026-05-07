// ItemTooltipData.cs
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
}