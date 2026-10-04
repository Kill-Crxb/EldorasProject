using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Which set of physical keys a bar can claim.
///
/// Sets are validated by the physical keys they claim, not by enum equality, because two
/// different sets can want the same key — QuickslotQ and ShiftCtrlQ both want Q. The first bar
/// listed in keybindRoutes keeps the key and the later one is demoted to None with a warning.
/// </summary>
public enum HotbarKeybindSet
{
    None,           // Bar has no keybinds (mouse / drag only)
    ZXCV,           // Z=slot0  X=slot1  C=slot2  V=slot3
    Hotbar1234,     // 1=slot0  2=slot1  3=slot2  4=slot3
    Hotbar5678,     // 5=slot0  6=slot1  7=slot2  8=slot3
    Hotbar9,        // 9=slot0  (single-key utility bar)
    QuickslotQ,     // Q=slot0  (ranged slot — mirrors equipped ranged inventory)
    MouseLR,        // LMB=slot0  RMB=slot1   (the horizontal focus bar)
    ShiftCtrlQ,     // Shift=slot0  Ctrl=slot1  Q=slot2   (the vertical focus bar)
}

/// <summary>
/// One bar's claim on a set of physical keys. heldTime is the per-slot charge timer and is
/// deliberately not serialised — it is runtime state that belongs with the route it measures,
/// which is what keeps the bridge from needing a per-bar array field.
/// </summary>
[Serializable]
public class BarKeybindRoute
{
    public string barId = "";
    public HotbarKeybindSet keybinds = HotbarKeybindSet.None;

    [NonSerialized] public float[] heldTime = new float[12];

    public BarKeybindRoute() { }

    public BarKeybindRoute(string barId, HotbarKeybindSet keybinds)
    {
        this.barId = barId;
        this.keybinds = keybinds;
    }
}

/// <summary>
/// Serialised form of the keybind routing config. Stored as "inputProfile" in the character save.
///
/// v1 stored three enum values, one per named bar. v2 stores a list of routes, so a bar added
/// later carries its own binding. The v1 fields are kept for migration and never written.
/// </summary>
[Serializable]
public class InputProfileSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public List<BarKeybindRoute> routes = new List<BarKeybindRoute>();

    // ── v1 legacy — read on load, never written ───────────────────────────
    public HotbarKeybindSet centreBarKeybinds;
    public HotbarKeybindSet bottomLeftKeybinds;
    public HotbarKeybindSet bottomRightKeybinds;
}

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
/// Universal Input System - Central control source for all entities.
///
/// Roles:
///   1. Raw input state provider     (IInputProvider)
///   2. Movement control source      (IMovementControlSource)
///   3. Ability control source       (IAbilityControlSource)
///   4. Keybind routing config owner (ISaveable → "inputProfile")
///
/// Keybind routing:
///   Each bar on HotbarSystem can claim one HotbarKeybindSet, listed in keybindRoutes.
///   Assignment is inspector-configurable and persisted per character via ISaveable.
///   Routes are validated by physical key, first listed wins, loser demoted to None.
///
///   All player ability input reaches abilities through BridgeBarInput → HotbarSystem.
///   AbilityLoadoutModule receives no player input.
/// </summary>
public class InputSystem : MonoBehaviour,
    IBrainModule,
    IInputProvider,
    IMovementControlSource,
    IAbilityControlSource,
    ISaveable
{
    public int InitOrder => 30;
    public bool PlayerOnly => true;

    // Inspector

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Control Mode")]
    [Tooltip("Current input mode - determines who/what controls this entity")]
    [SerializeField] private InputMode currentMode = InputMode.Player;

    [Tooltip("Transform player input to camera space (Player mode only)")]
    [SerializeField] private bool cameraRelativeMovement = true;

    [Header("Debug")]
    [Tooltip("Draws the live input state on screen. Nothing is written to the console.")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("Keybind Routing")]
    [Tooltip("Which keybind set each bar responds to. barId must match a HotbarSystem bar " +
             "definition. Order matters: on a key clash the bar listed first keeps the key.")]
    [SerializeField] private List<BarKeybindRoute> keybindRoutes = new List<BarKeybindRoute>
    {
        new BarKeybindRoute("modifier", HotbarKeybindSet.ShiftCtrlQ),
        new BarKeybindRoute("mouse", HotbarKeybindSet.MouseLR),
        new BarKeybindRoute("centre", HotbarKeybindSet.ZXCV),
        new BarKeybindRoute("bottomLeft", HotbarKeybindSet.Hotbar1234),
        new BarKeybindRoute("bottomRight", HotbarKeybindSet.None),
    };

    [Header("Optional Dependencies")]
    [Tooltip("Camera provider for camera-relative movement (auto-discovered)")]
    [SerializeField] private MonoBehaviour cameraProviderComponent;

    [Header("AI Control Source")]
    [Tooltip("Control source for NPCs without AI behavior (stub that returns zero input)")]
    [SerializeField] private StubAIControlSource stubAIControlSource;

    // References

    private ControllerBrain brain;
    private PlayerInputControls inputActions;
    private ICameraProvider cameraProvider;
    private HotbarSystem hotbarSystem;

    private string aiRequestedAbility;

    // Input State (IInputProvider)

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool CrouchHeld { get; private set; }
    public bool GaitTogglePressed { get; private set; }
    public bool DashPressed { get; private set; }

    public bool LightAttackPressed { get; private set; }
    public bool HeavyAttackPressed { get; private set; }
    public bool BlockHeld { get; private set; }
    public bool ParryPressed { get; private set; }
    public bool ToggleStancePressed { get; private set; }

    // Raw quickslot state — exposed for IInputProvider consumers and AI.
    // In Player mode ZXCV route to a hotbar bar via BridgeBarInput,
    // not to AbilityLoadoutModule.
    public bool AbilityQPressed { get; private set; }
    public bool AbilityZPressed { get; private set; }
    public bool AbilityXPressed { get; private set; }
    public bool AbilityCPressed { get; private set; }
    public bool AbilityVPressed { get; private set; }

    // Focus bar edges. The held side is read straight off the action in GetIsPressed — press and
    // hold stay separate fields the whole way down, per Movement_Ability_Interface.md.
    public bool AbilityShiftPressed { get; private set; }
    public bool AbilityCtrlPressed { get; private set; }
    public bool AbilityBlockPressed { get; private set; }

    public bool Hotbar1Pressed { get; private set; }
    public bool Hotbar2Pressed { get; private set; }
    public bool Hotbar3Pressed { get; private set; }
    public bool Hotbar4Pressed { get; private set; }
    public bool Hotbar5Pressed { get; private set; }
    public bool Hotbar6Pressed { get; private set; }
    public bool Hotbar7Pressed { get; private set; }
    public bool Hotbar8Pressed { get; private set; }
    public bool Hotbar9Pressed { get; private set; }

    public bool InteractPressed { get; private set; }

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;
    public InputMode CurrentMode => currentMode;
    public bool IsActive =>
        isEnabled &&
        (currentMode == InputMode.Player || currentMode == InputMode.AI);
    public string SourceName => $"InputSystem ({currentMode})";

    // IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        inputActions = brain.GetInputControls();

        if (inputActions == null && brain.IsPlayer)
        {
            Debug.LogError($"[InputSystem] PlayerInputControls is NULL on {brain.EntityName}! " +
                           $"This IS a Player entity but has no PlayerInputControls!");
        }
        // Non-player entities (NPC, Entity, etc.) never get PlayerInputControls —
        // that's expected, since they run in InputMode.AI below. No log needed.

        if (brain.IsNPC)
        {
            currentMode = InputMode.AI;

            // Auto-discover stub AI control source if not assigned
            if (stubAIControlSource == null)
            {
                stubAIControlSource = GetComponentInChildren<StubAIControlSource>();
            }

            if (stubAIControlSource == null)
            {
                Debug.LogWarning($"[InputSystem] No StubAIControlSource found on NPC {brain.EntityName}. Create a child GameObject with StubAIControlSource component.");
            }
        }

        SetupDependencies();
        ValidateKeybindRouting();
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        switch (currentMode)
        {
            case InputMode.Player: ReadPlayerInput(); break;
            default: ClearPlayerInput(); break;
        }
    }

    // ISaveable  —  "inputProfile"

    public string GetSaveId() => "inputProfile";
    public int GetSaveVersion() => InputProfileSaveData.CurrentVersion;

    public string GetSaveData()
    {
        var data = new InputProfileSaveData
        {
            version = InputProfileSaveData.CurrentVersion,
            routes = keybindRoutes,
        };
        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<InputProfileSaveData>(json);
        if (data == null) return;

        if (data.routes != null && data.routes.Count > 0)
            ApplySavedRoutes(data.routes);
        else
            MigrateLegacyRoutes(data);

        ValidateKeybindRouting();
    }

    /// <summary>
    /// A saved route only overwrites a bar this entity still has. Bars the save does not mention
    /// keep whatever the prefab authored, which is how a bar added since the save gets its keys.
    /// </summary>
    private void ApplySavedRoutes(List<BarKeybindRoute> saved)
    {
        foreach (var entry in saved)
        {
            if (entry == null) continue;

            var route = FindRoute(entry.barId);
            if (route == null) continue;

            route.keybinds = entry.keybinds;
        }
    }

    private void MigrateLegacyRoutes(InputProfileSaveData data)
    {
        SetRouteFromLegacy("centre", data.centreBarKeybinds);
        SetRouteFromLegacy("bottomLeft", data.bottomLeftKeybinds);
        SetRouteFromLegacy("bottomRight", data.bottomRightKeybinds);
    }

    /// <summary>
    /// v1 saves can hand a bar QuickslotQ, which the modifier bar now claims. Left alone, that bar
    /// would win the key by list order and take the whole ShiftCtrlQ set down with it, so Q is
    /// dropped at migration rather than at validation.
    /// </summary>
    private void SetRouteFromLegacy(string barId, HotbarKeybindSet set)
    {
        var route = FindRoute(barId);
        if (route == null) return;

        if (set == HotbarKeybindSet.QuickslotQ)
        {
            Debug.LogWarning($"[InputSystem] Legacy save gave '{barId}' the Q key, now claimed by " +
                             $"the modifier bar — '{barId}' migrated to None.");
            set = HotbarKeybindSet.None;
        }

        route.keybinds = set;
    }

    private BarKeybindRoute FindRoute(string barId)
    {
        if (string.IsNullOrEmpty(barId)) return null;

        foreach (var route in keybindRoutes)
        {
            if (route != null && route.barId == barId) return route;
        }

        return null;
    }

    // Keybind Routing Validation

    /// <summary>
    /// Enforces one owner per physical key. Routes are checked in list order, so the bar listed
    /// first keeps a contested key and the later one is demoted to None.
    /// Called at Initialize and after LoadSaveData.
    /// </summary>
    private void ValidateKeybindRouting()
    {
        claimedKeys.Clear();

        foreach (var route in keybindRoutes)
        {
            if (route == null || route.keybinds == HotbarKeybindSet.None) continue;

            if (route.heldTime == null) route.heldTime = new float[12];

            string clash = FirstClaimedKey(route.keybinds);
            if (clash != null)
            {
                Debug.LogWarning($"[InputSystem] Bar '{route.barId}' set '{route.keybinds}' wants " +
                                 $"key '{clash}', already claimed by an earlier bar — demoted to None.");
                route.keybinds = HotbarKeybindSet.None;
                continue;
            }

            claimedKeys.AddRange(KeysClaimedBy(route.keybinds));
        }
    }

    private readonly List<string> claimedKeys = new List<string>();

    private string FirstClaimedKey(HotbarKeybindSet set)
    {
        foreach (string key in KeysClaimedBy(set))
        {
            if (claimedKeys.Contains(key)) return key;
        }

        return null;
    }

    /// <summary>
    /// The physical keys a set occupies. Names are labels for the warning, not control paths —
    /// the actual bindings live on PlayerInputControls.
    /// </summary>
    private static string[] KeysClaimedBy(HotbarKeybindSet set)
    {
        switch (set)
        {
            case HotbarKeybindSet.ZXCV:       return new[] { "z", "x", "c", "v" };
            case HotbarKeybindSet.Hotbar1234: return new[] { "1", "2", "3", "4" };
            case HotbarKeybindSet.Hotbar5678: return new[] { "5", "6", "7", "8" };
            case HotbarKeybindSet.Hotbar9:    return new[] { "9" };
            case HotbarKeybindSet.QuickslotQ: return new[] { "q" };
            case HotbarKeybindSet.MouseLR:    return new[] { "lmb", "rmb" };
            case HotbarKeybindSet.ShiftCtrlQ: return new[] { "shift", "ctrl", "q" };
            default: return Array.Empty<string>();
        }
    }

    // Dependency Setup

    private void SetupDependencies()
    {
        if (cameraProviderComponent is ICameraProvider cp)
            cameraProvider = cp;
        else
            cameraProvider = brain.GetProvider<ICameraProvider>();
    }

    private HotbarSystem GetHotbarSystem()
    {
        if (hotbarSystem == null)
            hotbarSystem = brain?.GetModule<HotbarSystem>();
        return hotbarSystem;
    }

    // IMovementControlSource

    public MovementInput GetMovementInput()
    {
        switch (currentMode)
        {
            case InputMode.Player: return GetPlayerMovementInput();
            case InputMode.AI: return GetAIMovementInput();
            case InputMode.Admin: return GetAdminMovementInput();
            default: return MovementInput.Zero;
        }
    }

    public void OnActivated() { }
    public void OnDeactivated() { }

    public void UpdateSource()
    {
        if (!IsEnabled) return;
        switch (currentMode)
        {
            case InputMode.Player: ReadPlayerInput(); break;
            default: ClearPlayerInput(); break;
        }
    }

    // IAbilityControlSource

    public string GetAbilitySlotToTrigger()
    {
        switch (currentMode)
        {
            case InputMode.Player: return GetPlayerAbilityInput();
            case InputMode.AI: return GetAIAbilityInput();
            case InputMode.Admin: return GetAdminAbilityInput();
            default: return null;
        }
    }

    // Mode Switching

    public void SetMode(InputMode mode)
    {
        if (currentMode == mode) return;
        currentMode = mode;
    }

    public InputMode GetMode() => currentMode;

    // AI Control API

    public void RequestAbility(string slotKey)
    {
        if (currentMode != InputMode.AI)
        {
            Debug.LogWarning($"[InputSystem] RequestAbility() called but mode is {currentMode}");
            return;
        }
        aiRequestedAbility = slotKey;
    }

    public bool HasPendingAbilityRequest() => !string.IsNullOrEmpty(aiRequestedAbility);
    public void ClearAbilityRequest() => aiRequestedAbility = null;

    // Player Input Reading

    private int _lastReadFrame = -1;

    private void ReadPlayerInput()
    {
        if (Time.frameCount == _lastReadFrame) return;
        _lastReadFrame = Time.frameCount;

        ReadMovementInput();
        ReadCombatInput();
        ReadAbilityInput();
        ReadHotbarInput();
        ReadInteractionInput();
    }

    private void ReadMovementInput()
    {
        if (inputActions == null) return;

        MoveInput = inputActions.Player.Move.ReadValue<Vector2>();
        LookInput = inputActions.Player.Look.ReadValue<Vector2>();
        JumpPressed = inputActions.Player.Jump.WasPressedThisFrame();
        JumpHeld = inputActions.Player.Jump.IsPressed();
        CrouchPressed = inputActions.Player.Crouch.WasPressedThisFrame();
        CrouchHeld = inputActions.Player.Crouch.IsPressed();
        GaitTogglePressed = inputActions.Player.ToggleGait.WasPressedThisFrame();
        DashPressed = false; // TODO: Add Dash to PlayerInputControls
    }

    private void ReadCombatInput()
    {
        LightAttackPressed = inputActions.Player.Attack.WasPressedThisFrame();
        HeavyAttackPressed = false; // TODO: Add HeavyAttack
        BlockHeld = inputActions.Player.Block.IsPressed();
        ParryPressed = false; // TODO: Add Parry
        ToggleStancePressed = inputActions.Player.ToggleStance.WasPressedThisFrame();
    }

    private void ReadAbilityInput()
    {
        if (inputActions == null) return;

        AbilityQPressed = inputActions.Player.QuickslotQ.WasPressedThisFrame();
        AbilityZPressed = inputActions.Player.QuickslotZ.WasPressedThisFrame();
        AbilityXPressed = inputActions.Player.QuickslotX.WasPressedThisFrame();
        AbilityCPressed = inputActions.Player.QuickslotC.WasPressedThisFrame();
        AbilityVPressed = inputActions.Player.QuickslotV.WasPressedThisFrame();

        AbilityShiftPressed = inputActions.Player.QuickslotShift.WasPressedThisFrame();
        AbilityCtrlPressed = inputActions.Player.QuickslotCtrl.WasPressedThisFrame();
        AbilityBlockPressed = inputActions.Player.Block.WasPressedThisFrame();
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

        foreach (var route in keybindRoutes)
            BridgeBarInput(route);
    }

    private void ReadInteractionInput()
    {
        InteractPressed = inputActions != null && inputActions.Player.Interact.WasPressedThisFrame();
    }

    // Hotbar Bridge

    /// <summary>
    /// Routes one bar's keybind set to its slots.
    /// Handles charge-hold logic: waits for release on charge-ability slots,
    /// fires charge variant if held past chargeThreshold, otherwise fires base.
    /// </summary>
    private void BridgeBarInput(BarKeybindRoute route)
    {
        if (route == null || route.keybinds == HotbarKeybindSet.None) return;

        var hotbar = GetHotbarSystem();
        if (hotbar == null) return;

        string barId = route.barId;
        HotbarKeybindSet keybindSet = route.keybinds;

        float[] heldTime = route.heldTime;
        int slotCount = SlotCountFor(keybindSet);

        for (int i = 0; i < slotCount; i++)
        {
            bool isHeld = GetIsPressed(keybindSet, i);
            bool wasPressed = GetWasPressed(keybindSet, i);
            bool wasReleased = !isHeld && heldTime[i] > 0f;

            if (isHeld)
            {
                heldTime[i] += Time.deltaTime;
            }
            else if (wasReleased)
            {
                float duration = heldTime[i];
                heldTime[i] = 0f;

                var slot = hotbar.GetSlot(barId, i);
                var ability = hotbar.ResolveSlotAbility(slot);

                // Only a charge slot acts on release; every other slot already fired on press.
                if (ability?.chargeAbility == null) continue;

                if (duration >= ability.chargeThreshold)
                    brain.GetModule<AbilitySystem>()?.UseAbility(ability.chargeAbility.abilityId);
                else
                    hotbar.TriggerSlot(barId, i);

                continue;
            }

            // Immediate press — skip if slot has a charge ability (waits for release).
            if (wasPressed)
            {
                var slot = hotbar.GetSlot(barId, i);
                var ability = hotbar.ResolveSlotAbility(slot);

                if (ability?.chargeAbility != null) continue;

                hotbar.TriggerSlot(barId, i);
            }
        }
    }

    private static int SlotCountFor(HotbarKeybindSet set)
    {
        switch (set)
        {
            case HotbarKeybindSet.ZXCV: return 4;
            case HotbarKeybindSet.Hotbar1234: return 4;
            case HotbarKeybindSet.Hotbar5678: return 4;
            case HotbarKeybindSet.Hotbar9: return 1;
            case HotbarKeybindSet.QuickslotQ: return 1;
            case HotbarKeybindSet.MouseLR: return 2;
            case HotbarKeybindSet.ShiftCtrlQ: return 3;
            default: return 0;
        }
    }

    /// <summary>Returns true while the physical button for slot i is held.</summary>
    private bool GetIsPressed(HotbarKeybindSet set, int i)
    {
        switch (set)
        {
            case HotbarKeybindSet.ZXCV:
                switch (i)
                {
                    case 0: return inputActions.Player.QuickslotZ.IsPressed();
                    case 1: return inputActions.Player.QuickslotX.IsPressed();
                    case 2: return inputActions.Player.QuickslotC.IsPressed();
                    case 3: return inputActions.Player.QuickslotV.IsPressed();
                }
                break;

            case HotbarKeybindSet.Hotbar1234:
                switch (i)
                {
                    case 0: return inputActions.Player.Hotbar1.IsPressed();
                    case 1: return inputActions.Player.Hotbar2.IsPressed();
                    case 2: return inputActions.Player.Hotbar3.IsPressed();
                    case 3: return inputActions.Player.Hotbar4.IsPressed();
                }
                break;

            case HotbarKeybindSet.Hotbar5678:
                switch (i)
                {
                    case 0: return inputActions.Player.Hotbar5.IsPressed();
                    case 1: return inputActions.Player.Hotbar6.IsPressed();
                    case 2: return inputActions.Player.Hotbar7.IsPressed();
                    case 3: return inputActions.Player.Hotbar8.IsPressed();
                }
                break;

            case HotbarKeybindSet.Hotbar9:
                if (i == 0) return inputActions.Player.Hotbar9.IsPressed();
                break;

            case HotbarKeybindSet.QuickslotQ:
                if (i == 0) return inputActions.Player.QuickslotQ.IsPressed();
                break;

            case HotbarKeybindSet.MouseLR:
                switch (i)
                {
                    case 0: return inputActions.Player.Attack.IsPressed();
                    case 1: return inputActions.Player.Block.IsPressed();
                }
                break;

            case HotbarKeybindSet.ShiftCtrlQ:
                switch (i)
                {
                    case 0: return inputActions.Player.QuickslotShift.IsPressed();
                    case 1: return inputActions.Player.QuickslotCtrl.IsPressed();
                    case 2: return inputActions.Player.QuickslotQ.IsPressed();
                }
                break;
        }
        return false;
    }

    /// <summary>Returns true on the frame the physical button for slot i was pressed.</summary>
    private bool GetWasPressed(HotbarKeybindSet set, int i)
    {
        switch (set)
        {
            case HotbarKeybindSet.ZXCV:
                switch (i)
                {
                    case 0: return AbilityZPressed;
                    case 1: return AbilityXPressed;
                    case 2: return AbilityCPressed;
                    case 3: return AbilityVPressed;
                }
                break;

            case HotbarKeybindSet.Hotbar1234:
                switch (i)
                {
                    case 0: return Hotbar1Pressed;
                    case 1: return Hotbar2Pressed;
                    case 2: return Hotbar3Pressed;
                    case 3: return Hotbar4Pressed;
                }
                break;

            case HotbarKeybindSet.Hotbar5678:
                switch (i)
                {
                    case 0: return Hotbar5Pressed;
                    case 1: return Hotbar6Pressed;
                    case 2: return Hotbar7Pressed;
                    case 3: return Hotbar8Pressed;
                }
                break;

            case HotbarKeybindSet.Hotbar9:
                if (i == 0) return Hotbar9Pressed;
                break;

            case HotbarKeybindSet.QuickslotQ:
                if (i == 0) return AbilityQPressed;
                break;

            case HotbarKeybindSet.MouseLR:
                switch (i)
                {
                    case 0: return LightAttackPressed;
                    case 1: return BlockHeld; // a guard is a hold: keep asking until it can go up
                }
                break;

            case HotbarKeybindSet.ShiftCtrlQ:
                switch (i)
                {
                    case 0: return AbilityShiftPressed;
                    case 1: return AbilityCtrlPressed;
                    case 2: return AbilityQPressed;
                }
                break;
        }
        return false;
    }

    // Player Ability Input  (→ AbilityLoadoutModule)

    /// <summary>
    /// All player ability input now routes through BridgeBarInput → HotbarSystem.
    /// AbilityLoadoutModule receives no player input — returns null always.
    /// </summary>
    private string GetPlayerAbilityInput() => null;

    // AI / Admin Ability Input

    private string GetAIAbilityInput()
    {
        string slot = aiRequestedAbility;
        aiRequestedAbility = null;
        return slot;
    }

    private string GetAdminAbilityInput()
    {
        // TODO: scripted admin abilities
        return null;
    }

    // ClearPlayerInput

    private void ClearPlayerInput()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpPressed = JumpHeld = DashPressed = false;
        CrouchPressed = CrouchHeld = false;
        GaitTogglePressed = false;
        LightAttackPressed = HeavyAttackPressed = BlockHeld = ParryPressed = false;
        ToggleStancePressed = false;
        AbilityQPressed = AbilityZPressed = AbilityXPressed =
            AbilityCPressed = AbilityVPressed = false;
        AbilityShiftPressed = AbilityCtrlPressed = AbilityBlockPressed = false;
        Hotbar1Pressed = Hotbar2Pressed = Hotbar3Pressed = Hotbar4Pressed =
        Hotbar5Pressed = Hotbar6Pressed = Hotbar7Pressed = Hotbar8Pressed =
        Hotbar9Pressed = false;

        foreach (var route in keybindRoutes)
        {
            if (route?.heldTime == null) continue;
            Array.Clear(route.heldTime, 0, route.heldTime.Length);
        }

        InteractPressed = false;
    }

    // Movement Helpers

    private MovementInput GetPlayerMovementInput()
    {
        Vector2 raw = MoveInput;
        Vector2 moveDir = cameraRelativeMovement ? TransformToCameraSpace(raw) : raw;
        Vector2 lookDir = CalculateLookDirection(moveDir);

        return new MovementInput
        {
            MoveDirection = moveDir,
            LookDirection = lookDir,
            ToggleGait = GaitTogglePressed,
            Jump = JumpPressed,
            JumpHold = JumpHeld,
            Crouch = CrouchPressed,
            CrouchHold = CrouchHeld,
            Dash = DashPressed
        };
    }

    private Vector2 TransformToCameraSpace(Vector2 raw)
    {
        if (raw.magnitude < 0.01f) return Vector2.zero;

        Transform cam = cameraProvider?.CameraTransform ?? Camera.main?.transform;
        if (cam == null) return raw;

        Vector3 fwd = cam.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 right = cam.right; right.y = 0f; right.Normalize();

        Vector3 dir = fwd * raw.y + right * raw.x;
        return new Vector2(dir.x, dir.z);
    }

    private Vector2 CalculateLookDirection(Vector2 moveDir)
    {
        if (cameraProvider != null && cameraProvider.CameraDrivesFacing)
        {
            float camYaw = cameraProvider.GetCameraHorizontalRotation();
            Vector3 camForward = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
            return new Vector2(camForward.x, camForward.z);
        }

        return moveDir.magnitude > 0.1f ? moveDir : Vector2.zero;
    }

    private MovementInput GetAIMovementInput()
    {
        if (stubAIControlSource == null) return MovementInput.Zero;
        return stubAIControlSource.GetMovementInput();
    }

    private MovementInput GetAdminMovementInput()
    {
        // TODO: scripted admin movement
        return MovementInput.Zero;
    }

    // Keybind Routing Query

    /// <summary>
    /// Returns the keybind set assigned to the given bar.
    /// Used by ActionBarView to generate per-slot key labels.
    /// </summary>
    public HotbarKeybindSet GetKeybindSetForBar(string barId)
    {
        var route = FindRoute(barId);
        return route != null ? route.keybinds : HotbarKeybindSet.None;
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 100 + keybindRoutes.Count * 20));
        GUILayout.Label("=== INPUT SYSTEM ===");
        GUILayout.Label($"Mode:        {currentMode}");
        GUILayout.Label($"Active:      {IsActive}");
        GUILayout.Label($"Move:        {MoveInput}");
        GUILayout.Label($"Jump:        {JumpPressed}");

        foreach (var route in keybindRoutes)
        {
            if (route == null) continue;
            GUILayout.Label($"{route.barId}: {route.keybinds}");
        }

        GUILayout.EndArea();
    }
#endif

    // No OnDestroy needed — Brain owns and cleans up PlayerInputControls
}