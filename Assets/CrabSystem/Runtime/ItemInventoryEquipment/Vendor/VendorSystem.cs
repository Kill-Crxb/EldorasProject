using UnityEngine;

/// <summary>
/// Makes this brain's InventorySystem a shop shelf. The shop is the normal container window:
/// dragging off the shelf buys, dragging onto it sells. GridTransferManager asks Allows before a
/// move and calls Settle after one.
/// The shelf is stocked on OnLoaded, not LateInitialize: brains wake in Awake, and ItemManager may
/// not have loaded its definitions yet at that point.
/// </summary>
public class VendorSystem : MonoBehaviour, IBrainModule
{
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private VendorDefinition definition;

    private ControllerBrain brain;
    private InventorySystem shelf;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public VendorDefinition Definition => definition;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        shelf = brain.GetModule<InventorySystem>();
        if (shelf == null) { Debug.LogError($"[VendorSystem] {brain.name} needs an InventorySystem for its shelf.", this); return; }
        if (definition == null) { Debug.LogError($"[VendorSystem] {brain.name} has no VendorDefinition.", this); return; }

        if (brain.IsLoaded) StockShelf();
        else brain.OnLoaded += StockShelf;
    }

    private void OnDestroy()
    {
        if (brain != null) brain.OnLoaded -= StockShelf;
    }

    public void UpdateModule() { }

    /// <summary>The vendor whose shelf this grid shows, if any.</summary>
    public static VendorSystem Of(UniversalGrid grid)
    {
        var inventoryGrid = grid as UniversalInventoryGrid;
        if (inventoryGrid == null || inventoryGrid.InventorySystem == null) return null;

        ControllerBrain owner = inventoryGrid.InventorySystem.Brain;
        if (owner == null) return null;

        var vendor = owner.GetModule<VendorSystem>();
        if (vendor == null || !vendor.isEnabled || vendor.definition == null) return null;
        return vendor;
    }

    public void Open(ControllerBrain customer)
    {
        if (UniversalWindowManager.Instance == null) return;
        UniversalWindowManager.Instance.OpenContainerWindow(customer, brain);
    }

    public bool IsShelf(UniversalGrid grid)
    {
        var inventoryGrid = grid as UniversalInventoryGrid;
        return inventoryGrid != null && inventoryGrid.InventorySystem == shelf;
    }

    // ── Trade ─────────────────────────────────────────────────────────────

    public bool Allows(ItemInstance item, bool fromShelf, InventorySystem customer)
    {
        if (item == null || item.Definition == null) return false;
        if (fromShelf) return CanPay(customer, Valuation.Price(item.Definition, definition));
        return definition.buysFromPlayer && item.Definition.isTradeable;
    }

    /// <param name="from">Where the item sat before the move — a restock takes the same slot.</param>
    public void Settle(ItemInstance item, bool fromShelf, InventorySystem customer, GridPosition from)
    {
        if (fromShelf)
        {
            Charge(customer, Valuation.Price(item.Definition, definition));
            Restock(item, from);
            return;
        }

        Pay(customer, Valuation.Offer(item.Definition, definition));
    }

    public string PriceLabel(ItemInstance item)
    {
        if (Valuation.IsFree(definition)) return "Free";

        int price = Valuation.Price(item.Definition, definition);
        string currency = CurrencyName();
        return $"Price: {price} {currency}";
    }

    // ── Shelf ─────────────────────────────────────────────────────────────

    // A runtime copy, so buying and selling never write into an asset.
    private void StockShelf()
    {
        var contents = ScriptableObject.CreateInstance<ContainerContents>();
        contents.containerId = definition.vendorId;
        contents.displayName = definition.displayName;
        contents.gridWidth = definition.shelfWidth;
        contents.gridHeight = definition.shelfHeight;
        shelf.LoadFromContents(contents);

        foreach (var entry in definition.stock)
        {
            if (entry.item == null) continue;
            var copy = ItemManager.CreateItem(entry.item.itemId);
            if (copy == null || shelf.AddItem(copy)) continue;
            Debug.LogWarning($"[VendorSystem] {definition.vendorId}: no room on the shelf for {entry.item.itemId}.", this);
        }
    }

    private void Restock(ItemInstance sold, GridPosition from)
    {
        VendorStock entry = definition.StockFor(sold.definitionId);
        if (entry == null || !entry.restocks) return;

        var copy = ItemManager.CreateItem(sold.definitionId, sold.currentTier);
        if (copy == null) return;

        copy.PlaceAtPosition(from.x, from.y);
        shelf.AddItem(copy);
    }

    // ── Money ─────────────────────────────────────────────────────────────

    // Coins are plain items for now, one instance each (Economy_Design.md §6; stacking is build step 4).
    private bool CanPay(InventorySystem customer, int price)
    {
        if (price <= 0) return true;
        if (customer == null) return false;
        return ((IInventoryProvider)customer).GetItemCount(definition.currencyItemId) >= price;
    }

    private void Charge(InventorySystem customer, int price)
    {
        if (price <= 0 || customer == null) return;
        ((IInventoryProvider)customer).RemoveItem(definition.currencyItemId, price);
    }

    private void Pay(InventorySystem customer, int offer)
    {
        if (offer <= 0 || customer == null) return;
        if (((IInventoryProvider)customer).AddItem(definition.currencyItemId, offer)) return;
        Debug.LogWarning($"[VendorSystem] {definition.vendorId}: the customer had no room for all {offer} {definition.currencyItemId}.", this);
    }

    private string CurrencyName()
    {
        ItemDefinition currency = ItemManager.GetDefinition(definition.currencyItemId);
        return currency != null ? currency.displayName : definition.currencyItemId;
    }
}
