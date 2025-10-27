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
        EquipmentSlotComponent draggedEquip = EquipmentSlotComponent.GetCurrentDraggedEquipmentSlot();
        if (draggedEquip != null && !draggedEquip.IsEmpty)
        {
            var playerItems = FindFirstObjectByType<PlayerItemsModule>();
            if (playerItems != null)
            {
                var item = draggedEquip.CurrentItemInstance;
                if (item != null)
                {
                    // CRITICAL: Validate the item can fit at this position
                    int gridWidth = 8;
                    int gridHeight = 10;

                    // Check if item would extend beyond grid bounds
                    if (gridPosition.x + item.itemWidth > gridWidth ||
                        gridPosition.y + item.itemHeight > gridHeight)
                    {
                        Debug.LogWarning($"Cannot place {item.itemWidth}x{item.itemHeight} item at ({gridPosition.x}, {gridPosition.y}) - would exceed grid bounds");

                        // Show visual feedback
                        StartCoroutine(ShowInvalidDropFeedback());
                        return; // Don't unequip
                    }

                    // Check if position would overlap with existing items
                    if (!CanPlaceItemAt(gridPosition.x, gridPosition.y, item, playerItems))
                    {
                        Debug.LogWarning($"Cannot place item at ({gridPosition.x}, {gridPosition.y}) - overlaps existing item");
                        StartCoroutine(ShowInvalidDropFeedback());
                        return;
                    }

                    // Valid position - set it BEFORE unequipping
                    item.PlaceAtPosition(gridPosition.x, gridPosition.y);
                    playerItems.UnequipItem(draggedEquip.SlotType);
                }
            }
        }
    }
    private bool CanPlaceItemAt(int x, int y, ItemInstance itemToPlace, PlayerItemsModule playerItems)
    {
        var allItems = playerItems.GetAllInventoryItems();

        foreach (var existingItem in allItems)
        {
            if (existingItem == null || !existingItem.IsPlaced || existingItem.instanceId == itemToPlace.instanceId)
                continue;

            // Check if areas overlap
            bool overlapsX = x < existingItem.gridX + existingItem.itemWidth &&
                            x + itemToPlace.itemWidth > existingItem.gridX;
            bool overlapsY = y < existingItem.gridY + existingItem.itemHeight &&
                            y + itemToPlace.itemHeight > existingItem.gridY;

            if (overlapsX && overlapsY)
                return false;
        }

        return true;
    }
    private System.Collections.IEnumerator ShowInvalidDropFeedback()
    {
        Color originalColor = backgroundImage.color;
        backgroundImage.color = new Color(1f, 0.2f, 0.2f, 0.8f); // Red flash
        yield return new WaitForSeconds(0.3f);
        backgroundImage.color = originalColor;
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

    public void ClearHighlight()
    {
        SetNormal();
    }

    public GridPosition Position => gridPosition;
}