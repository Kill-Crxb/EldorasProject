using UnityEngine;

public class ChestPopulator : MonoBehaviour
{
    [Header("Items to Add")]
    [SerializeField] private ChestLootEntry[] itemsToAdd;

    [Header("Settings")]
    [SerializeField] private bool populateOnStart = true;
    [SerializeField] private bool clearExistingItems = true;

    void Start()
    {
        if (populateOnStart) PopulateChest();
    }

    [ContextMenu("Populate Chest Now")]
    public void PopulateChest()
    {
        var brain = GetComponent<ControllerBrain>()
            ?? GetComponentInParent<ControllerBrain>()
            ?? GetComponentInChildren<ControllerBrain>();

        if (brain == null) { Debug.LogError("[ChestPopulator] No ControllerBrain found!"); return; }

        var inventorySystem = brain.GetModule<InventorySystem>();
        if (inventorySystem == null) { Debug.LogError("[ChestPopulator] No InventorySystem on ControllerBrain!"); return; }

        if (clearExistingItems) inventorySystem.ClearInventory();

        int added = 0, failed = 0;

        foreach (var entry in itemsToAdd)
        {
            if (string.IsNullOrEmpty(entry.itemId)) { Debug.LogWarning("[ChestPopulator] Skipping entry with empty itemId"); continue; }
            if (!ItemManager.HasItem(entry.itemId)) { Debug.LogError($"[ChestPopulator] Item '{entry.itemId}' not found in ItemManager!"); failed++; continue; }

            var item = ItemManager.CreateItem(entry.itemId, entry.rarity);
            if (item == null) { Debug.LogError($"[ChestPopulator] Failed to create item '{entry.itemId}'"); failed++; continue; }

            if (inventorySystem.AddItem(item)) added++;
            else { Debug.LogWarning($"[ChestPopulator] No space for '{entry.itemId}'"); failed++; }
        }

        if (failed > 0)
            Debug.LogWarning($"[ChestPopulator] Population complete: {added} added, {failed} failed");
    }

    [ContextMenu("Clear Chest")]
    public void ClearChest()
    {
        var brain = GetComponent<ControllerBrain>() ?? GetComponentInChildren<ControllerBrain>();
        if (brain == null) return;
        brain.GetModule<InventorySystem>()?.ClearInventory();
    }
}

[System.Serializable]
public class ChestLootEntry
{
    public string itemId;
    public ItemRarity rarity = ItemRarity.Common;
}