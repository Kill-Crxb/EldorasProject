using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// ItemIconVisual - UI component for draggable item icons
/// 
/// Updated to work with UniversalGrid + GridTransferManager architecture
/// - Reports drag events to GridTransferManager (not individual grid)
/// - Manager handles all cross-grid logic
/// - Still supports equipment slot drops
/// 
/// Created: February 13, 2026 (Updated for universal grid system)
/// </summary>
public class ItemIconVisual : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    // Legacy controller reference (for backward compatibility during migration)
    private InventoryGridController legacyController;

    // New universal grid reference
    private UniversalGrid universalGrid;

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

    // Track which system we're using
    private bool useUniversalSystem = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (iconImage == null)
            iconImage = GetComponent<Image>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // Auto-create if still missing � required for drag alpha/raycast blocking
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    #region Initialization

    /// <summary>
    /// NEW: Initialize for universal grid system
    /// </summary>
    public void InitializeUniversal(string itemId, ItemInstance instance, Sprite icon, GridArea area,
                                     float slotSize, float slotSpacing, UniversalGrid grid)
    {
        this.itemInstanceId = itemId;
        this.itemInstance = instance;
        this.currentArea = area;
        this.slotSize = slotSize;
        this.slotSpacing = slotSpacing;
        this.universalGrid = grid;
        this.useUniversalSystem = true;

        if (iconImage != null)
            iconImage.sprite = icon;

        SetupTransform(area);
    }

    /// <summary>
    /// LEGACY: Initialize for old system (backward compatibility)
    /// </summary>
    public void Initialize(string itemId, Sprite icon, GridArea area, float slotSize, float slotSpacing,
                          InventoryGridController controller)
    {
        this.itemInstanceId = itemId;
        this.currentArea = area;
        this.slotSize = slotSize;
        this.slotSpacing = slotSpacing;
        this.legacyController = controller;
        this.useUniversalSystem = false;

        if (iconImage != null)
            iconImage.sprite = icon;

        SetupTransform(area);
    }

    private void SetupTransform(GridArea area)
    {
        // Ensure rectTransform is set
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

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

    #endregion

    #region Drag Handlers

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemInstanceId))
            return;

        isDragging = true;
        originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;
        originalSiblingIndex = transform.GetSiblingIndex();

        // CRITICAL: Move to root canvas to render on top of all windows
        Canvas rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
        {
            transform.SetParent(rootCanvas.transform, true); // Keep world position
            transform.SetAsLastSibling(); // Render on top
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        if (useUniversalSystem)
        {
            // NEW SYSTEM: Report to GridTransferManager
            GridTransferManager.Instance.BeginDrag(universalGrid, itemInstanceId, itemInstance, currentArea);
        }
        else
        {
            // LEGACY SYSTEM: Report to controller
            legacyController?.OnItemDragStart(itemInstanceId, currentArea);
        }

        Debug.Log($"[ItemIconVisual] Begin drag: {itemInstanceId} (Universal: {useUniversalSystem})");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        Vector2 screenPos = eventData.position;

        // Move the icon visually
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform.parent as RectTransform,
            screenPos,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            rectTransform.localPosition = localPoint;
        }

        if (useUniversalSystem)
        {
            // NEW SYSTEM: Manager handles preview updates
            GridTransferManager.Instance.UpdateDrag(screenPos);
        }
        else
        {
            // LEGACY SYSTEM: Controller handles it
            legacyController?.OnItemDragUpdate(screenPos, currentArea);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;

        // Tooltip never gets a PointerExit during drag, so hide it explicitly
        UniversalWindowManager.Instance?.HideTooltip();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        GameObject dropTarget = eventData.pointerEnter;

        // Check for equipment slot drop (NEW SYSTEM - uses EquipmentItemIcon)
        if (dropTarget != null)
        {
            EquipmentItemIcon equipSlot = dropTarget.GetComponent<EquipmentItemIcon>();
            if (equipSlot != null && itemInstance != null)
            {
                // EquipmentItemIcon.OnDrop() will handle this via Unity's event system
                Debug.Log($"[ItemIconVisual] Dropping on equipment slot");
                return;
            }
        }

        if (useUniversalSystem)
        {
            GridTransferManager.Instance.EndDrag(eventData.position);
        }
        else
        {
            legacyController?.OnItemDragEnd(eventData.position, itemInstanceId, currentArea);
        }
    }

    #endregion

    #region Hover Handlers

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;

        if (useUniversalSystem)
        {
            universalGrid?.OnItemHoverEnter(itemInstanceId, eventData.position);
        }
        else
        {
            legacyController?.OnItemHoverEnter(itemInstanceId, eventData.position);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (useUniversalSystem)
        {
            universalGrid?.OnItemHoverExit();
        }
        else
        {
            legacyController?.OnItemHoverExit();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[ItemIconVisual] OnPointerClick — button: {eventData.button}, isDragging: {isDragging}, useUniversal: {useUniversalSystem}");
        if (isDragging) return;
        if (eventData.button != PointerEventData.InputButton.Right) return;

        if (useUniversalSystem)
            universalGrid?.OnItemRightClicked(itemInstanceId);
    }

    #endregion

    #region Properties

    public string ItemInstanceId => itemInstanceId;
    public ItemInstance ItemInstance => itemInstance;
    public GridArea CurrentArea => currentArea;
    public bool IsDragging => isDragging;
    public bool IsUniversalSystem => useUniversalSystem;

    #endregion
}