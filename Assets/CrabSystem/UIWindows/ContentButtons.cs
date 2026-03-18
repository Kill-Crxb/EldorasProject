using UnityEngine;
using UnityEngine.UI;

public class ContentButtons : MonoBehaviour
{
    [Header("Panel References")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject statsPanel;

    [Header("Button References")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button statsButton;

    [Header("Settings")]
    [SerializeField] private bool startWithInventory = true;

    private bool isInitialized = false;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        if (isInitialized) return;

        bool hasErrors = false;

        if (inventoryPanel == null)
        {
            Debug.LogError("[ContentButtons] Inventory Panel not assigned!", this);
            hasErrors = true;
        }

        if (statsPanel == null)
        {
            Debug.LogError("[ContentButtons] Stats Panel not assigned!", this);
            hasErrors = true;
        }

        if (inventoryButton == null)
        {
            Debug.LogError("[ContentButtons] Inventory Button not assigned!", this);
            hasErrors = true;
        }

        if (statsButton == null)
        {
            Debug.LogError("[ContentButtons] Stats Button not assigned!", this);
            hasErrors = true;
        }

        if (hasErrors)
        {
            enabled = false;
            return;
        }

        inventoryButton.onClick.RemoveAllListeners();
        inventoryButton.onClick.AddListener(ShowInventory);

        statsButton.onClick.RemoveAllListeners();
        statsButton.onClick.AddListener(ShowStats);

        if (startWithInventory)
            ShowInventory();
        else
            ShowStats();

        isInitialized = true;
        Debug.Log("[ContentButtons] Initialized successfully");
    }

    public void ShowInventory()
    {
        Debug.Log($"[ContentButtons] ShowInventory called - inventoryPanel={(inventoryPanel != null ? "EXISTS" : "NULL")}, statsPanel={(statsPanel != null ? "EXISTS" : "NULL")}");

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(true);
            Debug.Log($"[ContentButtons] Inventory panel activated, isActive={inventoryPanel.activeSelf}");
        }
        else
        {
            Debug.LogError("[ContentButtons] Inventory panel is NULL!");
        }

        if (statsPanel != null)
        {
            statsPanel.SetActive(false);
            Debug.Log($"[ContentButtons] Stats panel deactivated");
        }
        else
        {
            Debug.LogError("[ContentButtons] Stats panel is NULL!");
        }
    }

    public void ShowStats()
    {
        Debug.Log($"[ContentButtons] ShowStats called - inventoryPanel={(inventoryPanel != null ? "EXISTS" : "NULL")}, statsPanel={(statsPanel != null ? "EXISTS" : "NULL")}");

        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
            Debug.Log($"[ContentButtons] Inventory panel deactivated");
        }
        else
        {
            Debug.LogError("[ContentButtons] Inventory panel is NULL!");
        }

        if (statsPanel != null)
        {
            statsPanel.SetActive(true);
            Debug.Log($"[ContentButtons] Stats panel activated, isActive={statsPanel.activeSelf}");
        }
        else
        {
            Debug.LogError("[ContentButtons] Stats panel is NULL!");
        }
    }

    public void TogglePanels()
    {
        if (inventoryPanel != null && inventoryPanel.activeSelf)
            ShowStats();
        else
            ShowInventory();
    }

    [ContextMenu("Test: Show Inventory")]
    void TestShowInventory()
    {
        ShowInventory();
    }

    [ContextMenu("Test: Show Stats")]
    void TestShowStats()
    {
        ShowStats();
    }
}