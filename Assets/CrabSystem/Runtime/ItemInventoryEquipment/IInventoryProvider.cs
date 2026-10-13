public interface IInventoryProvider
{
    bool HasItem(string itemId);
    int GetItemCount(string itemId);
    bool AddItem(string itemId, int quantity = 1);
    bool RemoveItem(string itemId, int quantity = 1);
    void ClearInventory();
}