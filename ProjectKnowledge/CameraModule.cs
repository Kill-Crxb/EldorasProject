using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraModule : MonoBehaviour, IBrainModule, ICameraProvider
{
    [Serializable]
    public class CameraModeSettings
    {
        public string modeName = "Default";
        public float distance = 1.3f;
        public float pivotHeight = 1.5f;
        public float shoulderOffset = 0.4f;
        public float sensitivity = 0.15f;
        public float minPitch = -35f;
        public float maxPitch = 60f;

        [Tooltip("Added to baseFieldOfView while this mode is active, and scaled by " +
                 "fovEffectScale. An OFFSET rather than an absolute so a user FOV preference " +
                 "and the comfort slider both keep working. Everything else on this class stays " +
                 "absolute, which is fine because exactly one mode is ever active.")]
        public float fovOffset = 0f;
    }

    /// <summary>
    /// A rule that says which mode to be in, and how strongly it wants it.
    ///
    /// This exists because SetMode(string) is last-caller-wins. Dialogue starting while you are
    /// sprinting used to leave you stuck in dialogue framing once the sprint ended, because
    /// nothing was left to call SetMode back. Rules are re-evaluated every frame, so a mode
    /// stops applying the moment its condition stops holding — there is no release to forget.
    /// </summary>
    [Serializable]
    public class CameraStateRule
    {
        [Tooltip("Blackboard fact to watch, e.g. 'IsSprinting' or 'IsInConversation'. Use this " +
                 "for facts something already publishes — it needs no asset at all.\n\n" +
                 "Takes precedence over Condition when both are set.")]
        public string factKey;

        [Tooltip("Evaluate a BlackboardCondition asset instead. Use this when the rule needs " +
                 "logic no fact expresses yet — a stat threshold, a composite.")]
        public BlackboardCondition condition;

        [Tooltip("Which mode from the list above.")]
        public string modeName;

        [Tooltip("Highest satisfied rule wins. Dialogue should outrank sprint.")]
        public int priority;

        // Hashing the key every frame per rule is wasteful and the key rarely changes, so keep
        // the last one. NonSerialized because a hash is derived data, not authored data.
        [NonSerialized] private string hashedFor;
        [NonSerialized] private int cachedHash;

        public bool IsUsable => !string.IsNullOrEmpty(modeName) &&
                                (!string.IsNullOrEmpty(factKey) || condition != null);

        /// <summary>
        /// A raw fact beats a condition asset. The fact is what something actually published;
        /// a condition wrapping the same fact would only be a second, slower route to it.
        /// </summary>
        public bool IsSatisfied(ControllerBrain brain, Blackboard blackboard)
        {
            if (!string.IsNullOrEmpty(factKey))
                return blackboard != null && blackboard.GetBool(FactHash);

            return condition != null && condition.Evaluate(brain);
        }

        private int FactHash
        {
            get
            {
                if (hashedFor != factKey)
                {
                    hashedFor = factKey;
                    cachedHash = new BlackboardKey(factKey).hash;
                }

                return cachedHash;
            }
        }
    }

    [SerializeField] private Transform cameraTransform;

    [Tooltip("The Camera whose field of view is driven. Optional — leave empty and FOV is " +
             "simply never touched, which is exactly how this module behaved before.")]
    [SerializeField] private Camera targetCamera;

    [SerializeField]
    private List<CameraModeSettings> modes = new List<CameraModeSettings>
    {
        new CameraModeSettings { modeName = "Default" },
        new CameraModeSettings
        {
            modeName = "Dialogue",
            distance = 0.6f,
            pivotHeight = 1.6f,
            shoulderOffset = 0.2f,
            sensitivity = 0.05f,
            minPitch = -20f,
            maxPitch = 30f
        }
    };

    [SerializeField] private string startingMode = "Default";
    [SerializeField] private float modeBlendDuration = 0.35f;

    [Header("Field of View")]
    [Tooltip("The resting FOV every mode offsets from. A user FOV setting writes here.")]
    [SerializeField] private float baseFieldOfView = 60f;

    [Tooltip("Scales every mode's fovOffset. Drop to 0 to disable FOV movement entirely without " +
             "touching the modes — FOV shifts are a common motion-sickness trigger, so this " +
             "wants to be an accessibility setting rather than a number buried in assets.")]
    [Range(0f, 1f)]
    [SerializeField] private float fovEffectScale = 1f;

    [Header("Vertical Follow")]
    [Tooltip("How fast the pivot's HEIGHT catches up to the body's. Horizontal follow stays rigid " +
             "— damping that would read as input lag. Step-up moves the body onto a stair in a " +
             "single frame by design, so without this the camera makes the same instant jump and " +
             "every riser pops. Higher is tighter; 0 disables and pins the camera to the body.")]
    [SerializeField] private float verticalFollowDamping = 15f;

    [Tooltip("Height gap beyond which the camera stops damping and simply snaps. Teleports, " +
             "respawns and long falls must not be smeared across half a second.")]
    [SerializeField] private float verticalSnapDistance = 1f;

    [Header("State Rules")]
    [Tooltip("Evaluated every frame; the highest-priority satisfied rule picks the mode. " +
             "Empty means the module behaves exactly as it did before — manual SetMode only.")]
    [SerializeField] private List<CameraStateRule> stateRules = new List<CameraStateRule>();

    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private Blackboard blackboard;
    private Transform target;
    private IInputProvider input;
    private IStateProvider stateProvider;
    private float yaw;
    private float pitch;

    // Active, possibly-blended values driving movement this frame.
    private float distance;
    private float pivotHeight;
    private float shoulderOffset;
    private float sensitivity;
    private float minPitch;
    private float maxPitch;
    private float fovOffset;

    // Set by SetMode, cleared by ClearManualMode. While set, state rules stand down — a
    // cutscene or a debug override must not be argued with every frame.
    private string manualMode;

    // ResolveStateMode runs every frame, so a rule naming a mode that does not exist would
    // otherwise log the same warning sixty times a second. Remember it and say it once.
    private string lastMissingMode;

    // Damped follow height, and whether it has been seeded from the body yet.
    private MovementSystem movement;
    private float followHeight;
    private bool followSeeded;

    private CameraModeSettings currentMode;
    private CameraModeSettings blendFrom;
    private CameraModeSettings blendTo;
    private float blendElapsed;
    private bool isBlending;

    public string CurrentModeName => currentMode?.modeName;
    public event Action<string> OnModeChanged;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        target = brain.EntityRoot;
        input = brain.GetModuleImplementing<IInputProvider>();
        stateProvider = brain.GetModule<IStateProvider>();

        if (cameraTransform == null)
            Debug.LogError("[CameraModule] cameraTransform is not assigned.", this);
        if (input == null)
            Debug.LogError("[CameraModule] No IInputProvider found on the brain.", this);
        if (modes == null || modes.Count == 0)
            Debug.LogError("[CameraModule] No camera modes configured.", this);

        if (target != null) yaw = target.eulerAngles.y;
        pitch = 15f;

        CameraModeSettings initial = FindMode(startingMode)
            ?? (modes != null && modes.Count > 0 ? modes[0] : new CameraModeSettings());
        currentMode = initial;
        ApplyImmediate(initial);

        // startingMode is the resting choice, not a manual override — leaving manualMode null
        // lets the state rules take over from frame one.
        manualMode = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// The blackboard belongs to another module, so it is resolved here rather than in
    /// Initialize — InitializeModules() runs before every module exists.
    /// </summary>
    public void LateInitialize()
    {
        blackboard = brain != null ? brain.Blackboard : null;

        // Resolved late, like the blackboard: MovementSystem may not exist yet during Initialize.
        // Only IsGrounded is wanted — the vertical damping is suspended in the air.
        movement = brain != null ? brain.GetModule<MovementSystem>() : null;
    }

    public void UpdateModule()
    {
        if (!IsEnabled || input == null) return;

        ResolveStateMode();
        UpdateBlend();

        if (UiWantsCursor() || !CameraInputAllowed())
        {
            SetCursorLocked(false);
            return;
        }

        SetCursorLocked(true);

        Vector2 look = input.LookInput;
        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    /// <summary>
    /// Switches to the named camera mode, blending distance/pivot/offset/sensitivity/pitch-clamp
    /// over modeBlendDuration seconds. Called directly, or forwarded by a CameraEventListener
    /// reacting to GameEvents.OnCameraModeChangeRequested.
    /// </summary>
    public void SetMode(string modeName)
    {
        manualMode = modeName;
        ApplyMode(modeName);
    }

    /// <summary>
    /// Hand control back to the state rules after a manual SetMode — end of a cutscene, end of
    /// a debug override. Without this a manual mode sticks forever, which is the old behaviour
    /// and is deliberate: a cutscene must not be overruled by the player happening to sprint.
    /// </summary>
    public void ClearManualMode()
    {
        manualMode = null;
    }

    /// <summary>
    /// Pick the mode the world currently calls for. Highest-priority satisfied rule wins; with
    /// nothing satisfied it falls back to startingMode.
    /// </summary>
    private void ResolveStateMode()
    {
        if (manualMode != null) return;
        if (stateRules == null || stateRules.Count == 0) return;

        CameraStateRule best = null;

        for (int i = 0; i < stateRules.Count; i++)
        {
            CameraStateRule rule = stateRules[i];
            if (rule == null || !rule.IsUsable) continue;

            // Priority first — a losing rule is not worth evaluating.
            if (best != null && rule.priority <= best.priority) continue;
            if (!rule.IsSatisfied(brain, blackboard)) continue;

            best = rule;
        }

        string wanted = best != null ? best.modeName : startingMode;

        if (string.Equals(wanted, currentMode?.modeName, StringComparison.OrdinalIgnoreCase)) return;

        if (FindMode(wanted) == null)
        {
            if (lastMissingMode != wanted)
            {
                lastMissingMode = wanted;
                Debug.LogWarning($"[CameraModule] State rule wants mode '{wanted}', which is not " +
                                 "in the modes list. That rule can never apply.", this);
            }
            return;
        }

        lastMissingMode = null;
        ApplyMode(wanted);
    }

    private void ApplyMode(string modeName)
    {
        CameraModeSettings next = FindMode(modeName);
        if (next == null)
        {
            Debug.LogWarning($"[CameraModule] No camera mode named '{modeName}' found.", this);
            return;
        }
        if (next == currentMode && !isBlending) return;

        blendFrom = new CameraModeSettings
        {
            modeName = "(blend)",
            distance = distance,
            pivotHeight = pivotHeight,
            shoulderOffset = shoulderOffset,
            sensitivity = sensitivity,
            minPitch = minPitch,
            maxPitch = maxPitch,
            fovOffset = fovOffset
        };
        blendTo = next;
        blendElapsed = 0f;
        isBlending = modeBlendDuration > 0f;
        currentMode = next;

        if (!isBlending) ApplyImmediate(next);

        OnModeChanged?.Invoke(next.modeName);
    }

    private void UpdateBlend()
    {
        if (!isBlending) return;

        blendElapsed += Time.deltaTime;
        float t = modeBlendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / modeBlendDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);

        distance = Mathf.Lerp(blendFrom.distance, blendTo.distance, eased);
        pivotHeight = Mathf.Lerp(blendFrom.pivotHeight, blendTo.pivotHeight, eased);
        shoulderOffset = Mathf.Lerp(blendFrom.shoulderOffset, blendTo.shoulderOffset, eased);
        sensitivity = Mathf.Lerp(blendFrom.sensitivity, blendTo.sensitivity, eased);
        minPitch = Mathf.Lerp(blendFrom.minPitch, blendTo.minPitch, eased);
        maxPitch = Mathf.Lerp(blendFrom.maxPitch, blendTo.maxPitch, eased);
        fovOffset = Mathf.Lerp(blendFrom.fovOffset, blendTo.fovOffset, eased);

        if (t >= 1f) isBlending = false;
    }

    private void ApplyImmediate(CameraModeSettings s)
    {
        distance = s.distance;
        pivotHeight = s.pivotHeight;
        shoulderOffset = s.shoulderOffset;
        sensitivity = s.sensitivity;
        minPitch = s.minPitch;
        maxPitch = s.maxPitch;
        fovOffset = s.fovOffset;
        isBlending = false;
    }

    private CameraModeSettings FindMode(string modeName)
    {
        if (modes == null || string.IsNullOrEmpty(modeName)) return null;
        for (int i = 0; i < modes.Count; i++)
        {
            if (string.Equals(modes[i].modeName, modeName, StringComparison.OrdinalIgnoreCase))
                return modes[i];
        }
        return null;
    }

    // Gates look input on the entity's own BrainState (Dialogue, Inventory, Crafting,
    // Reading, etc.) via the existing IStateProvider.AllowsCameraInput contract — this
    // was already defined for this purpose, just never wired to a camera before.
    private bool CameraInputAllowed() => stateProvider == null || stateProvider.AllowsCameraInput;

    private bool UiWantsCursor()
    {
        if (UIPanelRegistry.AnyOpen) return true;
        if (UniversalWindowManager.Instance != null && UniversalWindowManager.Instance.IsAnyWindowOpen()) return true;
        return false;
    }

    private void SetCursorLocked(bool locked)
    {
        if (locked)
        {
            if (Cursor.lockState == CursorLockMode.Locked) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (Cursor.lockState == CursorLockMode.None) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        if (!IsEnabled || target == null || cameraTransform == null) return;

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = FollowPoint() + Vector3.up * pivotHeight;
        pivot += orbit * Vector3.right * shoulderOffset;
        cameraTransform.position = pivot + orbit * new Vector3(0f, 0f, -distance);
        cameraTransform.rotation = orbit;

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFieldOfView + fovOffset * fovEffectScale;
    }

    /// <summary>
    /// The body's position with its HEIGHT damped and its horizontal axes left rigid.
    ///
    /// CharacterMotor climbs a stair by placing the body on top of the riser in one frame — a
    /// teleport, not a motion, and unavoidable in a sweep-based step-up. Pinned to that, the
    /// camera makes the same instant jump and every step pops. Damping only the height keeps the
    /// camera responsive to turning and strafing while the vertical settles over a few frames.
    ///
    /// Suspended while airborne: a jump or a fall IS genuine vertical motion and wants to read
    /// 1:1. Damping it makes arcs feel mushy and disconnects the camera from the character at
    /// exactly the moment the player is judging a landing. Landing re-enters damping with no
    /// discontinuity, because the airborne branch has been tracking the body exactly.
    ///
    /// Note this hides the vertical half of a step-up only. The horizontal 0.19 m the motor also
    /// jumps is real world motion and stays visible; if that reads badly it wants smoothing on
    /// the MODEL, not here, or the camera stops answering the mouse honestly.
    /// </summary>
    private Vector3 FollowPoint()
    {
        Vector3 position = target.position;

        if (!followSeeded)
        {
            followHeight = position.y;
            followSeeded = true;
        }

        bool grounded = movement != null && movement.IsGrounded;
        bool tooFar = Mathf.Abs(position.y - followHeight) > verticalSnapDistance;

        if (!grounded || tooFar || verticalFollowDamping <= 0f)
            followHeight = position.y;
        else
            followHeight = Mathf.Lerp(followHeight, position.y,
                                      1f - Mathf.Exp(-verticalFollowDamping * Time.deltaTime));

        position.y = followHeight;
        return position;
    }

    public Transform CameraTransform => cameraTransform;
    public float GetCameraHorizontalRotation() => yaw;
    public bool CameraDrivesFacing => true;
}
