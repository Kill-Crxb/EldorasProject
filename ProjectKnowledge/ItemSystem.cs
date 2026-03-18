using UnityEngine;
using System.Linq;

/// <summary>
/// ItemSystem - Inventory coordinator
/// 
/// Routes calls to InventorySystem and LootSystem.
/// Does NOT manage equipment - use EquipmentSystem directly for that.
/// 
/// Philosophy:
/// - Single responsibility: inventory management
/// - Equipment is a separate concern handled by EquipmentSystem
/// - Systems that need equipment should access EquipmentSystem via brain.GetModule<>()
/// 
/// Standards Compliance:
/// - Guard clauses throughout
/// - Clear separation of concerns
/// - Event-driven architecture
/// 
/// Refactored: February 18, 2026 (Removed equipment routing)
/// </summary>
public class ItemSystem : MonoBehaviour, IBrainModule, IInventoryProvider
{
    [Header("System Configuration")]
    [SerializeField] private bool hasInventory = true;
    [SerializeField] private bool dropsLoot = true;

    [Header("Child Systems")]
    [SerializeField] private InventorySystem inventorySystem;
    [SerializeField] private LootSystem lootSystem;

    [Header("Debug")]
    [SerializeField] private bool debugSystem = false;

    private ControllerBrain brain;
    private bool isInitialized = false;

    // Forward inventory events
    public event System.Action OnInventoryChanged
    {
        add { if (inventorySystem != null) inventorySystem.OnInventoryChanged += value; }
        remove { if (inventorySystem != null) inventorySystem.OnInventoryChanged -= value; }
    }

    #region IBrainModule

    public bool IsEnabled
    {
        get => enabled;
        set => enabled = value;
    }

    public void Initialize(ControllerBrain brain)
    {
        if (isInitialized) return;

        this.brain = brain;

        // Initialize child systems that are enabled
        if (hasInventory && inventorySystem != null)
        {
            inventorySystem.Initialize(brain);
        }

        if (dropsLoot && lootSystem != null)
        {
            lootSystem.Initialize(brain);
        }

        isInitialized = true;

        if (debugSystem)
            Debug.Log($"[ItemSystem] Initialized for {brain.name}");
    }

    public void LateInitialize()
    {
        inventorySystem?.LateInitialize();
        lootSystem?.LateInitialize();
    }

    public void UpdateModule()
    {
        inventorySystem?.UpdateModule();
        lootSystem?.UpdateModule();
    }

    public void Shutdown()
    {
        inventorySystem?.Shutdown();
        lootSystem?.Shutdown();
    }

    #endregion

    #region Inventory Operations (Routes to InventorySystem)

    public bool AddItem(ItemInstance item)
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return false;

        return inventorySystem.AddItem(item);
    }

    public bool RemoveItem(string instanceId)
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return false;

        return inventorySystem.RemoveItem(instanceId);
    }

    public ItemInstance GetItemInstance(string instanceId)
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return null;

        return inventorySystem.GetItemInstance(instanceId);
    }

    public ItemInstance[] GetAllInventoryItems()
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return new ItemInstance[0];

        return inventorySystem.GetAllItems();
    }

    #endregion

    #region Loot Operations (Routes to LootSystem)

    public void GenerateLoot(Vector3 position)
    {
        // Guard clause: No loot system
        if (!dropsLoot || lootSystem == null) return;

        lootSystem.GenerateLoot(position);
    }

    #endregion

    #region IInventoryProvider Implementation

    bool IInventoryProvider.HasItem(string itemId)
    {
        return inventorySystem?.GetAllItems().Any(i => i.definitionId == itemId) ?? false;
    }

    int IInventoryProvider.GetItemCount(string itemId)
    {
        return inventorySystem?.GetAllItems().Count(i => i.definitionId == itemId) ?? 0;
    }

    bool IInventoryProvider.AddItem(string itemId, int quantity)
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return false;

        for (int i = 0; i < quantity; i++)
        {
            var instance = new ItemInstance(itemId, ItemRarity.Common);
            if (!AddItem(instance))
                return false;
        }
        return true;
    }

    bool IInventoryProvider.RemoveItem(string itemId, int quantity)
    {
        // Guard clause: No inventory system
        if (!hasInventory || inventorySystem == null) return false;

        var items = inventorySystem.GetAllItems();
        int removed = 0;

        for (int i = items.Length - 1; i >= 0 && removed < quantity; i--)
        {
            if (items[i].definitionId == itemId)
            {
                RemoveItem(items[i].instanceId);
                removed++;
            }
        }

        return removed == quantity;
    }

    void IInventoryProvider.ClearInventory()
    {
        inventorySystem?.ClearInventory();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get child system directly (for advanced use cases).
    /// </summary>
    public T GetChildSystem<T>() where T : MonoBehaviour
    {
        if (typeof(T) == typeof(InventorySystem)) return inventorySystem as T;
        if (typeof(T) == typeof(LootSystem)) return lootSystem as T;
        return null;
    }

    #endregion

    #region Debug Utilities

    [ContextMenu("Debug: Print Systems")]
    private void DebugPrintSystems()
    {
        Debug.Log("=== ITEM SYSTEM ===");
        Debug.Log($"  Inventory: {(hasInventory ? "Enabled" : "Disabled")}");
        Debug.Log($"  Loot: {(dropsLoot ? "Enabled" : "Disabled")}");
        Debug.Log("  Equipment: Use EquipmentSystem directly");
    }

    #endregion
}