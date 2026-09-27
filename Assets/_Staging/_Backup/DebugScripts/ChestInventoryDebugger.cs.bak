using UnityEngine;

/// <summary>
/// Debug script to trace why items aren't loading into chest inventory
/// Attach this to the chest GameObject to diagnose the issue
/// </summary>
public class ChestInventoryDebugger : MonoBehaviour
{
    [Header("Run Diagnostics")]
    [SerializeField] private bool runOnStart = true;

    private void Start()
    {
        if (runOnStart)
        {
            Invoke(nameof(RunFullDiagnostics), 0.5f);
        }
    }

    [ContextMenu("Run Full Diagnostics")]
    public void RunFullDiagnostics()
    {
        Debug.Log("========================================");
        Debug.Log("CHEST INVENTORY DIAGNOSTICS");
        Debug.Log("========================================");

        // Step 1: Check ControllerBrain - search self, parent, and children
        var brain = GetComponent<ControllerBrain>();

        if (brain == null)
        {
            brain = GetComponentInParent<ControllerBrain>();
            if (brain != null)
                Debug.Log($"Found ControllerBrain on parent: {brain.gameObject.name}");
        }

        if (brain == null)
        {
            brain = GetComponentInChildren<ControllerBrain>();
            if (brain != null)
                Debug.Log($"Found ControllerBrain on child: {brain.gameObject.name}");
        }

        if (brain == null)
        {
            Debug.LogError("NO ControllerBrain found!");
            Debug.LogError("Checked: this GameObject, parents, and children");
            return;
        }
        Debug.Log($"ControllerBrain found: {brain.name}");

        // Step 2: Check InventorySystem
        var inventorySystem = brain.GetModule<InventorySystem>();
        if (inventorySystem == null)
        {
            Debug.LogError("NO InventorySystem module found!");
            return;
        }
        Debug.Log($"InventorySystem module found");

        // Step 3: Check if initialized
        var isInitializedField = typeof(InventorySystem).GetField("isInitialized",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bool isInitialized = (bool)isInitializedField?.GetValue(inventorySystem);
        Debug.Log($"InventorySystem.isInitialized = {isInitialized}");

        // Step 4: Check ContainerData array
        var containersField = typeof(InventorySystem).GetField("containers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var containers = containersField?.GetValue(inventorySystem) as ContainerData[];

        if (containers == null || containers.Length == 0)
        {
            Debug.LogError("NO ContainerData[] configured in InventorySystem!");
            Debug.LogError("Fix: Select chest, find InventorySystem component, set containers array");
            return;
        }
        Debug.Log($"ContainerData[] configured: {containers.Length} containers");
        Debug.Log($"Container[0]: {containers[0].gridWidth}x{containers[0].gridHeight}");

        // Step 5: Check initialContents
        var initialContentsField = typeof(InventorySystem).GetField("initialContents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var initialContents = initialContentsField?.GetValue(inventorySystem) as ContainerContents;

        if (initialContents == null)
        {
            Debug.LogWarning("NO initialContents assigned");
        }
        else
        {
            Debug.Log($"initialContents assigned: {initialContents.name}");
            Debug.Log($"Contains {initialContents.items.Count} items:");

            if (initialContents.items.Count == 0)
            {
                Debug.LogError("Items list is EMPTY!");
            }

            for (int i = 0; i < initialContents.items.Count; i++)
            {
                var item = initialContents.items[i];
                Debug.Log($"[Item {i}] ItemID: '{item.itemId}' (length: {item.itemId?.Length ?? 0})");
                Debug.Log($"  Rarity: {item.rarity}, Position: ({item.gridX}, {item.gridY})");

                if (string.IsNullOrEmpty(item.itemId))
                {
                    Debug.LogError("  ItemID is NULL or EMPTY!");
                }
                else
                {
                    bool itemExists = ItemManager.HasItem(item.itemId);
                    if (!itemExists)
                    {
                        Debug.LogError($"  ItemID '{item.itemId}' NOT FOUND IN ItemManager!");
                    }
                    else
                    {
                        Debug.Log($"  ItemID exists in ItemManager");
                        var def = ItemManager.GetDefinition(item.itemId);
                        if (def != null)
                        {
                            Debug.Log($"  DisplayName: '{def.displayName}', Size: {def.gridWidth}x{def.gridHeight}");
                        }
                    }
                }
            }
        }

        // Step 6: Check currentContents
        var currentContentsField = typeof(InventorySystem).GetField("currentContents",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var currentContents = currentContentsField?.GetValue(inventorySystem) as ContainerContents;

        if (currentContents == null)
        {
            Debug.LogWarning("currentContents is NULL");
        }
        else
        {
            Debug.Log($"currentContents exists");
            Debug.Log($"Grid: {currentContents.gridWidth}x{currentContents.gridHeight}");
            Debug.Log($"Items in currentContents: {currentContents.items.Count}");
        }

        // Step 7: Check actual inventory
        var inventoryItemsField = typeof(InventorySystem).GetField("inventoryItems",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var inventoryItems = inventoryItemsField?.GetValue(inventorySystem) as System.Collections.Generic.List<ItemInstance>;

        if (inventoryItems == null)
        {
            Debug.LogError("inventoryItems list is NULL!");
            return;
        }

        Debug.Log($"ACTUAL INVENTORY COUNT: {inventoryItems.Count}");

        if (inventoryItems.Count == 0)
        {
            Debug.LogError("Inventory is empty! Items were not loaded.");
        }
        else
        {
            Debug.Log($"Inventory has {inventoryItems.Count} items:");
            foreach (var item in inventoryItems)
            {
                Debug.Log($"  - {item.Definition.displayName} at ({item.gridX}, {item.gridY})");
            }
        }

        // Step 8: Check ItemManager
        Debug.Log("--- ItemManager Status ---");
        var allItemIds = ItemManager.GetAllItemIds();
        Debug.Log($"ItemManager has {allItemIds.Length} items loaded");
        if (allItemIds.Length > 0)
        {
            foreach (var id in allItemIds)
            {
                Debug.Log($"  - {id}");
            }
        }

        Debug.Log("========================================");
        Debug.Log("DIAGNOSTICS COMPLETE");
        Debug.Log("========================================");
    }
}