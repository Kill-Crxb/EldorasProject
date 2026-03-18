using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Input Mode - Determines how InputSystem interprets control
/// </summary>
public enum InputMode
{
    Player,     // Read Unity Input System (keyboard/gamepad)
    AI,         // Read AI decisions (GOAP, pathfinding)
    Network,    // Read network packets (multiplayer)
    Admin,      // Read scripted commands (possession/testing)
    Test        // Automated test sequences
}

/// <summary>
/// Universal Input System - Central control source for all entities
/// 
/// This system serves THREE roles:
/// 1. Raw input state provider (IInputProvider)
/// 2. Movement control source (IMovementControlSource)
/// 3. Ability control source (IAbilityControlSource)
/// 
/// Architecture:
/// - ONE InputSystem per entity
/// - Mode switching: Player/AI/Network/Admin/Test
/// - Systems poll InputSystem for input
/// - Enables possession, admin tools, multiplayer
/// 
/// Pattern: Universal control source that works for players, NPCs, and possessed entities
/// </summary>
public class InputSystem : MonoBehaviour,
    IBrainModule,
    IInputProvider,
    IMovementControlSource,
    IAbilityControlSource
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Control Mode")]
    [Tooltip("Current input mode - determines who/what controls this entity")]
    [SerializeField] private InputMode currentMode = InputMode.Player;

    [Tooltip("Transform player input to camera space (Player mode only)")]
    [SerializeField] private bool cameraRelativeMovement = true;

    [Header("Optional Dependencies")]
    [Tooltip("Camera provider for camera-relative movement (auto-discovered)")]
    [SerializeField] private MonoBehaviour cameraProviderComponent;

    [Tooltip("Target lock module for lock-on look direction (auto-discovered)")]
    [SerializeField] private TargetLockModule targetLock;

    [Tooltip("Pathfinding module for AI movement (auto-discovered)")]
    [SerializeField] private PathfindingModule pathfinding;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // ========================================
    // References
    // ========================================

    private ControllerBrain brain;
    private PlayerInputControls inputActions;
    private ICameraProvider cameraProvider;

    // AI ability queueing
    private string aiRequestedAbility;

    // ========================================
    // Input State (IInputProvider)
    // ========================================

    // Movement
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool DashPressed { get; private set; }

    // Combat
    public bool LightAttackPressed { get; private set; }
    public bool HeavyAttackPressed { get; private set; }
    public bool BlockHeld { get; private set; }
    public bool ParryPressed { get; private set; }

    // Ability Quickslots
    public bool AbilityQPressed { get; private set; }
    public bool AbilityZPressed { get; private set; }
    public bool AbilityXPressed { get; private set; }
    public bool AbilityCPressed { get; private set; }
    public bool AbilityVPressed { get; private set; }

    // Hotbar (1-9)
    public bool Hotbar1Pressed { get; private set; }
    public bool Hotbar2Pressed { get; private set; }
    public bool Hotbar3Pressed { get; private set; }
    public bool Hotbar4Pressed { get; private set; }
    public bool Hotbar5Pressed { get; private set; }
    public bool Hotbar6Pressed { get; private set; }
    public bool Hotbar7Pressed { get; private set; }
    public bool Hotbar8Pressed { get; private set; }
    public bool Hotbar9Pressed { get; private set; }

    // Interaction
    public bool InteractPressed { get; private set; }

    // ========================================
    // Properties
    // ========================================

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public InputMode CurrentMode => currentMode;

    // IMovementControlSource + IAbilityControlSource
    public bool IsActive =>
        isEnabled &&
        (currentMode == InputMode.Player || currentMode == InputMode.AI);

    public string SourceName => $"InputSystem ({currentMode})";

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        inputActions = brain.GetInputControls();

        if (inputActions == null)
        {
            Debug.LogError($"[InputSystem] PlayerInputControls is NULL on {brain.name}! " +
                          $"Entity type: {brain.EntityType}. " +
                          $"InputSystem will not work without PlayerInputControls.");

            if (brain.IsPlayer)
            {
                Debug.LogError($"[InputSystem] This IS a Player entity but has no PlayerInputControls! " +
                              $"ControllerBrain.InitializeInputSystem() may have failed.");
            }
            else
            {
                Debug.LogWarning($"[InputSystem] This is NOT a Player entity ({brain.EntityType}). " +
                                $"Consider setting InputMode to AI instead of Player.");
            }
        }

        SetupDependencies();
    }

    public void UpdateModule()
    {
        if (!IsEnabled)
        {
            if (showDebugInfo && Time.frameCount % 60 == 0)
                Debug.LogWarning("[InputSystem] UpdateModule called but IsEnabled = false!");
            return;
        }

        switch (currentMode)
        {
            case InputMode.Player:
                ReadPlayerInput();
                break;

            case InputMode.AI:
                ClearPlayerInput();
                break;

            case InputMode.Network:
                ClearPlayerInput();
                break;

            case InputMode.Admin:
                ClearPlayerInput();
                break;

            case InputMode.Test:
                ClearPlayerInput();
                break;
        }
    }

    // ========================================
    // Dependency Setup
    // ========================================

    private void SetupDependencies()
    {
        // Camera provider (for camera-relative movement)
        if (cameraProviderComponent != null && cameraProviderComponent is ICameraProvider)
        {
            cameraProvider = cameraProviderComponent as ICameraProvider;
        }
        else
        {
            cameraProvider = brain.GetModuleImplementing<ICameraProvider>();
        }

        // Target lock (for lock-on look direction)
        if (targetLock == null)
        {
            targetLock = brain.GetModule<TargetLockModule>();
        }

        // Pathfinding (for AI movement)
        if (pathfinding == null)
        {
            pathfinding = brain.GetModule<PathfindingModule>();
        }
    }

    // ========================================
    // IMovementControlSource Implementation
    // ========================================

    public MovementInput GetMovementInput()
    {
        switch (currentMode)
        {
            case InputMode.Player:
                return GetPlayerMovementInput();

            case InputMode.AI:
                return GetAIMovementInput();

            case InputMode.Admin:
                return GetAdminMovementInput();

            default:
                return MovementInput.Zero;
        }
    }

    public void OnActivated()
    {
        if (showDebugInfo)
            Debug.Log($"[InputSystem] Activated in {currentMode} mode");
    }

    public void OnDeactivated()
    {
        if (showDebugInfo)
            Debug.Log($"[InputSystem] Deactivated");
    }

    public void UpdateSource()
    {
        // We must guarantee fresh input is available
        if (!IsEnabled) return;

        // Read input NOW (not relying on UpdateModule order)
        switch (currentMode)
        {
            case InputMode.Player:
                ReadPlayerInput();
                break;

            case InputMode.AI:
                ClearPlayerInput();
                break;

            case InputMode.Network:
                ClearPlayerInput();
                break;

            case InputMode.Admin:
                ClearPlayerInput();
                break;

            case InputMode.Test:
                ClearPlayerInput();
                break;
        }
    }

    // ========================================
    // IAbilityControlSource Implementation
    // ========================================

    public string GetAbilitySlotToTrigger()
    {
        switch (currentMode)
        {
            case InputMode.Player:
                return GetPlayerAbilityInput();

            case InputMode.AI:
                return GetAIAbilityInput();

            case InputMode.Admin:
                return GetAdminAbilityInput();

            default:
                return null;
        }
    }

    // ========================================
    // Mode Switching
    // ========================================

    /// <summary>
    /// Switch input mode at runtime
    /// Example: inputSystem.SetMode(InputMode.Admin) for possession
    /// </summary>
    public void SetMode(InputMode mode)
    {
        if (currentMode == mode) return;

        InputMode oldMode = currentMode;
        currentMode = mode;

        if (showDebugInfo)
            Debug.Log($"[InputSystem] Mode changed: {oldMode} → {currentMode}");
    }

    /// <summary>
    /// Get current input mode
    /// </summary>
    public InputMode GetMode()
    {
        return currentMode;
    }

    // ========================================
    // AI Control API
    // ========================================

    /// <summary>
    /// Request an ability to be triggered (AI mode)
    /// Called by GOAP goals or combat behaviors
    /// </summary>
    public void RequestAbility(string slotKey)
    {
        if (currentMode != InputMode.AI)
        {
            Debug.LogWarning($"[InputSystem] RequestAbility() called but mode is {currentMode}, not AI");
            return;
        }

        aiRequestedAbility = slotKey;

        if (showDebugInfo)
            Debug.Log($"[InputSystem] AI requested ability: {slotKey}");
    }

    /// <summary>
    /// Check if an ability request is pending
    /// </summary>
    public bool HasPendingAbilityRequest()
    {
        return !string.IsNullOrEmpty(aiRequestedAbility);
    }

    /// <summary>
    /// Clear any pending ability request
    /// </summary>
    public void ClearAbilityRequest()
    {
        aiRequestedAbility = null;
    }

    // ========================================
    // Player Input Reading
    // ========================================

    private void ReadPlayerInput()
    {
        ReadMovementInput();
        ReadCombatInput();
        ReadAbilityInput();
        ReadHotbarInput();
        ReadInteractionInput();
    }

    private void ReadMovementInput()
    {
        if (inputActions == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[InputSystem] Cannot read movement input - PlayerInputControls is null!");
            return;
        }

        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        LookInput = inputActions.Player.Look.ReadValue<Vector2>();

        JumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
        JumpHeld = inputActions.Player.Jump.IsPressed();

        // Simple hold-to-sprint (no toggle complexity)
        SprintHeld = inputActions.Player.Sprint.IsPressed();

        DashPressed = false; // TODO: Add Dash to PlayerInputControls
    }

    private void ReadCombatInput()
    {
        LightAttackPressed = inputActions.Player.Attack.WasPressedThisFrame();
        HeavyAttackPressed = false; // TODO: Add HeavyAttack to PlayerInputControls

        BlockHeld = inputActions.Player.Block.IsPressed();
        ParryPressed = false; // TODO: Add Parry to PlayerInputControls
    }

    private void ReadAbilityInput()
    {
        if (inputActions == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("[InputSystem] Cannot read ability input - PlayerInputControls is null!");
            return;
        }

        AbilityQPressed = inputActions.Player.QuickslotQ.WasPressedThisFrame();
        AbilityZPressed = inputActions.Player.QuickslotZ.WasPressedThisFrame();
        AbilityXPressed = inputActions.Player.QuickslotX.WasPressedThisFrame();
        AbilityCPressed = inputActions.Player.QuickslotC.WasPressedThisFrame();
        AbilityVPressed = inputActions.Player.QuickslotV.WasPressedThisFrame();
    }

    private void ReadHotbarInput()
    {
        Hotbar1Pressed = inputActions.Player.Hotbar1.WasPressedThisFrame();
        Hotbar2Pressed = inputActions.Player.Hotbar2.WasPressedThisFrame();
        Hotbar3Pressed = inputActions.Player.Hotbar3.WasPressedThisFrame();
        Hotbar4Pressed = inputActions.Player.Hotbar4.WasPressedThisFrame();
        Hotbar5Pressed = inputActions.Player.Hotbar5.WasPressedThisFrame();
        Hotbar6Pressed = inputActions.Player.Hotbar6.WasPressedThisFrame();
        Hotbar7Pressed = inputActions.Player.Hotbar7.WasPressedThisFrame();
        Hotbar8Pressed = inputActions.Player.Hotbar8.WasPressedThisFrame();
        Hotbar9Pressed = inputActions.Player.Hotbar9.WasPressedThisFrame();
    }

    private void ReadInteractionInput()
    {
        // Use New Input System API
        // TODO: Add "Interact" action to PlayerInputControls.inputactions
        InteractPressed = Keyboard.current != null && Keyboard.current[Key.E].wasPressedThisFrame;
    }

    private void ClearPlayerInput()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpPressed = false;
        JumpHeld = false;
        SprintHeld = false;
        DashPressed = false;

        LightAttackPressed = false;
        HeavyAttackPressed = false;
        BlockHeld = false;
        ParryPressed = false;

        AbilityQPressed = false;
        AbilityZPressed = false;
        AbilityXPressed = false;
        AbilityCPressed = false;
        AbilityVPressed = false;

        Hotbar1Pressed = false;
        Hotbar2Pressed = false;
        Hotbar3Pressed = false;
        Hotbar4Pressed = false;
        Hotbar5Pressed = false;
        Hotbar6Pressed = false;
        Hotbar7Pressed = false;
        Hotbar8Pressed = false;
        Hotbar9Pressed = false;

        InteractPressed = false;
    }

    // ========================================
    // Player Mode - Movement Input
    // ========================================

    private MovementInput GetPlayerMovementInput()
    {
        Vector2 rawInput = MoveInput;

        // Transform to camera space if enabled
        Vector2 moveDirection = cameraRelativeMovement
            ? TransformInputToCameraSpace(rawInput)
            : rawInput;

        // Calculate look direction
        Vector2 lookDirection = CalculateLookDirection(moveDirection);

        return new MovementInput
        {
            MoveDirection = moveDirection,
            LookDirection = lookDirection,
            Sprint = SprintHeld,
            Jump = JumpPressed,
            Dash = DashPressed
        };
    }

    private Vector2 TransformInputToCameraSpace(Vector2 rawInput)
    {
        if (rawInput.magnitude < 0.01f)
            return Vector2.zero;

        Transform cameraTransform = cameraProvider?.CameraTransform;

        // Fallback to Camera.main
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
                return rawInput;

            cameraTransform = mainCam.transform;
        }

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 moveDirection = cameraForward * rawInput.y + cameraRight * rawInput.x;

        return new Vector2(moveDirection.x, moveDirection.z);
    }

    private Vector2 CalculateLookDirection(Vector2 moveDirection)
    {
        // Lock-on: look at target
        if (targetLock != null && targetLock.IsLockedOn)
        {
            Vector3 directionToTarget = targetLock.LockedTarget.position - brain.transform.position;
            directionToTarget.y = 0f;

            if (directionToTarget.magnitude > 0.1f)
            {
                directionToTarget.Normalize();
                return new Vector2(directionToTarget.x, directionToTarget.z);
            }
        }

        // Free movement: look in movement direction
        if (moveDirection.magnitude > 0.1f)
            return moveDirection;

        return Vector2.zero;
    }

    // ========================================
    // AI Mode - Movement Input
    // ========================================

    private MovementInput GetAIMovementInput()
    {
        if (pathfinding == null || !pathfinding.HasPath)
            return MovementInput.Zero;

        // Get direction to next path point
        Vector3 nextPosition = pathfinding.GetNextPathPosition();
        Vector3 directionToNext = nextPosition - brain.transform.position;
        directionToNext.y = 0f;

        if (directionToNext.magnitude < 0.1f)
            return MovementInput.Zero;

        directionToNext.Normalize();
        Vector2 moveDirection = new Vector2(directionToNext.x, directionToNext.z);

        return new MovementInput
        {
            MoveDirection = moveDirection,
            LookDirection = moveDirection,
            Sprint = false, // TODO: AI sprint logic
            Jump = false,   // TODO: AI jump logic
            Dash = false
        };
    }

    // ========================================
    // Admin Mode - Movement Input
    // ========================================

    private MovementInput GetAdminMovementInput()
    {
        // TODO: Implement admin scripted movement
        return MovementInput.Zero;
    }

    // ========================================
    // Player Mode - Ability Input
    // ========================================

    private string GetPlayerAbilityInput()
    {
        // Check basic attack
        if (LightAttackPressed)
            return "BasicAttack";

        // Check quickslots
        if (AbilityQPressed) return "Q";
        if (AbilityZPressed) return "Z";
        if (AbilityXPressed) return "X";
        if (AbilityCPressed) return "C";
        if (AbilityVPressed) return "V";

        return null;
    }

    // ========================================
    // AI Mode - Ability Input
    // ========================================

    private string GetAIAbilityInput()
    {
        // Return and clear queued ability
        string slot = aiRequestedAbility;
        aiRequestedAbility = null;
        return slot;
    }

    // ========================================
    // Admin Mode - Ability Input
    // ========================================

    private string GetAdminAbilityInput()
    {
        // TODO: Implement admin scripted abilities
        return null;
    }

    // ========================================
    // Debug Visualization
    // ========================================

    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label("=== INPUT SYSTEM ===");
        GUILayout.Label($"Mode: {currentMode}");
        GUILayout.Label($"Active: {IsActive}");
        GUILayout.Label($"Move: {MoveInput}");
        GUILayout.Label($"Sprint: {SprintHeld}");
        GUILayout.Label($"Jump: {JumpPressed}");
        GUILayout.EndArea();
    }

    // No OnDestroy needed - Brain owns and cleans up PlayerInputControls
}