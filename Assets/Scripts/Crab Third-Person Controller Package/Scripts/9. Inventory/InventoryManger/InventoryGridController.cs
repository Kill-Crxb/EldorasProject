// InventoryGridController.cs - The brain that orchestrates the three-layer inventory system
using UnityEngine;
using System.Collections.Generic;

public class InventoryGridController : MonoBehaviour
{
    [Header("Layer References")]
    [SerializeField] private RectTransform backgroundLayer;
    [SerializeField] private RectTransform overlayLayer;
    [SerializeField] private RectTransform iconLayer;

    [Header("Prefabs")]
    [SerializeField] private GameObject slotBackgroundPrefab;
    [SerializeField] private GameObject itemOverlayPrefab;
    [SerializeField] private GameObject itemIconPrefab;

    [Header("Grid Settings")]
    [SerializeField] private int gridWidth = 8;
    [SerializeField] private int gridHeight = 10;
    [SerializeField] private float slotSize = 64f;
    [SerializeField] private float slotSpacing = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // Data layer
    private InventoryGridData gridData;

    // Visual tracking
    private GridSlotBackground[,] backgrounds;
    private Dictionary<string, ItemOverlayVisual> overlays;
    private Dictionary<string, ItemIconVisual> icons;

    // Module reference
    private PlayerItemsModule playerItems;

    // Drag state
    private string draggedItemId;
    private GridArea draggedItemArea;
    private bool isDragging;

    void Start()
    {
        Initialize();
    }

    private void Initialize()
    {


        if (showDebugInfo)
            Debug.Log("InventoryGridController: Initializing...");

        if (!ValidateReferences())
        {
            Debug.LogError("InventoryGridController: Missing required references!");
            return;
        }

        gridData = new InventoryGridData(gridWidth, gridHeight);
        overlays = new Dictionary<string, ItemOverlayVisual>();
        icons = new Dictionary<string, ItemIconVisual>();

        CreateBackgroundGrid();
        ConnectToPlayerData();
        RefreshAllItems();

        if (showDebugInfo)
            Debug.Log($"InventoryGridController: Initialized {gridWidth}x{gridHeight} grid");
    }

    private bool ValidateReferences()
    {
        bool valid = true;
        if (backgroundLayer == null) { Debug.LogError("BackgroundLayer reference is missing!"); valid = false; }
        if (overlayLayer == null) { Debug.LogError("OverlayLayer reference is missing!"); valid = false; }
        if (iconLayer == null) { Debug.LogError("IconLayer reference is missing!"); valid = false; }
        return valid;
    }

    #region Background Grid Creation

    private void CreateBackgroundGrid()
    {
        backgrounds = new GridSlotBackground[gridWidth, gridHeight];

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                CreateBackgroundSlot(x, y);
            }
        }

        if (showDebugInfo)
            Debug.Log($"Created {gridWidth * gridHeight} background slots");
    }

    private void CreateBackgroundSlot(int x, int y)
    {
        GameObject bgObj;

        if (slotBackgroundPrefab != null)
        {
            bgObj = Instantiate(slotBackgroundPrefab, backgroundLayer);
        }
        else
        {
            bgObj = new GameObject($"SlotBG_{x}_{y}");
            bgObj.transform.SetParent(backgroundLayer, false);
            var image = bgObj.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        }

     
        GridSlotBackground bg = bgObj.GetComponent<GridSlotBackground>();
        if (bg == null)
        {
            bg = bgObj.AddComponent<GridSlotBackground>();
        }

        bg.Initialize(new GridPosition(x, y), slotSize, slotSpacing);

        backgrounds[x, y] = bg;
    }

    #endregion

    #region Player Data Connection

    private void ConnectToPlayerData()
    {
        var brain = FindObjectOfType<ControllerBrain>();
        if (brain != null)
        {
            playerItems = brain.GetModule<PlayerItemsModule>();
            if (playerItems != null)
            {
                playerItems.OnInventoryChanged += RefreshAllItems;

                if (showDebugInfo)
                    Debug.Log("Connected to PlayerItemsModule");
            }
            else
            {
                Debug.LogWarning("PlayerItemsModule not found on ControllerBrain!");
            }
        }
        else
        {
            Debug.LogWarning("ControllerBrain not found in scene!");
        }
    }

    #endregion

    #region Item Visual Management

    private void RefreshAllItems()
    {
        if (showDebugInfo)
            Debug.Log("RefreshAllItems called - rebuilding all visuals");

        ClearAllVisuals();

        if (playerItems == null)
        {
            if (showDebugInfo)
                Debug.Log("Cannot refresh items - PlayerItemsModule not connected");
            return;
        }

        var items = playerItems.GetAllInventoryItems();
        int itemCount = 0;
        int overlayCount = 0;

        foreach (var item in items)
        {
            if (item != null)
            {
                bool hadOverlay = item.itemWidth > 1 || item.itemHeight > 1;
                CreateItemVisuals(item);
                itemCount++;
                if (hadOverlay) overlayCount++;
            }
        }

        if (showDebugInfo)
            Debug.Log($"Refreshed: {itemCount} items ({overlayCount} with overlays)");
    }

    private void CreateItemVisuals(ItemInstance item)
    {
        var definition = ItemDatabase.GetDefinition(item.definitionId);
        if (definition == null)
        {
            Debug.LogWarning($"ItemDefinition not found for: {item.definitionId}");
            return;
        }

        if (!item.IsPlaced)
        {
            if (showDebugInfo)
                Debug.Log($"Item {item.instanceId} not placed in grid, skipping visual");
            return;
        }

        GridArea area = new GridArea(
            new GridPosition(item.gridX, item.gridY),
            item.itemWidth,
            item.itemHeight
        );

        if (!area.IsWithinBounds(gridWidth, gridHeight))
        {
            Debug.LogWarning($"Item {item.instanceId} has invalid grid position: ({item.gridX}, {item.gridY})");
            return;
        }

        if (item.itemWidth > 1 || item.itemHeight > 1)
        {
            CreateItemOverlay(item, area);
        }

        CreateItemIcon(item, area, definition);
    }

    private void CreateItemOverlay(ItemInstance item, GridArea area)
    {
        GameObject overlayObj;

        if (itemOverlayPrefab != null)
        {
            overlayObj = Instantiate(itemOverlayPrefab, overlayLayer);
        }
        else
        {
            overlayObj = new GameObject($"Overlay_{item.instanceId}");
            overlayObj.transform.SetParent(overlayLayer, false);
            var image = overlayObj.AddComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
        }

        ItemOverlayVisual overlay = overlayObj.GetComponent<ItemOverlayVisual>();
        if (overlay == null)
        {
            overlay = overlayObj.AddComponent<ItemOverlayVisual>();
        }

        overlay.Initialize(item.instanceId, area, (int)item.currentTier, slotSize, slotSpacing);
        overlays[item.instanceId] = overlay;
    }

    private void CreateItemIcon(ItemInstance item, GridArea area, ItemDefinition definition)
    {
        GameObject iconObj;

        if (itemIconPrefab != null)
        {
            iconObj = Instantiate(itemIconPrefab, iconLayer);
        }
        else
        {
            iconObj = new GameObject($"Icon_{item.instanceId}");
            iconObj.transform.SetParent(iconLayer, false);
            var image = iconObj.AddComponent<UnityEngine.UI.Image>();
            image.sprite = definition.icon;
            image.raycastTarget = true;
            var canvasGroup = iconObj.AddComponent<CanvasGroup>();
        }

        ItemIconVisual icon = iconObj.GetComponent<ItemIconVisual>();
        if (icon == null)
        {
            icon = iconObj.AddComponent<ItemIconVisual>();
        }

        icon.Initialize(item.instanceId, definition.icon, area, slotSize, slotSpacing, this);
        icons[item.instanceId] = icon;
    }
    private void ClearAllVisuals()
    {
        // CRITICAL FIX: Use Destroy() instead of DestroyImmediate()
        // DestroyImmediate can destroy objects during their own callbacks,
        // causing MissingReferenceException when OnEndDrag tries to access the RectTransform

        foreach (var overlay in overlays.Values)
        {
            if (overlay != null)
                Destroy(overlay.gameObject); // Changed from DestroyImmediate
        }
        overlays.Clear();

        foreach (var icon in icons.Values)
        {
            if (icon != null)
                Destroy(icon.gameObject); // Changed from DestroyImmediate
        }
        icons.Clear();
    }

    #endregion

    #region Drag Handlers

    public void OnItemDragStart(string itemId, GridArea area)
    {
        draggedItemId = itemId;
        draggedItemArea = area;
        isDragging = true;

        if (overlays.ContainsKey(itemId))
        {
            var overlay = overlays[itemId];
            if (overlay != null)
            {
                overlay.gameObject.SetActive(false);
            }
        }

        if (showDebugInfo)
            Debug.Log($"Drag start: {itemId} - overlay hidden");
    }

    public void OnItemDragUpdate(Vector2 screenPos, GridArea itemArea)
    {
        if (!isDragging) return;
        GridPosition gridPos = ScreenToGrid(screenPos);
        HighlightPlacement(gridPos, itemArea);
    }



    #endregion

    #region Coordinate Conversion

    private GridPosition ScreenToGrid(Vector2 screenPos)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            backgroundLayer, screenPos, null, out Vector2 localPos))
        {
            return GridPosition.Invalid;
        }

        int x = Mathf.FloorToInt(localPos.x / (slotSize + slotSpacing));
        int y = Mathf.FloorToInt(localPos.y / (slotSize + slotSpacing));

        return new GridPosition(x, y);
    }

    #endregion

    #region Placement Validation

    private bool CanPlaceAt(GridPosition pos, GridArea itemArea)
    {
        if (!pos.IsValid) return false;
        GridArea testArea = new GridArea(pos, itemArea.width, itemArea.height);
        return gridData.CanPlace(testArea, draggedItemId);
    }

    private void HighlightPlacement(GridPosition pos, GridArea itemArea)
    {
        ClearAllHighlights();
        bool canPlace = CanPlaceAt(pos, itemArea);
        Color highlightColor = canPlace ?
            new Color(0.2f, 1f, 0.2f, 0.3f) :
            new Color(1f, 0.2f, 0.2f, 0.3f);

        for (int x = pos.x; x < pos.x + itemArea.width; x++)
        {
            for (int y = pos.y; y < pos.y + itemArea.height; y++)
            {
                if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                {
                    backgrounds[x, y].SetHighlight(highlightColor);
                }
            }
        }
    }

    private void ClearAllHighlights()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                backgrounds[x, y].ClearHighlight();
            }
        }
    }

    #endregion

    #region Item Movement
    public void OnItemDragEnd(Vector2 screenPos, string itemId, GridArea itemArea)
    {
        if (!isDragging) return;

        ClearAllHighlights();
        GridPosition dropPos = ScreenToGrid(screenPos);

        // Check for swap scenario
        var overlappingItem = GetOverlappingItem(dropPos, itemArea, itemId);
        if (overlappingItem != null)
        {
            bool swapSuccess = MoveItemToPosition(itemId, dropPos, itemArea);
            if (swapSuccess)
            {
                if (showDebugInfo)
                    Debug.Log($"Swap performed: {itemId} ↔ {overlappingItem.instanceId}");
            }
            else
            {
                // Swap was rejected - restore visuals to item's ACTUAL data position
                RestoreItemVisuals(itemId);

                if (showDebugInfo)
                    Debug.Log($"Swap rejected - items restored to original positions");
            }

            draggedItemId = null;
            isDragging = false;
            return;
        }

        // Normal placement validation
        if (CanPlaceAt(dropPos, itemArea))
        {
            MoveItemToPosition(itemId, dropPos, itemArea);
            if (showDebugInfo)
                Debug.Log($"Valid drop: {itemId} moved to {dropPos}");
        }
        else
        {
            // Invalid drop - restore to item's ACTUAL data position
            RestoreItemVisuals(itemId);

            if (showDebugInfo)
            {
                var item = playerItems?.GetItemInstance(itemId);
                if (item != null)
                    Debug.Log($"Invalid drop - restored to ({item.gridX}, {item.gridY})");
            }
        }

        draggedItemId = null;
        isDragging = false;
    }

    // NEW: Helper method to restore visuals based on actual item data
    private void RestoreItemVisuals(string itemId)
    {
        if (playerItems == null) return;

        var item = playerItems.GetItemInstance(itemId);
        if (item == null) return;

        // Restore overlay
        if (overlays.ContainsKey(itemId))
            overlays[itemId]?.gameObject.SetActive(true);

        // Restore icon to item's actual grid position
        if (icons.ContainsKey(itemId))
        {
            var icon = icons[itemId];
            if (icon != null)
            {
                RectTransform iconRT = icon.GetComponent<RectTransform>();
                Vector2 correctPos = new Vector2(
                    item.gridX * (slotSize + slotSpacing),
                    item.gridY * (slotSize + slotSpacing)
                );
                iconRT.anchoredPosition = correctPos;

                if (showDebugInfo)
                    Debug.Log($"Restored icon to data position: ({item.gridX}, {item.gridY}) = {correctPos}");
            }
        }
    }
    // NEW method: Check if placement would overlap with another item
    private ItemInstance GetOverlappingItem(GridPosition pos, GridArea itemArea, string draggedItemId)
    {
        if (playerItems == null) return null;

        // Calculate the bounds of where the dragged item would be placed
        int x1 = pos.x;
        int y1 = pos.y;
        int x2 = pos.x + itemArea.width;
        int y2 = pos.y + itemArea.height;

        foreach (var item in playerItems.GetAllInventoryItems())
        {
            if (item == null || !item.IsPlaced || item.instanceId == draggedItemId)
                continue;

            // Check if this item's area overlaps with the drop area
            int itemX1 = item.gridX;
            int itemY1 = item.gridY;
            int itemX2 = item.gridX + item.itemWidth;
            int itemY2 = item.gridY + item.itemHeight;

            bool overlapsX = x1 < itemX2 && x2 > itemX1;
            bool overlapsY = y1 < itemY2 && y2 > itemY1;

            if (overlapsX && overlapsY)
            {
                return item; // Found overlapping item - swap candidate
            }
        }

        return null; // No overlap found
    }
    private bool MoveItemToPosition(string itemId, GridPosition newPos, GridArea itemArea)
    {
        if (playerItems == null) return false;

        var item = playerItems.GetItemInstance(itemId);
        if (item == null) return false;

        var targetItem = GetOverlappingItem(newPos, itemArea, itemId);
        if (targetItem != null)
        {
            // SWAP VALIDATION
            GridPosition draggedNewPos = newPos;
            GridPosition targetNewPos = new GridPosition(item.gridX, item.gridY);

            if (draggedNewPos.x + itemArea.width > gridWidth ||
                draggedNewPos.y + itemArea.height > gridHeight)
            {
                if (showDebugInfo)
                    Debug.Log("Swap rejected: dragged item would exceed grid bounds");
                return false; // Reject swap
            }

            if (targetNewPos.x + targetItem.itemWidth > gridWidth ||
                targetNewPos.y + targetItem.itemHeight > gridHeight)
            {
                if (showDebugInfo)
                    Debug.Log("Swap rejected: target item would exceed grid bounds");
                return false; // Reject swap
            }

            bool swappedItemsOverlap = DoAreasOverlap(
                draggedNewPos.x, draggedNewPos.y, itemArea.width, itemArea.height,
                targetNewPos.x, targetNewPos.y, targetItem.itemWidth, targetItem.itemHeight
            );

            if (swappedItemsOverlap)
            {
                if (showDebugInfo)
                    Debug.Log("Swap rejected: items would overlap each other after swap");
                return false; // Reject swap
            }

            if (WouldOverlapOtherItems(targetNewPos.x, targetNewPos.y, targetItem, new[] { itemId, targetItem.instanceId }))
            {
                if (showDebugInfo)
                    Debug.Log("Swap rejected: target item would overlap other items");
                return false; // Reject swap
            }

            // Valid swap - proceed
            if (showDebugInfo)
                Debug.Log($"Swapping {itemId} with {targetItem.instanceId}");

            targetItem.PlaceAtPosition(targetNewPos.x, targetNewPos.y);
            item.PlaceAtPosition(draggedNewPos.x, draggedNewPos.y);

            gridData.RemoveItem(itemId);
            gridData.RemoveItem(targetItem.instanceId);

            var swapArea = new GridArea(draggedNewPos, itemArea.width, itemArea.height);
            var targetArea = new GridArea(targetNewPos, targetItem.itemWidth, targetItem.itemHeight);

            gridData.PlaceItem(itemId, swapArea);
            gridData.PlaceItem(targetItem.instanceId, targetArea);

            UpdateItemVisuals(itemId, swapArea);
            UpdateItemVisuals(targetItem.instanceId, targetArea);

            if (showDebugInfo)
                Debug.Log($"Swapped items: {itemId} <-> {targetItem.instanceId}");

            return true; // Swap succeeded
        }

        // Original single-item move logic
        item.PlaceAtPosition(newPos.x, newPos.y);
        var newArea = new GridArea(newPos, itemArea.width, itemArea.height);
        gridData.RemoveItem(itemId);
        gridData.PlaceItem(itemId, newArea);
        UpdateItemVisuals(itemId, newArea);

        if (showDebugInfo)
            Debug.Log($"Moved {itemId} to ({newPos.x}, {newPos.y})");

        return true; // Move succeeded
    }

    // Add this helper method
    private bool DoAreasOverlap(int x1, int y1, int w1, int h1, int x2, int y2, int w2, int h2)
    {
        bool overlapsX = x1 < x2 + w2 && x1 + w1 > x2;
        bool overlapsY = y1 < y2 + h2 && y1 + h1 > y2;
        return overlapsX && overlapsY;
    }

    // New helper: Check if item would overlap with other items (excluding specified IDs)
    private bool WouldOverlapOtherItems(int x, int y, ItemInstance itemToPlace, string[] excludeIds)
    {
        if (playerItems == null) return false;

        foreach (var item in playerItems.GetAllInventoryItems())
        {
            if (item == null || !item.IsPlaced) continue;

            // Skip items we're excluding (the two items being swapped)
            bool isExcluded = false;
            foreach (string excludeId in excludeIds)
            {
                if (item.instanceId == excludeId)
                {
                    isExcluded = true;
                    break;
                }
            }
            if (isExcluded) continue;

            // Check overlap
            bool overlapsX = x < item.gridX + item.itemWidth &&
                            x + itemToPlace.itemWidth > item.gridX;
            bool overlapsY = y < item.gridY + item.itemHeight &&
                            y + itemToPlace.itemHeight > item.gridY;

            if (overlapsX && overlapsY)
                return true;
        }

        return false;
    }
    private ItemInstance GetItemAtPosition(GridPosition pos)
    {
        if (playerItems == null) return null;

        foreach (var item in playerItems.GetAllInventoryItems())
        {
            if (item == null || !item.IsPlaced) continue;

            // Check if position is within this item's area
            if (pos.x >= item.gridX && pos.x < item.gridX + item.itemWidth &&
                pos.y >= item.gridY && pos.y < item.gridY + item.itemHeight)
            {
                return item;
            }
        }

        return null;
    }
    private void UpdateItemVisuals(string itemId, GridArea newArea)
    {
        // Update or recreate overlay
        if (overlays.ContainsKey(itemId))
        {
            var overlay = overlays[itemId];
            if (overlay != null)
            {
                // Reposition and show the overlay
                overlay.gameObject.SetActive(true);
                RectTransform overlayRT = overlay.GetComponent<RectTransform>();
                Vector2 newPos = new Vector2(
                    newArea.position.x * (slotSize + slotSpacing),
                    newArea.position.y * (slotSize + slotSpacing)
                );
                overlayRT.anchoredPosition = newPos;

                if (showDebugInfo)
                    Debug.Log($"Updated overlay position to {newPos}");
            }
        }

        // Update icon position
        if (icons.ContainsKey(itemId))
        {
            var icon = icons[itemId];
            if (icon != null)
            {
                RectTransform iconRT = icon.GetComponent<RectTransform>();
                Vector2 newPos = new Vector2(
                    newArea.position.x * (slotSize + slotSpacing),
                    newArea.position.y * (slotSize + slotSpacing)
                );
                iconRT.anchoredPosition = newPos;

                if (showDebugInfo)
                    Debug.Log($"Updated icon position to {newPos}");
            }
        }
    }




    #endregion

    #region Tooltip Support

    public void OnItemHoverEnter(string itemId, Vector2 pointerPosition)
    {
        if (playerItems == null) return;

        var item = playerItems.GetItemInstance(itemId);
        if (item == null) return;

        var definition = ItemDatabase.GetDefinition(item.definitionId);
        if (definition == null) return;

        string description = definition.description;

        if (item.calculatedModifiers != null && item.calculatedModifiers.Length > 0)
        {
            description += "\n\nStats:";
            foreach (var modifier in item.calculatedModifiers)
            {
                description += $"\n+{modifier.value:F1} {modifier.statName}";
            }
        }

        var tooltipData = new ItemTooltipData(
            definition.displayName,
            description,
            definition.category.ToString(),
            item.currentTier,
            item.stackCount
        );

        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.ShowTooltip(tooltipData, pointerPosition); // Use passed position
        }
    }
    public void OnItemHoverExit()
    {
        if (TooltipManager.Instance != null)
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    #endregion

    void OnDestroy()
    {
        if (playerItems != null)
        {
            playerItems.OnInventoryChanged -= RefreshAllItems;
        }
    }

    [ContextMenu("Force Refresh")]
    public void ForceRefreshDisplay()
    {
        RefreshAllItems();
    }

    [ContextMenu("Print Grid State")]
    public void PrintGridState()
    {
        if (gridData != null)
            Debug.Log(gridData.GetDebugString());
        if (playerItems != null)
            Debug.Log($"PlayerItems: {playerItems.GetInventoryItemCount()} items");
        Debug.Log($"Visuals: {overlays.Count} overlays, {icons.Count} icons");
    }
}