using UnityEngine;
using System.Collections.Generic;

public class GridTransferManager : MonoBehaviour
{
    private static GridTransferManager instance;
    public static GridTransferManager Instance
    {
        get
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<GridTransferManager>();
            if (instance != null) return instance;
            var go = new GameObject("GridTransferManager");
            instance = go.AddComponent<GridTransferManager>();
            DontDestroyOnLoad(go);
            return instance;
        }
    }

    [Header("Transfer Rules")]
    [SerializeField] private bool allowContainerToContainer = true;
    [SerializeField] private bool allowAutoStacking = true;
    [SerializeField] private bool allowItemSwapping = true;

    private readonly List<UniversalGrid> registeredGrids = new List<UniversalGrid>();

    private bool isDragging = false;
    private UniversalGrid sourceGrid;
    private string draggedItemId;
    private ItemInstance draggedItem;
    private GridArea draggedItemArea;

    public bool IsDragging => isDragging;
    public ItemInstance DraggedItem => draggedItem;
    public UniversalGrid SourceGrid => sourceGrid;

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterGrid(UniversalGrid grid)
    {
        if (grid == null || registeredGrids.Contains(grid)) return;
        registeredGrids.Add(grid);
    }

    public void UnregisterGrid(UniversalGrid grid)
    {
        if (grid == null) return;
        registeredGrids.Remove(grid);
    }

    public UniversalGrid GetGridAtScreenPosition(Vector2 screenPos)
    {
        foreach (var grid in registeredGrids)
        {
            if (grid != null && grid.IsPointOverGrid(screenPos)) return grid;
        }
        return null;
    }

    public void BeginDrag(UniversalGrid grid, string itemId, ItemInstance item, GridArea area)
    {
        if (isDragging) { Debug.LogWarning("[GridTransferManager] Already dragging an item!"); return; }

        sourceGrid = grid;
        draggedItemId = itemId;
        draggedItem = item;
        draggedItemArea = area;
        isDragging = true;

        sourceGrid.OnItemDragStarted(itemId, area);
    }

    public void UpdateDrag(Vector2 screenPos)
    {
        if (!isDragging) return;

        var targetGrid = GetGridAtScreenPosition(screenPos);
        if (targetGrid != null)
            targetGrid.ShowPlacementPreview(targetGrid.ScreenToGridPosition(screenPos), draggedItemArea, draggedItemId);
        else
            ClearAllPreviews();
    }

    public void EndDrag(Vector2 screenPos)
    {
        if (!isDragging) { Debug.LogWarning("[GridTransferManager] EndDrag called but not dragging!"); return; }

        ClearAllPreviews();

        var targetGrid = GetGridAtScreenPosition(screenPos);
        if (targetGrid == null)
        {
            sourceGrid.OnItemDragCancelled(draggedItemId);
            ResetDragState();
            return;
        }

        bool success = AttemptTransfer(sourceGrid, targetGrid, draggedItemId, draggedItem, targetGrid.ScreenToGridPosition(screenPos));
        if (!success) sourceGrid.OnItemDragCancelled(draggedItemId);

        ResetDragState();
    }

    public void CancelDrag()
    {
        if (!isDragging) return;
        ClearAllPreviews();
        sourceGrid?.OnItemDragCancelled(draggedItemId);
        ResetDragState();
    }

    private void ResetDragState()
    {
        isDragging = false;
        sourceGrid = null;
        draggedItemId = null;
        draggedItem = null;
        draggedItemArea = default;
    }

    private void ClearAllPreviews()
    {
        foreach (var grid in registeredGrids)
            grid?.ClearPlacementPreview();
    }

    private bool AttemptTransfer(UniversalGrid source, UniversalGrid target, string itemId, ItemInstance item, GridPosition targetPos)
    {
        if (source == target) return HandleSameGridMove(source, itemId, item, targetPos);
        return HandleCrossGridTransfer(source, target, itemId, item, targetPos);
    }

    private bool HandleSameGridMove(UniversalGrid grid, string itemId, ItemInstance item, GridPosition targetPos)
    {
        if (!grid.CanPlaceItemAt(item, targetPos, itemId)) return false;
        return grid.MoveItem(itemId, targetPos);
    }

    private bool HandleCrossGridTransfer(UniversalGrid source, UniversalGrid target, string itemId, ItemInstance item, GridPosition targetPos)
    {
        if (!allowContainerToContainer && !source.IsPlayerInventory && !target.IsPlayerInventory) return false;
        if (!target.CanPlaceItemAt(item, targetPos, itemId)) return false;

        if (!source.RemoveItem(itemId))
        {
            Debug.LogError("[GridTransferManager] Failed to remove item from source grid!");
            return false;
        }

        if (!target.AddItem(item, targetPos))
        {
            Debug.LogError("[GridTransferManager] Failed to add item to target grid - returning to source!");
            source.AddItem(item, new GridPosition(item.gridX, item.gridY));
            return false;
        }

        return true;
    }

    [ContextMenu("Debug: Print Registered Grids")]
    private void PrintRegisteredGrids()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GridTransferManager] Registered grids ({registeredGrids.Count}):");
        foreach (var grid in registeredGrids)
            if (grid != null) sb.AppendLine($"  {grid.GridName} ({grid.GridWidth}x{grid.GridHeight})");
        Debug.Log(sb.ToString());
    }
}