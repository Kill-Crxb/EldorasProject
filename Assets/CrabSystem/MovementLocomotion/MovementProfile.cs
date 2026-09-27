using UnityEngine;

/// <summary>
/// Every tunable number the parkour controller reads, in an asset rather than on a prefab.
///
/// One asset can be shared by the test prefab, the real player and any future entity, so a feel
/// change is made once instead of once per prefab. At slice 3 these stay the BASE values and
/// `mov.*` stat contributions are added on top at the point of use — nothing here is cached.
/// </summary>
[CreateAssetMenu(fileName = "MovementProfile", menuName = "Crab/Movement/Movement Profile")]
public class MovementProfile : ScriptableObject
{
    [Header("Wish Speed")]
    [Tooltip("Default speed. Every one of these is a REQUEST, not a ceiling — velocity earned " +
             "above it is kept, because only acceleration is clamped.")]
    public float walkSpeed = 2.5f;

    [Tooltip("Speed in the run gait, which the gait toggle key selects.")]
    public float runSpeed = 6f;

    [Tooltip("Speed the run gait is served at while the SprintGranted blackboard tag is up. " +
             "Not selectable by the player — something has to grant it.")]
    public float sprintSpeed = 9f;

    [Header("Gait")]
    [Tooltip("Start in walk rather than run. The gait toggle key swaps between the two; sprint is " +
             "not a gait — it is granted through the blackboard and upgrades run when raised.")]
    public bool startInWalkMode = true;

    [Header("Acceleration")]
    public float groundAccel = 12f;
    public float airAccel = 14f;

    [Tooltip("Wish speed is clamped to this while airborne, which is the only thing that changes " +
             "about acceleration when the player leaves the ground. Governs whether air input can " +
             "add SPEED; steering and sideways drift are separate mechanisms below.")]
    public float airSpeedCap = 2.5f;

    [Header("Speed Limit")]
    [Tooltip("Hard ceiling on horizontal speed, m/s. Zero disables it.\n\n" +
             "⚠ This is a CORRECTNESS guard, not a feel dial — set it well above anything normal " +
             "play reaches so it never shapes the game. Past roughly 17.5 m/s (snapDistance 0.35 " +
             "at 50 Hz) ground snapping can no longer follow terrain between physics steps, and " +
             "maxSlideIterations starts running out on complex geometry; the symptom is the " +
             "character skipping off stairs and slope crests.\n\n" +
             "If this ever fires in play, something is feeding unbounded speed — it logs once so " +
             "you find out rather than quietly capping forever.")]
    public float maxHorizontalSpeed = 22f;

    [Tooltip("Hard ceiling on UPWARD speed, m/s. Zero disables it. Falling is not limited — " +
             "terminal velocity is a separate idea and gravity is not the thing that runs away.\n\n" +
             "Same purpose as the horizontal cap: a diagonal impulse repeated faster than gravity " +
             "can bleed it climbs without bound. Must sit above every intentional launch — jump 8, " +
             "wall climb 9, and a solved mantle up to mantleMaxUpSpeed.")]
    public float maxRiseSpeed = 18f;

    [Header("Friction")]
    [Tooltip("Ground friction coefficient. Higher stops faster and also turns faster.")]
    public float groundFriction = 6f;

    [Tooltip("Friction applied ONLY while moving faster than the current gait. Speed above the " +
             "gait is earned — a dash, a drop, a launch, a knockback — and at the normal " +
             "coefficient it evaporates in about a quarter of a second, which makes every " +
             "momentum source feel pointless.\n\n" +
             "Lower keeps earned speed longer. 1.2 gives roughly 6-7 m of running before a fast " +
             "landing settles back to gait; 0.8 gives about 10 m; the normal value would give 1.6 m.\n\n" +
             "Set this EQUAL to Ground Friction to restore the single-regime behaviour exactly.")]
    public float momentumFriction = 1.2f;

    [Tooltip("Floor on the friction drop, so slow movement bleeds off in finite time instead of " +
             "halving forever and never quite reaching zero.")]
    public float stopSpeed = 1.5f;

    [Tooltip("Seconds of no friction after landing. Without it, chaining a landing into the next " +
             "move is punished rather than rewarded.")]
    public float landingFrictionGrace = 0.08f;

    [Header("Gravity")]
    [Tooltip("Downward acceleration, as a POSITIVE magnitude.")]
    public float gravity = 25f;

    [Tooltip("Gravity multiplier while falling, or while rising after jump was released. Heavier " +
             "down than up is most of what makes a jump arc feel authored.")]
    public float fallGravityMultiplier = 1.6f;

    [Tooltip("Small bias held while grounded so the capsule stays pressed into the surface. " +
             "Applied ALONG THE GROUND NORMAL, not along world down — a straight-down bias is " +
             "not perpendicular to a tilted plane, and what survives collide-and-slide is a " +
             "downhill component that slides you off everything.")]
    public float groundedGravity = -2f;

    [Header("Jump")]
    public float jumpSpeed = 8f;

    [Tooltip("Rising velocity is multiplied by this the moment jump is released. The other half of " +
             "variable jump height.")]
    [Range(0f, 1f)] public float jumpCutMultiplier = 0.45f;

    [Tooltip("Grace period after walking off a ledge during which a jump still counts.")]
    public float coyoteTime = 0.12f;

    [Tooltip("How early a jump press is remembered so it fires the instant you land.")]
    public float jumpBufferTime = 0.12f;

    [Tooltip("Ground snapping is suppressed for this long after a jump, or the snap drags the " +
             "player straight back onto the floor they just left.")]
    public float snapSuppressAfterJump = 0.1f;

    [Header("Air Jump")]
    [Tooltip("Airborne jumps allowed per airtime, BEFORE any grant. Reset on landing.\n\n" +
             "This is a base budget so the move is testable today; the DoubleJumpGranted " +
             "blackboard fact adds one on top, which is the channel statuses and abilities use. " +
             "Set to 0 once something actually raises that fact and the double jump becomes a " +
             "granted capability rather than a default one.")]
    public int airJumps = 1;

    [Tooltip("Upward speed an air jump ASSIGNS. Assigned, not added, so a double jump out of a " +
             "long fall gives the same height as one off the apex instead of being swallowed by " +
             "downward velocity.")]
    public float airJumpSpeed = 7f;

    [Tooltip("A wall jump gives the air jump budget back.")]
    public bool airJumpRefillOnWallJump = false;

    [Tooltip("A mantle gives the air jump budget back.")]
    public bool airJumpRefillOnMantle = false;

    [Header("Wall Jump")]
    [Tooltip("Wall jumps allowed per airtime, reset on landing.\n\n" +
             "⚠ This is the only thing stopping a flat wall being climbed forever. 1 gives a " +
             "single kick off. Raise it for corridor chaining between opposite walls — but at 2+ " +
             "a single wall can also be climbed, so if you want chaining without climbing, the " +
             "rule you want is 'the next wall must face differently', not a bigger number.")]
    public int wallJumps = 1;

    [Tooltip("An air jump gives the wall jump budget back.")]
    public bool wallJumpRefillOnAirJump = false;

    [Tooltip("A mantle gives the wall jump budget back.")]
    public bool wallJumpRefillOnMantle = false;

    [Tooltip("Upward speed a wall jump ASSIGNS. Usually a little under jumpSpeed — a wall kick " +
             "that out-climbs a standing jump reads as a ladder.")]
    public float wallJumpUpSpeed = 7f;

    [Tooltip("Speed pushed ALONG THE WALL NORMAL, away from the surface. This is what makes it a " +
             "kick rather than a hop. Added to existing horizontal velocity, so speed carried " +
             "into the wall is kept.")]
    public float wallJumpAwaySpeed = 5f;

    [Tooltip("Speed added along the wall, in the direction the camera is looking.\n\n" +
             "Measured on the wall PLANE, not raw camera forward: looking straight at the wall " +
             "would otherwise fight the push-off. Facing the wall square gives nothing here and " +
             "the kick is purely up and away, which is what that camera angle should mean.")]
    public float wallJumpForwardSpeed = 2f;

    [Tooltip("Within this many degrees of facing the wall head on, the wall jump becomes a CLIMB " +
             "instead of a kick: mostly straight up, barely any push away, no along-wall travel.\n\n" +
             "Walking up to a wall and looking at it means you want to go up it, not be thrown " +
             "backwards off it. Outside this cone you are passing the wall rather than addressing " +
             "it, and the normal kick is right.\n\n" +
             "Zero disables the climb variant entirely; 180 makes every wall jump a climb.")]
    [Range(0f, 180f)] public float wallJumpClimbAngle = 35f;

    [Tooltip("Upward speed of the climb variant. Higher than the kick — all the energy the kick " +
             "spends pushing away goes into height instead.")]
    public float wallJumpClimbUpSpeed = 9f;

    [Tooltip("Push away from the wall during a climb. Small, not zero: a little clearance stops " +
             "the capsule grinding up the face, and leaves room to reach a ledge just above.")]
    public float wallJumpClimbAwaySpeed = 1f;

    [Header("Mantle")]
    [Tooltip("Turn the move off without touching the sensor.")]
    public bool mantleEnabled = true;

    [Tooltip("Mantles allowed per airtime, reset on landing. Budgeted the same way the two jumps " +
             "are, so a run of ledges costs something rather than being free.\n\n" +
             "Note that landing ON the ledge you just mantled refills this immediately — the cost " +
             "only bites when you mantle, miss, and reach for another one.")]
    public int mantles = 1;

    [Tooltip("A wall jump gives the mantle budget back.")]
    public bool mantleRefillOnWallJump = false;

    [Tooltip("An air jump gives the mantle budget back.")]
    public bool mantleRefillOnAirJump = false;

    [Tooltip("Lowest edge worth mantling, above the feet. Below this the motor's step-up already " +
             "walks you up, and a launch would turn every kerb into a hop.")]
    public float mantleMinHeight = 0.9f;

    [Tooltip("Highest edge the character can pull themselves over. Above this the press falls " +
             "through to a normal jump instead.")]
    public float mantleMaxHeight = 2f;

    [Tooltip("Extra height cleared above the lip, in metres. The launch speed is SOLVED from the " +
             "ledge height, and this is the safety margin baked into that solve — too little and " +
             "a mantle clips the edge it was aimed at.")]
    public float mantleClearance = 0.35f;

    [Tooltip("Minimum speed toward the ledge the launch guarantees. Not added on top of speed you " +
             "carried in — topped UP to, so a sprinting mantle does not overshoot the surface.")]
    public float mantleForwardSpeed = 4f;

    [Tooltip("Ceiling on the solved launch speed. A mis-read ledge — a sliver of geometry, a " +
             "collider seam — should be a disappointing mantle, never a catapult.")]
    public float mantleMaxUpSpeed = 12f;

    [Tooltip("Seconds before another mantle may fire. Stops a held jump re-mantling every frame " +
             "while the sensor can still see the edge you are climbing.")]
    public float mantleCooldown = 0.3f;


    [Header("Crouch")]
    [Tooltip("Wish speed while crouched. Also the speed a slide decays to before it hands over " +
             "to a crouch, so these are deliberately the same number — a slide ends when it is " +
             "no longer faster than walking would have been.")]
    public float crouchSpeed = 1.8f;

    [Tooltip("Capsule height while crouched or sliding. The capsule's UNDERSIDE is held fixed, " +
             "so grounding is unaffected by the resize — only the head comes down.")]
    public float crouchHeight = 1f;

    [Tooltip("Metres per second the capsule changes height. Instant looks like a pop and can " +
             "shove the character through a ceiling on the way back up.")]
    public float crouchResizeSpeed = 6f;

    [Header("Facing")]
    [Tooltip("Degrees per second the body tracks the camera. High — the mouse is the primary way " +
             "to turn, and lag here reads as input latency rather than weight.")]
    public float cameraTurnDegreesPerSecond = 1440f;

    [Tooltip("While grounded with no move input and no speed, the body holds its rotation and the " +
             "head look takes the horizontal camera turn instead. Moving turns the body back to the camera.")]
    public bool holdFacingWhileStationary = true;

    [Header("Step Smoothing")]
    [Tooltip("How fast the model catches back up after a step-up, exponential, per second.\n\n" +
             "A step-up moves the body instantly — it has to, collision resolves in one physics " +
             "step — so without this the character teleports up every stair. The model is left " +
             "where it was and eased up over this rate instead. Purely visual: collision, " +
             "grounding and velocity are untouched.\n\n" +
             "Zero disables it and restores the snap. Too low and the feet visibly sink into the " +
             "stair; 12-20 reads as weight rather than lag.")]
    public float stepSmoothSpeed = 16f;

    [Tooltip("Largest visual lag allowed, in metres. Running a staircase lands a step every few " +
             "frames and the offsets would otherwise stack faster than they decay until the model " +
             "is buried. Roughly one step height is plenty.")]
    public float stepSmoothMaxOffset = 0.4f;

    [Header("Model Lean")]
    [Tooltip("Largest yaw the MODEL is turned toward the direction of travel while the body keeps " +
             "facing the camera. Zero disables it. Replaces the strafe animation above walk speed, " +
             "where the animator plays a single forward clip.")]
    public float modelLeanMaxAngle = 35f;

    public float modelLeanDegreesPerSecond = 360f;

    [Header("Strafe")]
    [Tooltip("How much of a sideways input survives while the player is committed forward. Below 1 " +
             "this turns strafe into a trim — fine steering to sit alongside the mouse. Full " +
             "strength is reserved for when forward is not the intent.")]
    [Range(0f, 1f)] public float strafeBias = 0.25f;

    [Tooltip("How fast the trimmed sideways input eases in. Exponential, so it moves most of the " +
             "way immediately and settles into the rest. Higher is snappier.")]
    public float strafeRampSpeed = 8f;

    [Header("Air Control")]
    [Tooltip("Degrees per second the airborne heading may be steered toward the CAMERA, scaled by " +
             "how far forward the player is pushing. Redirection, not acceleration: the velocity " +
             "vector rotates and its length is untouched, so a turn costs and gains nothing.")]
    public float airTurnDegreesPerSecond = 90f;

    [Tooltip("Top sideways speed an air strafe shifts the player across their own heading. An " +
             "OFFSET, not a turn: hold it and the trajectory angles off, release it and it " +
             "straightens.")]
    public float airStrafeSpeed = 3f;

    [Tooltip("How fast the sideways offset builds while held, in m/s per second.")]
    public float airStrafeAccel = 12f;

    [Tooltip("How fast it bleeds back to zero once released. Lower than the build rate on purpose: " +
             "sideways speed carried INTO a jump is earned momentum, and snapping it away the " +
             "moment the player leaves the ground reads as theft.")]
    public float airStrafeReturn = 6f;
}
