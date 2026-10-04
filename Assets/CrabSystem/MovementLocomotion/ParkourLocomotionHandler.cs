using UnityEngine;

/// <summary>
/// Momentum-first locomotion. Owns an explicit velocity vector and integrates it by hand.
///
/// You keep what you earn: only ACCELERATION is clamped, never total speed, so a lower wish speed
/// stops the player gaining rather than taking away what they carried in. Friction does all the
/// bleeding. This is the opposite of ARPGLocomotionHandler, which lerps toward a target velocity
/// every frame and therefore erases momentum by design.
///
/// It deliberately does NOT call base.Initialize: that hard-errors without a CharacterController,
/// which this handler replaces. It assigns movementSystem and rootTransform itself and overrides
/// the properties anything upstream reads, which is what lets it drop into the existing
/// MovementSystem socket with no edits to shared code.
///
/// MovementSystem drives ExecuteMovement from Update; integration runs in FixedUpdate. Input is
/// latched in the first and consumed in the second, so movement steps at a fixed rate whatever the
/// framerate is doing.
/// </summary>
// What a jump press resolved into. Presentation reads it through OnMoveAction.
public enum MoveAction { GroundJump, AirJump, WallJump, Mantle }

public class ParkourLocomotionHandler : LocomotionHandler
{
    [Header("Parkour Movement")]
    [Tooltip("Every tunable number. Without one this handler disables itself rather than guessing.")]
    [SerializeField] private MovementProfile profile;

    [Tooltip("The motor on the entity root. Found automatically if left empty.")]
    [SerializeField] private CharacterMotor motor;

    [Header("Animation Parameters")]
    [SerializeField] private string movementSpeedParam = "MovementSpeed";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string movementStateParam = "MovementState";
    [SerializeField] private string jumpTriggerParam = "JumpTrigger";

    [Tooltip("Animator trigger for the mantle. Left empty, the jump trigger is used instead, so " +
             "the move is never silent just because the clip has not been authored yet.")]
    [SerializeField] private string mantleTriggerParam = "Mantle";
    [SerializeField] private string isLockedOnParam = "IsLockedOn";
    [SerializeField] private string strafeXParam = "StrafeX";
    [SerializeField] private string strafeYParam = "StrafeY";

    IAnimationProvider animationProvider;
    ICameraProvider cameraProvider;
    ModelModule modelModule;

    Transform modelRoot;
    Quaternion modelBaseRotation = Quaternion.identity;
    Vector3 modelBasePosition;
    float modelLean;

    // World-space distance the model is currently lagging behind the body, so a step-up reads as
    // a stride rather than a teleport. Visual only — nothing downstream of this reads it.
    Vector3 stepSmooth;

    // Latched input, written in Update, read in FixedUpdate.
    Vector2 moveInput;
    Vector2 lookInput;
    bool jumpHeld;

    // Camera basis, resolved once per step so every consumer shares one answer.
    Vector3 cameraForward = Vector3.forward;
    Vector3 cameraRight = Vector3.right;

    // This step's input, split against that basis.
    Vector2 strafeBlend;
    float lateralSmoothed;
    float forwardCommitment;
    float rawLateral;

    bool walkMode;

    // Crouch and slide. Posture is NOT tracked here — StateMachineModule owns it, and a second
    // copy would be the bool soup the design doc replaced. This holds only the input edge and the
    // capsule geometry needed to resize.
    StateMachineModule stateMachine;
    CapsuleCollider bodyCapsule;
    float standingHeight;
    float capsuleBottom;
    bool crouchQueued;
    bool crouchHeld;
    bool warnedPostureRefused;

    bool IsCrouching => stateMachine != null && stateMachine.IsInPostureState(PostureState.Crouching);

    // Accumulated external motion, added by ApplyImpulse from any thread of control and drained
    // once per physics step. See the post-pass slot in FixedUpdate.
    Vector3 pendingImpulse;

    // Ground friction is held off until this timestamp. Set by SuppressFriction, read by
    // ApplyFriction. Absolute time rather than a countdown so overlapping requests just take the
    // later deadline instead of needing to be tracked individually.
    float frictionSuppressedUntil;

    // Wall and air jump budgets, both reset the moment the feet are back down.
    ParkourAssistant assistant;
    int airJumpsUsed;
    int wallJumpsUsed;

    // Mantle is a one-shot launch, not a state — it fires and the arc plays out under normal
    // gravity like any other. Budgeted per airtime alongside the two jumps.
    float mantleBlockedUntil;
    int mantlesUsed;

    float lastJumpPressTime = Mathf.NegativeInfinity;
    float lastGroundedTime = Mathf.NegativeInfinity;
    float landedTime = Mathf.NegativeInfinity;
    float snapSuppressedUntil;
    bool jumpCutApplied;
    bool warnedSpeedClamped;
    bool wasGrounded;

    /// <summary>
    /// Speed along the ground normal below which the character counts as resting on the surface
    /// rather than leaving it. Not a feel dial — it only has to sit above the overbounce residue
    /// (thousandths) and below a real launch (jump is 8 m/s).
    /// </summary>
    const float GroundStickTolerance = 0.5f;

    bool Ready => motor != null && profile != null;
    float HorizontalSpeed => new Vector2(currentVelocity.x, currentVelocity.z).magnitude;

    /// <summary>The tunables this handler is running on. For debug displays and editor tooling.</summary>
    public MovementProfile Profile => profile;

    /// <summary>The motor is the single grounding authority, so MovementSystem reads it through here.</summary>
    public override bool ProvidesGrounding => true;
    public override bool IsGrounded => motor != null && motor.IsGrounded;
    public override bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;
    public override Vector3 Velocity => currentVelocity;
    public override Vector3 VerticalVelocity => Vector3.up * currentVelocity.y;

    public override void Initialize(MovementSystem system)
    {
        movementSystem = system;
        rootTransform = system.Brain.EntityRoot != null ? system.Brain.EntityRoot : system.Brain.transform;
        animationProvider = system.Brain.GetProvider<IAnimationProvider>();
        cameraProvider = system.Brain.GetProvider<ICameraProvider>();
        modelModule = system.Brain.GetModule<ModelModule>();

        if (motor == null)
            motor = rootTransform.GetComponent<CharacterMotor>();

        if (motor == null)
            Debug.LogError($"[{GetType().Name}] No CharacterMotor on {rootTransform.name} — movement is off.");

        if (profile == null)
            Debug.LogError($"[{GetType().Name}] No MovementProfile assigned — movement is off.");

        if (profile != null)
            walkMode = profile.startInWalkMode;

        // The state machine already owns posture and gates transitions through
        // StatePermissionMatrix, so crouch and slide are requests to it rather than flags here.
        // Optional on purpose: without it the wall jump is simply unavailable and everything else
        // behaves normally. An NPC with no sensor is not a broken character.
        assistant = system.Brain.GetModule<ParkourAssistant>();

        stateMachine = system.Brain.GetModule<StateMachineModule>();

        if (stateMachine == null)
            Debug.LogWarning($"[{GetType().Name}] No StateMachineModule — crouch and slide are off.");

        bodyCapsule = motor != null ? motor.Capsule : null;

        if (bodyCapsule != null)
        {
            // Hold the capsule's UNDERSIDE fixed across every height change. Resize about the
            // centre instead and the feet move, which moves grounding with them.
            standingHeight = bodyCapsule.height;
            capsuleBottom = bodyCapsule.center.y - standingHeight * 0.5f;
        }
    }

    /// <summary>
    /// Latch only. Nothing is integrated here — Update runs at a variable rate and an acceleration
    /// model stepped at a variable rate gives a different top speed on every machine.
    ///
    /// Guarded rather than relying on `enabled`: MovementSystem calls this directly on the
    /// reference, and Unity's enabled flag only gates messages like FixedUpdate.
    /// </summary>
    public override void ExecuteMovement(MovementInput input)
    {
        if (!Ready) return;

        moveInput = input.MoveDirection;
        lookInput = input.LookDirection;

        // The HOLD, not the press edge. ApplyGravity cuts the rise the moment this goes false, so
        // feeding it the edge cuts every jump on the frame after launch and throws away most of
        // the height — variable jump height stops existing and the profile numbers stop meaning
        // anything. The edge below is a separate concern: it stamps the buffer.
        jumpHeld = input.JumpHold;

        if (input.Jump)
            lastJumpPressTime = Time.time;

        // The gait edge is consumed here rather than in FixedUpdate: a tap can start and end inside
        // a single physics step, and a toggle the player made and did not get is worse than a frame
        // of latency.
        if (input.ToggleGait)
            walkMode = !walkMode;

        // The crouch EDGE is queued rather than acted on, because whether it means crouch or
        // slide depends on speed, and speed is only meaningful in FixedUpdate. The HOLD is a
        // level and can be sampled straight through.
        if (input.Crouch) crouchQueued = true;
        crouchHeld = input.CrouchHold;
    }

    /// <summary>
    /// The frame pipeline, in the order the base design doc fixes.
    ///
    /// The order is not cosmetic: wall running needs to change the wish direction, the friction
    /// mode AND the gravity scale, all consumed before any single state-resolution step could run.
    /// That is why state resolution is two passes, and the marked slots below are where they go.
    /// </summary>
    void FixedUpdate()
    {
        if (!Ready) return;

        float dt = Time.fixedDeltaTime;
        bool grounded = motor.IsGrounded;

        if (grounded && !wasGrounded) landedTime = Time.time;
        if (grounded) lastGroundedTime = Time.time;
        wasGrounded = grounded;

        // All three budgets are per AIRTIME, so landing is the only thing that refills them
        // unconditionally. Everything else goes through the refill toggles.
        if (grounded)
        {
            airJumpsUsed = 0;
            wallJumpsUsed = 0;
            mantlesUsed = 0;
        }

        UpdateCameraBasis();

        // ── ResolveStateMods() pre-pass. Crouch and slide are the first states to use it: the
        //    posture decided here is read by BuildWishDir and ApplyFriction below, both of
        //    which consume it in the same step. Wall states join it at slice 2.
        ResolveStance(grounded, dt);

        Vector3 wishDir = BuildWishDir(dt, out float wishSpeed);

        ApplyFriction(grounded, dt, wishDir);
        Accelerate(wishDir, wishSpeed, grounded, dt);

        if (!grounded)
        {
            // Heading first, then the sideways offset laid across it.
            ApplyAirTurn(dt);
            ApplyAirStrafe(dt);
        }

        ApplyGravity(grounded, dt);

        // ── ResolveStateImpulses() post-pass: one-shot impulses and outright velocity overrides,
        //    the only things allowed to land on an already-integrated velocity.
        //
        //    Overrides go FIRST, impulses second. TryJump ASSIGNS currentVelocity.y, so draining
        //    ahead of it would let a jump on the same step erase an impulse's whole vertical
        //    component — an uppercut launch of +12 plus a buffered jump would leave you at exactly
        //    jumpSpeed, i.e. the launch made you jump LOWER. Impulses add; they add last.

        TryJump(grounded);

        DrainImpulses();

        ClampSpeed();

        currentVelocity = motor.Move(currentVelocity, dt, Time.time >= snapSuppressedUntil, ResolveFacing(dt));

        // Collect whatever that step gained instantly. Accumulates rather than overwrites: a
        // staircase at a run lands a step every few frames, and the previous one has not finished
        // easing off yet.
        stepSmooth += motor.LastStepOffset;
        stepSmooth = Vector3.ClampMagnitude(stepSmooth, profile.stepSmoothMaxOffset);

        UpdateModelLean(wishDir, dt);
        UpdateAnimator(grounded);
    }

    /// <summary>
    /// Camera yaw, from the provider rather than MovementInput.LookDirection: that only carries
    /// camera forward while the provider says it drives facing, and collapses to zero with no move
    /// input, so the body would stop tracking the mouse the moment the player stood still.
    /// LookDirection stays as the fallback for control sources with no camera, such as AI.
    /// </summary>
    void UpdateCameraBasis()
    {
        if (cameraProvider != null)
        {
            cameraForward = Quaternion.Euler(0f, cameraProvider.GetCameraHorizontalRotation(), 0f) * Vector3.forward;
        }
        else
        {
            Vector3 look = new Vector3(lookInput.x, 0f, lookInput.y);
            cameraForward = look.sqrMagnitude < 0.01f ? rootTransform.forward : look.normalized;
        }

        cameraRight = Vector3.Cross(Vector3.up, cameraForward);
    }

    /// <summary>
    /// MoveDirection arrives already camera-relative and in world space from the control source, so
    /// this is a reshape rather than a transform. Slice 2 projects the result onto a wall's tangent
    /// plane here when a state asks for it.
    /// </summary>
    Vector3 BuildWishDir(float dt, out float wishSpeed)
    {
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
        float magnitude = direction.magnitude;

        strafeBlend = Vector2.zero;
        wishSpeed = 0f;

        if (magnitude < 0.01f)
        {
            lateralSmoothed = Damp(lateralSmoothed, 0f, profile.strafeRampSpeed, dt);
            forwardCommitment = 0f;
            rawLateral = 0f;
            return Vector3.zero;
        }

        float ahead = Vector3.Dot(direction, cameraForward);
        float lateral = Vector3.Dot(direction, cameraRight);

        // Kept untrimmed for ApplyAirStrafe, which owns the lateral axis outright while airborne.
        rawLateral = lateral;

        // How committed forward the input is: 0 for a pure sidestep or anything backpedalling, 1
        // for straight ahead, continuous between. A threshold test would put W+D — both components
        // at 0.707 — on the wrong side of "primarily forward", and would pop the wish direction as
        // the player rotated across the boundary.
        forwardCommitment = Mathf.Clamp01(ahead);

        // Committed forward, a sideways key is a TRIM rather than a sidestep: the mouse does the
        // turning and a full-strength sidestep on top of it is too coarse to steer with. Eased in,
        // because a correction that lands whole on the first frame reads as a twitch.
        float target = lateral * Mathf.Lerp(1f, profile.strafeBias, forwardCommitment);
        lateralSmoothed = Damp(lateralSmoothed, target, profile.strafeRampSpeed, dt);

        // Analog input scales the REQUEST, not the clamp. A pure sidestep is a WALK whatever mode
        // the player is in — not a fraction of the current tier, or run and sprint would each land
        // somewhere different and only one would sit in the animator's walk band.
        float analog = Mathf.Min(magnitude, 1f);
        float sidestep = IsCrouching ? profile.crouchSpeed : profile.walkSpeed;
        wishSpeed = Mathf.Lerp(sidestep, ResolveWishSpeed(), forwardCommitment) * analog;


        Vector3 shaped = cameraForward * ahead + cameraRight * lateralSmoothed;
        if (shaped.sqrMagnitude < 0.0001f) return Vector3.zero;

        // The blend tree gets the trimmed DIRECTION at full input magnitude, not the trimmed vector
        // itself. Trimming shortens the vector, and a short sample sits near the centre of the tree
        // — the legs would blend back toward idle for pressing an extra key.
        strafeBlend = new Vector2(lateralSmoothed, ahead).normalized * analog;

        return shaped.normalized;
    }

    /// <summary>
    /// Gait is the player's; sprint is granted.
    ///
    /// The toggle key owns walk versus run and nothing else touches it. Sprint is a tag on the
    /// blackboard that an ability, a status or a stat gate raises — this substitutes sprint speed
    /// wherever RUN would have applied, and leaves walking alone. A grant cannot turn a walk into
    /// a sprint; it upgrades a run. CannotSprint beats the grant (LocomotionHandler.SprintAvailable).
    ///
    /// Read at the point of use, never cached. The blackboard belongs to another module and is not
    /// resolved until LateInitialize, which is after this handler's Initialize has already run —
    /// and a grant that changes mid-stride is the entire point of putting it on the blackboard.
    /// </summary>
    float ResolveWishSpeed()
    {
        switch (CurrentGait)
        {
            case Gait.Crouch: return profile.crouchSpeed;
            case Gait.Walk: return profile.walkSpeed;
            case Gait.Sprint: return profile.sprintSpeed;
            default: return profile.runSpeed;
        }
    }

    // The same decision ResolveWishSpeed serves, exposed so MovementSystem publishes IsRunning and
    // IsSprinting from what the player chose rather than from how fast momentum carried them.
    // One source, so the speed you are served and the state everyone reads cannot disagree.
    public override Gait CurrentGait
    {
        get
        {
            if (IsCrouching) return Gait.Crouch;
            if (walkMode) return Gait.Walk;

            return SprintAvailable() ? Gait.Sprint : Gait.Run;
        }
    }

    /// <summary>
    /// Crouch, slide, and standing back up.
    ///
    /// SPEED AT THE PRESS decides which one, not how long the key is held. A tap window cannot
    /// resolve until release, so it would delay the crouch by the whole window and start the
    /// slide late — and the slide is the half that is already bleeding the momentum it exists to
    /// spend. "Hold while moving" is really "press while moving"; the moving part is the
    /// discriminator.
    /// </summary>
    void ResolveStance(bool grounded, float dt)
    {
        if (stateMachine == null || profile == null) return;

        if (crouchQueued)
        {
            crouchQueued = false;
            ToggleCrouch();
        }

        ResizeCapsule(dt);
    }

    /// <summary>
    /// Crouch is a plain toggle: press to go down, press again to come back up.
    ///
    /// Sliding used to live here, entered on speed at the moment of the press. It was removed
    /// deliberately — see the note on ApplyGravity. `PostureState.Sliding` and
    /// `LowerBodyState.Sliding` still exist in the enums; nothing requests them.
    /// </summary>
    void ToggleCrouch()
    {
        if (IsCrouching)
        {
            TryStand();
            return;
        }

        RequestPosture(PostureState.Crouching);
    }

    void TryStand()
    {
        // Refuse under a ledge. Growing the capsule into geometry resolves as a depenetration
        // shove, which throws the character through the ceiling or down through the floor.
        if (motor != null && !motor.Fits(standingHeight, capsuleBottom + standingHeight * 0.5f)) return;

        RequestPosture(PostureState.Standing);
    }

    /// <summary>
    /// Ask the state machine, and say something the first time it says no.
    ///
    /// StatePermissionMatrix can refuse a posture — while stunned, rooted, mid-ability — which is
    /// exactly what it is for. But a refusal is otherwise silent, and "crouch does nothing" with
    /// no console output is a long afternoon.
    /// </summary>
    bool RequestPosture(PostureState posture)
    {
        if (stateMachine.TryTransitionPosture(posture)) return true;
        if (warnedPostureRefused) return false;

        warnedPostureRefused = true;
        Debug.LogWarning($"[{GetType().Name}] StateMachineModule refused posture {posture}. " +
                         "Check StatePermissionMatrix — an unauthored matrix that denies by " +
                         "default makes crouch and slide silently do nothing.");
        return false;
    }

    void ResizeCapsule(float dt)
    {
        if (bodyCapsule == null) return;

        float target = IsCrouching ? profile.crouchHeight : standingHeight;
        float height = Mathf.MoveTowards(bodyCapsule.height, target, profile.crouchResizeSpeed * dt);

        // The motor re-reads height, radius and centre every step, so resizing the collider
        // resizes the motor with no notification. Underside held fixed — see Initialize.
        bodyCapsule.height = height;
        bodyCapsule.center = new Vector3(bodyCapsule.center.x,
                                         capsuleBottom + height * 0.5f,
                                         bodyCapsule.center.z);
    }

    /// <summary>Frame-rate independent exponential approach — fast first, settling after.</summary>
    static float Damp(float current, float target, float rate, float dt)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
    }

    /// <summary>
    /// Grounded friction, or skipped. The third mode — along-wall, applied while AIRBORNE and
    /// ramping over time-in-state — arrives with wall running.
    /// </summary>
    void ApplyFriction(bool grounded, float dt, Vector3 wishDir)
    {
        if (!grounded) return;
        if (Time.time < landedTime + profile.landingFrictionGrace) return;

        // The ability channel's friction holiday. Same shape as the landing grace above, different
        // owner: that one is the handler forgiving a landing, this one is an ability buying itself
        // a window where the impulse it just applied is not immediately bled away.
        if (Time.time < frictionSuppressedUntil) return;

        Vector3 flat = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float speed = flat.magnitude;
        if (speed < 0.001f) return;

        // Split the velocity about the direction being ASKED for, and give each half its own
        // friction. This is grip, in the tyre sense: what you are driving toward persists, what
        // you are sliding sideways scrubs off.
        //
        // ⚠ The obvious version — one friction chosen by whether total speed exceeds the gait —
        // is wrong, and wrong in a way that only shows up in turns. Accelerate adds ONLY along
        // wishDir, so a 90° turn at exactly gait speed leaves the old heading untouched and adds
        // a perpendicular component beside it: √(5² + 0.6²) = 5.04, above gait. Every turn then
        // entered the momentum band, friction dropped to a quarter, the old heading refused to
        // die, and the character drifted like a car while its facing snapped instantly to camera.
        //
        // Splitting by axis fixes it at the root. Lateral velocity is never "earned speed" no
        // matter how fast the total is, so it never gets the discount.
        Vector3 axis = wishDir.sqrMagnitude > 0.0001f ? wishDir.normalized : Vector3.zero;
        Vector3 along = axis * Vector3.Dot(flat, axis);
        Vector3 across = flat - along;

        // Neutral stick leaves axis zero, so everything is "across" and decays at the normal
        // coefficient. Letting go still stops you exactly as it always did.
        float alongFriction = along.magnitude > ResolveWishSpeed()
            ? profile.momentumFriction
            : profile.groundFriction;

        // Both scales are computed from TOTAL speed, the way the single-regime version was. That
        // is what makes momentumFriction == groundFriction restore the old behaviour EXACTLY:
        // equal coefficients give equal scales, and the vector shrinks uniformly as before.
        float alongScale = FrictionScale(speed, alongFriction, dt);
        float acrossScale = FrictionScale(speed, profile.groundFriction, dt);

        Vector3 result = along * alongScale + across * acrossScale;

        currentVelocity.x = result.x;
        currentVelocity.z = result.z;
    }

    /// <summary>
    /// Quake's friction curve as a scale factor. stopSpeed is a FLOOR on the control value, so
    /// slow movement bleeds off in finite time instead of halving forever.
    /// </summary>
    float FrictionScale(float speed, float friction, float dt)
    {
        float control = Mathf.Max(speed, profile.stopSpeed);
        return Mathf.Max(speed - control * friction * dt, 0f) / speed;
    }

    /// <summary>
    /// The crux of the whole controller.
    ///
    /// It caps how much acceleration may add ALONG the requested direction, never total speed.
    /// Moving perpendicular to current velocity leaves `current` near zero, so the full add lands —
    /// momentum conservation and speed gain both fall out of this one clamp.
    ///
    /// Air is the same function with wish speed clamped low. That clamp is the entire difference
    /// between ground and air acceleration.
    /// </summary>
    void Accelerate(Vector3 wishDir, float wishSpeed, bool grounded, float dt)
    {
        if (wishSpeed <= 0f) return;

        float accel = grounded ? profile.groundAccel : profile.airAccel;
        if (!grounded) wishSpeed = Mathf.Min(wishSpeed, profile.airSpeedCap);

        float current = Vector3.Dot(currentVelocity, wishDir);
        float add = wishSpeed - current;
        if (add <= 0f) return;

        currentVelocity += wishDir * Mathf.Min(accel * wishSpeed * dt, add);
    }

    /// <summary>
    /// Steer the airborne heading toward the camera, at a limited rate and only in proportion to
    /// how far forward the player is pushing.
    ///
    /// Rotates horizontal velocity and leaves its length alone, so a mid-air turn neither costs nor
    /// earns speed — the rate is the only thing limiting it, and that rate is the skill.
    ///
    /// Accelerate cannot do this job: airborne, `add = wishSpeed - current` goes negative as soon
    /// as the player already exceeds airSpeedCap along the direction they are asking for, so aiming
    /// thirty degrees off a 6 m/s heading does nothing at all. That is the "locked in the air"
    /// feeling, and no airSpeedCap value fixes it.
    /// </summary>
    void ApplyAirTurn(float dt)
    {
        float rate = profile.airTurnDegreesPerSecond * forwardCommitment;
        if (rate <= 0f) return;

        Vector3 flat = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float speed = flat.magnitude;
        if (speed < 0.01f) return;

        // Zero magnitude delta: the target is the same speed in a new direction, so the vector can
        // only pivot. Nothing here can accelerate or brake the player.
        Vector3 turned = Vector3.RotateTowards(flat, cameraForward * speed, rate * Mathf.Deg2Rad * dt, 0f);

        currentVelocity.x = turned.x;
        currentVelocity.z = turned.z;
    }

    /// <summary>
    /// Shift the player sideways across their own heading, and let it straighten when released.
    ///
    /// The camera-right component of velocity is driven toward a target and back to zero through
    /// the same call, so holding the key angles the trajectory off and letting go returns it. That
    /// symmetry is the feel: a correction the player lays on and takes off, not a commitment.
    ///
    /// Works on the AXIS rather than through a wish direction, which is what makes it survive
    /// forward input. Accelerate reads the projection of velocity onto the direction asked for, and
    /// with forward held that projection sits far above airSpeedCap, so it refuses. Here forward
    /// speed is not part of the question.
    ///
    /// Coming back is slower than going out: sideways speed carried into a jump is earned momentum
    /// and must not be confiscated the instant the feet leave the ground.
    /// </summary>
    void ApplyAirStrafe(float dt)
    {
        if (profile.airStrafeSpeed <= 0f) return;

        float current = Vector3.Dot(currentVelocity, cameraRight);
        float target = rawLateral * profile.airStrafeSpeed;

        float rate = Mathf.Abs(target) >= Mathf.Abs(current)
            ? profile.airStrafeAccel
            : profile.airStrafeReturn;

        currentVelocity += cameraRight * (Mathf.MoveTowards(current, target, rate * dt) - current);
    }

    /// <summary>
    /// Variable jump height lives here, in two halves: velocity is cut once on release, and gravity
    /// is heavier on the way down than on the way up. One arc with no expression is what you get
    /// without both.
    /// </summary>
    void ApplyGravity(bool grounded, float dt)
    {
        // Gate on velocity along the GROUND NORMAL, never on world Y.
        //
        // `currentVelocity.y <= 0f` looks equivalent and fails on every slope. ClipVelocity's
        // overbounce leaves a hair of OUTWARD velocity along the normal, and on a tilted plane
        // that has a positive Y — so the test is false on the very next frame, the grounded
        // branch is skipped, and the AIRBORNE path runs while the character is stood on the
        // floor: the jump cut fires, gravity goes heavy, and it is applied straight DOWN where
        // collide-and-slide turns it back into a downhill component. Net effect is a creep on
        // every slope and a real tax on climbing one — 4.70 m/s up a 26° ramp instead of 5.80.
        //
        // The tolerance is what separates resting from launched. A jump or an impulse leaves
        // several m/s along the normal and must reach the airborne path; the overbounce leaves
        // thousandths. Anything in between is still standing on the ground.
        if (grounded && Vector3.Dot(currentVelocity, motor.GroundNormal) <= GroundStickTolerance)
        {
            // Flatten onto the surface, then press back into it ALONG ITS NORMAL.
            //
            // Assigning currentVelocity.y directly looks equivalent and is not. A straight-DOWN
            // bias is not perpendicular to a tilted plane, so collide-and-slide clips it and what
            // survives is a DOWNHILL tangential component of groundedGravity * sin(angle) — every
            // step, on every slope above zero degrees. Friction cannot keep up with a term that is
            // re-injected every frame, so it settles into a permanent slide: 1.6 m/s down a 5°
            // ramp, 3.3 m/s down a 20° one. The character slides down everything, forever.
            //
            // Pressing along the normal is perpendicular by construction, so ClipVelocity removes
            // essentially all of it and none of it becomes travel. On flat ground the normal is up
            // and this is identical to the old line.
            Vector3 normal = motor.GroundNormal;
            float into = Vector3.Dot(currentVelocity, normal);

            // Kill the component heading into the surface — the landing impact — while keeping
            // everything along it, so momentum carried onto a slope is not confiscated.
            if (into < 0f) currentVelocity -= normal * into;

            // NO tangential gravity. A walkable surface behaves exactly like flat ground: no
            // uphill cost, no downhill gain, no creep. Everything steeper than the motor's
            // maxSlopeAngle is not ground at all, so this branch is skipped and the airborne path
            // below carries the body down the face — which is the whole slope model now.
            currentVelocity += normal * profile.groundedGravity;
            return;
        }

        bool released = !jumpHeld && currentVelocity.y > 0f;

        if (released && !jumpCutApplied)
        {
            currentVelocity.y *= profile.jumpCutMultiplier;
            jumpCutApplied = true;
        }

        bool heavy = released || currentVelocity.y < 0f;
        currentVelocity.y -= profile.gravity * (heavy ? profile.fallGravityMultiplier : 1f) * dt;
    }


    /// <summary>
    /// Coyote time and jump buffering, both forgiveness rather than physics: the player who pressed
    /// jump one frame after the ledge, or one frame before landing, meant to jump and gets to.
    /// </summary>
    /// <summary>
    /// Up and over a ledge — a launch, not a state and not a jump.
    ///
    /// Works grounded and airborne alike, and deliberately spends NO jump budget: a mantle is
    /// something the geometry affords you, not one of your jumps, so arriving at a wall with the
    /// double jump already gone still gets you up it.
    ///
    /// Tried FIRST of the four claims on a jump press. Standing at a waist-high lip and pressing
    /// jump should put you on top of it, not bonk you into its face.
    /// </summary>
    bool TryMantle()
    {
        if (!profile.mantleEnabled || assistant == null) return false;
        if (profile.mantles <= 0 || mantlesUsed >= profile.mantles) return false;
        if (Time.time < mantleBlockedUntil) return false;

        LedgeInfo ledge = assistant.Ledge;
        if (!ledge.Detected || !ledge.Clear) return false;

        float height = ledge.Point.y - rootTransform.position.y;
        if (height < profile.mantleMinHeight) return false;
        if (height > profile.mantleMaxHeight) return false;

        // Solve the arc rather than pick a number: rise needed is the lip plus clearance, and the
        // speed that buys it is sqrt(2*g*h). A fixed mantleUpSpeed either fails on a tall ledge or
        // launches you off a short one, and which of those you got would depend on the geometry.
        //
        // Computed against the HEAVY gravity — that is the multiplier a released jump rises under,
        // and a mantle tapped rather than held must still clear. Erring high costs a little
        // overshoot; erring low drops you back down the wall you were trying to climb.
        float rise = height + profile.mantleClearance;
        float gravity = profile.gravity * profile.fallGravityMultiplier;
        float up = Mathf.Sqrt(2f * gravity * Mathf.Max(rise, 0.01f));

        currentVelocity.y = Mathf.Min(up, profile.mantleMaxUpSpeed);

        // Guarantee a minimum approach toward the ledge WITHOUT stacking on top of speed already
        // carried in. Pressed flat against the face your into-wall component has been clipped to
        // nothing, and a plain add would leave a fast runner overshooting the surface entirely.
        Vector3 toLedge = -ledge.WallNormal;
        Vector3 flat = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float approach = Vector3.Dot(flat, toLedge);

        if (approach < profile.mantleForwardSpeed)
            flat += toLedge * (profile.mantleForwardSpeed - approach);

        currentVelocity.x = flat.x;
        currentVelocity.z = flat.z;

        // The lip is above us, so the snap would drag us straight back off it.
        snapSuppressedUntil = Time.time + profile.snapSuppressAfterJump;
        mantleBlockedUntil = Time.time + profile.mantleCooldown;

        mantlesUsed++;
        RefillOnMantle();

        // Not cuttable. Releasing the key mid-arc on a computed launch would land you short of the
        // very ledge the arc was solved for.
        jumpCutApplied = true;

        lastJumpPressTime = Mathf.NegativeInfinity;

        string trigger = !string.IsNullOrEmpty(mantleTriggerParam) ? mantleTriggerParam : jumpTriggerParam;
        if (animationProvider != null && !string.IsNullOrEmpty(trigger))
            animationProvider.SetTrigger(trigger);

        return true;
    }

    /// <summary>
    /// One buffered press, three things it might mean, tried strongest-claim first.
    ///
    /// Ground before wall before air is not arbitrary. Standing next to a wall must give the
    /// ordinary jump, or a player hugging a corner silently loses their normal one; and a wall
    /// within reach should be preferred to spending the air jump, because the wall is free and
    /// the air jump is a budget.
    /// </summary>
    void TryJump(bool grounded)
    {
        if (Time.time > lastJumpPressTime + profile.jumpBufferTime) return;

        // Mantle first, and it is not one of the three. It spends no budget, so it cannot be the
        // thing that leaves you without a jump — and a ledge in front of your face is almost
        // always what the press meant.
        if (TryMantle())
        {
            RaiseMoveAction(MoveAction.Mantle, rootTransform.position, Vector3.up);
            return;
        }

        if (TryGroundJump(grounded))
        {
            RaiseMoveAction(MoveAction.GroundJump, rootTransform.position, Vector3.up);
            return;
        }

        if (TryWallJump())
        {
            RaiseMoveAction(MoveAction.WallJump, kickPoint, kickNormal);
            return;
        }

        if (TryAirJump()) RaiseMoveAction(MoveAction.AirJump, rootTransform.position, Vector3.up);
    }

    // Presentation hook, raised once per resolved jump press. Movement never learns who listens.
    // Point and normal are the wall contact for a wall jump, the feet and up for everything else.
    public event System.Action<MoveAction, Vector3, Vector3> OnMoveAction;

    Vector3 kickPoint;
    Vector3 kickNormal;

    void RaiseMoveAction(MoveAction action, Vector3 point, Vector3 normal)
    {
        OnMoveAction?.Invoke(action, point, normal);
    }

    bool TryGroundJump(bool grounded)
    {
        if (!grounded && Time.time > lastGroundedTime + profile.coyoteTime) return false;

        currentVelocity.y = profile.jumpSpeed;

        // Spent, or the coyote window hands out a second jump from the same ledge.
        lastGroundedTime = Mathf.NegativeInfinity;

        ConsumeJump();
        return true;
    }

    /// <summary>
    /// Kick off a nearby wall: up, away along its normal, and a little along the wall toward wherever
    /// the camera is pointing.
    ///
    /// Vertical is ASSIGNED and horizontal is ADDED. Assigning vertical makes the kick a fixed
    /// height whether you were still rising or already falling. Adding horizontal keeps whatever
    /// you carried in — the motor has already clipped the component heading into the wall, so what
    /// survives is along-wall speed, and a wall jump should reward arriving fast.
    /// </summary>
    bool TryWallJump()
    {
        if (assistant == null || profile.wallJumps <= 0) return false;
        if (wallJumpsUsed >= profile.wallJumps) return false;

        ParkourContact wall = NearestWall();
        if (!wall.Detected) return false;

        Vector3 away = new Vector3(wall.Normal.x, 0f, wall.Normal.z);
        if (away.sqrMagnitude < 0.0001f) return false;
        away.Normalize();

        // Camera forward flattened onto the wall plane. Raw camera forward would point into the
        // wall whenever the player looks at what they are kicking off, and cancel the push.
        Vector3 along = cameraForward - away * Vector3.Dot(cameraForward, away);
        if (along.sqrMagnitude > 0.0001f) along.Normalize();
        else along = Vector3.zero;

        // Two flavours of the same move, chosen by how square the player is to the wall.
        //
        // Facing it head on, being shoved backwards is the opposite of the intent — you walked up
        // to it to go UP it. So the kick trades its away-push for height and becomes a climb.
        //
        // Note the along-wall term needs no special case: `along` is camera forward with the
        // into-wall component removed, and staring straight at a wall makes camera forward equal
        // to -away exactly, so it already computes to zero. Squaring up has always killed the
        // forward part on its own — the away push was the only thing fighting the player.
        Vector3 facing = Vector3.ProjectOnPlane(rootTransform.forward, Vector3.up);
        bool climbing = facing.sqrMagnitude > 0.0001f
                     && Vector3.Angle(facing, -away) <= profile.wallJumpClimbAngle;

        float upSpeed = climbing ? profile.wallJumpClimbUpSpeed : profile.wallJumpUpSpeed;
        float awaySpeed = climbing ? profile.wallJumpClimbAwaySpeed : profile.wallJumpAwaySpeed;
        float alongSpeed = climbing ? 0f : profile.wallJumpForwardSpeed;

        currentVelocity.y = upSpeed;
        currentVelocity += away * awaySpeed + along * alongSpeed;

        kickPoint = wall.Point;
        kickNormal = wall.Normal;

        wallJumpsUsed++;
        RefillOnWallJump();

        ConsumeJump();
        return true;
    }

    /// <summary>
    /// The double jump. Budget is the profile's base plus one if DoubleJumpGranted is raised, so
    /// the move works today and still reads the grant channel statuses and abilities will use.
    /// </summary>
    bool TryAirJump()
    {
        if (airJumpsUsed >= AirJumpBudget()) return false;

        currentVelocity.y = profile.airJumpSpeed;

        airJumpsUsed++;
        RefillOnAirJump();

        ConsumeJump();
        return true;
    }

    // ── Refills ───────────────────────────────────────────────────────────────────────────
    //
    // Each move declares what refills IT, and each of these three says what one move gives back
    // to the others. Written the same way three times on purpose: the alternative is a matrix or
    // a loop over move types, and neither survives contact with a fourth move as readably as
    // adding one more line to one of these.
    //
    // A move never refills itself — its own counter increments before these run — so a budget of
    // one is always a budget of one however the toggles are set.
    //
    // ⚠ These form a graph, and the graph can cycle. Any two moves that refill each other are an
    // unbounded loop as long as the geometry keeps offering them: wall+air climbs any wall, and
    // mantle+anything climbs any stack of ledges. That is a design choice, not a bug — but it is
    // the choice being made, and it cannot be seen from any single checkbox.

    void RefillOnWallJump()
    {
        if (profile.airJumpRefillOnWallJump) airJumpsUsed = 0;
        if (profile.mantleRefillOnWallJump) mantlesUsed = 0;
    }

    void RefillOnAirJump()
    {
        if (profile.wallJumpRefillOnAirJump) wallJumpsUsed = 0;
        if (profile.mantleRefillOnAirJump) mantlesUsed = 0;
    }

    void RefillOnMantle()
    {
        if (profile.airJumpRefillOnMantle) airJumpsUsed = 0;
        if (profile.wallJumpRefillOnMantle) wallJumpsUsed = 0;
    }

    int AirJumpBudget()
    {
        Blackboard blackboard = movementSystem.Brain != null ? movementSystem.Brain.Blackboard : null;
        bool granted = blackboard != null && blackboard.GetBool(BlackboardKey.DoubleJumpGranted);

        return profile.airJumps + (granted ? 1 : 0);
    }

    /// <summary>
    /// Budgets left this airtime, and whether a wall is close enough to kick. For the overlay —
    /// all three are otherwise completely invisible while testing, which makes a wall jump that
    /// silently found no wall indistinguishable from one that found a wall and did nothing.
    /// </summary>
    public int AirJumpsLeft => profile == null ? 0 : Mathf.Max(AirJumpBudget() - airJumpsUsed, 0);
    public int WallJumpsLeft => profile == null ? 0 : Mathf.Max(profile.wallJumps - wallJumpsUsed, 0);
    public int MantlesLeft => profile == null ? 0 : Mathf.Max(profile.mantles - mantlesUsed, 0);
    public bool WallInReach => assistant != null && NearestWall().Detected;

    /// <summary>
    /// Would a wall jump right now be the CLIMB variant rather than the kick? Mirrors the test in
    /// TryWallJump. Worth surfacing: the climb cone is a feel number, and the only way to tune it
    /// is to stand at the edge of it and see which side you are on.
    /// </summary>
    public bool WallClimbInReach
    {
        get
        {
            if (assistant == null || profile == null) return false;

            ParkourContact wall = NearestWall();
            if (!wall.Detected) return false;

            Vector3 away = new Vector3(wall.Normal.x, 0f, wall.Normal.z);
            Vector3 facing = Vector3.ProjectOnPlane(rootTransform.forward, Vector3.up);
            if (away.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f) return false;

            return Vector3.Angle(facing, -away.normalized) <= profile.wallJumpClimbAngle;
        }
    }

    /// <summary>Closest wall the sensor can see. Ties do not matter; anything in reach will do.</summary>
    ParkourContact NearestWall()
    {
        ParkourContact best = default;
        float nearest = Mathf.Infinity;

        for (int i = 0; i < assistant.Walls.Count; i++)
        {
            ParkourContact wall = assistant.Walls[i];
            if (!wall.Detected || wall.Distance >= nearest) continue;

            nearest = wall.Distance;
            best = wall;
        }

        return best;
    }

    /// <summary>Everything every jump does regardless of what it pushed off.</summary>
    void ConsumeJump()
    {
        lastJumpPressTime = Mathf.NegativeInfinity;

        // Ground snapping would otherwise drag the player back onto the floor they just left.
        snapSuppressedUntil = Time.time + profile.snapSuppressAfterJump;
        jumpCutApplied = false;

        if (animationProvider != null && !string.IsNullOrEmpty(jumpTriggerParam))
            animationProvider.SetTrigger(jumpTriggerParam);
    }

    /// <summary>
    /// The body faces the camera, and nothing else is allowed to turn it.
    ///
    /// Never velocity: collide-and-slide clips velocity along walls, so a velocity-facing body
    /// swings parallel to any wall it touches and the level ends up steering the player. It also
    /// breaks the wall sensor, which derives left/right from Dot(toContact, body.right) — a head-on
    /// approach would classify as a side contact within a frame or two.
    ///
    /// Handed to the motor rather than assigned here: the Rigidbody is the only thing that may
    /// write this transform, and assigning rotation directly discards the queued MovePosition.
    /// </summary>
    Quaternion ResolveFacing(float dt)
    {
        if (movementSystem.FacingLocked) return rootTransform.rotation;
        if (profile.holdFacingWhileStationary && IsStationary()) return rootTransform.rotation;

        return Quaternion.RotateTowards(
            rootTransform.rotation,
            Quaternion.LookRotation(cameraForward),
            profile.cameraTurnDegreesPerSecond * dt);
    }

    bool IsStationary()
    {
        if (!motor.IsGrounded) return false;
        if (moveInput.sqrMagnitude > 0.01f) return false;
        return new Vector3(currentVelocity.x, 0f, currentVelocity.z).sqrMagnitude < 0.01f;
    }

    /// <summary>
    /// Yaw the MODEL toward where the player is travelling while the body keeps facing the camera.
    ///
    /// Above walk speed the animator plays a single forward clip with no strafe blending, so the
    /// lean carries the direction information StrafeX used to. It fades in as the strafe tree fades
    /// out; overlap them and the same turn gets described twice.
    ///
    /// The MODEL only. Leaning the body would rotate `body.right`, which the wall sensor reads.
    /// </summary>
    void UpdateModelLean(Vector3 wishDir, float dt)
    {
        Transform pivot = ResolveLeanPivot();
        if (pivot == null) return;

        // A fresh instance brings its own authored rotation, which the lean is applied on top of —
        // plenty of rigs sit at a non-identity yaw and overwriting it turns the character sideways.
        if (pivot != modelRoot)
        {
            modelRoot = pivot;
            modelBaseRotation = pivot.localRotation;
            modelBasePosition = pivot.localPosition;
            modelLean = 0f;
            stepSmooth = Vector3.zero;
        }

        modelLean = Mathf.MoveTowardsAngle(modelLean, ResolveLeanAngle(wishDir), profile.modelLeanDegreesPerSecond * dt);
        modelRoot.localRotation = Quaternion.Euler(0f, modelLean, 0f) * modelBaseRotation;
    }

    /// <summary>
    /// Ease the model back up after a step-up, so a stair reads as a stride instead of a teleport.
    ///
    /// Runs per FRAME rather than per physics step, because this is the thing being looked at —
    /// decaying it at 50 Hz would trade one visible stair-step for another.
    ///
    /// Purely cosmetic. The body, the capsule, grounding and velocity are all already where they
    /// should be; only the mesh lags. That is deliberate — a step-up MUST resolve collision in one
    /// step, and anything that spreads the real motion over several frames puts the collider
    /// somewhere the physics did not agree to.
    /// </summary>
    void LateUpdate()
    {
        if (modelRoot == null || profile == null) return;

        if (profile.stepSmoothSpeed <= 0f) stepSmooth = Vector3.zero;
        else stepSmooth = Vector3.Lerp(stepSmooth, Vector3.zero, 1f - Mathf.Exp(-profile.stepSmoothSpeed * Time.deltaTime));

        // Snap the tail to zero. An exponential never quite arrives, and a permanent millimetre of
        // offset is a permanent millimetre of wrong.
        if (stepSmooth.sqrMagnitude < 0.000001f) stepSmooth = Vector3.zero;

        // InverseTransformDirection, not a raw subtraction: localPosition is expressed in the
        // ROOT's space and the root yaws to face the camera, so a world offset written straight
        // into it would swing around the character as they turned.
        modelRoot.localPosition = modelBasePosition - rootTransform.InverseTransformDirection(stepSmooth);
    }

    /// <summary>
    /// The model instance ModelModule is currently showing, resolved every step because SwapModel
    /// destroys the instance and builds a new one — a cached transform survives as a destroyed
    /// reference and the lean stops silently the first time the character changes model.
    ///
    /// Never the entity root. Writing the root's transform assigns the interpolated Rigidbody's
    /// pose directly and discards that step's MovePosition, which is stutter, not rotation.
    /// Brain.ModelRoot is not trusted for exactly that reason — it falls back to the entity root
    /// when it cannot find a child by name.
    /// </summary>
    Transform ResolveLeanPivot()
    {
        if (modelModule == null || modelModule.CurrentModel == null) return null;

        Transform model = modelModule.CurrentModel.transform;
        return model == rootTransform ? null : model;
    }

    float ResolveLeanAngle(Vector3 wishDir)
    {
        if (profile.modelLeanMaxAngle <= 0f) return 0f;
        if (wishDir.sqrMagnitude < 0.01f) return 0f;

        // Never in the air: an air strafe is a sideways shift, not a turn, so the character stays
        // pointed where they are going to land while the trajectory slides across.
        if (!motor.IsGrounded) return 0f;

        // Gated on commitment as well as speed. Speed alone is not enough — the moment a running
        // player switches to a pure sidestep their velocity is still high while friction bleeds it,
        // and the model would swing to full deflection during exactly the case that should face
        // forward and let the strafe animation do the talking.
        float blend = forwardCommitment * Mathf.InverseLerp(profile.walkSpeed, profile.runSpeed, HorizontalSpeed);
        if (blend <= 0f) return 0f;

        float angle = Vector3.SignedAngle(cameraForward, wishDir, Vector3.up);
        return Mathf.Clamp(angle, -profile.modelLeanMaxAngle, profile.modelLeanMaxAngle) * blend;
    }

    /// <summary>
    /// Speed states come from MovementSystem's hysteretic facts rather than being re-derived from
    /// velocity with a second set of thresholds — that is how two systems end up disagreeing about
    /// the same moment.
    /// </summary>
    void UpdateAnimator(bool grounded)
    {
        if (animationProvider == null) return;

        float speed = HorizontalSpeed;

        animationProvider.SetFloat(movementSpeedParam, speed);
        animationProvider.SetBool(isGroundedParam, grounded);
        animationProvider.SetInteger(movementStateParam, ResolveMovementState(speed));

        // Always locked on: this controller is camera-relative, and the animator's free-movement
        // state is the one that has no strafe blending. The blend tree gets the shaped split, so
        // the legs show the trim the movement got rather than the sidestep the key implies.
        animationProvider.SetBool(isLockedOnParam, true);
        animationProvider.SetFloat(strafeXParam, strafeBlend.x);
        animationProvider.SetFloat(strafeYParam, strafeBlend.y);
    }

    int ResolveMovementState(float speed)
    {
        // 4 extends the existing int rather than adding a parameter. The Locomotion tree blends
        // on MovementSpeed, so nothing reads it yet — the animator needs a transition authored on
        // MovementState before a crouch clip will play.
        if (IsCrouching) return 4;
        if (speed < 0.1f) return 0;
        if (movementSystem.IsSprinting) return 3;
        if (movementSystem.IsRunning) return 2;
        return 1;
    }

    /// <summary>
    /// Spend everything ApplyImpulse collected since the last physics step.
    ///
    /// It runs AFTER gravity, and after TryJump. After gravity because the grounded branch would
    /// otherwise flatten any outward component against the ground normal in the same step, pinning
    /// a launch to the floor for one frame. After TryJump because that ASSIGNS currentVelocity.y
    /// and would erase the vertical half of an impulse arriving on the same step.
    ///
    /// Impulses ADD rather than replace, because this controller keeps what you earn — a knockback
    /// taken at speed should carry the speed. An ability wanting a fixed distance regardless of
    /// approach has to zero the velocity itself, deliberately.
    /// </summary>
    void DrainImpulses()
    {
        if (pendingImpulse.sqrMagnitude < 0.000001f) return;

        // Only an impulse pushing AWAY from the surface needs snapping held off, and only that one
        // should pay for it. A flat shove has nothing for the snap to drag back, so suppressing it
        // there just lets the capsule step off a stair nose or a slope crest and hop for the
        // duration. Measured before the add, because the add is what we are deciding about.
        bool leavingGround = !IsGrounded || Vector3.Dot(pendingImpulse, motor.GroundNormal) > 0.01f;

        currentVelocity += pendingImpulse;
        pendingImpulse = Vector3.zero;

        if (leavingGround)
            snapSuppressedUntil = Time.time + profile.snapSuppressAfterJump;
    }

    /// <summary>
    /// The last word on this step's velocity, and a safety net rather than a design limit.
    ///
    /// Nothing else in this controller bounds total speed — that is the core law, and it is right
    /// for speed the player EARNED. But the impulse channel adds without asking, air has no
    /// friction at all, and Accelerate cannot take speed away above wishSpeed, so anything that
    /// pushes repeatedly while airborne compounds with no sink. A dash chained into jumps was the
    /// first thing to find that; it will not be the last.
    ///
    /// The ceilings exist because the MOTOR stops being correct above them, not because fast is
    /// unfun: past ~17.5 m/s ground snapping cannot follow terrain between steps and the sweep
    /// starts skipping. Set them high enough that normal play never touches them.
    ///
    /// Falling is deliberately unbounded. Gravity is not a runaway source, and a terminal velocity
    /// is a feel decision that belongs in the gravity section if it is wanted at all.
    /// </summary>
    void ClampSpeed()
    {
        ClampHorizontal();
        ClampRise();
    }

    void ClampHorizontal()
    {
        if (profile.maxHorizontalSpeed <= 0f) return;

        Vector3 flat = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float speed = flat.magnitude;
        if (speed <= profile.maxHorizontalSpeed) return;

        flat *= profile.maxHorizontalSpeed / speed;
        currentVelocity.x = flat.x;
        currentVelocity.z = flat.z;

        WarnClamped($"horizontal speed hit the {profile.maxHorizontalSpeed:F0} m/s ceiling ({speed:F1})");
    }

    void ClampRise()
    {
        if (profile.maxRiseSpeed <= 0f) return;
        if (currentVelocity.y <= profile.maxRiseSpeed) return;

        WarnClamped($"rise speed hit the {profile.maxRiseSpeed:F0} m/s ceiling ({currentVelocity.y:F1})");
        currentVelocity.y = profile.maxRiseSpeed;
    }

    /// <summary>
    /// Once per session, not once per frame. A clamp that fires fires every step while it holds,
    /// and a warning that floods the console is a warning nobody reads.
    /// </summary>
    void WarnClamped(string detail)
    {
        if (warnedSpeedClamped) return;

        warnedSpeedClamped = true;
        Debug.LogWarning($"[{GetType().Name}] Safety clamp engaged — {detail}. This is a guard " +
                         $"against unbounded momentum, not a tuning value: something is adding " +
                         $"speed faster than anything removes it. Check impulse speed caps.");
    }

    /// <summary>
    /// Additive impulse channel — knockback, launches, air jumps, anything that adds to momentum
    /// rather than replacing it. This is the design doc's IntegrateExternal, and what abilities
    /// push through.
    ///
    /// ⚠ It QUEUES rather than writing velocity, because abilities fire from Update and this
    /// handler integrates in FixedUpdate. Writing straight to currentVelocity means two Update
    /// frames between physics steps apply the impulse twice, and one arriving just after a step
    /// gets a full extra frame of gravity before anything consumes it. Neither is reproducible,
    /// which is the worst kind of movement bug to chase.
    /// </summary>
    public override void ApplyImpulse(Vector3 direction, float force)
    {
        pendingImpulse += direction.normalized * force;
    }

    public override void TeleportTo(Vector3 position)
    {
        if (motor == null) return;

        motor.Teleport(position);
        currentVelocity = Vector3.zero;
        pendingImpulse = Vector3.zero;
        frictionSuppressedUntil = 0f;
    }

    /// <summary>
    /// Hold ground friction off for a window, so an impulse survives long enough to be a dash.
    ///
    /// Takes the LATER of the two deadlines rather than overwriting, or a short holiday landing on
    /// top of a long one would cut the long one short — the opposite of what stacking two mobility
    /// effects should do.
    /// </summary>
    public override void SuppressFriction(float seconds)
    {
        if (seconds <= 0f) return;

        frictionSuppressedUntil = Mathf.Max(frictionSuppressedUntil, Time.time + seconds);
    }

    /// <summary>Is ground friction currently held off, and for how much longer. For the overlay.</summary>
    public bool FrictionSuppressed => Time.time < frictionSuppressedUntil;
    public float FrictionHolidayRemaining => Mathf.Max(frictionSuppressedUntil - Time.time, 0f);
}
