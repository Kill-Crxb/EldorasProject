using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryWindow : UIWindow
{
    [Header("Inventory Specific")]
    [SerializeField] private TextMeshProUGUI windowTitle;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform gridHolder;
    [SerializeField] private GameObject inventoryGridPrefab;

    [Header("Grid Settings")]
    [SerializeField] private float slotSize = 64f;
    [SerializeField] private float slotSpacing = 2f;
    [SerializeField] private float padding = 10f;

    private InventoryGridController gridController;
    private ItemSystem playerItemSystem;
    private int gridWidth = 8;  // Default fallback (will be overridden)
    private int gridHeight = 2; // Default fallback (will be overridden)

    protected override void SetupWindow()
    {
        // Set window title from IdentitySystem (single source of truth)
        if (windowTitle != null)
        {
            string playerName = GetPlayerName();
            windowTitle.text = $"{playerName}'s Inventory";
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseThisWindow);

        // Read grid dimensions from ContainerContents
        ReadGridDimensions();

        SetupWindowLayout();
        CreateInventoryGrid();
        SizeWindowToGrid();
    }

    /// <summary>
    /// Get player name from IdentitySystem (standards-compliant with guard clauses)
    /// </summary>
    private string GetPlayerName()
    {
        // Guard clause: no player brain
        if (playerBrain == null) return "Player";

        // Read from IdentitySystem (single source of truth for entity name)
        var identitySystem = playerBrain.GetModule<IdentitySystem>();
        if (identitySystem != null)
            return identitySystem.GetEntityName();

        // Fallback to GameObject name
        return playerBrain.name;
    }

    /// <summary>
    /// Read grid dimensions from ContainerContents (standards-compliant with guard clauses)
    /// </summary>
    private void ReadGridDimensions()
    {
        // Guard clause: no player brain
        if (playerBrain == null)
        {
            if (debugMode)
                Debug.LogWarning("[InventoryWindow] ReadGridDimensions: No player brain");
            return;
        }

        playerItemSystem = playerBrain.GetModule<ItemSystem>();

        // Guard clause: no item system
        if (playerItemSystem == null)
        {
            if (debugMode)
                Debug.LogWarning("[InventoryWindow] ReadGridDimensions: No ItemSystem found");
            return;
        }

        var inventorySystem = playerBrain.GetModule<InventorySystem>();

        // Guard clause: no inventory system
        if (inventorySystem == null)
        {
            if (debugMode)
                Debug.LogWarning("[InventoryWindow] ReadGridDimensions: No InventorySystem found");
            return;
        }

        // Read from ContainerContents (single source of truth for grid dimensions)
        var contents = inventorySystem.GetCurrentContents();
        if (contents != null)
        {
            gridWidth = contents.gridWidth;
            gridHeight = contents.gridHeight;

            if (debugMode)
                Debug.Log($"[InventoryWindow] Read grid size from ContainerContents: {gridWidth}×{gridHeight}");
        }
        else
        {
            // Fallback: Try reading from containers array directly
            var containerData = GetContainerDataFallback(inventorySystem);
            if (containerData.HasValue)
            {
                gridWidth = containerData.Value.width;
                gridHeight = containerData.Value.height;

                if (debugMode)
                    Debug.Log($"[InventoryWindow] Read grid size from containers array fallback: {gridWidth}×{gridHeight}");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[InventoryWindow] Could not read grid dimensions - using defaults: {gridWidth}×{gridHeight}");
            }
        }
    }

    /// <summary>
    /// Fallback: Read directly from containers array if ContainerContents not available
    /// </summary>
    private (int width, int height)? GetContainerDataFallback(InventorySystem inventorySystem)
    {
        // Guard clause: null system
        if (inventorySystem == null) return null;

        // Use reflection to read containers array
        var containerField = inventorySystem.GetType().GetField("containers",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (containerField == null) return null;

        var containers = containerField.GetValue(inventorySystem) as ContainerData[];
        if (containers == null || containers.Length == 0) return null;

        // Return first container dimensions
        return (containers[0].gridWidth, containers[0].gridHeight);
    }

    private void SetupWindowLayout()
    {
        if (windowBackground == null) return;

        var verticalLayout = windowBackground.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
            verticalLayout = windowBackground.gameObject.AddComponent<VerticalLayoutGroup>();

        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.spacing = 10f;
        verticalLayout.padding = new RectOffset(10, 10, 10, 10);

        var contentSizeFitter = windowBackground.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter == null)
            contentSizeFitter = windowBackground.gameObject.AddComponent<ContentSizeFitter>();

        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void CreateInventoryGrid()
    {
        if (inventoryGridPrefab == null || gridHolder == null)
        {
            Debug.LogError("[InventoryWindow] Missing grid prefab or holder!");
            return;
        }

        GameObject gridObj = Instantiate(inventoryGridPrefab, gridHolder);
        gridController = gridObj.GetComponent<InventoryGridController>();

        if (gridController == null)
        {
            Debug.LogError("[InventoryWindow] Grid prefab missing InventoryGridController!");
            return;
        }

        // Set grid dimensions
        SetGridDimensions(gridController, gridWidth, gridHeight);

        // Connect grid to player's ItemSystem BEFORE it initializes
        if (playerItemSystem != null)
        {
            gridController.SetItemSystem(playerItemSystem);

            if (debugMode)
                Debug.Log("[InventoryWindow] Connected grid to player's ItemSystem");
        }

        var gridRect = gridController.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.anchorMin = new Vector2(0, 0);
            gridRect.anchorMax = new Vector2(0, 0);
            gridRect.pivot = new Vector2(0, 0);
            gridRect.anchoredPosition = new Vector2(padding, padding);
        }

        if (debugMode)
            Debug.Log($"[InventoryWindow] Grid created at {gridWidth}×{gridHeight}");
    }

    private void SetGridDimensions(InventoryGridController grid, int width, int height)
    {
        var gridWidthField = grid.GetType().GetField("gridWidth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gridHeightField = grid.GetType().GetField("gridHeight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (gridWidthField != null)
            gridWidthField.SetValue(grid, width);
        if (gridHeightField != null)
            gridHeightField.SetValue(grid, height);
    }

    private void SizeWindowToGrid()
    {
        if (gridHolder == null) return;

        float gridPixelWidth = (gridWidth * slotSize) + ((gridWidth - 1) * slotSpacing);
        float gridPixelHeight = (gridHeight * slotSize) + ((gridHeight - 1) * slotSpacing);

        var layoutElement = gridHolder.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gridHolder.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = gridPixelWidth + (padding * 2);
        layoutElement.preferredHeight = gridPixelHeight + (padding * 2);
        layoutElement.minWidth = layoutElement.preferredWidth;
        layoutElement.minHeight = layoutElement.preferredHeight;

        if (debugMode)
            Debug.Log($"[InventoryWindow] Sized to {layoutElement.preferredWidth}×{layoutElement.preferredHeight}");
    }

    public override void OnClose()
    {
        base.OnClose();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseThisWindow);
    }
}