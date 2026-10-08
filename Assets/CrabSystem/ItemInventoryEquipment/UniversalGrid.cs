using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base for every grid-based item display — player inventory, container, stash, bench.
/// Owns the visuals only: GridTransferManager owns drag logic, and contents come through
/// the abstract data hooks so any storage can back a grid.
/// </summary>
public abstract class UniversalGrid : MonoBehaviour
{
    #region Configuration

    [Header("Grid Identity")]
    [SerializeField] protected string gridName = "Unnamed Grid";
    [SerializeField] protected bool isPlayerInventory = false;

    [Header("Grid Settings")]
    [SerializeField] protected int gridWidth = 8;
    [SerializeField] protected int gridHeight = 10;
    [SerializeField] protected float slotSize = 64f;
    [SerializeField] protected float slotSpacing = 2f;

    [Header("Layer References")]
    [SerializeField] protected RectTransform backgroundLayer;
    [SerializeField] protected RectTransform overlayLayer;
    [SerializeField] protected RectTransform iconLayer;

    [Header("Prefabs")]
    [SerializeField] protected GameObject slotBackgroundPrefab;
    [SerializeField] protected GameObject itemOverlayPrefab;
    [SerializeField] protected GameObject itemIconPrefab;

    #endregion

    #region State

    protected bool isInitialized = false;
    protected GridSlotBackground[,] backgrounds;
    protected Dictionary<string, ItemOverlayVisual> overlays = new Dictionary<string, ItemOverlayVisual>();
    protected Dictionary<string, ItemIconVisual> icons = new Dictionary<string, ItemIconVisual>();

    #endregion

    #region Unity Lifecycle

    protected virtual void OnEnable()
    {
        GridTransferManager.Instance.RegisterGrid(this);
    }

    protected virtual void OnDisable()
    {
        if (GridTransferManager.Instance != null)
        {
            GridTransferManager.Instance.UnregisterGrid(this);
        }
    }

    protected virtual void Start()
    {
        // Don't auto-initialize in Start
        // UniversalInventoryGrid.SetInventorySource() will call Initialize() after configuration
        // This prevents "missing background references" errors when instantiated without setup
    }

    #endregion

    #region Initialization

    protected virtual void Initialize()
    {
        if (isInitialized) return;

        backgrounds = new GridSlotBackground[gridWidth, gridHeight];

        CreateBackgroundGrid();
        ConnectToDataSource();

        isInitialized = true;
    }

    protected virtual void CreateBackgroundGrid()
    {
        if (backgroundLayer == null || slotBackgroundPrefab == null)
        {
            Debug.LogError($"[UniversalGrid] {gridName} missing background references!");
            return;
        }

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                GameObject slotObj = Instantiate(slotBackgroundPrefab, backgroundLayer);
                RectTransform slotRT = slotObj.GetComponent<RectTransform>();

                slotRT.anchorMin = new Vector2(0, 0);
                slotRT.anchorMax = new Vector2(0, 0);
                slotRT.pivot = new Vector2(0, 0);
                slotRT.sizeDelta = new Vector2(slotSize, slotSize);
                slotRT.anchoredPosition = new Vector2(
                    x * (slotSize + slotSpacing),
                    y * (slotSize + slotSpacing)
                );

                GridSlotBackground bg = slotObj.GetComponent<GridSlotBackground>();
                if (bg == null)
                {
                    bg = slotObj.AddComponent<GridSlotBackground>();
                }
                backgrounds[x, y] = bg;
            }
        }
    }

    /// <summary>
    /// Override this to connect to your data source (InventorySystem, etc.)
    /// </summary>
    protected abstract void ConnectToDataSource();

    #endregion

    #region Abstract Data Interface

    /// <summary>
    /// Get all items that should be displayed in this grid
    /// </summary>
    protected abstract ItemInstance[] GetItems();

    /// <summary>
    /// Add an item to the underlying data source
    /// </summary>
    protected abstract bool AddItemToData(ItemInstance item, int gridX, int gridY);

    /// <summary>
    /// Remove an item from the underlying data source
    /// </summary>
    protected abstract bool RemoveItemFromData(string itemId);

    /// <summary>
    /// Move an item within the data source
    /// </summary>
    protected abstract bool MoveItemInData(string itemId, int newX, int newY);

    /// <summary>
    /// Check if an item can be placed at a position in the data source
    /// </summary>
    protected abstract bool CanPlaceInData(ItemInstance item, int gridX, int gridY, string excludeItemId = null);

    #endregion

    #region Public API - Called by GridTransferManager

    public bool IsPointOverGrid(Vector2 screenPos)
    {
        if (backgroundLayer == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            backgroundLayer,
            screenPos,
            null
        );
    }

    public GridPosition ScreenToGridPosition(Vector2 screenPos)
    {
        if (backgroundLayer == null) return GridPosition.Invalid;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            backgroundLayer, screenPos, null, out Vector2 localPos))
        {
            return GridPosition.Invalid;
        }

        int x = Mathf.FloorToInt(localPos.x / (slotSize + slotSpacing));
        int y = Mathf.FloorToInt(localPos.y / (slotSize + slotSpacing));

        return new GridPosition(x, y);
    }

    public bool CanPlaceItemAt(ItemInstance item, GridPosition pos, string excludeItemId = null)
    {
        if (!pos.IsValid) return false;
        if (pos.x + item.itemWidth > gridWidth) return false;
        if (pos.y + item.itemHeight > gridHeight) return false;

        return CanPlaceInData(item, pos.x, pos.y, excludeItemId);
    }

    public bool AddItem(ItemInstance item, GridPosition pos)
    {
        if (!CanPlaceItemAt(item, pos)) return false;

        bool success = AddItemToData(item, pos.x, pos.y);
        if (success) RefreshVisuals();

        return success;
    }

    public bool RemoveItem(string itemId)
    {
        bool success = RemoveItemFromData(itemId);
        if (success) RefreshVisuals();

        return success;
    }

    public bool MoveItem(string itemId, GridPosition newPos)
    {
        bool success = MoveItemInData(itemId, newPos.x, newPos.y);
        if (success) RefreshVisuals();

        return success;
    }

    private static readonly Color s_validColor   = new Color(0.2f, 1f,   0.2f, 0.4f);
    private static readonly Color s_invalidColor = new Color(1f,   0.2f, 0.2f, 0.4f);
    private static readonly Color s_dragValid    = new Color(0.2f, 1f,   0.2f, 0.12f);
    private static readonly Color s_dragInvalid  = new Color(1f,   0.2f, 0.2f, 0.12f);

    // Uses the real CanPlaceItemAt check so occupied cells show red correctly.
    public void ShowPlacementPreview(GridPosition pos, ItemInstance draggedItem, string draggedItemId)
    {
        ClearPlacementPreview();
        if (!pos.IsValid || draggedItem == null) return;

        bool canPlace = CanPlaceItemAt(draggedItem, pos, draggedItemId);
        Color highlight = canPlace ? s_validColor : s_invalidColor;

        for (int x = pos.x; x < pos.x + draggedItem.itemWidth; x++)
            for (int y = pos.y; y < pos.y + draggedItem.itemHeight; y++)
                if (x >= 0 && x < gridWidth && y >= 0 && y < gridHeight)
                    backgrounds[x, y].SetHighlight(highlight);
    }

    public void ClearPlacementPreview()
    {
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                backgrounds[x, y].ClearHighlight();
    }

    // Colors every cell: green if it's part of any valid placement, red otherwise.
    // Called once on drag start across all registered grids.
    public void ShowAllDragHighlights(ItemInstance item, string excludeId)
    {
        if (!isInitialized || item == null) return;

        // Start everything red (no valid placement covers this cell yet).
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                backgrounds[x, y].SetDragHighlight(s_dragInvalid);

        // For every top-left position where the item fits, paint those cells green.
        int maxX = gridWidth  - item.itemWidth;
        int maxY = gridHeight - item.itemHeight;
        for (int x = 0; x <= maxX; x++)
        {
            for (int y = 0; y <= maxY; y++)
            {
                if (!CanPlaceItemAt(item, new GridPosition(x, y), excludeId)) continue;
                for (int dx = 0; dx < item.itemWidth; dx++)
                    for (int dy = 0; dy < item.itemHeight; dy++)
                        backgrounds[x + dx, y + dy].SetDragHighlight(s_dragValid);
            }
        }
    }

    public void ClearAllDragHighlights()
    {
        if (!isInitialized) return;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                backgrounds[x, y].ClearDragHighlight();
    }

    public virtual void OnItemDragStarted(string itemId, GridArea area)
    {
        // Hide the overlay
        if (overlays.ContainsKey(itemId))
        {
            overlays[itemId]?.gameObject.SetActive(false);
        }
    }

    public virtual void OnItemDragCancelled(string itemId)
    {
        // The icon was reparented to the root canvas on drag start, so re-showing the
        // overlay is not enough — rebuild both from the unchanged data.
        RefreshVisuals();
    }

    #endregion

    #region Visual Management

    public void RefreshVisuals()
    {
        ClearAllVisuals();

        ItemInstance[] items = GetItems();
        if (items == null) return;

        foreach (var item in items)
        {
            if (item == null || !item.IsPlaced) continue;
            CreateItemVisuals(item);
        }
    }

    protected virtual void CreateItemVisuals(ItemInstance item)
    {
        GridArea area = new GridArea(item.gridX, item.gridY, item.itemWidth, item.itemHeight);

        CreateItemOverlay(item, area);
        CreateItemIcon(item, area);
    }

    protected virtual void CreateItemOverlay(ItemInstance item, GridArea area)
    {
        GameObject overlayObj = InstantiateVisual(itemOverlayPrefab, overlayLayer, $"Overlay_{item.instanceId}");

        var image = overlayObj.GetComponent<Image>();
        if (image != null) image.raycastTarget = false;

        var overlay = overlayObj.GetComponent<ItemOverlayVisual>();
        if (overlay == null) overlay = overlayObj.AddComponent<ItemOverlayVisual>();

        overlay.Initialize(item.instanceId, area, (int)item.currentTier, slotSize, slotSpacing);
        overlays[item.instanceId] = overlay;
    }

    protected virtual void CreateItemIcon(ItemInstance item, GridArea area)
    {
        GameObject iconObj = InstantiateVisual(itemIconPrefab, iconLayer, $"Icon_{item.instanceId}");

        var image = iconObj.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = item.Definition.icon;
            image.raycastTarget = true;
        }

        var icon = iconObj.GetComponent<ItemIconVisual>();
        if (icon == null) icon = iconObj.AddComponent<ItemIconVisual>();

        icon.Initialize(item.instanceId, item, item.Definition.icon, area, slotSize, slotSpacing, this);
        icons[item.instanceId] = icon;
    }

    /// <summary>
    /// Uses the authored prefab so the look lives in the asset rather than in code,
    /// and falls back to a bare Image so a grid with no art assigned still works.
    /// </summary>
    private GameObject InstantiateVisual(GameObject prefab, Transform layer, string objectName)
    {
        if (prefab != null)
        {
            GameObject spawned = Instantiate(prefab, layer);
            spawned.name = objectName;
            return spawned;
        }

        var built = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        built.transform.SetParent(layer, false);
        return built;
    }

    protected virtual void ClearAllVisuals()
    {
        // A destroyed icon never receives OnPointerExit, so a tooltip opened over one
        // would hang around with nothing left to dismiss it.
        if (icons.Count > 0) UniversalWindowManager.Instance?.HideTooltip();

        foreach (var overlay in overlays.Values)
        {
            if (overlay != null) Destroy(overlay.gameObject);
        }
        overlays.Clear();

        foreach (var icon in icons.Values)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        icons.Clear();
    }

    #endregion

    #region Tooltips

    public virtual void OnItemHoverEnter(string itemId, Vector2 pointerPosition)
    {
        var tooltip = ItemTooltipData.For(GetItemInstance(itemId));
        if (tooltip == null) return;

        DecorateTooltip(tooltip, GetItemInstance(itemId));
        UniversalWindowManager.Instance?.ShowTooltip(tooltip, pointerPosition);
    }

    /// <summary>Adds what only this grid knows about the item — a vendor shelf adds its price.</summary>
    protected virtual void DecorateTooltip(ItemTooltipData tooltip, ItemInstance item) { }

    public virtual void OnItemHoverExit()
    {
        UniversalWindowManager.Instance?.HideTooltip();
    }

    public virtual void OnItemRightClicked(string itemId) { }

    // Shift-click: move item to the most logical other grid (container ↔ inventory).
    public virtual void OnItemShiftClicked(string itemId) { }

    /// <summary>
    /// Get item instance by ID - must be implemented by subclass
    /// </summary>
    protected virtual ItemInstance GetItemInstance(string itemId)
    {
        // Subclasses should override this to get items from their data source
        return null;
    }

    #endregion

    #region Properties

    public string GridName => gridName;
    public bool IsPlayerInventory => isPlayerInventory;
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public float SlotSize => slotSize;
    public float SlotSpacing => slotSpacing;

    #endregion
}