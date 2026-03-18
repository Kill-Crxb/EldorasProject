using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// EquipmentSlotVisual_Simple - Manages pre-placed equipment slot components
/// 
/// Works with complete equipment slot prefabs that already have all 3 layers assembled.
/// Instead of creating prefabs at runtime, this just finds and uses the existing ones.
/// 
/// Architecture:
/// - GridSlotBackground: Already in BackgroundLayer (from inventory prefab)
/// - ItemOverlayVisual: Already in OverlayLayer (from inventory prefab)
/// - EquipmentItemIcon: Already in IconLayer (equipment-specific prefab)
/// 
/// This script just:
/// 1. Gets references to the pre-placed components
/// 2. Initializes them
/// 3. Shows/hides them based on equipped state
/// 
/// Standards Compliance:
/// - Guard clauses throughout
/// - Event-driven updates
/// - Single responsibility
/// 
/// Created: February 18, 2026
/// </summary>
public class EquipmentSlotVisual_Simple : MonoBehaviour
{
    #region Inspector References

    [Header("Component References - Assign from Prefab")]
    [Tooltip("Drag GridSlotBackground from BackgroundLayer")]
    [SerializeField] private GridSlotBackground background;

    [Tooltip("Drag ItemOverlayVisual from OverlayLayer")]
    [SerializeField] private ItemOverlayVisual overlay;

    [Tooltip("Drag EquipmentItemIcon from IconLayer")]
    [SerializeField] private EquipmentItemIcon itemIcon;

    [Header("Slot Configuration")]
    [SerializeField] private float slotSize = 64f;
    [SerializeField] private Vector2 weaponIconSize = new Vector2(64, 128);

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region State

    private EquipmentSlotDefinition slotDefinition; // The SO that defines this slot
    private EquipmentSlotConfig slotConfig; // Full configuration
    private EquipmentSystem equipmentSystem;
    private ItemInstance currentItem;
    private bool isInitialized = false;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialize this slot - sets up the pre-placed components
    /// Called by EquipmentWindow when creating slots
    /// </summary>
    public void Initialize(EquipmentSlotConfig config, EquipmentSystem system)
    {
        // Guard clause: already initialized
        if (isInitialized)
        {
            Debug.LogWarning("[EquipmentSlotVisual_Simple] Already initialized!");
            return;
        }

        // Guard clause: null config
        if (config == null)
        {
            Debug.LogError("[EquipmentSlotVisual_Simple] EquipmentSlotConfig is null!");
            return;
        }

        // Guard clause: null system
        if (system == null)
        {
            Debug.LogError("[EquipmentSlotVisual_Simple] EquipmentSystem_DataDriven is null!");
            return;
        }

        slotConfig = config;
        slotDefinition = config.slotDefinition; // Store the ScriptableObject
        equipmentSystem = system;

        // Initialize the pre-placed components
        InitializeBackground(config);
        InitializeOverlay();
        InitializeIcon();

        // Subscribe to equipment changes
        equipmentSystem.OnEquipmentChanged += HandleEquipmentChanged;

        // Must be true before RefreshDisplay so guard clause passes
        isInitialized = true;

        // Display current item if already equipped when window opens
        RefreshDisplay();

        if (debugMode)
            Debug.Log($"[EquipmentSlotVisual_Simple] Initialized slot: {config}");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (equipmentSystem != null)
        {
            equipmentSystem.OnEquipmentChanged -= HandleEquipmentChanged;
        }
    }

    private void InitializeBackground(EquipmentSlotConfig config)
    {
        // Guard clause: no background
        if (background == null)
        {
            Debug.LogWarning($"[EquipmentSlotVisual_Simple] No background for {slotDefinition.displayName}!");
            return;
        }

        // Initialize background at position (0,0) with slot size
        background.Initialize(new GridPosition(0, 0), slotSize, 0f);

        // Set custom background sprite if provided
        if (config.slotBackground != null)
        {
            Image bgImage = background.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.sprite = config.slotBackground;
            }
        }

        // Set empty slot icon if provided
        // TODO: Add SlotTypeIcon image to show when empty
        // For now, this would need to be added to the prefab structure

        if (debugMode)
            Debug.Log($"[EquipmentSlotVisual_Simple] Initialized background for {slotDefinition.displayName}");
    }

    private void InitializeOverlay()
    {
        // Guard clause: no overlay
        if (overlay == null)
        {
            if (debugMode)
                Debug.LogWarning($"[EquipmentSlotVisual_Simple] No overlay for {slotDefinition.displayName}");
            return;
        }

        // Determine size based on slot config
        bool isWeapon = slotConfig != null && slotConfig.isWeaponSlot;
        int width = isWeapon ? 1 : 1;
        int height = isWeapon ? 2 : 1;

        // Create grid area
        GridArea area = new GridArea(new GridPosition(0, 0), width, height);

        // Initialize with rarity 0 (will be updated when item equipped)
        overlay.Initialize("", area, 0, slotSize, 0f);

        // Hide initially (shown when item equipped)
        overlay.gameObject.SetActive(false);

        if (debugMode)
            Debug.Log($"[EquipmentSlotVisual_Simple] Initialized overlay for {slotDefinition.displayName}");
    }

    private void InitializeIcon()
    {
        // Guard clause: no icon
        if (itemIcon == null)
        {
            Debug.LogWarning($"[EquipmentSlotVisual_Simple] No icon for {slotDefinition.displayName}!");
            return;
        }

        // Initialize icon with slot config and equipment system
        itemIcon.Initialize(slotConfig, equipmentSystem);

        // Hide initially (shown when item equipped)
        itemIcon.gameObject.SetActive(false);

        if (debugMode)
            Debug.Log($"[EquipmentSlotVisual_Simple] Initialized icon for {slotDefinition.displayName}");
    }

    #endregion

    #region Display Updates

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        // Guard clause: not our slot
        if (slot != slotDefinition) return;

        currentItem = item;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        // Guard clause: not initialized
        if (!isInitialized) return;

        if (currentItem == null)
        {
            ShowEmpty();
        }
        else
        {
            ShowEquipped(currentItem);
        }
    }

    private void ShowEmpty()
    {
        // Hide overlay
        if (overlay != null)
        {
            overlay.gameObject.SetActive(false);
        }

        // Hide icon
        if (itemIcon != null)
        {
            itemIcon.gameObject.SetActive(false);
        }

        // Background stays visible (shows empty slot)
    }

    private void ShowEquipped(ItemInstance item)
    {
        // Guard clause: no definition
        if (item.Definition == null)
        {
            ShowEmpty();
            return;
        }

        // Show overlay with rarity color
        if (overlay != null)
        {
            // Determine size based on slot config
            bool isWeapon = slotConfig != null && slotConfig.isWeaponSlot;
            int width = isWeapon ? 1 : 1;
            int height = isWeapon ? 2 : 1;
            GridArea area = new GridArea(new GridPosition(0, 0), width, height);

            // Re-initialize with new rarity (sets color internally)
            overlay.Initialize(item.instanceId, area, (int)item.currentTier, slotSize, 0f);
            overlay.gameObject.SetActive(true);
        }

        // Show icon with item sprite
        if (itemIcon != null)
        {
            itemIcon.SetItem(item);
            itemIcon.gameObject.SetActive(true);
        }
    }

    #endregion

    #region Properties

    public EquipmentSlotDefinition SlotDefinition => slotDefinition;
    public ItemInstance CurrentItem => currentItem;
    public bool IsOccupied => currentItem != null;

    #endregion

    #region Debug

    [ContextMenu("Debug: Print Slot Info")]
    private void DebugPrintInfo()
    {
        Debug.Log($"=== Equipment Slot: {slotDefinition?.displayName} ({slotDefinition?.slotId}) ===");
        Debug.Log($"Initialized: {isInitialized}");
        Debug.Log($"Has Background: {background != null}");
        Debug.Log($"Has Overlay: {overlay != null}");
        Debug.Log($"Has Icon: {itemIcon != null}");
        Debug.Log($"Current Item: {(currentItem != null ? currentItem.Definition.displayName : "None")}");
    }

    #endregion
}
