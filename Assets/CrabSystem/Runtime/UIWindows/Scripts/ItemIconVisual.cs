using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Draggable icon for one item in a UniversalGrid. Drag state belongs to
/// GridTransferManager — this moves the visual and reports what the pointer did.
/// </summary>
public class ItemIconVisual : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
                              IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const float DragAlpha = 0.6f;

    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup canvasGroup;

    private UniversalGrid grid;
    private string itemInstanceId;
    private ItemInstance itemInstance;
    private GridArea currentArea;
    private float slotSize;
    private float slotSpacing;

    private RectTransform rect;
    private bool isDragging;

    public string ItemInstanceId => itemInstanceId;
    public ItemInstance ItemInstance => itemInstance;
    public GridArea CurrentArea => currentArea;
    public bool IsDragging => isDragging;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        if (iconImage == null) iconImage = GetComponent<Image>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(string itemId, ItemInstance instance, Sprite icon, GridArea area,
                           float cellSize, float cellSpacing, UniversalGrid owner)
    {
        itemInstanceId = itemId;
        itemInstance = instance;
        currentArea = area;
        slotSize = cellSize;
        slotSpacing = cellSpacing;
        grid = owner;

        if (iconImage != null) iconImage.sprite = icon;

        LayOut(area);
    }

    private void LayOut(GridArea area)
    {
        if (rect == null) rect = GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;

        rect.anchoredPosition = new Vector2(area.position.x * (slotSize + slotSpacing),
                                            area.position.y * (slotSize + slotSpacing));

        rect.sizeDelta = new Vector2(area.width * slotSize + (area.width - 1) * slotSpacing,
                                     area.height * slotSize + (area.height - 1) * slotSpacing);
    }

    #region Drag

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(itemInstanceId)) return;

        isDragging = true;

        // Reparented to the canvas so the icon draws over every panel while it travels.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            transform.SetParent(canvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
        }

        canvasGroup.alpha = DragAlpha;
        canvasGroup.blocksRaycasts = false;

        GridTransferManager.Instance.BeginDrag(grid, itemInstanceId, itemInstance, currentArea);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            rect.localPosition = local;
        }

        GridTransferManager.Instance.UpdateDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;

        isDragging = false;

        // No pointer exit arrives mid-drag, so the tooltip has to be closed by hand.
        UniversalWindowManager.Instance?.HideTooltip();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // Unity fires OnDrop before OnEndDrag, so an equipment socket has already taken the
        // item if it wanted it. Closing the drag out regardless is what keeps the manager
        // from staying stuck mid-drag and stranding this icon on the canvas.
        GridTransferManager.Instance.EndDrag(eventData.position);
    }

    #endregion

    #region Pointer

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isDragging) return;

        grid?.OnItemHoverEnter(itemInstanceId, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        grid?.OnItemHoverExit();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            grid?.OnItemRightClicked(itemInstanceId);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left && ShiftHeld())
            grid?.OnItemShiftClicked(itemInstanceId);
    }

    // The project runs on the new Input System, where UnityEngine.Input is unavailable.
    private static bool ShiftHeld()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.shiftKey.isPressed;
    }

    #endregion
}
