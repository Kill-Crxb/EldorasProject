using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UniversalGrid - Base class for all grid-based inventory displays
/// 
/// Can represent:
/// - Player inventory
/// - Container (chest, crate, corpse)
/// - Stash
/// - Trade window
/// - Crafting bench
/// - Any other grid-based item storage
/// 
/// Architecture:
/// - Registers with GridTransferManager on enable
/// - Provides data through IGridDataProvider interface
/// - Handles visual updates only - manager handles logic
/// - Works with any data source (InventorySystem, custom storage, etc.)
/// 
/// Created: February 13, 2026
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

    [Header("Debug")]
    [SerializeField] protected bool debugMode = false;

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

        if (debugMode)
            Debug.Log($"[UniversalGrid] Initializing {gridName}...");

        backgrounds = new GridSlotBackground[gridWidth, gridHeight];

        CreateBackgroundGrid();
        ConnectToDataSource();

        isInitialized = true;

        if (debugMode)
            Debug.Log($"[UniversalGrid] {gridName} initialized ({gridWidth}x{gridHeight})");
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

        if (debugMode)
            Debug.Log($"[UniversalGrid] Created {gridWidth * gridHeight} background slots");
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

    /// <summary>
    /// Check if a screen point is over this grid
    /// </summary>
    public bool IsPointOverGrid(Vector2 screenPos)
    {
        if (backgroundLayer == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(
            backgroundLayer,
            screenPos,
            null
        );
    }

    /// <summary>
    /// Convert screen position to grid position
    /// </summary>
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

    /// <summary>
    /// Check if an item can be placed at a position
    /// </summary>
    public bool CanPlaceItemAt(ItemInstance item, GridPosition pos, string excludeItemId = null)
    {
        if (!pos.IsValid) return false;
        if (pos.x + item.itemWidth > gridWidth) return false;
        if (pos.y + item.itemHeight > gridHeight) return false;

        return CanPlaceInData(item, pos.x, pos.y, excludeItemId);
    }

    /// <summary>
    /// Add an item to this grid
    /// </summary>
    public bool AddItem(ItemInstance item, GridPosition pos)
    {
        if (!CanPlaceItemAt(item, pos))
        {
            if (debugMode)
                Debug.Log($"[UniversalGrid] Cannot place {item.Definition.displayName} at {pos}");
            return false;
        }

        bool success = AddItemToData(item, pos.x, pos.y);

        if (success)
        {
            RefreshVisuals();
            if (debugMode)
                Debug.Log($"[UniversalGrid] Added {item.Definition.displayName} to {gridName}");
        }

        return success;
    }

    /// <summary>
    /// Remove an item from this grid
    /// </summary>
    public bool RemoveItem(string itemId)
    {
        bool success = RemoveItemFromData(itemId);

        if (success)
        {
            RefreshVisuals();
            if (debugMode)
                Debug.Log($"[UniversalGrid] Removed item {itemId} from {gridName}");
        }

        return success;
    }

    /// <summary>
    /// Move an item within this grid
    /// </summary>
    public bool MoveItem(string itemId, GridPosition newPos)
    {
        bool success = MoveItemInData(itemId, newPos.x, newPos.y);

        if (success)
        {
            RefreshVisuals();
            if (debugMode)
                Debug.Log($"[UniversalGrid] Moved item {itemId} to {newPos}");
        }

        return success;
    }

    /// <summary>
    /// Show visual preview of where item would be placed
    /// </summary>
    public void ShowPlacementPreview(GridPosition pos, GridArea itemArea, string draggedItemId)
    {
        ClearPlacementPreview();

        if (!pos.IsValid) return;

        // Create temporary item for validation
        // We need to check if placement is valid
        bool canPlace = pos.x >= 0 && pos.y >= 0 &&
                       pos.x + itemArea.width <= gridWidth &&
                       pos.y + itemArea.height <= gridHeight;

        Color highlightColor = canPlace ?
            new Color(0.2f, 1f, 0.2f, 0.3f) : // Green
            new Color(1f, 0.2f, 0.2f, 0.3f);  // Red

        // Highlight the cells
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

    /// <summary>
    /// Clear placement preview highlighting
    /// </summary>
    public void ClearPlacementPreview()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                backgrounds[x, y].ClearHighlight();
            }
        }
    }

    /// <summary>
    /// Called when an item drag starts from this grid
    /// </summary>
    public virtual void OnItemDragStarted(string itemId, GridArea area)
    {
        // Hide the overlay
        if (overlays.ContainsKey(itemId))
        {
            overlays[itemId]?.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Called when drag is cancelled - restore item visuals
    /// </summary>
    public virtual void OnItemDragCancelled(string itemId)
    {
        // Show the overlay again
        if (overlays.ContainsKey(itemId))
        {
            overlays[itemId]?.gameObject.SetActive(true);
        }
    }

    #endregion

    #region Visual Management

    /// <summary>
    /// Refresh all item visuals based on current data
    /// </summary>
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

        if (debugMode)
            Debug.Log($"[UniversalGrid] Refreshed {items.Length} item visuals");
    }

    protected virtual void CreateItemVisuals(ItemInstance item)
    {
        GridArea area = new GridArea(item.gridX, item.gridY, item.itemWidth, item.itemHeight);

        CreateItemOverlay(item, area);
        CreateItemIcon(item, area);
    }

    protected virtual void CreateItemOverlay(ItemInstance item, GridArea area)
    {
        GameObject overlayObj = new GameObject($"Overlay_{item.instanceId}");
        overlayObj.transform.SetParent(overlayLayer, false);

        var image = overlayObj.AddComponent<UnityEngine.UI.Image>();
        image.raycastTarget = false;

        ItemOverlayVisual overlay = overlayObj.AddComponent<ItemOverlayVisual>();
        overlay.Initialize(item.instanceId, area, (int)item.currentTier, slotSize, slotSpacing);

        overlays[item.instanceId] = overlay;
    }

    protected virtual void CreateItemIcon(ItemInstance item, GridArea area)
    {
        GameObject iconObj = new GameObject($"Icon_{item.instanceId}");
        iconObj.transform.SetParent(iconLayer, false);

        var image = iconObj.AddComponent<UnityEngine.UI.Image>();
        image.sprite = item.Definition.icon;
        image.raycastTarget = true;

        var canvasGroup = iconObj.AddComponent<CanvasGroup>();

        ItemIconVisual icon = iconObj.AddComponent<ItemIconVisual>();
        icon.InitializeUniversal(item.instanceId, item, item.Definition.icon, area, slotSize, slotSpacing, this);

        icons[item.instanceId] = icon;
    }

    protected virtual void ClearAllVisuals()
    {
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

    /// <summary>
    /// Show tooltip when hovering over an item
    /// Called by ItemIconVisual
    /// </summary>
    public virtual void OnItemHoverEnter(string itemId, Vector2 pointerPosition)
    {
        var item = GetItemInstance(itemId);
        if (item == null) return;

        var definition = ItemManager.GetDefinition(item.definitionId);
        if (definition == null) return;

        string description = definition.description;

        // Add stat modifiers to description
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

        UniversalWindowManager.Instance?.ShowTooltip(tooltipData, pointerPosition);
    }

    /// <summary>
    /// Hide tooltip when mouse leaves item
    /// Called by ItemIconVisual
    /// </summary>
    public virtual void OnItemHoverExit()
    {
        UniversalWindowManager.Instance?.HideTooltip();
    }

    /// <summary>
    /// Called when an item icon is right-clicked.
    /// Override in subclasses to handle context actions (e.g. equip from inventory).
    /// </summary>
    public virtual void OnItemRightClicked(string itemId) { }

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