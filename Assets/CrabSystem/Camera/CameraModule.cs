using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraModule : MonoBehaviour, IBrainModule, ICameraProvider
{
    public int InitOrder => 40;
    public bool PlayerOnly => true;

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
        [IdRef(IdKind.Fact)] public string factKey;

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

    [Header("Orbit")]
    [Tooltip("Swings the camera around the character without turning the character. Added to yaw " +
             "in LateUpdate only \u2014 facing, aim and parkour all read the un-offset yaw through " +
             "GetCameraHorizontalRotation, so the character keeps looking where the player " +
             "aimed it.\n\nDeliberately not part of CameraModeSettings and never written by code: " +
             "mode switches, blends and Initialize all leave it alone, so a value set here stays " +
             "set.")]
    [Range(-180f, 180f)]
    [SerializeField] private float orbitYawOffset = 0f;

    [Header("Shoulder Swap")]
    [Tooltip("Move the camera to the other shoulder when the static world is about to obstruct " +
             "the side it sits on, and move it back once that side clears.\n\n" +
             "SIDE IS AN AXIS, NOT A MODE. The active mode owns the shoulder offset's " +
             "MAGNITUDE; this owns its SIGN. Making side a mode would need a left and a right " +
             "copy of every mode — Default, Sprint, Dialogue — and a swap rule that re-encodes " +
             "all of them, because ResolveStateMode picks exactly one winner by priority.")]
    [SerializeField] private bool shoulderSwapEnabled = true;

    [Tooltip("Which shoulder is home. +1 is the character's right, -1 its left.")]
    [SerializeField] private float homeShoulderSide = 1f;

    [Tooltip("What is allowed to obstruct the camera. STATIC world only by default — Terrain " +
             "(3) and StaticProp (6), mask 72. DynamicProp is deliberately excluded: a crate " +
             "drifting past the camera seat would swap the shoulder and swap it back, and a " +
             "swap the player did not cause reads as a bug. Keep this a SUBSET of " +
             "CharacterMotor.collisionLayers — an obstruction you cannot touch is a swap with " +
             "no visible reason.")]
    [SerializeField] private LayerMask shoulderSwapLayers = (1 << 3) | (1 << 6);

    [Tooltip("Radius of the sphere swept from the pivot out to the candidate camera seat. This " +
             "is the clearance you want kept around the lens, not the body radius — a ray " +
             "threads gaps that the near plane does not.")]
    [SerializeField] private float shoulderProbeRadius = 0.25f;

    [Tooltip("Seconds to cross from one shoulder to the other. Doubles as the lookahead " +
             "window: the swap has to FINISH before the prop arrives, so the probe leads the " +
             "body by speed * this. That makes the lookahead derived rather than a second " +
             "number to keep in step, the same way ParkourAssistant derives its reach.")]
    [SerializeField] private float shoulderSwapBlendDuration = 0.25f;

    [Tooltip("Blends of travel the probe leads the body by while GROUNDED. 1 is the bare " +
             "derivation — the swap merely finishes as the prop arrives. Above 1 buys margin, " +
             "and is a tuned number sitting on a derived base rather than a derivation itself.")]
    [SerializeField] private float groundLookaheadBlends = 1.5f;

    [Tooltip("Blends of travel the probe leads by while AIRBORNE, and it wants to be larger. " +
             "A grounded character can turn away from a prop and cancel the swap; in the air " +
             "the trajectory is already committed, so the decision may as well be made early. " +
             "This is also the only state where VERTICAL velocity is allowed to lead the " +
             "probe.")]
    [SerializeField] private float airLookaheadBlends = 3f;

    [Tooltip("Hard ceiling on the lead, in metres. A long fall reaches tens of m/s and an " +
             "unclamped lead would put the probe well below the map, reading floor geometry as " +
             "an obstruction beside the character. Clamps the whole vector, so it bounds " +
             "runaway horizontal speed too.")]
    [SerializeField] private float maxLookahead = 2.5f;

    [Tooltip("Metres scanned BEYOND the camera seat when deciding to LEAVE a side. Larger " +
             "leaves earlier.")]
    [SerializeField] private float swapTriggerMargin = 0.5f;

    [Tooltip("Metres that must be clear before RETURNING to a side. Must exceed the trigger " +
             "margin — the gap between the two is the hysteresis, and without it a fence with " +
             "gaps in it alternates the answer every few frames and strobes the camera. Same " +
             "fix as the run/sprint enter-and-exit thresholds on MovementSystem.")]
    [SerializeField] private float swapReturnMargin = 0.9f;

    [Tooltip("Shortest time a side is held before another change is allowed. Backstop for the " +
             "margins: geometry that alternates faster than this cannot drive the camera.")]
    [SerializeField] private float minSideHoldTime = 0.35f;

    [Tooltip("Extra pivot height applied once BOTH sides are blocked and the camera settles " +
             "centred. Driven by the settled TARGET, never by the axis position — a swap " +
             "travels through 0 on its way to the far shoulder, so scaling this by the axis " +
             "bobbed the camera up and back down on every swap. Set to 0 for no vertical " +
             "movement at all.")]
    [SerializeField] private float centeredPivotLift = 0.35f;

    [Header("State Rules")]
    [Tooltip("Evaluated every frame; the highest-priority satisfied rule picks the mode. " +
             "Empty means the module behaves exactly as it did before — manual SetMode only.")]
    [SerializeField] private List<CameraStateRule> stateRules = new List<CameraStateRule>();

    [Header("Look Target (hard lock)")]
    [Tooltip("How fast the camera swings round onto a look target, degrees per second. The lock turns the camera " +
             "only; the mouse keeps the tilt.")]
    [SerializeField] private float lookTargetTurnSpeed = 540f;
    [Tooltip("Tilt range while locked (positive looks down), so a close target can't drag the view into the floor.")]
    [SerializeField] private float lockMinPitch = -10f;
    [SerializeField] private float lockMaxPitch = 20f;

    [Header("Aim")]
    [Tooltip("Degrees the aim ray points above the screen centre, so a camera tilted down to frame the character " +
             "still aims level. The crosshair sits where the ray lands.")]
    [SerializeField] private float aimPitchOffset = 6f;
    [SerializeField] private LayerMask aimMask = ~0;

    private static readonly RaycastHit[] AimHits = new RaycastHit[16];

    public bool IsEnabled { get; set; } = true;

    // While set, the camera yaws onto this and the mouse keeps only the tilt. Unused since the Tab hard lock was
    // parked (10 Oct); kept for an ability that locks a target (a berserker rage).
    public Transform LookTarget { get; set; }

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

    // Signed shoulder side: +1 home, -1 swapped, 0 both sides blocked. Deliberately on its own
    // timer rather than modeBlendDuration — a mode change and a shoulder swap can be in flight
    // at the same time and neither should retime the other.
    private float shoulderSide;
    private float shoulderSideTarget;
    private float lastSideChangeTime;
    private bool shoulderSideSeeded;

    // Blended 0..1, and NOT derived from shoulderSide. It tracks whether the settled answer is
    // "both sides blocked", which a swap in progress is not.
    private float centeredWeight;

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
        input = brain.GetProvider<IInputProvider>();
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
        pitch -= look.y * sensitivity;

        if (LookTarget == null)
        {
            yaw += look.x * sensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            return;
        }

        pitch = Mathf.Clamp(pitch, Mathf.Max(minPitch, lockMinPitch), Mathf.Min(maxPitch, lockMaxPitch));
        TurnToLookTarget();
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
    // Yaw only: aiming the pitch at the target pointed the camera into the floor whenever the target was close.
    private void TurnToLookTarget()
    {
        Vector3 to = LookTarget.position - target.position;
        if (new Vector2(to.x, to.z).sqrMagnitude < 0.0001f) return;

        float wantedYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        yaw = Mathf.MoveTowardsAngle(yaw, wantedYaw, lookTargetTurnSpeed * Time.deltaTime);
    }

    public Vector3 AimPoint(float range, out Collider hit)
    {
        hit = null;
        if (cameraTransform == null) return target != null ? target.position + target.forward * range : Vector3.zero;

        Vector3 origin = cameraTransform.position;
        Vector3 direction = Quaternion.AngleAxis(-aimPitchOffset, cameraTransform.right) * cameraTransform.forward;
        // Skip what sits between the lens and the player: the aim starts at the player's depth along the ray.
        float skip = target != null ? Mathf.Max(0f, Vector3.Dot(target.position - origin, direction)) : 0f;
        float reach = range + skip;

        int count = Physics.RaycastNonAlloc(origin, direction, AimHits, reach, aimMask, QueryTriggerInteraction.Ignore);
        float nearest = reach;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = AimHits[i];
            if (candidate.distance < skip || candidate.distance >= nearest) continue;
            if (candidate.collider.GetComponentInParent<ControllerBrain>() == brain) continue;

            nearest = candidate.distance;
            hit = candidate.collider;
        }

        return origin + direction * nearest;
    }

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

        // orbitYawOffset is folded in HERE and nowhere else. Everything that asks this
        // module which way the camera faces gets raw yaw, which is what keeps the offset
        // a camera-only orbit instead of a slow turn of the character.
        Quaternion orbit = Quaternion.Euler(pitch, yaw + orbitYawOffset, 0f);
        Vector3 basePivot = FollowPoint() + Vector3.up * pivotHeight;

        UpdateShoulderSide(orbit, basePivot);
        AdvanceShoulderSide();

        // Both sides blocked parks the axis at 0, the midpoint of the two offsets. The lift
        // reads off centeredWeight rather than the axis, so a swap crossing contributes none.
        Vector3 pivot = basePivot + Vector3.up * (centeredPivotLift * centeredWeight);
        pivot += orbit * Vector3.right * (shoulderOffset * shoulderSide);

        cameraTransform.position = pivot + orbit * new Vector3(0f, 0f, -distance);
        cameraTransform.rotation = orbit;

        if (targetCamera != null)
            targetCamera.fieldOfView = baseFieldOfView + fovOffset * fovEffectScale;
    }

    /// <summary>
    /// Choose which shoulder to sit on.
    ///
    /// Probes the candidate camera SEAT on each side, not the space around the body. The seat
    /// is a mode's distance behind and its shoulder offset out, so a prop that obstructs it is
    /// frequently nowhere near the character, and a wall the body is brushing frequently does
    /// not obstruct it at all. ParkourAssistant answers the body question and is sized to the
    /// body — it is the wrong volume for this one, and reusing it would both miss props behind
    /// the character and swap for walls that never cross the lens.
    /// </summary>
    private void UpdateShoulderSide(Quaternion orbit, Vector3 basePivot)
    {
        // Seeded before the enabled check on purpose: the axis multiplies shoulderOffset, so a
        // side left at 0 would silently centre the camera for anyone who turns the swap off.
        if (!shoulderSideSeeded)
        {
            // Sign only — a fractional home would seed a side the decision below can
            // never target, and the camera would creep to full offset on the first probe.
            shoulderSide = homeShoulderSide >= 0f ? 1f : -1f;
            shoulderSideTarget = shoulderSide;
            shoulderSideSeeded = true;
        }

        if (!shoulderSwapEnabled)
        {
            shoulderSideTarget = homeShoulderSide;
            return;
        }

        if (Time.time - lastSideChangeTime < minSideHoldTime) return;

        Vector3 probePivot = basePivot + LookaheadOffset();
        float home = homeShoulderSide >= 0f ? 1f : -1f;
        float away = -home;

        // Leaving a side is judged with the trigger margin, returning to one with the larger
        // return margin. Anything not already sitting at home has to clear the stricter test to
        // get back, which is where the hysteresis lives.
        bool atHome = Mathf.Approximately(shoulderSideTarget, home);
        float homeMargin = atHome ? swapTriggerMargin : swapReturnMargin;

        if (SeatClear(probePivot, orbit, home, homeMargin))
        {
            SetSideTarget(home);
            return;
        }

        if (SeatClear(probePivot, orbit, away, swapTriggerMargin))
        {
            SetSideTarget(away);
            return;
        }

        SetSideTarget(0f);
    }

    private void SetSideTarget(float side)
    {
        if (Mathf.Approximately(shoulderSideTarget, side)) return;
        shoulderSideTarget = side;
        lastSideChangeTime = Time.time;
    }

    private void AdvanceShoulderSide()
    {
        float wantCentered = Mathf.Approximately(shoulderSideTarget, 0f) ? 1f : 0f;

        if (shoulderSwapBlendDuration <= 0f)
        {
            shoulderSide = shoulderSideTarget;
            centeredWeight = wantCentered;
            return;
        }

        // Two units per blend duration for the side, because a full swap travels from -1 to +1.
        // Linear rather than eased so the duration is exact — the lookahead is derived from it,
        // and an asymptotic blend would make that derivation a guess.
        float step = Time.deltaTime / shoulderSwapBlendDuration;
        shoulderSide = Mathf.MoveTowards(shoulderSide, shoulderSideTarget, step * 2f);
        centeredWeight = Mathf.MoveTowards(centeredWeight, wantCentered, step);
    }

    /// <summary>
    /// How far ahead to probe.
    ///
    /// The base is one blend's worth of travel — the swap has to be FINISHED by the time the
    /// prop reaches the seat — and the blend multipliers buy margin on top of that. Deriving
    /// the base from the blend duration is what stops the lead going stale when the duration is
    /// retuned, the way ParkourAssistant derives its reach from maxExpectedSpeed.
    ///
    /// AIR LEADS FURTHER, AND IN THREE DIMENSIONS. A grounded character can turn away and
    /// cancel the swap, so deciding late costs nothing; in the air the trajectory is committed
    /// and the swap has to be over before the landing. Vertical velocity is admitted only while
    /// airborne, because on the ground the motor puts the body on top of a stair riser in a
    /// single frame — a vertical lead there reads that teleport as approach and would swap the
    /// shoulder for the ceiling above a staircase.
    /// </summary>
    private Vector3 LookaheadOffset()
    {
        if (movement == null) return Vector3.zero;

        bool airborne = !movement.IsGrounded;
        Vector3 lead = movement.Velocity;
        if (!airborne) lead.y = 0f;

        float blends = airborne ? airLookaheadBlends : groundLookaheadBlends;
        lead *= shoulderSwapBlendDuration * blends;

        return Vector3.ClampMagnitude(lead, Mathf.Max(0f, maxLookahead));
    }

    /// <summary>
    /// Is the camera seat for this side reachable from the pivot without hitting the static
    /// world? Swept as a sphere rather than a ray, because a ray threads railings and seams the
    /// near plane does not.
    ///
    /// QueryTriggerInteraction.Ignore is passed explicitly. The project runs with
    /// m_QueriesHitTriggers on, so inheriting the default would let interaction volumes and the
    /// character's own hurtboxes count as obstructions — the same trap every CharacterMotor cast
    /// sidesteps the same way.
    /// </summary>
    private bool SeatClear(Vector3 pivot, Quaternion orbit, float side, float margin)
    {
        Vector3 seat = pivot
                     + orbit * Vector3.right * (shoulderOffset * side)
                     + orbit * new Vector3(0f, 0f, -distance);

        Vector3 delta = seat - pivot;
        float length = delta.magnitude;
        if (length <= 0.0001f) return true;

        return !Physics.SphereCast(pivot, shoulderProbeRadius, delta / length, out RaycastHit _,
                                   length + margin, shoulderSwapLayers,
                                   QueryTriggerInteraction.Ignore);
    }

    void OnValidate()
    {
        // Clamped rather than warned because an inverted pair has no useful behaviour: the
        // camera would leave a side and return to it on the same test and strobe every frame.
        if (swapReturnMargin < swapTriggerMargin) swapReturnMargin = swapTriggerMargin;
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
    // Raw yaw on purpose - orbitYawOffset is excluded. See the Orbit field.
    public float GetCameraHorizontalRotation() => yaw;
    public bool CameraDrivesFacing => true;
}
