using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class GridSlotBackground : MonoBehaviour, IDropHandler
{
    [SerializeField] private Image backgroundImage;

    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    private GridPosition gridPosition;
    private RectTransform rectTransform;

    // Drag highlights persist under hover-preview highlights and are cleared separately.
    private bool hasDragHighlight;
    private Color dragHighlightColor;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();

        backgroundImage.color = normalColor;
        backgroundImage.raycastTarget = true; // Changed to true
    }

    public void Initialize(GridPosition pos, float size, float spacing)
    {
        gridPosition = pos;

        // Ensure rectTransform is set (Initialize may be called before Awake)
        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(size, size);
        rectTransform.anchoredPosition = new Vector2(
            pos.x * (size + spacing),
            pos.y * (size + spacing)
        );

        SetNormal();
    }
    public void OnDrop(PointerEventData eventData)
    {
        // The old equipment-to-inventory drop lived here. Unequip is now right-click on the socket.
    }
    public void SetNormal()
    {
        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    public void SetHover()
    {
        if (backgroundImage != null)
            backgroundImage.color = hoverColor;
    }

    public void SetHighlight(Color color)
    {
        if (backgroundImage != null)
            backgroundImage.color = color;
    }

    // Restores to drag highlight if one is active, otherwise returns to normal.
    public void ClearHighlight()
    {
        if (hasDragHighlight)
            SetHighlight(dragHighlightColor);
        else
            SetNormal();
    }

    public void SetDragHighlight(Color color)
    {
        hasDragHighlight = true;
        dragHighlightColor = color;
        SetHighlight(color);
    }

    public void ClearDragHighlight()
    {
        hasDragHighlight = false;
        SetNormal();
    }

    public GridPosition Position => gridPosition;
}