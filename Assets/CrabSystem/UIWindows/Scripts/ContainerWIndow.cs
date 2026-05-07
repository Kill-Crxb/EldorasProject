using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ContainerWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI References")]
    [SerializeField] private RectTransform windowBackground;
    [SerializeField] private RectTransform containerNameBackground;
    [SerializeField] private TextMeshProUGUI containerNameText;
    [SerializeField] private RectTransform gridHolder;
    [SerializeField] private Button containerExit;

    [Header("Grid Prefab")]
    [SerializeField] private GameObject inventoryGridPrefab;

    [Header("Window Settings")]
    [SerializeField] private float maxInteractionDistance = 5f;
    [Tooltip("Automatically open player's inventory window when container opens")]
    [SerializeField] private bool autoOpenPlayerInventory = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private Canvas canvas;
    private RectTransform canvasRect;
    private Vector2 dragOffset;
    private bool isDragging;
    private ControllerBrain containerBrain;
    private IInventoryProvider containerInventory;
    private ControllerBrain playerBrain;
    private InventoryGridController gridController;

    // Position persistence (per-container)
    private string positionKey = "";

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas?.GetComponent<RectTransform>();

        if (containerExit != null)
            containerExit.onClick.AddListener(Close);

        gameObject.SetActive(false);
    }

    public void Open(ControllerBrain player, ControllerBrain container)
    {
        Debug.Log($"[ContainerWindow] Open called. Player: {player?.name}, Container: {container?.name}");

        // Guard clauses
        if (container == null)
        {
            Debug.LogError("[ContainerWindow] Container is null!");
            return;
        }
        if (player == null)
        {
            Debug.LogError("[ContainerWindow] Player is null!");
            return;
        }

        playerBrain = player;
        containerBrain = container;
        containerInventory = container.GetProvider<IInventoryProvider>();

        Debug.Log($"[ContainerWindow] IInventoryProvider: {containerInventory != null}");

        // Guard clause: no inventory provider
        if (containerInventory == null)
        {
            Debug.LogError("[ContainerWindow] Container has no IInventoryProvider!");
            return;
        }

        // Generate unique position key for this container
        positionKey = GetContainerPositionKey();

        Debug.Log("[ContainerWindow] Calling SetupWindow()");
        SetupWindow();

        Debug.Log("[ContainerWindow] Restoring position");
        RestoreWindowPosition();

        Debug.Log("[ContainerWindow] Activating GameObject");
        gameObject.SetActive(true);
        IsOpen = true;

        Debug.Log($"[ContainerWindow] Window opened. IsOpen: {IsOpen}, GameObject.activeSelf: {gameObject.activeSelf}");

        // Auto-open player's inventory window
        OpenPlayerInventory();
    }

    public void Close()
    {
        if (!IsOpen) return;

        // Save position before closing
        SaveWindowPosition();

        // Destroy container grid
        if (gridController != null)
        {
            Destroy(gridController.gameObject);
            gridController = null;
        }

        gameObject.SetActive(false);
        IsOpen = false;

        // Close player's inventory window
        ClosePlayerInventory();

        containerBrain = null;
        containerInventory = null;
        playerBrain = null;
        positionKey = "";
    }

    private void SetupWindow()
    {
        var inventorySystem = containerBrain.GetModule<InventorySystem>();
        if (inventorySystem == null) return;

        string containerName = GetContainerName(inventorySystem);
        if (containerNameText != null)
            containerNameText.text = containerName;

        SetupWindowLayout();
        CreateInventoryGrid(inventorySystem);
    }

    private void SetupWindowLayout()
    {
        if (windowBackground == null) return;

        var vertLayout = windowBackground.GetComponent<VerticalLayoutGroup>();
        if (vertLayout != null)
        {
            vertLayout.childAlignment = TextAnchor.UpperCenter;
            vertLayout.childControlWidth = true;
            vertLayout.childControlHeight = true;
            vertLayout.childForceExpandWidth = false;
            vertLayout.childForceExpandHeight = false;

            Debug.Log($"[ContainerWindow] Updated existing VerticalLayoutGroup. Spacing: {vertLayout.spacing}, childControlHeight: {vertLayout.childControlHeight}");
        }

        var windowLayout = windowBackground.GetComponent<ContentSizeFitter>();
        if (windowLayout != null)
        {
            windowLayout.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            windowLayout.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    private string GetContainerName(InventorySystem inventorySystem)
    {
        // Guard clause: null inventory system
        if (inventorySystem == null) return "Container";

        // Guard clause: no brain reference
        if (containerBrain == null) return "Container";

        // Read from IdentitySystem (single source of truth for entity name)
        var identitySystem = containerBrain.GetModule<IdentitySystem>();
        if (identitySystem != null)
            return identitySystem.GetEntityName();

        // Fallback to GameObject name
        return containerBrain.name;
    }

    private void CreateInventoryGrid(InventorySystem inventorySystem)
    {
        if (gridHolder == null) return;
        if (inventoryGridPrefab == null) return;

        foreach (Transform child in gridHolder)
            Destroy(child.gameObject);

        var dimensions = GetContainerGridDimensions(inventorySystem);
        int gridWidth = dimensions.width;
        int gridHeight = dimensions.height;

        GameObject gridObj = Instantiate(inventoryGridPrefab, gridHolder);
        gridController = gridObj.GetComponent<InventoryGridController>();

        if (gridController == null) return;

        SetGridDimensionsAndConnection(gridController, gridWidth, gridHeight, inventorySystem);
        float gridHeight_px = SizeGridAndContainers(gridObj, gridWidth, gridHeight);

        gridObj.SetActive(true);
    }

    private (int width, int height) GetContainerGridDimensions(InventorySystem inventorySystem)
    {
        // Guard clause: null inventory system
        if (inventorySystem == null) return (8, 6);

        // Read from ContainerContents (single source of truth for grid dimensions)
        var contents = inventorySystem.GetCurrentContents();
        if (contents != null)
            return (contents.gridWidth, contents.gridHeight);

        // Fallback to default dimensions
        return (8, 6);
    }

    private void SetGridDimensionsAndConnection(InventoryGridController gridController, int width, int height, InventorySystem inventorySystem)
    {
        var gridWidthField = gridController.GetType().GetField("gridWidth",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gridHeightField = gridController.GetType().GetField("gridHeight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (gridWidthField != null)
            gridWidthField.SetValue(gridController, width);
        if (gridHeightField != null)
            gridHeightField.SetValue(gridController, height);

        var containerItemSystem = containerBrain.GetModule<ItemSystem>();
        if (containerItemSystem != null)
        {
            gridController.SetItemSystem(containerItemSystem);
        }
        else
        {
            Debug.LogError("[ContainerWindow] Container has no ItemSystem!");
        }
    }

    private float SizeGridAndContainers(GameObject gridObj, int gridWidth, int gridHeight)
    {
        float slotSize = 64f;
        float slotSpacing = 2f;

        var slotSizeField = gridController.GetType().GetField("slotSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var slotSpacingField = gridController.GetType().GetField("slotSpacing",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (slotSizeField != null)
            slotSize = (float)slotSizeField.GetValue(gridController);
        if (slotSpacingField != null)
            slotSpacing = (float)slotSpacingField.GetValue(gridController);

        float totalWidth = (gridWidth * slotSize) + ((gridWidth - 1) * slotSpacing);
        float totalHeight = (gridHeight * slotSize) + ((gridHeight - 1) * slotSpacing);

        Debug.Log($"[ContainerWindow] Grid dimensions: {gridWidth}�{gridHeight}");
        Debug.Log($"[ContainerWindow] Calculated size: {totalWidth}�{totalHeight}");

        float padding = 10f;

        var gridRect = gridObj.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.zero;
            gridRect.pivot = Vector2.zero;
            gridRect.anchoredPosition = new Vector2(padding, padding);
            gridRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            Debug.Log($"[ContainerWindow] Grid RectTransform size set to: {gridRect.sizeDelta}");
        }

        if (gridHolder != null)
        {
            var holderLayout = gridHolder.GetComponent<LayoutElement>();
            if (holderLayout == null)
                holderLayout = gridHolder.gameObject.AddComponent<LayoutElement>();

            float holderWidth = totalWidth + (padding * 2);
            float holderHeight = totalHeight + (padding * 2);

            holderLayout.preferredWidth = holderWidth;
            holderLayout.preferredHeight = holderHeight;
            holderLayout.minWidth = holderWidth;
            holderLayout.minHeight = holderHeight;

            Debug.Log($"[ContainerWindow] GridHolder LayoutElement set to: {holderWidth}�{holderHeight}");
            Debug.Log($"[ContainerWindow] GridHolder actual size: {gridHolder.sizeDelta}");
        }

        return totalHeight;
    }

    private void CenterWindow()
    {
        if (windowBackground == null)
        {
            Debug.LogError("[ContainerWindow] windowBackground is NULL!");
            return;
        }
        if (canvasRect == null)
        {
            Debug.LogError("[ContainerWindow] canvasRect is NULL!");
            return;
        }

        windowBackground.anchoredPosition = Vector2.zero;

        Debug.Log($"[ContainerWindow] Window centered. Position: {windowBackground.anchoredPosition}, Size: {windowBackground.sizeDelta}");
        Debug.Log($"[ContainerWindow] Canvas: {canvas?.name}, RenderMode: {canvas?.renderMode}");
        Debug.Log($"[ContainerWindow] Window parent: {windowBackground.parent?.name}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Guard clause: must drag from title bar
        if (containerNameBackground == null) return;
        if (!RectTransformUtility.RectangleContainsScreenPoint(containerNameBackground, eventData.position, canvas?.worldCamera))
            return;

        isDragging = true;

        // Calculate where on the window we grabbed (in parent space)
        RectTransform parentRect = windowBackground.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 localMousePos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localMousePos))
        {
            // Store the offset from window position to mouse position
            dragOffset = localMousePos - windowBackground.anchoredPosition;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Guard clauses
        if (!isDragging) return;
        if (windowBackground == null) return;

        // Get parent RectTransform
        RectTransform parentRect = windowBackground.parent as RectTransform;
        if (parentRect == null) return;

        // Convert screen position to local position in parent space
        Vector2 localMousePos;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out localMousePos))
        {
            // Update window position (subtract offset to maintain grab point)
            windowBackground.anchoredPosition = localMousePos - dragOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        // Save position after drag
        SaveWindowPosition();
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (containerBrain == null) return;
        if (playerBrain == null) return;

        float distance = Vector3.Distance(playerBrain.transform.position, containerBrain.transform.position);
        if (distance > maxInteractionDistance)
            Close();
    }

    #region Position Persistence (Standards-Compliant)

    /// <summary>
    /// Generate unique PlayerPrefs key for this specific container
    /// Standards: Reads from IdentitySystem (single source of truth)
    /// </summary>
    private string GetContainerPositionKey()
    {
        // Guard clause: no container brain
        if (containerBrain == null) return "ContainerWindow_Default";

        // Read container ID from IdentitySystem (single source of truth)
        var identitySystem = containerBrain.GetModule<IdentitySystem>();
        if (identitySystem != null)
        {
            string containerName = identitySystem.GetEntityName().Replace(" ", "_");
            return $"ContainerWindow_{containerName}_Position";
        }

        // Fallback to GameObject name
        return $"ContainerWindow_{containerBrain.name}_Position";
    }

    /// <summary>
    /// Save window position to PlayerPrefs
    /// Standards: Guard clauses, clear error handling
    /// </summary>
    private void SaveWindowPosition()
    {
        // Guard clause: no window background
        if (windowBackground == null) return;

        // Guard clause: no position key
        if (string.IsNullOrEmpty(positionKey)) return;

        Vector2 position = windowBackground.anchoredPosition;
        PlayerPrefs.SetFloat($"{positionKey}_X", position.x);
        PlayerPrefs.SetFloat($"{positionKey}_Y", position.y);
        PlayerPrefs.Save();

        if (debugMode)
            Debug.Log($"[ContainerWindow] Saved position for '{positionKey}': {position}");
    }

    /// <summary>
    /// Restore window position from PlayerPrefs or center if no saved position
    /// Standards: Guard clauses, fallback to center
    /// </summary>
    private void RestoreWindowPosition()
    {
        // Guard clause: no window background
        if (windowBackground == null) return;

        // Guard clause: no position key
        if (string.IsNullOrEmpty(positionKey))
        {
            CenterWindow();
            return;
        }

        // Check if saved position exists
        if (PlayerPrefs.HasKey($"{positionKey}_X") && PlayerPrefs.HasKey($"{positionKey}_Y"))
        {
            Vector2 savedPosition = new Vector2(
                PlayerPrefs.GetFloat($"{positionKey}_X"),
                PlayerPrefs.GetFloat($"{positionKey}_Y")
            );

            windowBackground.anchoredPosition = savedPosition;

            if (debugMode)
                Debug.Log($"[ContainerWindow] Restored position for '{positionKey}': {savedPosition}");
        }
        else
        {
            // No saved position - center window
            CenterWindow();

            if (debugMode)
                Debug.Log($"[ContainerWindow] No saved position for '{positionKey}' - centered window");
        }
    }

    #endregion

    #region Player Inventory Auto-Open (Standards-Compliant)

    /// <summary>
    /// Open player's inventory window when container is opened
    /// Standards: Guard clauses, provider pattern
    /// </summary>
    private void OpenPlayerInventory()
    {
        // Guard clause: feature disabled
        if (!autoOpenPlayerInventory) return;

        // Guard clause: no player brain
        if (playerBrain == null) return;

        // Get UIWindowManager instance
        var windowManager = UIWindowManager.Instance;

        // Guard clause: no window manager
        if (windowManager == null)
        {
            if (debugMode)
                Debug.LogWarning("[ContainerWindow] UIWindowManager.Instance not found - cannot auto-open player inventory");
            return;
        }

        // Only open if not already open
        if (!windowManager.IsWindowOpen("Inventory"))
        {
            windowManager.ToggleInventoryWindow();

            if (debugMode)
                Debug.Log("[ContainerWindow] Auto-opened player's inventory window");
        }
    }

    /// <summary>
    /// Close player's inventory window when container is closed
    /// Standards: Guard clauses
    /// </summary>
    private void ClosePlayerInventory()
    {
        // Guard clause: feature disabled
        if (!autoOpenPlayerInventory) return;

        // Guard clause: no player brain
        if (playerBrain == null) return;

        var windowManager = UIWindowManager.Instance;

        // Guard clause: no window manager
        if (windowManager == null) return;

        // Only close if it's open
        if (windowManager.IsWindowOpen("Inventory"))
        {
            windowManager.CloseWindow("Inventory");

            if (debugMode)
                Debug.Log("[ContainerWindow] Auto-closed player's inventory window");
        }
    }

    #endregion
}