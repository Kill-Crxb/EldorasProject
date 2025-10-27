using UnityEngine;
using UnityEngine.InputSystem;

public class ControllerDebugUI : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugWindow = true;
    [SerializeField] private bool showModuleStates = true;
    [SerializeField] private bool showInputStates = true;
    [SerializeField] private bool showMovementData = true;
    [SerializeField] private bool showMeleeData = true; // CHANGED from showCombatData
    [SerializeField] private string toggleKeyName = "F1"; // String instead of Key enum

    [Header("Window Settings")]
    [SerializeField] private Vector2 windowPosition = new Vector2(10, 10);
    [SerializeField] private Vector2 windowSize = new Vector2(320, 500);

    // References (auto-found) - UPDATED FOR MELEE
    private ThirdPersonController controller;
    private TargetLockModule targetLock;
    private AnimationStateModule animationState;

    private MeleeModule melee; // CHANGED from CombatModule combat
    private ControllerBrain brain;

    // Input tracking
    private Vector2 lastMovementInput;
    private Vector2 lastCameraInput;
    private bool lastSprintInput;

    // Input System
    private PlayerInputControls inputControls;
    private bool isSubscribedToInput;

    void Start()
    {
        FindReferences();
        SetupInputSystem();
    }

    void SetupInputSystem()
    {
        // Try to get input controls from Brain first
        if (brain != null)
        {
            inputControls = brain.GetInputControls();
        }
        else
        {
            // Fallback: create our own input controls
            inputControls = new PlayerInputControls();
        }

        SubscribeToInputs();
    }

    void SubscribeToInputs()
    {
        if (inputControls == null || isSubscribedToInput) return;

        // Try to find an action for the toggle key
        // If your Input Actions don't have a specific debug toggle, we'll use keyboard directly
        try
        {
            // Enable the input controls
            inputControls.Enable();
            isSubscribedToInput = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"ControllerDebugUI: Could not set up input controls: {e.Message}");
        }
    }

    void UnsubscribeFromInputs()
    {
        if (!isSubscribedToInput) return;

        // Only disable if we created our own input controls
        if (brain == null && inputControls != null)
        {
            inputControls.Disable();
        }

        isSubscribedToInput = false;
    }

    void OnDestroy()
    {
        UnsubscribeFromInputs();

        // Only dispose if we created our own
        if (brain == null)
        {
            inputControls?.Dispose();
        }
    }

    void FindReferences()
    {
        // Since this script is on UI Canvas, we need to search globally for the player components

        // First, try to find the Brain directly
        brain = FindFirstObjectByType<ControllerBrain>();

        if (brain != null)
        {
            // Get all modules from the brain
            controller = brain.Controller;
            targetLock = brain.TargetLock;
            animationState = brain.AnimationState;

            melee = brain.GetModule<MeleeModule>(); // CHANGED from CombatModule
        }
        else
        {
            // Fallback: Try to find by Player tag
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                brain = player.GetComponentInChildren<ControllerBrain>();

                if (brain != null)
                {
                    controller = brain.Controller;
                    targetLock = brain.TargetLock;
                    animationState = brain.AnimationState;

                    melee = brain.GetModule<MeleeModule>(); // CHANGED from CombatModule
                }
            }
        }

        // Final fallback: Try to find controller directly if brain not found
        if (controller == null)
        {
            controller = FindFirstObjectByType<ThirdPersonController>();
            if (controller != null)
            {
                brain = controller.GetBrain();
            }
        }

        // Try direct searches for modules if brain method failed
        if (melee == null) // CHANGED from combat
        {
            melee = FindFirstObjectByType<MeleeModule>(); // CHANGED from CombatModule
        }

        if (targetLock == null)
        {
            targetLock = FindFirstObjectByType<TargetLockModule>();
        }

        if (animationState == null)
        {
            animationState = FindFirstObjectByType<AnimationStateModule>();
        }
    }

    void Update()
    {
        // Toggle debug window using new Input System with safer key detection
        if (Keyboard.current != null)
        {
            bool keyPressed = false;

            // Check for F1 key specifically (most reliable approach)
            switch (toggleKeyName.ToUpper())
            {
                case "F1":
                    keyPressed = Keyboard.current.f1Key.wasPressedThisFrame;
                    break;
                case "F2":
                    keyPressed = Keyboard.current.f2Key.wasPressedThisFrame;
                    break;
                case "F3":
                    keyPressed = Keyboard.current.f3Key.wasPressedThisFrame;
                    break;
                case "F4":
                    keyPressed = Keyboard.current.f4Key.wasPressedThisFrame;
                    break;
                case "F5":
                    keyPressed = Keyboard.current.f5Key.wasPressedThisFrame;
                    break;
                case "F6":
                    keyPressed = Keyboard.current.f6Key.wasPressedThisFrame;
                    break;
                case "F7":
                    keyPressed = Keyboard.current.f7Key.wasPressedThisFrame;
                    break;
                case "F8":
                    keyPressed = Keyboard.current.f8Key.wasPressedThisFrame;
                    break;
                case "F9":
                    keyPressed = Keyboard.current.f9Key.wasPressedThisFrame;
                    break;
                case "F10":
                    keyPressed = Keyboard.current.f10Key.wasPressedThisFrame;
                    break;
                case "F11":
                    keyPressed = Keyboard.current.f11Key.wasPressedThisFrame;
                    break;
                case "F12":
                    keyPressed = Keyboard.current.f12Key.wasPressedThisFrame;
                    break;
                case "TAB":
                    keyPressed = Keyboard.current.tabKey.wasPressedThisFrame;
                    break;
                case "BACKQUOTE":
                case "`":
                    keyPressed = Keyboard.current.backquoteKey.wasPressedThisFrame;
                    break;
                default:
                    // Fallback to F1 if unknown key
                    keyPressed = Keyboard.current.f1Key.wasPressedThisFrame;
                    break;
            }

            if (keyPressed)
            {
                showDebugWindow = !showDebugWindow;
            }
        }

        // Track input for display
        if (controller != null)
        {
            lastMovementInput = controller.MovementInput;
            lastSprintInput = controller.IsSprinting;
        }
    }

    void OnGUI()
    {
        if (!showDebugWindow || controller == null) return;

        try
        {
            // Create debug window
            GUILayout.BeginArea(new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y));

            // Window background
            GUI.Box(new Rect(0, 0, windowSize.x, windowSize.y), "");

            GUILayout.BeginVertical();

            // Header - use built-in style to avoid null reference
            var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("=== CONTROLLER DEBUG ===", headerStyle);
            GUILayout.Space(5);

            // Core Controller State
            DrawControllerState();

            if (showModuleStates)
            {
                GUILayout.Space(10);
                DrawModuleStates();
            }

            if (showInputStates)
            {
                GUILayout.Space(10);
                DrawInputStates();
            }

            if (showMovementData)
            {
                GUILayout.Space(10);
                DrawMovementData();
            }

            if (showMeleeData) // CHANGED from showCombatData
            {
                GUILayout.Space(10);
                DrawMeleeData(); // CHANGED from DrawCombatData
            }

            // Melee Testing Buttons - CHANGED from Combat
            if (melee != null)
            {
                GUILayout.Space(10);
                DrawMeleeTestButtons(); // CHANGED from DrawCombatTestButtons
            }

            // Footer
            GUILayout.Space(10);
            GUILayout.Label($"Press {toggleKeyName} to toggle", GUI.skin.label);

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ControllerDebugUI OnGUI Error: {e.Message}");
        }
    }

    void DrawControllerState()
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("CORE CONTROLLER:", headerStyle);

        // Movement state
        string movementState = "Idle";
        if (controller.IsSprinting) movementState = "Sprinting";
        else if (controller.CurrentVelocity.magnitude > controller.WalkSpeed + 0.1f) movementState = "Running";
        else if (controller.CurrentVelocity.magnitude > 0.1f) movementState = "Walking";

        GUILayout.Label($"State: {movementState}");
        GUILayout.Label($"Can Move: {controller.CanMove()}");
        GUILayout.Label($"Can Act: {controller.CanAct()}");
        GUILayout.Label($"Can Sprint: {controller.CanSprint()}");

        // Stamina
        GUILayout.Label($"Stamina: {controller.CurrentStamina:F1}/{controller.MaxStamina:F0}");

        // Target lock
        GUILayout.Label($"Target Lock: {(controller.IsLockedOn ? "LOCKED" : "FREE")}");
        if (controller.IsLockedOn && controller.LockedTarget != null)
        {
            GUILayout.Label($"Target: {controller.LockedTarget.name}");
        }
    }

    void DrawModuleStates()
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("MODULES:", headerStyle);

        // Animation State
        if (animationState != null)
        {
            GUILayout.Label($"Animation: {animationState.CurrentState}");
        }

        // Target Lock Details
        if (targetLock != null)
        {
            GUILayout.Label($"Lock Range: {targetLock.LockOnRange:F1}m");
            if (targetLock.IsLockedOn && targetLock.LockedTarget != null)
            {
                float distance = Vector3.Distance(controller.transform.position, targetLock.LockedTarget.position);
                GUILayout.Label($"Target Distance: {distance:F1}m");
            }
        }


        // Brain info
        if (brain != null)
        {
            // Count modules by checking the brain's children
            int moduleCount = 0;
            for (int i = 0; i < brain.transform.childCount; i++)
            {
                var child = brain.transform.GetChild(i);
                if (child.name.StartsWith("Component_"))
                {
                    moduleCount += child.GetComponents<IPlayerModule>().Length;
                }
            }
            GUILayout.Label($"Brain Modules: {moduleCount}");
        }
    }

    void DrawInputStates()
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("INPUT:", headerStyle);

        GUILayout.Label($"Movement: ({lastMovementInput.x:F2}, {lastMovementInput.y:F2})");
        GUILayout.Label($"Sprint: {(lastSprintInput ? "HELD" : "RELEASED")}");

        // Show input magnitude
        float inputMagnitude = lastMovementInput.magnitude;
        GUILayout.Label($"Input Strength: {inputMagnitude:F2}");

        // Input direction
        if (inputMagnitude > 0.1f)
        {
            string direction = "Forward";
            if (lastMovementInput.y < -0.5f) direction = "Backward";
            else if (lastMovementInput.x > 0.5f) direction = "Right";
            else if (lastMovementInput.x < -0.5f) direction = "Left";
            else if (lastMovementInput.y > 0.5f && Mathf.Abs(lastMovementInput.x) > 0.3f)
            {
                direction = lastMovementInput.x > 0 ? "Forward-Right" : "Forward-Left";
            }

            GUILayout.Label($"Direction: {direction}");
        }
    }

    void DrawMovementData()
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("MOVEMENT DATA:", headerStyle);

        Vector3 velocity = controller.CurrentVelocity;
        GUILayout.Label($"Velocity: {velocity.magnitude:F2} m/s");
        GUILayout.Label($"Speed: ({velocity.x:F1}, {velocity.z:F1})");

        // Speed compared to max speeds
        GUILayout.Label($"Walk Speed: {controller.WalkSpeed:F1}");
        GUILayout.Label($"Run Speed: {controller.RunSpeed:F1}");
        GUILayout.Label($"Sprint Speed: {controller.SprintSpeed:F1}");

        // Current speed percentage
        float speedPercent = (velocity.magnitude / controller.SprintSpeed) * 100f;
        GUILayout.Label($"Speed %: {speedPercent:F0}%");
    }

    void DrawMeleeData() // CHANGED method name from DrawCombatData
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("MELEE DATA:", headerStyle); // CHANGED header

        // Debug info to help troubleshoot
        if (brain == null)
        {
            GUILayout.Label("Brain: NOT FOUND");
            // Try to find it again
            brain = FindFirstObjectByType<ControllerBrain>();
            if (brain != null)
            {
                GUILayout.Label("Brain: Found on retry!");
                melee = brain.GetModule<MeleeModule>(); // CHANGED from CombatModule
            }
        }
        else
        {
            GUILayout.Label($"Brain: Found ({brain.name})");
        }

        if (melee == null) // CHANGED from combat
        {
            GUILayout.Label("Melee Module: NOT FOUND"); // UPDATED message

            // Try direct search as fallback
            melee = FindFirstObjectByType<MeleeModule>(); // CHANGED from CombatModule

            if (melee != null)
            {
                GUILayout.Label("Melee Module: Found via direct search!"); // UPDATED message
            }
            else
            {
                GUILayout.Label("Melee Module: Still not found"); // UPDATED message

                // Show what we can find for debugging
                var allMeleeModules = FindObjectsByType<MeleeModule>(FindObjectsSortMode.None); // CHANGED
                GUILayout.Label($"Total MeleeModules in scene: {allMeleeModules.Length}"); // UPDATED message

                if (brain != null)
                {
                    GUILayout.Label("Brain children:");
                    for (int i = 0; i < brain.transform.childCount; i++)
                    {
                        var child = brain.transform.GetChild(i);
                        GUILayout.Label($"- {child.name}");
                    }
                }
                return;
            }
        }

        // Melee States - UPDATED for MeleeModule API
        GUILayout.Label($"Is Attacking: {(melee.IsAttacking ? "YES" : "NO")}");
        GUILayout.Label($"Is Blocking: {(melee.IsBlocking ? "YES" : "NO")}");

        // Combo System
        GUILayout.Label($"Combo Count: {melee.CurrentComboCount}");
        GUILayout.Label($"Can Cancel: {(melee.CanCancelAttack ? "YES" : "NO")}");

        // Melee Abilities - UPDATED method names
        GUILayout.Label($"Can Attack: {(melee.CanAttack() ? "YES" : "NO")}");
        GUILayout.Label($"Can Heavy: {(melee.CanHeavyAttack() ? "YES" : "NO")}");
        GUILayout.Label($"Can Block: {(melee.CanBlock() ? "YES" : "NO")}");

        // Overall State - UPDATED property name (not method)
        GUILayout.Label($"Melee Busy: {(melee.IsBusyWithMelee ? "YES" : "NO")}");
    }

    void DrawMeleeTestButtons() // CHANGED method name from DrawCombatTestButtons
    {
        var headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        GUILayout.Label("MELEE TESTING:", headerStyle); // CHANGED header

        GUILayout.BeginHorizontal();

        // Action buttons - UPDATED to use melee instead of combat
        if (GUILayout.Button("Light Attack") && melee.CanAttack())
        {
            melee.StartLightAttack();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Heavy Attack") && melee.CanHeavyAttack())
        {
            melee.StartHeavyAttack();
        }

        if (GUILayout.Button(melee.IsBlocking ? "Stop Block" : "Start Block"))
        {
            if (melee.IsBlocking)
                melee.StopBlock();
            else if (melee.CanBlock())
                melee.StartBlock();
        }

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Force End All"))
        {
            melee.ForceEndCurrentAction();
        }

        GUILayout.EndHorizontal();

        // Quick stamina controls for testing
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Stamina"))
        {
            controller.AddStamina(25f);
        }

        if (GUILayout.Button("Drain Stamina"))
        {
            controller.ConsumeStamina(25f);
        }

        GUILayout.EndHorizontal();
    }

    // Public methods to toggle sections - UPDATED
    public void ToggleModuleStates() => showModuleStates = !showModuleStates;
    public void ToggleInputStates() => showInputStates = !showInputStates;
    public void ToggleMovementData() => showMovementData = !showMovementData;
    public void ToggleMeleeData() => showMeleeData = !showMeleeData; // CHANGED from ToggleCombatData
    public void ToggleDebugWindow() => showDebugWindow = !showDebugWindow;
}