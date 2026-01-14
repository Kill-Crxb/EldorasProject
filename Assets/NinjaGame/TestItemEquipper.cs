
using UnityEngine;

public class TestWeaponEquipper : MonoBehaviour
{
    [Header("Test Setup")]
    [SerializeField] private PlayerItemsModule playerItems;
    [SerializeField] private string weaponItemId = "sword_iron";
    [SerializeField] private ItemRarity weaponTier = ItemRarity.Common;

    [ContextMenu("Equip Test Weapon")]
    public void EquipTestWeapon()
    {
        if (playerItems == null)
        {
            playerItems = FindFirstObjectByType<PlayerItemsModule>();
        }

        // Create weapon instance
        var weaponInstance = ItemDatabase.CreateItem(weaponItemId, weaponTier);

        if (weaponInstance != null)
        {
            // Add to inventory
            playerItems.AddItem(weaponInstance);

            // Equip to Weapon1 slot
            bool success = playerItems.EquipItem(weaponInstance, EquipmentSlot.Weapon1);

            Debug.Log(success ?
                $"✓ Equipped {weaponItemId} (Tier: {weaponTier})" :
                "✗ Failed to equip weapon");
        }
        else
        {
            Debug.LogError($"Failed to create weapon: {weaponItemId}");
        }
    }

    [ContextMenu("Unequip Weapon")]
    public void UnequipWeapon()
    {
        if (playerItems == null)
        {
            playerItems = FindFirstObjectByType<PlayerItemsModule>();
        }

        playerItems.UnequipItem(EquipmentSlot.Weapon1);
        Debug.Log("Unequipped weapon");
    }
}