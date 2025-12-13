using UnityEngine;

public class NullInventoryProvider : MonoBehaviour, IInventoryProvider
{
    public bool HasItem(string itemId) => false;
    public int GetItemCount(string itemId) => 0;
    public bool AddItem(string itemId, int quantity = 1) => false;
    public bool RemoveItem(string itemId, int quantity = 1) => false;
    public void ClearInventory() { }
}