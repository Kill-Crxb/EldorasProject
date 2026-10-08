using UnityEngine;

/// <summary>Grid backed by an entity's InventorySystem — the player's bag or a container's.</summary>
public class UniversalInventoryGrid : UniversalGrid
{
    private InventorySystem inventorySystem;
    private ControllerBrain ownerBrain;

    public ControllerBrain OwnerBrain => ownerBrain;
    public InventorySystem InventorySystem => inventorySystem;
    public bool HasSource => inventorySystem != null;

    public void SetInventorySource(InventorySystem system, string displayName, bool isPlayer = false, ControllerBrain brain = null)
    {
        if (system == null) { Debug.LogError("[UniversalInventoryGrid] Attempted to set null inventory system!"); return; }

        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= OnInventoryDataChanged;

        inventorySystem = system;
        isPlayerInventory = isPlayer;
        gridName = displayName;
        ownerBrain = brain;

        AdoptSourceSize();

        if (!isInitialized)
            Initialize();
        else
            ConnectToDataSource();
    }

    // Live contents win; the authored container is the fallback before anything is loaded.
    private void AdoptSourceSize()
    {
        var contents = inventorySystem.GetCurrentContents();
        if (contents != null)
        {
            gridWidth = contents.gridWidth;
            gridHeight = contents.gridHeight;
            return;
        }

        var container = inventorySystem.DefaultContainer;
        if (container == null) return;

        gridWidth = container.gridWidth;
        gridHeight = container.gridHeight;
    }

    public void SetPlayerInventory(InventorySystem playerSystem, ControllerBrain playerBrain = null)
        => SetInventorySource(playerSystem, "Player Inventory", isPlayer: true, brain: playerBrain);

    public void SetContainerInventory(ControllerBrain containerBrain)
    {
        if (containerBrain == null) { Debug.LogError("[UniversalInventoryGrid] Container brain is null!"); return; }

        var system = containerBrain.GetModule<InventorySystem>();
        if (system == null) { Debug.LogError($"[UniversalInventoryGrid] {containerBrain.name} has no InventorySystem!"); return; }

        string displayName = containerBrain.Identity?.DisplayName ?? containerBrain.name;
        SetInventorySource(system, displayName, isPlayer: false);
    }

    protected override void ConnectToDataSource()
    {
        if (inventorySystem == null) return;

        inventorySystem.OnInventoryChanged += OnInventoryDataChanged;
        RefreshVisuals();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (inventorySystem != null)
            inventorySystem.OnInventoryChanged -= OnInventoryDataChanged;
    }

    private void OnInventoryDataChanged() => RefreshVisuals();

    protected override ItemInstance[] GetItems()
    {
        if (inventorySystem == null) return new ItemInstance[0];
        return inventorySystem.GetAllItems();
    }

    protected override bool AddItemToData(ItemInstance item, int gridX, int gridY)
    {
        if (inventorySystem == null) return false;
        item.gridX = gridX;
        item.gridY = gridY;
        return inventorySystem.AddItem(item);
    }

    protected override bool RemoveItemFromData(string itemId)
    {
        if (inventorySystem == null) return false;
        return inventorySystem.RemoveItem(itemId);
    }

    protected override bool MoveItemInData(string itemId, int newX, int newY)
    {
        if (inventorySystem == null) return false;
        var item = inventorySystem.GetItemInstance(itemId);
        if (item == null) return false;
        item.PlaceAtPosition(newX, newY);
        return true;
    }

    protected override bool CanPlaceInData(ItemInstance item, int gridX, int gridY, string excludeItemId = null)
    {
        if (inventorySystem == null) return false;
        if (gridX < 0 || gridY < 0) return false;
        if (gridX + item.itemWidth > gridWidth) return false;
        if (gridY + item.itemHeight > gridHeight) return false;

        foreach (var existing in inventorySystem.GetAllItems())
        {
            if (existing == null || !existing.IsPlaced) continue;
            if (excludeItemId != null && existing.instanceId == excludeItemId) continue;

            bool overlapsX = gridX < existing.gridX + existing.itemWidth && gridX + item.itemWidth > existing.gridX;
            bool overlapsY = gridY < existing.gridY + existing.itemHeight && gridY + item.itemHeight > existing.gridY;

            if (overlapsX && overlapsY) return false;
        }

        return true;
    }

    protected override ItemInstance GetItemInstance(string itemId)
    {
        if (inventorySystem == null) return null;
        return inventorySystem.GetItemInstance(itemId);
    }

    protected override void DecorateTooltip(ItemTooltipData tooltip, ItemInstance item)
    {
        VendorSystem vendor = VendorSystem.Of(this);
        if (vendor == null) return;
        tooltip.description += $"\n\n{vendor.PriceLabel(item)}";
    }

    public override void OnItemShiftClicked(string itemId)
    {
        var item = inventorySystem?.GetItemInstance(itemId);
        if (item == null) return;

        GridTransferManager.Instance.QuickTransfer(this, item);
    }

    public override void OnItemRightClicked(string itemId)
    {
        // Only handle right-click equip for player inventory
        if (!isPlayerInventory || ownerBrain == null) return;

        var item = inventorySystem?.GetItemInstance(itemId);
        if (item?.Definition?.subType == null) return;

        var targetSlot = item.Definition.subType.equipmentSlot;
        if (targetSlot == null) return; // Item isn't equippable

        var equipmentSystem = ownerBrain.GetModule<EquipmentSystem>();
        if (equipmentSystem == null) return;

        // Handles the bag side of the swap, and leaves the inventory untouched if it fails.
        equipmentSystem.EquipFromInventory(item, targetSlot);
    }
}