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

    [Header("Friction")]
    [Tooltip("Ground friction coefficient. Higher stops faster and also turns faster.")]
    public float groundFriction = 6f;

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
