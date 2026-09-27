using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The only place UI keys, Escape and the cursor are decided.
/// Owns its own copy of the UI action map so it works before the player spawns,
/// and borrows the player's controls only to suspend gameplay while a panel is open.
/// </summary>
public class UIInputRouter : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Release the cursor while a panel is open.")]
    [SerializeField] private bool controlCursor = true;

    [Tooltip("Disable the Player action map while a panel is open.")]
    [SerializeField] private bool suspendGameplayInput = true;

    public static UIInputRouter Instance { get; private set; }

    /// <summary>True while the UI owns the cursor and keyboard.</summary>
    public static bool InterfaceMode { get; private set; }

    private PlayerInputControls uiControls;
    private ControllerBrain player;

    private void Awake()
    {
        Instance = this;

        uiControls = new PlayerInputControls();
        uiControls.UI.PlayerMenu.performed += HandlePlayerMenu;
        uiControls.UI.ToggleCursor.performed += HandleCancel;
    }

    private void OnEnable() => uiControls.UI.Enable();

    private void OnDisable() => uiControls.UI.Disable();

    private void OnDestroy()
    {
        uiControls.UI.PlayerMenu.performed -= HandlePlayerMenu;
        uiControls.UI.ToggleCursor.performed -= HandleCancel;
        uiControls.Dispose();

        if (Instance == this) Instance = null;
    }

    private void HandlePlayerMenu(InputAction.CallbackContext _)
    {
        var left = UIPanelRegistry.Left;

        if (left == null)
        {
            Debug.LogWarning("[UIInputRouter] No left panel registered");
            return;
        }

        left.Toggle();
    }

    /// <summary>Escape closes the world panel first, then the player panel.</summary>
    private void HandleCancel(InputAction.CallbackContext _)
    {
        var right = UIPanelRegistry.Right;

        if (right != null && right.IsOpen) right.Close();
        else UIPanelRegistry.Left?.Close();
    }

    /// <summary>Called by UIPanel whenever a panel opens or closes.</summary>
    public void ApplyMode()
    {
        bool wanted = UIPanelRegistry.AnyOpen;
        if (wanted == InterfaceMode) return;

        InterfaceMode = wanted;

        if (controlCursor)
        {
            Cursor.lockState = wanted ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = wanted;
        }

        SuspendGameplay(wanted);
    }

    /// <summary>
    /// The player spawns after this object, so the lookup is deferred until first use
    /// and re-resolved if the previous player was destroyed.
    /// </summary>
    private void SuspendGameplay(bool suspend)
    {
        if (!suspendGameplayInput) return;

        if (player == null) player = PlayerBrainAccess.Find();

        var controls = player?.GetInputControls();
        if (controls == null) return;

        if (suspend) controls.Player.Disable();
        else controls.Player.Enable();
    }
}
