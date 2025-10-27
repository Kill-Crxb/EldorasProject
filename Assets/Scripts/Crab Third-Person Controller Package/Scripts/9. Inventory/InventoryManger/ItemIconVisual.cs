using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ItemIconVisual : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    private InventoryGridController controller;
    private PlayerItemsModule playerItemsModule;
    private string itemInstanceId;
    private ItemInstance itemInstance;
    private GridArea currentArea;
    private float slotSize;
    private float slotSpacing;

    private RectTransform rectTransform;
    private Transform originalParent;
    private Vector2 originalPosition;
    private int originalSiblingIndex;
    private bool isDragging = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        playerItemsModule = FindFirstObjectByType<PlayerItemsModule>();
    }

    public void Initialize(string itemId, Sprite icon, GridArea area, float slotSize, float slotSpacing, InventoryGridController controller)
    {
        this.itemInstanceId = itemId;
        this.currentArea = area;
        this.slotSize = slotSize;
        this.slotSpacing = slotSpacing;
        this.controller = controller;

        if (playerItemsModule != null)
        {
            itemInstance = playerItemsModule.GetItemInstance(itemId);
        }

        if (iconImage != null)
            iconImage.sprite = icon;

        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);

        Vector2 position = new Vector2(
            area.position.x * (slotSize + slotSpacing),
            area.position.y * (slotSize + slotSpacing)
        );
        rectTransform.anchoredPosition = position;

        float width = area.width * slotSize + (area.width - 1) * slotSpacing;
        float height = area.height * slotSize + (area.height - 1) * slotSpacing;
        rectTransform.sizeDelta = new Vector2(width, height);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (controller == null || string.IsNullOrEmpty(itemInstanceId))
            return;

        isDragging = true;
        originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;
        originalSiblingIndex = transform.GetSiblingIndex();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        controller.OnItemDragStart(itemInstanceId, currentArea);
        Debug.Log($"[ItemIconVisual] Begin drag: {itemInstanceId}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        Vector2 screenPos = eventData.position;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform,
            screenPos,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
        }

        controller.OnItemDragUpdate(screenPos, currentArea);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        GameObject dropTarget = eventData.pointerEnter;

        // Handle equipment slot drop
        if (dropTarget != null)
        {
            EquipmentSlotComponent equipSlot = dropTarget.GetComponent<EquipmentSlotComponent>();
            if (equipSlot != null)
            {
                if (itemInstance != null && equipSlot.HandleInventoryToEquipmentDrop(itemInstance))
                {
                    Debug.Log($"[ItemIconVisual] Equipped {itemInstance.definitionId} to {equipSlot.SlotType}");
                    return; // Success - don't call controller
                }
            }
        }

        // Always notify controller to handle inventory drops or reset position
        controller.OnItemDragEnd(eventData.position, itemInstanceId, currentArea);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging || controller == null)
            return;

        // Pass the event data along so controller can use eventData.position
        controller.OnItemHoverEnter(itemInstanceId, eventData.position); // Pass position
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (controller == null)
            return;

        controller.OnItemHoverExit();
    }

    public string ItemInstanceId => itemInstanceId;
    public ItemInstance ItemInstance => itemInstance;
    public GridArea CurrentArea => currentArea;
    public bool IsDragging => isDragging;
}