// EquipmentSlotComponent.cs - Updated to remove InventorySlot dependencies
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

public class EquipmentSlotComponent : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Equipment Slot Settings")]
    [SerializeField] private EquipmentSlot equipmentSlotType;
    [SerializeField] private ItemType[] acceptedItemTypes; // Legacy fallback validation

    [Header("Slot Components")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image itemIcon;
    [SerializeField] private Image slotTypeIcon; // Shows what goes here when empty

    [Header("Visual States")]
    [SerializeField] private Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color equippedSlotColor = new Color(0.2f, 0.5f, 0.8f, 0.8f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);

    // Equipment state
    private ItemInstance currentItemInstance;
    private EquippedItemData equippedItem; // Legacy compatibility
    private bool isEmpty = true;

    // System references
    private PlayerItemsModule playerItemsModule;
    private InventoryGridController inventoryController;
    private bool isDraggingFromThisSlot = false;

    // Drag support
    private static EquipmentSlotComponent currentDraggedEquipmentSlot;
    private GameObject dragIcon;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    // Properties
    public EquipmentSlot SlotType => equipmentSlotType;
    public bool IsEmpty => isEmpty;
    public EquippedItemData EquippedItem => equippedItem; // Legacy support
    public ItemInstance CurrentItemInstance => currentItemInstance;

    public static EquipmentSlotComponent GetCurrentDraggedEquipmentSlot() => currentDraggedEquipmentSlot;

    void Awake()
    {
        // Auto-find components if not assigned
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (itemIcon == null)
        {
            Transform iconChild = transform.Find("ItemIcon");
            if (iconChild != null)
                itemIcon = iconChild.GetComponent<Image>();
        }

        if (slotTypeIcon == null)
        {
            Transform typeIconChild = transform.Find("SlotTypeIcon");
            if (typeIconChild != null)
                slotTypeIcon = typeIconChild.GetComponent<Image>();
        }

        // Get canvas and setup canvas group for drag alpha
        canvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (playerItemsModule == null)
        {
            playerItemsModule = FindFirstObjectByType<PlayerItemsModule>();
            if (playerItemsModule != null)
            {
                playerItemsModule.RegisterEquipmentSlot(this);
            }
        }
        inventoryController = FindFirstObjectByType<InventoryGridController>();
        // Initialize empty state
        SetEmpty();
    }

    // Called by PlayerItemsModule during initialization
    public void SetEquipmentManager(PlayerItemsModule equipmentManager)
    {
        playerItemsModule = equipmentManager;
    }

    public void SetEquippedItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
        {
            SetEmpty();
            return;
        }

        // Get item definition
        var definition = ItemDatabase.GetDefinition(itemInstance.definitionId);
        if (definition == null)
        {
            Debug.LogError($"Could not find definition for item: {itemInstance.definitionId}");
            SetEmpty();
            return;
        }

        // Store the ItemInstance
        currentItemInstance = itemInstance;
        isEmpty = false;

        // Create legacy EquippedItemData for display compatibility
        equippedItem = new EquippedItemData
        {
            itemName = definition.displayName,
            icon = definition.icon,
            rarity = itemInstance.currentTier,
            itemType = definition.category.ToString()
        };

        // Update visuals
        UpdateSlotDisplay(definition.icon, itemInstance.currentTier);
    }

    // Legacy method - still supported
    public void SetEquippedItem(EquippedItemData itemData)
    {
        if (itemData == null)
        {
            SetEmpty();
            return;
        }

        currentItemInstance = null; // Clear ItemInstance when using legacy method
        equippedItem = itemData;
        isEmpty = false;

        // Update visuals
        UpdateSlotDisplay(itemData.icon, itemData.rarity);
    }

    private void UpdateSlotDisplay(Sprite icon, ItemRarity rarity)
    {
        // Update visuals
        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = true;
        }

        if (slotTypeIcon != null)
            slotTypeIcon.enabled = false;

        if (slotBackground != null)
            slotBackground.color = GetRarityColor(rarity);
    }

    public void SetEmpty()
    {
        currentItemInstance = null;
        equippedItem = null;
        isEmpty = true;

        // Update visuals
        if (itemIcon != null)
        {
            itemIcon.sprite = null;
            itemIcon.enabled = false;
        }

        if (slotTypeIcon != null)
            slotTypeIcon.enabled = true; // Show what type goes here

        if (slotBackground != null)
            slotBackground.color = emptySlotColor;

        // Reset canvas group alpha
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    #region Drag and Drop Implementation

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isEmpty) return;

        isDraggingFromThisSlot = true; // Set flag BEFORE anything else
        currentDraggedEquipmentSlot = this;
        CreateDragIcon();

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;

        // Notify inventory controller
        if (inventoryController != null && currentItemInstance != null)
        {
            GridArea area = new GridArea(
                new GridPosition(0, 0),
                currentItemInstance.itemWidth,
                currentItemInstance.itemHeight
            );
            inventoryController.OnItemDragStart(currentItemInstance.instanceId, area);
        }
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDraggingFromThisSlot || dragIcon == null) return;

        dragIcon.transform.position = eventData.position;

        // Update inventory highlights
        if (inventoryController != null && currentItemInstance != null)
        {
            GridArea area = new GridArea(
                new GridPosition(0, 0),
                currentItemInstance.itemWidth,
                currentItemInstance.itemHeight
            );
            inventoryController.OnItemDragUpdate(eventData.position, area);
        }
    }
    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDraggingFromThisSlot) return; // Check drag flag instead of isEmpty

        // ALWAYS cleanup first
        CleanupDragIcon();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        isDraggingFromThisSlot = false; // Clear flag
        StartCoroutine(ClearDraggedEquipmentAfterDelay());
    }

    private IEnumerator ClearDraggedEquipmentAfterDelay()
    {
        yield return null; // Wait one frame
        currentDraggedEquipmentSlot = null;
    }

    // UPDATED: Remove InventorySlot dependency
    public void OnDrop(PointerEventData eventData)
    {
        // TODO: Handle inventory to equipment drops (new system)
        // This will be implemented when we add drag tracking to the new visual system

        // For now, just handle equipment to equipment drops (future feature)
        EquipmentSlotComponent draggedEquipmentSlot = GetCurrentDraggedEquipmentSlot();
        if (draggedEquipmentSlot != null && draggedEquipmentSlot != this)
        {
            // Equipment to equipment swapping (future feature)
            Debug.Log($"Equipment to equipment drop attempted: {draggedEquipmentSlot.SlotType} → {equipmentSlotType}");
            return;
        }

        // If no valid drop was detected, show invalid feedback
        if (draggedEquipmentSlot == null)
        {
            StartCoroutine(ShowInvalidDropFeedback());
        }
    }

    public void CleanupDragIcon()
    {
        if (dragIcon != null)
        {
            dragIcon.SetActive(false); // Hide immediately
            Destroy(dragIcon); // Queue destruction
            dragIcon = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }
    private void CreateDragIcon()
    {
        if (itemIcon == null || itemIcon.sprite == null || canvas == null) return;
        if (currentItemInstance == null) return;

        GameObject dragObject = new GameObject("EquipmentDragIcon");
        dragIcon = dragObject;
        dragIcon.transform.SetParent(canvas.transform, false);
        dragIcon.transform.SetAsLastSibling();

        Image dragImage = dragIcon.AddComponent<Image>();
        dragImage.sprite = itemIcon.sprite;
        dragImage.raycastTarget = false;

        // Use actual item size instead of hardcoded 70x70
        float slotSize = 64f;
        float slotSpacing = 2f;
        float width = currentItemInstance.itemWidth * slotSize + (currentItemInstance.itemWidth - 1) * slotSpacing;
        float height = currentItemInstance.itemHeight * slotSize + (currentItemInstance.itemHeight - 1) * slotSpacing;

        RectTransform dragRect = dragIcon.GetComponent<RectTransform>();
        dragRect.sizeDelta = new Vector2(width, height);
        dragRect.position = transform.position;

        CanvasGroup dragCanvasGroup = dragIcon.AddComponent<CanvasGroup>();
        dragCanvasGroup.alpha = 0.8f;
        dragCanvasGroup.blocksRaycasts = false;
    }

    #endregion

    // UPDATED: Legacy validation method (used until new drag system is implemented)
    private bool CanEquipItemLegacy(string itemName)
    {
        // Map common item names to equipment slots
        string lowerName = itemName.ToLower();

        // Check by item definition if possible
        var allDefinitions = new string[] { "headguard_common", "headband_common", "circlet_common", "sword_common", "axe_common" };

        foreach (string defId in allDefinitions)
        {
            var definition = ItemDatabase.GetDefinition(defId);
            if (definition != null && definition.displayName.ToLower().Contains(lowerName))
            {
                return definition.equipmentSlot == equipmentSlotType;
            }
        }

        // Fallback to type matching
        foreach (ItemType acceptedType in acceptedItemTypes)
        {
            if (lowerName.Contains(acceptedType.ToString().ToLower()))
            {
                return true;
            }
        }

        return false;
    }

    private IEnumerator ShowInvalidDropFeedback()
    {
        Color originalColor = slotBackground.color;
        slotBackground.color = invalidDropColor;
        yield return new WaitForSeconds(0.3f);
        slotBackground.color = originalColor;
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.5f, 0.5f, 0.5f, 0.8f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f, 0.8f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f, 0.8f);
            case ItemRarity.Epic: return new Color(0.8f, 0.2f, 0.8f, 0.8f);
            case ItemRarity.Legendary: return new Color(1f, 0.6f, 0.1f, 0.8f);
            default: return equippedSlotColor;
        }
    }

    // Tooltip support - enhanced with ItemInstance stats
    public void OnPointerEnter(PointerEventData eventData)
    {
        // Drag feedback
        if (eventData.pointerDrag != null)
        {
            ItemIconVisual draggedIcon = eventData.pointerDrag.GetComponent<ItemIconVisual>();
            if (draggedIcon != null && draggedIcon.ItemInstance != null)
            {
                var definition = ItemDatabase.GetDefinition(draggedIcon.ItemInstance.definitionId);
                bool canEquip = definition != null && definition.equipmentSlot == equipmentSlotType;

                slotBackground.color = canEquip ?
                    new Color(0f, 1f, 0f, 0.5f) :
                    new Color(1f, 0f, 0f, 0.5f);
                return;
            }
        }
        if (isEmpty) return;

        string tooltipDescription;

        if (currentItemInstance != null)
        {
            // Enhanced tooltip with ItemInstance stats
            var definition = ItemDatabase.GetDefinition(currentItemInstance.definitionId);
            tooltipDescription = definition?.description ?? "Equipped item";

            // Add stat information
            if (currentItemInstance.calculatedModifiers?.Length > 0)
            {
                tooltipDescription += "\n\nStats:";
                foreach (var modifier in currentItemInstance.calculatedModifiers)
                {
                    tooltipDescription += $"\n+{modifier.value:F1} {modifier.statName}";
                }
            }
        }
        else if (equippedItem != null)
        {
            // Legacy tooltip
            tooltipDescription = $"Equipped {equippedItem.itemType}";
        }
        else
        {
            return;
        }

        ItemTooltipData tooltipData = new ItemTooltipData(
            equippedItem?.itemName ?? "Unknown Item",
            tooltipDescription,
            equippedItem?.itemType ?? "Unknown",
            equippedItem?.rarity ?? ItemRarity.Common,
            1
        );

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(tooltipData, eventData.position); // Use eventData.position, not Input.mousePosition
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Reset background color
        slotBackground.color = isEmpty ? emptySlotColor : equippedSlotColor;

        // Hide tooltip (existing code)
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    // Public method for new system integration (called by InteractionZone or similar)
    public bool HandleInventoryToEquipmentDrop(ItemInstance item)
    {
        Debug.Log($"[EquipSlot] HandleDrop called - item: {item?.definitionId}, slot: {equipmentSlotType}");

        if (item == null)
        {
            Debug.LogWarning("[EquipSlot] Item is null");
            return false;
        }

        if (playerItemsModule == null)
        {
            Debug.LogError("[EquipSlot] PlayerItemsModule is null!");
            return false;
        }

        var definition = ItemDatabase.GetDefinition(item.definitionId);
        if (definition == null)
        {
            Debug.LogError($"[EquipSlot] Definition not found for: {item.definitionId}");
            return false;
        }

        Debug.Log($"[EquipSlot] Item slot: {definition.equipmentSlot}, Target slot: {equipmentSlotType}");

        if (definition.equipmentSlot != equipmentSlotType)
        {
            Debug.LogWarning($"[EquipSlot] Slot mismatch! Item wants {definition.equipmentSlot}, dropping on {equipmentSlotType}");
            StartCoroutine(ShowInvalidDropFeedback());
            return false;
        }

        bool result = playerItemsModule.EquipItem(item, equipmentSlotType);
        Debug.Log($"[EquipSlot] EquipItem result: {result}");
        return result;
    }
}

// Equipment item data - kept for backward compatibility
[System.Serializable]
public class EquippedItemData
{
    public string itemName;
    public Sprite icon;
    public ItemRarity rarity;
    public string itemType;
}