using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // For dragging
using TMPro; // Add TextMeshPro support

/// <summary>
/// UniversalInventoryWindow - Single window prefab for ALL inventory displays
/// 
/// Can be used for:
/// - Player inventory
/// - Chests/containers
/// - Stash
/// - Trade windows
/// - Crafting benches
/// - Any InventorySystem
/// 
/// Architecture:
/// - Window manager creates window from prefab
/// - Calls Open() with the inventory source
/// - Window creates/configures the grid
/// - Grid registers with GridTransferManager
/// - User can drag between any windows
/// 
/// Example Usage:
/// ```csharp
/// // Player inventory
/// window.OpenPlayerInventory(playerBrain);
/// 
/// // Container
/// window.OpenContainer(chestBrain, "Wooden Chest");
/// 
/// // Stash
/// window.OpenStash(stashSystem, "Personal Stash");
/// ```
/// 
/// Created: February 13, 2026
/// </summary>
public class UniversalInventoryWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    #region Inspector References

    [Header("Window Components")]
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private RectTransform headerRect;

    [Tooltip("Use either legacy Text or TextMeshPro - leave one null")]
    [SerializeField] private Text titleText;
    [SerializeField] private TMP_Text titleTextTMP; // Changed to TMP_Text for compatibility

    [SerializeField] private Button closeButton;

    [Header("Grid Container")]
    [SerializeField] private RectTransform gridContainer;

    [Header("Grid Prefab")]
    [SerializeField] private GameObject gridPrefab;

    [Header("Settings")]
    [SerializeField] private bool savePosition = true;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Vector2 defaultPositionOffset = new Vector2(100, -100);

    [Header("Container Settings (Only for Containers)")]
    [Tooltip("Maximum distance player can be from container before auto-closing")]
    [SerializeField] private float maxInteractionDistance = 5f;
    [Tooltip("Check distance every frame and close if too far")]
    [SerializeField] private bool autoCloseOnDistance = true;
    [Tooltip("Close window when ESC key is pressed")]
    [SerializeField] private bool closeOnEscape = true;

    #endregion

    #region State

    private UniversalInventoryGrid currentGrid;
    private InventorySystem currentInventorySystem;
    private ControllerBrain currentOwner; // Player or container
    private string windowId;
    private bool isOpen = false;
    private bool isPlayerInventory = false; // Track if this is player inventory window

    // Dragging state
    private Vector2 dragStartPos; // Local offset when drag begins

    #endregion

    #region Initialization

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    private void Update()
    {
        if (!isOpen) return;

        // ESC key handling
        if (closeOnEscape && UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }
        }

        // Distance checking for container windows (not player inventory)
        if (autoCloseOnDistance && currentGrid != null && !currentGrid.IsPlayerInventory)
        {
            CheckDistanceAndClose();
        }
    }

    private void CheckDistanceAndClose()
    {
        // Need both player and container
        if (currentOwner == null) return;

        // Find player brain
        ControllerBrain playerBrain = FindPlayerBrain();
        if (playerBrain == null) return;

        // Calculate distance
        float distance = Vector3.Distance(playerBrain.transform.position, currentOwner.transform.position);

        if (distance > maxInteractionDistance)
        {
            if (debugMode)
                Debug.Log($"[UniversalInventoryWindow] Player too far ({distance:F1}m > {maxInteractionDistance}m), closing container");

            Close();
        }
    }

    private ControllerBrain FindPlayerBrain()
    {
        // Check all brains for player
        var allBrains = FindObjectsByType<ControllerBrain>(FindObjectsSortMode.None);
        foreach (var brain in allBrains)
        {
            if (brain.IsPlayer || brain.EntityType == EntityType.Player)
            {
                return brain;
            }
        }
        return null;
    }

    #endregion

    #region Open Methods

    /// <summary>
    /// Open this window for the player's inventory
    /// </summary>
    public void OpenPlayerInventory(ControllerBrain playerBrain)
    {
        if (playerBrain == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Player brain is null!");
            return;
        }

        var inventorySystem = playerBrain.GetModule<InventorySystem>();
        if (inventorySystem == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Player has no InventorySystem!");
            return;
        }

        string title = "Inventory";
        windowId = "PlayerInventory";

        Open(inventorySystem, playerBrain, title, isPlayerInv: true);
    }

    /// <summary>
    /// Open this window for a container (chest, crate, corpse, etc.)
    /// </summary>
    public void OpenContainer(ControllerBrain containerBrain, string containerName = null)
    {
        if (containerBrain == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Container brain is null!");
            return;
        }

        var inventorySystem = containerBrain.GetModule<InventorySystem>();
        if (inventorySystem == null)
        {
            Debug.LogError($"[UniversalInventoryWindow] Container {containerBrain.name} has no InventorySystem!");
            return;
        }

        string title = containerName ?? containerBrain.Identity?.GetEntityName() ?? "Container";
        windowId = $"Container_{containerBrain.GetInstanceID()}";

        Open(inventorySystem, containerBrain, title, isPlayerInv: false);
    }

    /// <summary>
    /// Open this window for any inventory system (generic)
    /// </summary>
    public void OpenGeneric(InventorySystem system, string title, string id, bool isPlayer = false)
    {
        if (system == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Inventory system is null!");
            return;
        }

        windowId = id;
        Open(system, null, title, isPlayer);
    }

    /// <summary>
    /// Core open method - all other open methods call this
    /// </summary>
    private void Open(InventorySystem system, ControllerBrain owner, string title, bool isPlayerInv)
    {
        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] Opening: {title}");

        currentInventorySystem = system;
        currentOwner = owner;
        isPlayerInventory = isPlayerInv;

        // Set title (supports both legacy Text and TextMeshPro)
        SetTitle(title);

        // Create or configure grid
        SetupGrid(system, title, isPlayerInv);

        // Size the grid container to fit the grid
        SizeGridContainerToGrid();

        // Restore position if saved
        if (savePosition)
        {
            RestorePosition();
        }

        // Show window
        gameObject.SetActive(true);
        isOpen = true;

        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] {title} opened");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Set window title - supports both legacy Text and TextMeshPro
    /// </summary>
    private void SetTitle(string title)
    {
        if (titleTextTMP != null)
        {
            titleTextTMP.text = title;
        }
        else if (titleText != null)
        {
            titleText.text = title;
        }
    }

    #endregion

    #region Grid Setup

    private void SetupGrid(InventorySystem system, string displayName, bool isPlayer)
    {
        // Clear existing grid if any
        if (currentGrid != null)
        {
            Destroy(currentGrid.gameObject);
            currentGrid = null;
        }

        // Create grid from prefab
        if (gridPrefab == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Grid prefab is not assigned!");
            return;
        }

        GameObject gridObj = Instantiate(gridPrefab, gridContainer);

        // Ensure proper RectTransform setup
        RectTransform gridRect = gridObj.GetComponent<RectTransform>();
        if (gridRect != null)
        {
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            gridRect.anchoredPosition = Vector2.zero;
        }

        currentGrid = gridObj.GetComponent<UniversalInventoryGrid>();

        if (currentGrid == null)
        {
            Debug.LogError("[UniversalInventoryWindow] Grid prefab doesn't have UniversalInventoryGrid component!");
            Destroy(gridObj);
            return;
        }

        // Configure the grid
        currentGrid.SetInventorySource(system, displayName, isPlayer, isPlayer ? currentOwner : null);

        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] Grid configured for {displayName}");
    }

    /// <summary>
    /// Sizes the grid container to fit the grid based on its dimensions
    /// </summary>
    private void SizeGridContainerToGrid()
    {
        if (gridContainer == null || currentGrid == null) return;

        // Get grid dimensions from the UniversalGrid
        int width = currentGrid.GridWidth;
        int height = currentGrid.GridHeight;
        float slotSize = currentGrid.SlotSize;
        float slotSpacing = currentGrid.SlotSpacing;

        // Calculate pixel size
        float gridPixelWidth = (width * slotSize) + ((width - 1) * slotSpacing);
        float gridPixelHeight = (height * slotSize) + ((height - 1) * slotSpacing);

        // Set the grid container size using LayoutElement
        var layoutElement = gridContainer.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gridContainer.gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = gridPixelWidth;
        layoutElement.preferredHeight = gridPixelHeight;
        layoutElement.minWidth = gridPixelWidth;
        layoutElement.minHeight = gridPixelHeight;

        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] Sized grid container to {gridPixelWidth}×{gridPixelHeight}");
    }

    #endregion

    #region Close

    public void Close()
    {
        if (!isOpen) return;

        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] Closing {windowId}");

        // Save position if enabled
        if (savePosition)
        {
            SavePosition();
        }

        // Hide window
        gameObject.SetActive(false);
        isOpen = false;

        // Clean up grid
        if (currentGrid != null)
        {
            Destroy(currentGrid.gameObject);
            currentGrid = null;
        }

        currentInventorySystem = null;
        currentOwner = null;
    }

    #endregion

    #region Position Management

    private void RestorePosition()
    {
        if (windowRect == null || string.IsNullOrEmpty(windowId)) return;

        string key = $"WindowPos_{windowId}";
        if (PlayerPrefs.HasKey(key + "_x"))
        {
            // Restore saved position
            float x = PlayerPrefs.GetFloat(key + "_x");
            float y = PlayerPrefs.GetFloat(key + "_y");
            windowRect.anchoredPosition = new Vector2(x, y);

            if (debugMode)
                Debug.Log($"[UniversalInventoryWindow] Restored position: {x}, {y}");
        }
        else
        {
            // First time opening - use default offset
            windowRect.anchoredPosition = defaultPositionOffset;

            if (debugMode)
                Debug.Log($"[UniversalInventoryWindow] Using default position: {defaultPositionOffset}");
        }
    }

    private void SavePosition()
    {
        if (windowRect == null || string.IsNullOrEmpty(windowId)) return;

        string key = $"WindowPos_{windowId}";
        PlayerPrefs.SetFloat(key + "_x", windowRect.anchoredPosition.x);
        PlayerPrefs.SetFloat(key + "_y", windowRect.anchoredPosition.y);
        PlayerPrefs.Save();

        if (debugMode)
            Debug.Log($"[UniversalInventoryWindow] Saved position: {windowRect.anchoredPosition}");
    }

    #endregion

    #region Dragging

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;

        // Only drag from header
        if (headerRect != null && !RectTransformUtility.RectangleContainsScreenPoint(headerRect, eventData.position))
            return;

        // Convert screen point to local point in the window's coordinate space
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowRect,
            eventData.position,
            eventData.pressEventCamera,
            out dragStartPos
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (windowRect == null) return;

        // Convert screen point to local point in the parent's coordinate space
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowRect.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint))
        {
            windowRect.anchoredPosition = localPoint - dragStartPos;
        }
    }

    #endregion

    #region Properties

    public bool IsOpen => isOpen;
    public string WindowId => windowId;
    public UniversalInventoryGrid Grid => currentGrid;
    public InventorySystem InventorySystem => currentInventorySystem;

    #endregion
}