using UnityEngine;

/// <summary>
/// Hosts the player's inventory grid inside a panel slot.
/// Same grid the old floating window used, without the window chrome.
/// </summary>
public class InventoryPanelView : UIPanelView
{
    [Header("Grid")]
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private GameObject gridPrefab;

    [Header("Source")]
    [Tooltip("Leave empty to bind the player found at runtime.")]
    [SerializeField] private ControllerBrain sourceBrain;

    private UniversalInventoryGrid grid;

    protected override void OnShown()
    {
        if (sourceBrain == null) sourceBrain = PlayerBrainAccess.Find();

        if (sourceBrain == null)
        {
            Debug.LogWarning($"[InventoryPanelView] no player brain for {name}");
            return;
        }

        if (!sourceBrain.IsInitialized)
        {
            sourceBrain.OnInitialized += HandleBrainReady;
            return;
        }

        Bind(sourceBrain);
    }

    protected override void OnHidden()
    {
        if (sourceBrain != null) sourceBrain.OnInitialized -= HandleBrainReady;
    }

    private void HandleBrainReady(ControllerBrain ready)
    {
        ready.OnInitialized -= HandleBrainReady;
        Bind(ready);
    }

    private void Bind(ControllerBrain owner)
    {
        var inventory = owner.Inventory;

        if (inventory == null)
        {
            Debug.LogWarning($"[InventoryPanelView] {owner.name} has no InventorySystem");
            return;
        }

        if (!EnsureGrid()) return;

        // Size only after the source is set: SetPlayerInventory overwrites the grid's
        // dimensions from the container data, so fitting before this uses the prefab's.
        grid.SetPlayerInventory(inventory, owner);
        FitToGrid(grid.GetComponent<RectTransform>());
    }

    private bool EnsureGrid()
    {
        if (grid != null) return true;

        if (gridContainer == null || gridPrefab == null)
        {
            Debug.LogError($"[InventoryPanelView] {name} needs a grid container and grid prefab");
            return false;
        }

        var instance = Instantiate(gridPrefab, gridContainer);
        grid = instance.GetComponent<UniversalInventoryGrid>();

        if (grid == null)
        {
            Debug.LogError($"[InventoryPanelView] {gridPrefab.name} has no UniversalInventoryGrid");
            Destroy(instance);
            return false;
        }

        return true;
    }

    /// <summary>
    /// The grid maps screen points to cells by dividing the local point inside its layers
    /// by the slot pitch, so the rect must be exactly the grid's pixel size or every drop
    /// lands on the wrong cell. Pinned top-left of the container, never stretched.
    /// </summary>
    private void FitToGrid(RectTransform rect)
    {
        if (rect == null) return;

        float width = (grid.GridWidth * grid.SlotSize) + ((grid.GridWidth - 1) * grid.SlotSpacing);
        float height = (grid.GridHeight * grid.SlotSize) + ((grid.GridHeight - 1) * grid.SlotSpacing);

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
    }
}
