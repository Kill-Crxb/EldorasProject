// MenuManager.cs - High-level menu state and input management
using UnityEngine;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
    [Header("Menu References")]
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private GameObject inventoryPanel;

    [Header("Camera Control")]
    [SerializeField] private SimpleThirdPersonCamera playerCamera;

    [Header("Input")]
    [SerializeField] private InputActionReference toggleInventoryAction;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // State
    private bool isMenuOpen = false;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    // Singleton for easy access
    public static MenuManager Instance { get; private set; }

    // Properties
    public bool IsMenuOpen => isMenuOpen;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Start with menu closed
        if (menuCanvas != null)
            menuCanvas.SetActive(false);
    }

    void OnEnable()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.action.Enable();
            toggleInventoryAction.action.performed += OnToggleInventory;
        }
    }

    void OnDisable()
    {
        if (toggleInventoryAction != null)
        {
            toggleInventoryAction.action.performed -= OnToggleInventory;
            toggleInventoryAction.action.Disable();
        }
    }

    private void OnToggleInventory(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (isMenuOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        if (isMenuOpen) return;

        isMenuOpen = true;

        // Show menu
        if (menuCanvas != null)
            menuCanvas.SetActive(true);

        // Show inventory panel by default
        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);

        // Disable camera INPUT (not the component)
        if (playerCamera != null)
            playerCamera.SetInputEnabled(false);

        // Free cursor
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (showDebugInfo)
            Debug.Log("Menu opened - cursor freed, camera input disabled");
    }

    public void CloseMenu()
    {
        if (!isMenuOpen) return;

        isMenuOpen = false;

        // Hide menu
        if (menuCanvas != null)
            menuCanvas.SetActive(false);

        // Re-enable camera input
        if (playerCamera != null)
            playerCamera.SetInputEnabled(true);

        // Restore cursor state
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;

        if (showDebugInfo)
            Debug.Log("Menu closed - cursor locked, camera input enabled");
    }

    // Panel switching methods for future use
    public void ShowInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);
    }

    public void HideInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }

    // ESC to close menu using New Input System
    void Update()
    {
        // Only check ESC when menu is open
        if (isMenuOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }
}