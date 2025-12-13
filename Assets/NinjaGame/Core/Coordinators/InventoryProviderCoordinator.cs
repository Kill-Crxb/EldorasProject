using UnityEngine;

/// <summary>
/// Coordinates inventory and equipment provider modules.
/// Validates that inventory/equipment implementations conform to their interfaces.
/// </summary>
public class InventoryProviderCoordinator : MonoBehaviour
{
    [Header("Provider Slots")]
    [SerializeField] private MonoBehaviour inventoryProvider;
    [SerializeField] private MonoBehaviour equipmentProvider;

    private ControllerBrain brain;
    private IInventoryProvider inventory;
    private IEquipmentProvider equipment;

    public IInventoryProvider Inventory => inventory;
    public IEquipmentProvider Equipment => equipment;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        ValidateAndCacheProviders();
    }
    public void InitializeChildModules()
    {
        // InventoryProviderCoordinator has no child modules to initialize
        // This method exists for compatibility with ControllerBrain's initialization flow
    }
    public void UpdateCoordinator()
    {
        // Inventory providers typically don't need update loops
        // They're event-driven through Add/Remove/Use item calls
    }

    public void PhysicsUpdateCoordinator()
    {
        // No physics updates needed for inventory
    }

    void ValidateAndCacheProviders()
    {
        // Validate Inventory Provider
        if (inventoryProvider != null)
        {
            inventory = inventoryProvider as IInventoryProvider;
            if (inventory == null)
            {
                Debug.LogError($"[InventoryProviderCoordinator] {inventoryProvider.GetType().Name} does not implement IInventoryProvider!");
            }
        }
        else
        {
            inventory = new NullInventoryProvider();
            if (Application.isPlaying)
                Debug.LogWarning($"[InventoryProviderCoordinator] No inventory provider assigned on {brain.name}, using NullInventoryProvider");
        }

        // Validate Equipment Provider
        if (equipmentProvider != null)
        {
            equipment = equipmentProvider as IEquipmentProvider;
            if (equipment == null)
            {
                Debug.LogError($"[InventoryProviderCoordinator] {equipmentProvider.GetType().Name} does not implement IEquipmentProvider!");
            }
        }
        else
        {
            equipment = new NullEquipmentProvider();
            if (Application.isPlaying)
                Debug.LogWarning($"[InventoryProviderCoordinator] No equipment provider assigned on {brain.name}, using NullEquipmentProvider");
        }
    }

    void OnValidate()
    {
        // Editor-time validation
        if (inventoryProvider != null && !(inventoryProvider is IInventoryProvider))
        {
            Debug.LogError($"[InventoryProviderCoordinator] {inventoryProvider.GetType().Name} must implement IInventoryProvider!");
            inventoryProvider = null;
        }

        if (equipmentProvider != null && !(equipmentProvider is IEquipmentProvider))
        {
            Debug.LogError($"[InventoryProviderCoordinator] {equipmentProvider.GetType().Name} must implement IEquipmentProvider!");
            equipmentProvider = null;
        }
    }
}