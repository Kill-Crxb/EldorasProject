using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UniversalWindowManager - Central UI hub.
///
/// Owns:
/// - Named canvas layer references (Overlay, UI, Dialogue, Effect, BG)
/// - Tooltip lifecycle (prefab, instantiation, show/hide)
/// - All inventory/equipment window lifecycle
///
/// TooltipManager has been fully absorbed into this class.
/// External callers use UniversalWindowManager.Instance.ShowTooltip / HideTooltip.
/// No script should ever search for a Canvas or call FindObjectOfType for UI.
/// </summary>
public class UniversalWindowManager : MonoBehaviour
{
    #region Inspector — Canvas Layers

    [Header("Canvas Layers")]
    [SerializeField] private Canvas overlayCanvas;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Canvas dialogueCanvas;
    [SerializeField] private Canvas effectCanvas;
    [SerializeField] private Canvas bgCanvas;

    #endregion

    #region Inspector — Tooltip

    [Header("Tooltip")]
    [SerializeField] private ItemTooltip tooltipPrefab;

    #endregion

    #region Inspector — Windows

    [Header("Window Prefabs")]
    [SerializeField] private GameObject universalWindowPrefab;
    [SerializeField] private GameObject equipmentWindowPrefab;

    [Header("World Space Container Settings")]
    [SerializeField] private bool useWorldSpaceContainers = false;
    [SerializeField] private GameObject worldSpaceWindowPrefab;
    [SerializeField] private Vector3 worldSpaceOffset = new Vector3(0, 2f, 0);

    [Header("Settings")]
    [SerializeField] private bool closeOtherWindowsOnOpen = false;
    [SerializeField] private bool debugMode = false;

    #endregion

    #region Singleton

    private static UniversalWindowManager instance;
    public static UniversalWindowManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<UniversalWindowManager>();
                if (instance == null)
                    Debug.LogError("[UniversalWindowManager] No instance found in scene!");
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        InitializeTooltip();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        CloseAllWindows();
    }

    #endregion

    #region Canvas Accessors

    public Canvas OverlayCanvas => overlayCanvas;
    public Canvas UICanvas => uiCanvas;
    public Canvas DialogueCanvas => dialogueCanvas;
    public Canvas EffectCanvas => effectCanvas;
    public Canvas BGCanvas => bgCanvas;

    #endregion

    #region Tooltip

    private ItemTooltip currentTooltip;

    private void InitializeTooltip()
    {
        if (tooltipPrefab == null) { Debug.LogError("[UniversalWindowManager] Tooltip prefab not assigned!"); return; }
        if (overlayCanvas == null) { Debug.LogError("[UniversalWindowManager] Overlay canvas not assigned — tooltip cannot initialize!"); return; }

        currentTooltip = Instantiate(tooltipPrefab, overlayCanvas.transform);
        currentTooltip.Hide();
    }

    public void ShowTooltip(ItemTooltipData data, Vector2 screenPosition)
    {
        if (currentTooltip == null) { Debug.LogWarning("[UniversalWindowManager] Tooltip not initialized."); return; }
        currentTooltip.Show(data, screenPosition);
    }

    public void HideTooltip()
    {
        currentTooltip?.Hide();
    }

    #endregion

    #region State

    private Dictionary<string, UniversalInventoryWindow> activeWindows = new Dictionary<string, UniversalInventoryWindow>();
    private UniversalInventoryWindow playerInventoryWindow;
    private EquipmentWindow equipmentWindow;

    #endregion

    #region Player Inventory

    public void OpenPlayerInventory(ControllerBrain playerBrain)
    {
        if (playerBrain == null) { Debug.LogError("[UniversalWindowManager] Player brain is null!"); return; }
        if (playerInventoryWindow != null && playerInventoryWindow.IsOpen) return;

        if (playerInventoryWindow == null)
            playerInventoryWindow = CreateWindow("PlayerInventory");

        playerInventoryWindow.OpenPlayerInventory(playerBrain);

        if (debugMode) Debug.Log("[UniversalWindowManager] Opened player inventory");
    }

    public void ClosePlayerInventory()
    {
        if (playerInventoryWindow != null && playerInventoryWindow.IsOpen)
        {
            playerInventoryWindow.Close();
            if (debugMode) Debug.Log("[UniversalWindowManager] Closed player inventory");
        }
    }

    public void TogglePlayerInventory(ControllerBrain playerBrain)
    {
        if (playerInventoryWindow != null && playerInventoryWindow.IsOpen)
            ClosePlayerInventory();
        else
            OpenPlayerInventory(playerBrain);
    }

    #endregion

    #region Container Windows

    public UniversalInventoryWindow OpenContainerWindow(ControllerBrain playerBrain, ControllerBrain containerBrain)
    {
        if (containerBrain == null) { Debug.LogError("[UniversalWindowManager] Container brain is null!"); return null; }

        string windowId = $"Container_{containerBrain.GetInstanceID()}";

        if (activeWindows.ContainsKey(windowId))
        {
            var existing = activeWindows[windowId];
            if (existing != null && existing.IsOpen) return existing;
        }

        if (closeOtherWindowsOnOpen) CloseAllContainerWindows();

        UniversalInventoryWindow window = useWorldSpaceContainers
            ? CreateWorldSpaceWindow(windowId, containerBrain.transform)
            : CreateWindow(windowId);

        string containerName = containerBrain.Identity?.GetEntityName() ?? containerBrain.name;
        window.OpenContainer(containerBrain, containerName);

        if (playerBrain != null) OpenPlayerInventory(playerBrain);

        if (debugMode) Debug.Log($"[UniversalWindowManager] Opened container window: {containerName}");
        return window;
    }

    public void CloseContainerWindow(ControllerBrain containerBrain)
    {
        if (containerBrain == null) return;

        string windowId = $"Container_{containerBrain.GetInstanceID()}";
        if (!activeWindows.ContainsKey(windowId)) return;

        var window = activeWindows[windowId];
        if (window != null) { window.Close(); Destroy(window.gameObject); }
        activeWindows.Remove(windowId);

        if (debugMode) Debug.Log($"[UniversalWindowManager] Closed container window: {windowId}");
    }

    public void CloseAllContainerWindows()
    {
        var toRemove = new List<string>();
        foreach (var kvp in activeWindows)
        {
            if (!kvp.Key.StartsWith("Container_")) continue;
            if (kvp.Value != null) { kvp.Value.Close(); Destroy(kvp.Value.gameObject); }
            toRemove.Add(kvp.Key);
        }
        foreach (var key in toRemove) activeWindows.Remove(key);

        if (debugMode && toRemove.Count > 0)
            Debug.Log($"[UniversalWindowManager] Closed {toRemove.Count} container windows");
    }

    #endregion

    #region Equipment Window

    public void OpenEquipmentWindow(ControllerBrain playerBrain)
    {
        if (playerBrain == null) { Debug.LogError("[UniversalWindowManager] Player brain is null!"); return; }
        if (equipmentWindow != null && equipmentWindow.IsOpen) return;

        if (equipmentWindow == null) equipmentWindow = CreateEquipmentWindow();
        if (equipmentWindow == null) { Debug.LogError("[UniversalWindowManager] Failed to create equipment window!"); return; }

        equipmentWindow.Initialize("Equipment", playerBrain, null);
        equipmentWindow.gameObject.SetActive(true);

        if (debugMode) Debug.Log("[UniversalWindowManager] Opened equipment window");
    }

    public void CloseEquipmentWindow()
    {
        if (equipmentWindow != null && equipmentWindow.IsOpen)
        {
            equipmentWindow.OnClose();
            equipmentWindow.gameObject.SetActive(false);

            if (debugMode) Debug.Log("[UniversalWindowManager] Closed equipment window");
        }
    }

    public void ToggleEquipmentWindow(ControllerBrain playerBrain)
    {
        if (equipmentWindow != null && equipmentWindow.IsOpen)
            CloseEquipmentWindow();
        else
            OpenEquipmentWindow(playerBrain);
    }

    private EquipmentWindow CreateEquipmentWindow()
    {
        if (equipmentWindowPrefab == null) { Debug.LogError("[UniversalWindowManager] Equipment window prefab not assigned!"); return null; }

        Transform parent = uiCanvas != null ? uiCanvas.transform : transform;
        GameObject windowObj = Instantiate(equipmentWindowPrefab, parent);
        windowObj.name = "EquipmentWindow";

        var window = windowObj.GetComponent<EquipmentWindow>();
        if (window == null)
        {
            Debug.LogError("[UniversalWindowManager] Equipment window prefab missing EquipmentWindow component!");
            Destroy(windowObj);
            return null;
        }

        windowObj.SetActive(false);
        return window;
    }

    #endregion

    #region Window Factory

    private UniversalInventoryWindow CreateWindow(string windowId)
    {
        if (activeWindows.TryGetValue(windowId, out var cached) && cached != null)
            return cached;

        if (universalWindowPrefab == null) { Debug.LogError("[UniversalWindowManager] Universal window prefab not assigned!"); return null; }

        Transform parent = uiCanvas != null ? uiCanvas.transform : transform;
        GameObject windowObj = Instantiate(universalWindowPrefab, parent);
        windowObj.name = $"Window_{windowId}";

        var window = windowObj.GetComponent<UniversalInventoryWindow>();
        if (window == null)
        {
            Debug.LogError("[UniversalWindowManager] Window prefab missing UniversalInventoryWindow component!");
            Destroy(windowObj);
            return null;
        }

        activeWindows[windowId] = window;
        return window;
    }

    private UniversalInventoryWindow CreateWorldSpaceWindow(string windowId, Transform containerTransform)
    {
        if (activeWindows.TryGetValue(windowId, out var cached) && cached != null)
            return cached;

        GameObject prefabToUse = worldSpaceWindowPrefab != null ? worldSpaceWindowPrefab : universalWindowPrefab;
        if (prefabToUse == null) { Debug.LogError("[UniversalWindowManager] No window prefab assigned!"); return null; }

        GameObject canvasObj = new GameObject($"WorldSpaceCanvas_{windowId}");
        canvasObj.transform.position = containerTransform.position + worldSpaceOffset;

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(600, 400);
        canvasRect.localScale = Vector3.one * 0.01f;

        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        WorldSpaceWindowAdapter adapter = canvasObj.AddComponent<WorldSpaceWindowAdapter>();
        adapter.SetContainer(containerTransform);

        GameObject windowObj = Instantiate(prefabToUse, canvasObj.transform);
        windowObj.name = $"Window_{windowId}";

        var window = windowObj.GetComponent<UniversalInventoryWindow>();
        if (window == null)
        {
            Debug.LogError("[UniversalWindowManager] Window prefab missing UniversalInventoryWindow component!");
            Destroy(canvasObj);
            return null;
        }

        activeWindows[windowId] = window;
        return window;
    }

    #endregion

    #region Cleanup

    public void CloseAllWindows()
    {
        if (playerInventoryWindow != null)
        {
            playerInventoryWindow.Close();
            Destroy(playerInventoryWindow.gameObject);
            playerInventoryWindow = null;
        }

        foreach (var window in activeWindows.Values)
            if (window != null) { window.Close(); Destroy(window.gameObject); }

        activeWindows.Clear();
    }

    #endregion

    #region Queries

    public bool IsAnyWindowOpen()
    {
        if (playerInventoryWindow != null && playerInventoryWindow.IsOpen) return true;
        foreach (var window in activeWindows.Values)
            if (window != null && window.IsOpen) return true;
        return false;
    }

    public int GetOpenWindowCount()
    {
        int count = playerInventoryWindow != null && playerInventoryWindow.IsOpen ? 1 : 0;
        foreach (var window in activeWindows.Values)
            if (window != null && window.IsOpen) count++;
        return count;
    }

    #endregion

    #region Debug

    [ContextMenu("Print Active Windows")]
    private void PrintActiveWindows()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[UniversalWindowManager] Active windows: {activeWindows.Count + 1}");
        if (playerInventoryWindow != null)
            sb.AppendLine($"  - Player Inventory (Open: {playerInventoryWindow.IsOpen})");
        foreach (var kvp in activeWindows)
            sb.AppendLine($"  - {kvp.Key} ({(kvp.Value != null && kvp.Value.IsOpen ? "OPEN" : "CLOSED")})");
        Debug.Log(sb.ToString());
    }

    #endregion
}