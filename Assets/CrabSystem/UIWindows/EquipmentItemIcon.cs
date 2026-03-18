using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// EquipmentItemIcon - Handles equipped item display and interactions
/// 
/// Similar to ItemIconVisual but specialized for equipment slots:
/// - Receives drops from inventory (via IDropHandler)
/// - Shows tooltips on hover
/// - Right-click to unequip
/// - No dragging (use right-click instead to prevent accidents)
/// 
/// Standards Compliance:
/// - Guard clauses throughout
/// - Event-driven
/// - Single responsibility
/// 
/// Created: February 18, 2026
/// </summary>
public class EquipmentItemIcon : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Visual")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Color validDropColor = new Color(0.2f, 0.8f, 0.2f, 0.6f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private EquipmentSlotDefinition slotDefinition;
    private EquipmentSlotConfig slotConfig;
    private EquipmentSystem equipmentSystem;
    private ItemInstance currentItem;
    private Color originalColor;

    #region Initialization

    public void Initialize(EquipmentSlotConfig config, EquipmentSystem system)
    {
        slotConfig = config;
        slotDefinition = config.slotDefinition;
        equipmentSystem = system;

        // Get or add image component
        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }
        if (iconImage == null)
        {
            iconImage = gameObject.AddComponent<Image>();
        }

        iconImage.raycastTarget = true; // IMPORTANT: Must receive drops!
        originalColor = Color.white;
    }

    public void SetItem(ItemInstance item)
    {
        currentItem = item;

        // Guard clause: no item
        if (item == null || item.Definition == null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
            return;
        }

        // Set sprite
        iconImage.sprite = item.Definition.icon;
        iconImage.color = originalColor;
        iconImage.enabled = true;
    }

    #endregion

    #region Drop Handling

    public void OnDrop(PointerEventData eventData)
    {
        // Guard clause: no dragged object
        if (eventData.pointerDrag == null) return;

        // Get ItemIconVisual from dragged object
        ItemIconVisual draggedIcon = eventData.pointerDrag.GetComponent<ItemIconVisual>();

        // Guard clause: not dragging an item
        if (draggedIcon == null) return;

        ItemInstance item = draggedIcon.ItemInstance;

        // Guard clause: no item instance
        if (item == null)
        {
            if (debugMode)
                Debug.LogWarning("[EquipmentItemIcon] Dragged icon has no ItemInstance");
            return;
        }

        // Always hide tooltip on any drop interaction
        HideTooltip();

        // Validate and equip
        if (CanEquipItem(item))
        {
            EquipItem(item);
        }
        else
        {
            ShowInvalidDropFeedback();
        }
    }

    private bool CanEquipItem(ItemInstance item)
    {
        // Guard clause: null item
        if (item == null) return false;

        // Guard clause: no definition
        if (item.Definition == null) return false;

        // Use config validation if available (more flexible)
        if (slotConfig != null)
        {
            return slotConfig.CanEquipItem(item);
        }

        // If we get here, slotConfig validation passed
        return true;
    }

    private void EquipItem(ItemInstance item)
    {
        // Guard clause: no equipment system
        if (equipmentSystem == null)
        {
            Debug.LogError("[EquipmentItemIcon] EquipmentSystem is null!");
            return;
        }

        bool success = equipmentSystem.EquipItem(item, slotDefinition);

        if (debugMode)
            Debug.Log($"[EquipmentItemIcon] Equip {item.Definition.displayName} to {slotDefinition.displayName}: {success}");

        // Visual update will happen via EquipmentSlotVisual.HandleEquipmentChanged
    }

    #endregion

    #region Pointer Events

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Drag preview feedback
        if (eventData.pointerDrag != null)
        {
            ItemIconVisual draggedIcon = eventData.pointerDrag.GetComponent<ItemIconVisual>();
            if (draggedIcon != null && draggedIcon.ItemInstance != null)
            {
                bool canEquip = CanEquipItem(draggedIcon.ItemInstance);
                if (iconImage != null)
                {
                    iconImage.color = canEquip ? validDropColor : invalidDropColor;
                }
                return;
            }
        }

        // Tooltip for equipped item
        if (currentItem != null)
        {
            ShowTooltip(eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Reset color
        if (iconImage != null)
        {
            iconImage.color = originalColor;
        }

        // Hide tooltip
        HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Right-click to unequip
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            UnequipItem();
        }
    }

    private void UnequipItem()
    {
        // Guard clause: nothing equipped
        if (currentItem == null) return;

        // Guard clause: no equipment system
        if (equipmentSystem == null)
        {
            Debug.LogError("[EquipmentItemIcon] EquipmentSystem is null!");
            return;
        }

        bool success = equipmentSystem.UnequipItemToInventory(slotDefinition);

        if (debugMode)
            Debug.Log($"[EquipmentItemIcon] Unequip from {slotDefinition.displayName}: {success}");

        // Visual update will happen via EquipmentSlotVisual.HandleEquipmentChanged
    }

    #endregion

    #region Tooltips

    private void ShowTooltip(Vector2 position)
    {
        if (currentItem == null) return;
        if (currentItem.Definition == null) return;

        string description = currentItem.Definition.description;

        if (currentItem.calculatedModifiers != null && currentItem.calculatedModifiers.Length > 0)
        {
            description += "\n\nStats:";
            foreach (var modifier in currentItem.calculatedModifiers)
                description += $"\n+{modifier.value:F1} {modifier.statName}";
        }

        ItemTooltipData tooltipData = new ItemTooltipData(
            currentItem.Definition.displayName,
            description,
            currentItem.Definition.category.ToString(),
            currentItem.currentTier,
            1
        );

        UniversalWindowManager.Instance?.ShowTooltip(tooltipData, position);
    }

    private void HideTooltip()
    {
        UniversalWindowManager.Instance?.HideTooltip();
    }

    #endregion

    #region Visual Feedback

    private void ShowInvalidDropFeedback()
    {
        if (iconImage == null) return;

        // Flash red briefly
        StartCoroutine(FlashColor(invalidDropColor, 0.3f));
    }

    private System.Collections.IEnumerator FlashColor(Color flashColor, float duration)
    {
        Color originalColor = iconImage.color;
        iconImage.color = flashColor;
        yield return new WaitForSeconds(duration);
        iconImage.color = originalColor;
    }

    #endregion
}