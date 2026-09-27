using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles death drops and loot generation.
/// Reads from EquipmentSystem and InventorySystem to generate loot.
/// </summary>
public class LootSystem : MonoBehaviour, IBrainModule
{
    [Header("Loot Configuration")]
    // [SerializeField] private EntityLootConfiguration lootConfiguration; // TODO: Create this ScriptableObject later

    [Header("Drop Rules")]
    [SerializeField] private bool dropEquipment = true;
    [SerializeField] private float equipmentDropChance = 1.0f; // 100%
    [SerializeField] private bool dropInventory = true;
    [SerializeField] private float inventoryDropChance = 0.5f; // 50%
    [SerializeField] private bool usePoolSystem = true;

    [Header("Loot Container")]
    [SerializeField] private GameObject lootContainerPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugLoot = false;

    private ControllerBrain brain;
    private EquipmentSystem equipmentSystem;
    private InventorySystem inventorySystem; // May be null
    private bool isInitialized = false;

    public event Action<LootContainer> OnLootGenerated;

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
        equipmentSystem = brain.GetModule<EquipmentSystem>();
        inventorySystem = brain.GetModule<InventorySystem>(); // Optional

        // Subscribe to death
        var damageSystem = brain.GetModule<DamageSystem>();
        if (damageSystem != null)
        {
            damageSystem.OnDeath += OnEntityDeath;
        }

        isInitialized = true;

        if (debugLoot)
            Debug.Log($"[LootSystem] Initialized for {brain.name}");
    }

    public void LateInitialize() { }

    public void UpdateModule() { }

    public void Shutdown()
    {
        var damageSystem = brain?.GetModule<DamageSystem>();
        if (damageSystem != null)
        {
            damageSystem.OnDeath -= OnEntityDeath;
        }
    }

    #endregion

    #region Death Handling

    private void OnEntityDeath()
    {
        Vector3 deathPosition = brain.transform.position;
        GenerateLoot(deathPosition);
    }

    public void GenerateLoot(Vector3 position)
    {
        var lootedItems = new List<ItemInstance>();

        // Source 1: Loot pools (NPCs without items)
        if (usePoolSystem)
        {
            AddPoolLoot(lootedItems, position);
        }

        // Source 2: Equipped items (players, geared NPCs)
        if (dropEquipment)
        {
            AddEquipmentLoot(lootedItems);
        }

        // Source 3: Inventory items (players, pickpocket-able NPCs)
        if (dropInventory)
        {
            AddInventoryLoot(lootedItems);
        }

        // Spawn loot container if we have items
        if (lootedItems.Count > 0)
        {
            SpawnLootContainer(position, lootedItems);
        }
        else if (debugLoot)
        {
            Debug.Log($"[LootSystem] {brain.name} dropped no loot");
        }
    }

    #endregion

    #region Loot Sources

    private void AddPoolLoot(List<ItemInstance> lootedItems, Vector3 position)
    {
        // Guard clause: No loot configuration (TODO: Implement when EntityLootConfiguration exists)
        // if (lootConfiguration == null) return;

        // var poolDrops = LootManager.RollLoot(lootConfiguration, position);

        // foreach (var drop in poolDrops)
        // {
        //     var item = new ItemInstance(drop.itemId, drop.tier);
        //     
        //     // Guard clause: Invalid item
        //     if (item.Definition == null)
        //     {
        //         Debug.LogError($"[LootSystem] Invalid loot item: {drop.itemId}");
        //         continue;
        //     }
        //     
        //     lootedItems.Add(item);
        //
        //     if (debugLoot)
        //         Debug.Log($"[LootSystem] Pool dropped: {item.Definition.displayName}");
        // }
    }

    private void AddEquipmentLoot(List<ItemInstance> lootedItems)
    {
        // Guard clause: No equipment system
        if (equipmentSystem == null) return;

        var equippedItems = equipmentSystem.GetAllEquippedItems();

        // Iterate over dictionary values (items, not KeyValuePairs)
        foreach (var kvp in equippedItems)
        {
            ItemInstance item = kvp.Value;

            // Skip empty slots
            if (item == null) continue;

            // Roll drop chance
            if (UnityEngine.Random.value > equipmentDropChance) continue;

            lootedItems.Add(item);

            if (debugLoot)
                Debug.Log($"[LootSystem] Equipment dropped: {item.Definition.displayName}");
        }
    }

    private void AddInventoryLoot(List<ItemInstance> lootedItems)
    {
        // Guard clause: No inventory system
        if (inventorySystem == null) return;

        var inventoryItems = inventorySystem.GetAllItems();

        foreach (var item in inventoryItems)
        {
            // Roll drop chance
            if (UnityEngine.Random.value > inventoryDropChance) continue;

            lootedItems.Add(item);

            if (debugLoot)
                Debug.Log($"[LootSystem] Inventory dropped: {item.Definition.displayName}");
        }
    }

    #endregion

    #region Container Spawning

    private void SpawnLootContainer(Vector3 position, List<ItemInstance> items)
    {
        // Guard clause: No prefab
        if (lootContainerPrefab == null)
        {
            Debug.LogWarning("[LootSystem] No lootContainerPrefab assigned - dropping items on ground");
            SpawnItemsOnGround(position, items);
            return;
        }

        var containerObj = Instantiate(lootContainerPrefab, position, Quaternion.identity);
        var lootComponent = containerObj.GetComponent<LootContainer>();

        // Guard clause: No LootContainer component
        if (lootComponent == null)
        {
            Debug.LogError("[LootSystem] lootContainerPrefab missing LootContainer component");
            Destroy(containerObj);
            SpawnItemsOnGround(position, items);
            return;
        }

        lootComponent.Initialize(items);
        OnLootGenerated?.Invoke(lootComponent);

        if (debugLoot)
            Debug.Log($"[LootSystem] Spawned loot container with {items.Count} items");
    }

    private void SpawnItemsOnGround(Vector3 center, List<ItemInstance> items)
    {
        float radius = 2f;

        foreach (var item in items)
        {
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * radius;
            Vector3 spawnPos = center + new Vector3(randomOffset.x, 0, randomOffset.y);

            // TODO: Implement ItemManager.SpawnItemInWorld in Phase 3
            // ItemManager.SpawnItemInWorld(item.definitionId, spawnPos, item.currentTier);
            Debug.LogWarning($"[LootSystem] Would spawn {item.Definition.displayName} at {spawnPos} - ItemManager not implemented yet");
        }
    }

    #endregion

    #region Debug Utilities

    [ContextMenu("Debug: Force Drop Loot")]
    private void DebugForceDropLoot()
    {
        GenerateLoot(brain.transform.position);
        Debug.Log("[LootSystem] Forced loot drop");
    }

    #endregion
}

/// <summary>
/// Loot drop data from pool rolls.
/// </summary>
public class LootDrop
{
    public string itemId;
    public int quantity;
    public ItemRarity tier;
}

/// <summary>
/// Loot container component (attach to prefab).
/// </summary>
public class LootContainer : MonoBehaviour
{
    private List<ItemInstance> containedItems = new List<ItemInstance>();

    public void Initialize(List<ItemInstance> items)
    {
        containedItems = items;
    }

    public ItemInstance[] GetItems()
    {
        return containedItems.ToArray();
    }

    public bool RemoveItem(ItemInstance item)
    {
        return containedItems.Remove(item);
    }

    public int ItemCount => containedItems.Count;
}