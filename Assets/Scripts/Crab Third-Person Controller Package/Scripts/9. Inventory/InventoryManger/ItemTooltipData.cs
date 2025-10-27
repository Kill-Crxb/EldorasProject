// ItemTooltipData.cs - Data structure for tooltip information
[System.Serializable]
public class ItemTooltipData
{
    public string itemName;
    public string description;
    public string itemType;
    public ItemRarity rarity;
    public int stackCount;

    public ItemTooltipData(string name, string desc, string type, ItemRarity itemRarity, int stack)
    {
        itemName = name;
        description = desc;
        itemType = type;
        rarity = itemRarity;
        stackCount = stack;
    }
}
