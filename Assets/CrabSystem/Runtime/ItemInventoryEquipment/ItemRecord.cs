using UnityEngine;
using UnityEngine.Serialization;

// One saved shape for an item instance, used by the inventory save, the equipment save and container contents
// (Known Issues B17). Everything an instance carries between sessions lives here, so a new instance field
// (purity, crafter) is added once and survives every container. Field names match the old save entries, so
// existing saves and ContainerContents assets load unchanged; fields an old record lacks keep the defaults below.
[System.Serializable]
public class ItemRecord
{
    public string instanceId;
    [FormerlySerializedAs("itemId")]
    [IdRef(IdKind.Item)] public string definitionId;
    public ItemRarity rarity = ItemRarity.Common;
    public int gridX = -1;
    public int gridY = -1;
    public int stackCount = 1;
    public float durability = 100f;

    public static ItemRecord From(ItemInstance item)
    {
        var record = new ItemRecord();
        record.CopyFrom(item);
        return record;
    }

    public void CopyFrom(ItemInstance item)
    {
        instanceId = item.instanceId;
        definitionId = item.definitionId;
        rarity = item.currentTier;
        gridX = item.gridX;
        gridY = item.gridY;
        stackCount = item.stackCount;
        durability = item.durability;
    }

    // An authored record has no instanceId; the new instance keeps the one it was made with.
    public ItemInstance ToInstance()
    {
        if (string.IsNullOrEmpty(definitionId)) return null;

        ItemInstance item = ItemManager.CreateItem(definitionId, rarity);
        if (item == null) return null;

        if (!string.IsNullOrEmpty(instanceId)) item.instanceId = instanceId;
        item.stackCount = Mathf.Max(1, stackCount);
        item.durability = durability;
        item.PlaceAtPosition(gridX, gridY);
        return item;
    }
}
