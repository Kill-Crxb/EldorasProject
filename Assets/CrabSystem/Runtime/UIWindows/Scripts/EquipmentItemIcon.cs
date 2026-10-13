using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The item shown in one equipment socket. Takes drops from the inventory, unequips on
/// right-click, and shows the same tooltip a bag slot would. Deliberately not draggable —
/// right-click is harder to do by accident than a drag into empty space.
/// </summary>
public class EquipmentItemIcon : MonoBehaviour, IDropHandler, IPointerEnterHandler,
                                 IPointerExitHandler, IPointerClickHandler
{
    private const float InvalidFlashSeconds = 0.3f;

    [Header("Visual")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Color validDropColor = new Color(0.2f, 0.8f, 0.2f, 0.6f);
    [SerializeField] private Color invalidDropColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);

    private EquipmentSlotDefinition slotDefinition;
    private EquipmentSlotConfig slotConfig;
    private EquipmentSystem equipmentSystem;
    private ItemInstance currentItem;
    private Color restingColor = Color.white;

    public void Initialize(EquipmentSlotConfig config, EquipmentSystem system)
    {
        slotConfig = config;
        slotDefinition = config.slotDefinition;
        equipmentSystem = system;

        if (iconImage == null) iconImage = GetComponent<Image>();
        if (iconImage == null) iconImage = gameObject.AddComponent<Image>();

        iconImage.raycastTarget = true;
    }

    public void SetItem(ItemInstance item)
    {
        currentItem = item;

        // The image stays enabled while empty so the socket still receives drops —
        // a disabled Graphic takes no raycasts.
        if (item == null || item.Definition == null)
        {
            iconImage.sprite = slotConfig?.emptySlotIcon;
            iconImage.color = iconImage.sprite != null ? restingColor : Color.clear;
            iconImage.enabled = true;
            return;
        }

        iconImage.sprite = item.Definition.icon;
        iconImage.color = restingColor;
        iconImage.enabled = true;
    }

    #region Drop

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        var draggedIcon = eventData.pointerDrag.GetComponent<ItemIconVisual>();
        if (draggedIcon == null) return;

        ItemInstance item = draggedIcon.ItemInstance;
        if (item == null)
        {
            Debug.LogWarning("[EquipmentItemIcon] Dragged icon has no ItemInstance");
            return;
        }

        HideTooltip();

        if (!CanEquip(item))
        {
            StartCoroutine(FlashInvalid());
            return;
        }

        if (equipmentSystem == null)
        {
            Debug.LogError("[EquipmentItemIcon] No EquipmentSystem bound");
            return;
        }

        // Moves the item out of the bag as well — plain EquipItem would leave a copy behind.
        equipmentSystem.EquipFromInventory(item, slotDefinition);
    }

    private bool CanEquip(ItemInstance item)
    {
        if (item == null || item.Definition == null) return false;

        return slotConfig == null || slotConfig.CanEquipItem(item);
    }

    #endregion

    #region Pointer

    public void OnPointerEnter(PointerEventData eventData)
    {
        var draggedIcon = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<ItemIconVisual>()
            : null;

        if (draggedIcon != null && draggedIcon.ItemInstance != null)
        {
            iconImage.color = CanEquip(draggedIcon.ItemInstance) ? validDropColor : invalidDropColor;
            return;
        }

        if (currentItem == null) return;

        var tooltip = ItemTooltipData.For(currentItem);
        if (tooltip != null) UniversalWindowManager.Instance?.ShowTooltip(tooltip, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        iconImage.color = restingColor;
        HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right) return;
        if (currentItem == null) return;

        if (equipmentSystem == null)
        {
            Debug.LogError("[EquipmentItemIcon] No EquipmentSystem bound");
            return;
        }

        equipmentSystem.UnequipItemToInventory(slotDefinition);
    }

    #endregion

    private void HideTooltip() => UniversalWindowManager.Instance?.HideTooltip();

    private IEnumerator FlashInvalid()
    {
        iconImage.color = invalidDropColor;
        yield return new WaitForSeconds(InvalidFlashSeconds);
        iconImage.color = restingColor;
    }
}
