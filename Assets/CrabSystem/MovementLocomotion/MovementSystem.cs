using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Universal Movement System - Works for all entities
/// 
/// Architecture:
/// - ONE LocomotionHandler (defines game's movement style - interchangeable)
/// - ONE active ControlSource (defines who controls - switchable at runtime)
/// - Universal handlers (animation, constraints - same for all)
/// 
/// This system enables:
/// - Player and NPC using same movement code
/// - Swapping locomotion style between games (RPG → MilSim)
/// - Runtime control switching (possession, pets, cutscenes)
/// - Zero code duplication
/// </summary>
public class MovementSystem : MonoBehaviour, IBrainModule
{
    public int InitOrder => 60;

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Core Handlers")]
    [SerializeField] private LocomotionHandler locomotionHandler;

    [Header("Optional Modules")]
    [Tooltip("Feet detection module for grounded state (optional - will auto-discover)")]
    [SerializeField] private FeetDetectionModule feetDetection;

    [Header("Gait States")]
    [Tooltip("With a gait-aware locomotion handler, IsRunning and IsSprinting come from the gait " +
             "the player chose, gated only by actually moving. This is the speed (m/s) at which " +
             "'moving' turns ON.")]
    [SerializeField] private float moveEnterSpeed = 1f;

    [Tooltip("Speed at which 'moving' turns OFF. Lower than the enter speed, so stopping does not " +
             "flicker the camera between modes.")]
    [SerializeField] private float moveExitSpeed = 0.5f;

    [Header("Speed States")]
    [Tooltip("Horizontal speed (m/s) at which Running turns ON — used for SpeedBlend, and for " +
             "IsRunning only when the locomotion handler has no gait. Sits above your walk speed.")]
    [SerializeField] private float runEnterSpeed = 4f;

    [Tooltip("Speed at which Running turns OFF. LOWER than the enter speed on purpose. A single " +
             "threshold flickers whenever you hover on it — clip a corner, drop under for two " +
             "frames — and a flickering fact makes the camera pump and the speed lines stutter.")]
    [SerializeField] private float runExitSpeed = 3.2f;

    [Tooltip("Speed at which Sprinting turns ON.")]
    [SerializeField] private float sprintEnterSpeed = 8f;

    [Tooltip("Speed at which Sprinting turns OFF. Again lower than the enter speed.")]
    [SerializeField] private float sprintExitSpeed = 6.8f;

    [Header("Speed Blend")]
    [Tooltip("Blend gained per second while accelerating.")]
    [SerializeField] private float blendAttack = 4f;

    [Tooltip("Blend lost per second while slowing. LOWER than attack — effects that snap in and " +
             "ease out read as momentum; a symmetric fade reads as a toggle.")]
    [SerializeField] private float blendRelease = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // References
    private ControllerBrain brain;
    private StateMachineModule stateMachine;
    private Blackboard blackboard;

    private bool isRunning;
    private bool isSprinting;
    private bool isInMotion;
    private float speedBlend;
    private IMovementControlSource activeControlSource;
    private List<IMovementControlSource> availableControlSources;

    // ========================================
    // Properties
    // ========================================

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public LocomotionHandler Locomotion => locomotionHandler;
    public IMovementControlSource ActiveControlSource => activeControlSource;
    public ControllerBrain Brain => brain;

    /// <summary>Does the current upper-body state allow this movement? True with no state machine.</summary>
    public bool Permits(LowerBodyState state) => stateMachine == null || stateMachine.CanPerformMovement(state);

    /// <summary>
    /// Grounded state from the locomotion handler when it owns grounding, otherwise from
    /// FeetDetectionModule, otherwise the Brain fallback.
    ///
    /// A sweep-based handler already runs a downward capsule cast every step and knows the ground
    /// normal and distance, not just a bool — asking a trigger volume the same question in
    /// parallel gives two answers that disagree on stairs, slopes and anything moving. The
    /// handler flag decides which source is authoritative, so handlers that do NOT own grounding
    /// keep the existing behaviour untouched.
    /// </summary>
    public bool IsGrounded => locomotionHandler != null && locomotionHandler.ProvidesGrounding
        ? locomotionHandler.IsGrounded
        : feetDetection?.IsGrounded ?? brain?.IsGrounded ?? false;

    // Locomotion state passthrough
    public bool IsMoving => locomotionHandler?.IsMoving ?? false;
    public Vector3 Velocity => locomotionHandler?.Velocity ?? Vector3.zero;

    /// <summary>
    /// Horizontal speed in m/s. Vertical motion is excluded deliberately — falling is not
    /// sprinting, and including Y would trip every speed state off a long drop.
    /// </summary>
    public float Speed
    {
        get
        {
            Vector3 flat = Velocity;
            flat.y = 0f;
            return flat.magnitude;
        }
    }

    /// <summary>
    /// In the run or sprint gait AND moving. Gait is what the player chose (or was granted), not
    /// how fast momentum is carrying them — a jump burst at walk gait is not running. Falls back to
    /// speed hysteresis for a handler with no gait.
    /// </summary>
    public bool IsRunning => isRunning;

    /// <summary>In the sprint gait AND moving. A player with no sprint grant never sprints.</summary>
    public bool IsSprinting => isSprinting;

    /// <summary>
    /// 0 at the run threshold, 1 at the sprint threshold, damped asymmetrically.
    ///
    /// This is the CONTINUOUS half of the same information the two bools carry. Anything that
    /// fades — camera FOV, speed lines, wind audio — reads this rather than building its own
    /// ramp from a boolean, which is how two effects end up disagreeing about the same moment.
    /// </summary>
    public float SpeedBlend => speedBlend;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        stateMachine = brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
            Debug.LogWarning("[MovementSystem] No StateMachineModule found — movement permission checks disabled");

        // Auto-discover feet detection if not assigned
        if (feetDetection == null)
        {
            feetDetection = GetComponent<FeetDetectionModule>();

            if (feetDetection == null)
            {
                Debug.LogWarning("[MovementSystem] No FeetDetectionModule found - will use Brain.IsGrounded fallback");
            }
        }

        // Validate locomotion handler
        if (locomotionHandler == null)
        {
            locomotionHandler = GetComponentInChildren<LocomotionHandler>();

            if (locomotionHandler == null)
            {
                Debug.LogError("[MovementSystem] No LocomotionHandler found!");
                isEnabled = false;
                return;
            }
        }

        // Initialize locomotion handler
        locomotionHandler.Initialize(this);

        // Setup control sources
        SetupControlSources();

        // Activate default control source
        ActivateDefaultControlSource();

        if (showDebugInfo)
        {
            Debug.Log($"[MovementSystem] Initialized on {brain.EntityName}");
            Debug.Log($"  Feet Detection: {(feetDetection != null ? "Active" : "Using Brain fallback")}");
            Debug.Log($"  Locomotion: {locomotionHandler.GetType().Name}");
            Debug.Log($"  Active Control: {activeControlSource?.SourceName ?? "NONE"}");
        }
    }

    /// <summary>
    /// The blackboard belongs to another module, so it is resolved here rather than in
    /// Initialize — InitializeModules() runs before every module exists.
    /// </summary>
    public void LateInitialize()
    {
        blackboard = brain != null ? brain.Blackboard : null;
    }

    public void UpdateModule()
    {
        if (!isEnabled || locomotionHandler == null) return;

        UpdateSpeedState();

        // Rebind if control source becomes inactive
        if (activeControlSource == null || !activeControlSource.IsActive)
        {
            ActivateDefaultControlSource();
        }

        // Update active control source
        activeControlSource?.UpdateSource();

        // Get raw input from control source
        MovementInput input = activeControlSource?.GetMovementInput() ?? MovementInput.Zero;

        // ── State machine permission check ────────────────────────────────
        // Map the requested input to a LowerBodyState and ask the state machine
        // if it's currently allowed. This is where StatePermissionMatrix enforces
        // all movement restrictions — Dark Souls lock, blocking slow walk, etc.
        // No movement lock bools needed anywhere else in the codebase.
        if (input.HasMovementInput && stateMachine != null)
        {
            LowerBodyState desired = ResolveLowerBodyState(input);
            if (!Permits(desired))
                input = MovementInput.Zero;
        }

        input = ApplyDenials(input);

        locomotionHandler.ExecuteMovement(input);
    }

    // Facing comes from the camera inside the handler, not through MovementInput, so the denials
    // below never reach it. The handler asks this instead. Hard control (stun, flinch) holds it too.
    public bool FacingLocked => IsDead || (blackboard != null && blackboard.GetBool(BlackboardKey.CannotAct));

    // Where the body should face this frame instead of the camera (an attack tracking its target, a locked-on
    // run). Zero = the camera. Written each frame by its owner (TargetingModule), so a stale value can't stick.
    public Vector3 FacingOverride { get; set; }

    // A castWhileMoving-off move is playing: input is stripped and the clip owns the whole body.
    public bool IsRooted => blackboard != null && blackboard.GetBool(BlackboardKey.MoveRooted);

    // A useRootMotion move is playing: the handler moves by the clip's travel instead of its own speed.
    public bool RootMotionDriven => blackboard != null && blackboard.GetBool(BlackboardKey.RootMotionDriven);

    // How much of the clip's travel the move in flight keeps (AbilityDefinition.rootMotionScale).
    public float RootMotionScale => blackboard != null ? blackboard.GetFloat(BlackboardKey.RootMotionScale) : 1f;

    private bool IsDead => brain != null && brain.Damage != null && brain.Damage.IsDead;

    // Denial facts, each with its own writer (BlackboardKey). The Cannot* facts come from statuses;
    // MoveRooted from AbilitySystem while a castWhileMoving-off ability plays. CannotSprint is not
    // here — it lives in the gait (LocomotionHandler.SprintAvailable), so speed and IsSprinting
    // drop together.
    private MovementInput ApplyDenials(MovementInput input)
    {
        if (IsDead)
            return MovementInput.Zero;

        if (blackboard == null) return input;

        if (blackboard.GetBool(BlackboardKey.CannotAct) || blackboard.GetBool(BlackboardKey.MoveRooted))
            return MovementInput.Zero;

        if (blackboard.GetBool(BlackboardKey.CannotMove))
        {
            input.MoveDirection = Vector2.zero;
            input.Dash = false;
        }

        if (blackboard.GetBool(BlackboardKey.CannotJump))
            input.Jump = false;

        return input;
    }

    /// <summary>
    /// Derive the speed states and publish them.
    ///
    /// THIS IS THE ONLY PLACE SPEED THRESHOLDS LIVE. The camera, the speed lines and anything
    /// else that reacts to going fast read the facts or the blend rather than re-deriving them
    /// from Velocity with their own numbers, which is how they drift apart.
    ///
    /// The facts are written DIRECTLY rather than authored as BlackboardConditions. A condition
    /// would give SemanticBridgeSystem a second writer for the same key and the two would fight
    /// every frame. MovementValueSource exists so conditions can still read speed as an INPUT
    /// and produce their own, different facts.
    /// </summary>
    private void UpdateSpeedState()
    {
        float speed = Speed;
        Gait gait = locomotionHandler != null ? locomotionHandler.CurrentGait : Gait.None;

        // A root-motion move is the clip travelling, not the character running: the gait facts read it
        // as standing. Otherwise a lunge raised IsRunning and the next press became a running attack.
        float gaitSpeed = RootMotionDriven ? 0f : speed;

        if (gait == Gait.None)
        {
            isRunning = Hysteresis(isRunning, gaitSpeed, runEnterSpeed, runExitSpeed);
            isSprinting = Hysteresis(isSprinting, gaitSpeed, sprintEnterSpeed, sprintExitSpeed);
        }
        else
        {
            isInMotion = Hysteresis(isInMotion, gaitSpeed, moveEnterSpeed, moveExitSpeed);
            isRunning = isInMotion && (gait == Gait.Run || gait == Gait.Sprint);
            isSprinting = isInMotion && gait == Gait.Sprint;
        }

        // SpeedBlend stays MEASURED on purpose — effects that should answer "how fast", however the
        // speed was reached (the speed vignette), read this; effects that answer "which gait" read
        // the bools.
        float target = Mathf.InverseLerp(runEnterSpeed, sprintEnterSpeed, speed);
        float rate = target > speedBlend ? blendAttack : blendRelease;
        speedBlend = Mathf.MoveTowards(speedBlend, target, rate * Time.deltaTime);

        if (blackboard == null) return;

        // SetBool/SetFloat only fire their change events when the value actually moves, so
        // publishing every frame costs a dictionary lookup and nothing else.
        blackboard.SetBool(BlackboardKey.IsRunning, isRunning);
        blackboard.SetBool(BlackboardKey.IsSprinting, isSprinting);
        blackboard.SetFloat(BlackboardKey.SpeedBlend, speedBlend);
    }

    /// <summary>
    /// Two thresholds, not one: rising past `enter` turns it on, and it only turns off once the
    /// value falls back below `exit`. The gap between them is what stops the chatter.
    /// </summary>
    private static bool Hysteresis(bool current, float value, float enter, float exit)
    {
        return current ? value > exit : value >= enter;
    }

    /// <summary>
    /// Maps a MovementInput to the most appropriate LowerBodyState for permission checking.
    /// The state machine uses this to decide whether the current upper body / posture allows it.
    /// </summary>
    private LowerBodyState ResolveLowerBodyState(MovementInput input)
    {
        if (input.Dash) return LowerBodyState.Dashing;
        if (locomotionHandler.CurrentGait == Gait.Sprint) return LowerBodyState.Sprinting;
        return LowerBodyState.Running;
    }

    // ========================================
    // Control Source Management
    // ========================================

    private void SetupControlSources()
    {
        availableControlSources = new List<IMovementControlSource>();

        // Find all control sources in children (for AI, network, etc.)
        var sources = GetComponentsInChildren<IMovementControlSource>();
        availableControlSources.AddRange(sources);

        // Check if InputSystem implements IMovementControlSource
        // InputSystem lives at Brain level, not as a child of MovementSystem
        var inputSystem = brain.GetModule<InputSystem>();
        if (inputSystem is IMovementControlSource inputAsControlSource && !availableControlSources.Contains(inputAsControlSource))
        {
            availableControlSources.Add(inputAsControlSource);
        }

        if (showDebugInfo)
        {
            Debug.Log($"[MovementSystem] Found {availableControlSources.Count} control sources:");
            foreach (var source in availableControlSources)
            {
                Debug.Log($"  - {source.SourceName}");
            }
        }
    }

    private void ActivateDefaultControlSource()
    {
        // Find first enabled AND active control source
        foreach (var source in availableControlSources)
        {
            var monoBehaviour = source as MonoBehaviour;
            if (monoBehaviour != null &&
                monoBehaviour.enabled &&
                source.IsActive)
            {
                SetControlSource(source);
                return;
            }
        }

        Debug.LogWarning($"[MovementSystem] No enabled control source found on {brain.EntityName}");
    }

    /// <summary>
    /// Switch to a different control source at runtime
    /// This enables possession, pets, cutscenes, etc.
    /// </summary>
    public void SetControlSource(IMovementControlSource newSource)
    {
        if (newSource == activeControlSource) return;

        // Deactivate current source
        if (activeControlSource != null)
        {
            activeControlSource.OnDeactivated();

            if (showDebugInfo)
                Debug.Log($"[MovementSystem] Deactivated: {activeControlSource.SourceName}");
        }

        // Activate new source
        activeControlSource = newSource;

        if (activeControlSource != null)
        {
            activeControlSource.OnActivated();

            if (showDebugInfo)
                Debug.Log($"[MovementSystem] Activated: {activeControlSource.SourceName}");
        }
    }

    /// <summary>
    /// Get control source by type
    /// Useful for possession system: SetControlSource(GetControlSource<AdminControlSource>())
    /// </summary>
    public T GetControlSource<T>() where T : class, IMovementControlSource
    {
        return availableControlSources.OfType<T>().FirstOrDefault();
    }

    // ========================================
    // Debug Info
    // ========================================

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 300, 300, 200));
        GUILayout.Label("=== MOVEMENT SYSTEM ===");
        GUILayout.Label($"Handler: {locomotionHandler?.GetType().Name ?? "NONE"}");
        GUILayout.Label($"Control: {activeControlSource?.SourceName ?? "NONE"}");
        GUILayout.Label($"Grounded: {IsGrounded}");
        GUILayout.Label($"Moving: {IsMoving}");
        GUILayout.Label($"Velocity: {Velocity.magnitude:F2}");
        GUILayout.Label($"Speed (flat): {Speed:F2}");
        GUILayout.Label($"Run/Sprint: {isRunning} / {isSprinting}   Blend: {speedBlend:F2}");
        GUILayout.EndArea();
    }
#endif
}