using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class UIWindowManager : MonoBehaviour
{
    public static UIWindowManager Instance { get; private set; }

    [Header("Window Prefabs")]
    [SerializeField] private GameObject inventoryWindowPrefab;
    [SerializeField] private GameObject statsWindowPrefab;
    [SerializeField] private GameObject equipmentWindowPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private Dictionary<string, UIWindow> activeWindows = new Dictionary<string, UIWindow>();

    private Canvas UICanvas
    {
        get
        {
            if (UniversalWindowManager.Instance != null)
                return UniversalWindowManager.Instance.UICanvas;

            Debug.LogError("[UIWindowManager] UniversalWindowManager not found — cannot resolve UI Canvas.");
            return null;
        }
    }

    private ControllerBrain PlayerBrain =>
        ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        HandleGlobalInput();
    }

    private void HandleGlobalInput()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.iKey.wasPressedThisFrame)
            ToggleInventoryWindow();

        if (keyboard.cKey.wasPressedThisFrame)
            ToggleStatsWindow();

        if (keyboard.kKey.wasPressedThisFrame)
            ToggleEquipmentWindow();

        if (keyboard.escapeKey.wasPressedThisFrame)
            CloseTopWindow();
    }

    public void ToggleInventoryWindow()
    {
        var brain = PlayerBrain;
        if (UniversalWindowManager.Instance != null && brain != null)
            UniversalWindowManager.Instance.TogglePlayerInventory(brain);
        else
            ToggleWindow("Inventory", inventoryWindowPrefab);
    }

    public void ToggleStatsWindow()
    {
        ToggleWindow("Stats", statsWindowPrefab);
    }

    public void ToggleEquipmentWindow()
    {
        ToggleWindow("Equipment", equipmentWindowPrefab);
    }

    private void ToggleWindow(string windowId, GameObject prefab)
    {
        if (activeWindows.ContainsKey(windowId))
            CloseWindow(windowId);
        else
            OpenWindow(windowId, prefab);
    }

    private void OpenWindow(string windowId, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError($"[UIWindowManager] Window prefab is null for: {windowId}");
            return;
        }

        var canvas = UICanvas;
        if (canvas == null)
            return;

        GameObject windowObj = Instantiate(prefab, canvas.transform);
        UIWindow window = windowObj.GetComponent<UIWindow>();

        if (window == null)
        {
            Debug.LogError($"[UIWindowManager] Window prefab missing UIWindow component: {windowId}");
            Destroy(windowObj);
            return;
        }

        window.Initialize(windowId, PlayerBrain, this);
        activeWindows[windowId] = window;

        if (debugMode)
            Debug.Log($"[UIWindowManager] Opened window: {windowId}");
    }

    public void CloseWindow(string windowId)
    {
        if (!activeWindows.ContainsKey(windowId))
            return;

        UIWindow window = activeWindows[windowId];
        activeWindows.Remove(windowId);

        if (window != null)
        {
            window.OnClose();
            Destroy(window.gameObject);
        }

        if (debugMode)
            Debug.Log($"[UIWindowManager] Closed window: {windowId}");
    }

    private void CloseTopWindow()
    {
        if (activeWindows.Count == 0)
            return;

        UIWindow topWindow = null;
        int highestSiblingIndex = -1;

        foreach (var window in activeWindows.Values)
        {
            int siblingIndex = window.transform.GetSiblingIndex();
            if (siblingIndex > highestSiblingIndex)
            {
                highestSiblingIndex = siblingIndex;
                topWindow = window;
            }
        }

        if (topWindow != null)
            CloseWindow(topWindow.WindowId);
    }

    public void BringWindowToFront(UIWindow window)
    {
        if (window != null)
            window.transform.SetAsLastSibling();
    }

    public bool IsWindowOpen(string windowId) => activeWindows.ContainsKey(windowId);

    public bool IsAnyWindowOpen => activeWindows.Count > 0;

    public ControllerBrain GetPlayerBrain() => PlayerBrain;

    public void CloseAllWindows()
    {
        var windowIds = new List<string>(activeWindows.Keys);
        foreach (var windowId in windowIds)
            CloseWindow(windowId);
    }
}