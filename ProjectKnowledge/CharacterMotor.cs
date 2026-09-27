using UnityEngine;

/// <summary>
/// Sweep-based capsule motor. Owns position, collision response and grounding — and nothing else.
///
/// No input, no gameplay, no states. It takes a velocity, moves the body as far as that velocity
/// honestly gets it, and hands back the velocity that survived the trip. Everything about how the
/// velocity was earned lives in the handler above it.
///
/// Replaces CharacterController for one disqualifying reason: CharacterController's capsule is
/// permanently Y-up and OnControllerColliderHit is the only route to a contact normal, which puts
/// wall-relative motion and a rotated capsule out of reach by construction. CapsuleCast gives a
/// normal on every hit and does not care which way the capsule points.
///
/// GROUNDING IS THE SINGLE AUTHORITY. FeetDetectionModule's trigger volume is not consulted and
/// should not be present on a prefab driven by this motor — two answers to "am I grounded" is one
/// too many.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CharacterMotor : MonoBehaviour
{
    [Header("Body")]
    [Tooltip("The capsule this motor sweeps. Its height, radius and centre are read every frame, " +
             "so resizing the collider resizes the motor.")]
    [SerializeField] private CapsuleCollider capsule;

    [Header("Collision")]
    [Tooltip("Everything the capsule sweeps against. MUST exclude the character's own layer or " +
             "every cast hits the capsule it started inside.")]
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Tooltip("Gap held between capsule and surface. Too small and the capsule tunnels into " +
             "geometry it is resting on; too large and it visibly floats.")]
    [SerializeField] private float skinWidth = 0.02f;

    [Tooltip("How many times one move may hit something and slide along it before the rest of " +
             "the motion is discarded. Five covers a corner inside a corner.")]
    [SerializeField, Range(1, 8)] private int maxSlideIterations = 5;

    [Header("Ground")]
    [Tooltip("Steepest surface the character can grip. At or below this it is ground and behaves " +
             "exactly like flat floor. Above it there is no grip at all: not grounded, no " +
             "friction, full gravity, and collide-and-slide carries the body down the face until " +
             "the surface is walkable again. This single angle IS the slope model.")]
    [SerializeField] private float maxSlopeAngle = 40f;

    [Tooltip("Degrees of slack on the grip test. Once footing is lost the surface must be this " +
             "much SHALLOWER before it counts as ground again.\n\n" +
             "Without a gap, a surface sitting on the threshold — terrain, a rock, any curved " +
             "mesh — flickers between grounded and falling every frame, and each flip fires a " +
             "ground press and a snap. Same hysteresis MovementSystem already uses for its speed " +
             "facts, and for the same reason.")]
    [SerializeField, Range(0f, 15f)] private float slopeGripHysteresis = 3f;

    [Tooltip("How far below the capsule the ground probe looks. This is the 'am I grounded' " +
             "question, kept short and honest.")]
    [SerializeField] private float groundProbeDistance = 0.08f;

    [Tooltip("How far the body is pulled back down to ground it is about to leave. This is what " +
             "stops the capsule launching off every crest and hitching on every step.")]
    [SerializeField] private float snapDistance = 0.35f;

    [Tooltip("Tallest lip the capsule walks up instead of colliding with. Zero disables stepping.")]
    [SerializeField] private float stepHeight = 0.3f;

    const float MinMoveDistance = 0.0001f;

    Rigidbody body;
    readonly Collider[] overlaps = new Collider[8];

    /// <summary>True while a surface no steeper than maxSlopeAngle sits within the ground probe.</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>Normal of whatever the ground probe last found, up when it found nothing.</summary>
    public Vector3 GroundNormal { get; private set; } = Vector3.up;

    /// <summary>Gap to that surface, infinity when the probe found nothing.</summary>
    public float GroundDistance { get; private set; } = Mathf.Infinity;

    /// <summary>
    /// World roll applied to the capsule for movement sweeps. Slice 1 leaves this upright; wall
    /// running sets it. Grounding deliberately ignores it — see UpdateGrounding.
    /// </summary>
    public Quaternion CapsuleRotation { get; set; } = Quaternion.identity;

    /// <summary>
    /// The capsule this motor actually sweeps.
    ///
    /// Exposed so ParkourAssistant can size its sensor from the same collider rather than hunting
    /// for one with GetComponentInChildren — which on a real character prefab will happily return
    /// a hurtbox capsule, or the sensor's own trigger capsule, and produce a sensor sized from the
    /// wrong thing while looking perfectly correct in the inspector.
    /// </summary>
    public CapsuleCollider Capsule => capsule;

    /// <summary>
    /// Would a capsule of this height, centred at this local height, fit here without overlapping
    /// the world?
    ///
    /// Crouch asks before standing up: under a ledge the answer is no, and growing the capsule
    /// anyway resolves as a depenetration shove — through the ceiling, or down through the floor.
    /// Probed from the UPRIGHT reference for the same reason UpdateGrounding is.
    /// </summary>
    public bool Fits(float height, float centreY)
    {
        if (capsule == null) return true;

        float half = Mathf.Max(height * 0.5f - capsule.radius, 0f);
        Vector3 centre = body.position + Vector3.up * centreY;

        return !Physics.CheckCapsule(centre + Vector3.up * half, centre - Vector3.up * half,
                                     capsule.radius, collisionLayers, QueryTriggerInteraction.Ignore);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();

        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();

        if (capsule == null)
        {
            Debug.LogError("[CharacterMotor] No CapsuleCollider assigned or found — motor disabled.");
            enabled = false;
            return;
        }

        body.isKinematic = true;
        body.useGravity = false;

        // Interpolation is not optional at speed: without it the body visibly steps once per
        // physics tick. Kinematic bodies only support the speculative sweep, so asking for plain
        // Continuous here would be rejected with a warning.
        if (body.interpolation == RigidbodyInterpolation.None)
            body.interpolation = RigidbodyInterpolation.Interpolate;

        // Speculative is the only continuous mode a kinematic body supports; the others are
        // rejected with a warning, so normalise rather than trusting whatever the prefab says.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Every sweep starts inside this character's own capsule. If its layer is in the mask,
        // the first cast in any direction hits itself at distance zero, the move is clipped to
        // nothing, and grounding reads whatever normal the degenerate hit invents — the character
        // stands still playing a run animation. Strip it rather than trusting the inspector.
        int ownLayer = 1 << gameObject.layer;

        if ((collisionLayers.value & ownLayer) != 0)
        {
            collisionLayers.value &= ~ownLayer;
            Debug.LogWarning($"[CharacterMotor] Removed layer {LayerMask.LayerToName(gameObject.layer)} " +
                             "from Collision Layers — a character cannot sweep against itself.");
        }

        // An enabled CharacterController owns this transform. It re-asserts its own position after
        // every Rigidbody.MovePosition, so the motor computes a correct move, writes it, and has
        // it silently undone — which reads as a character that plays the run animation on the spot
        // and only drifts on the frames the controller happens not to fight. Turning it off is the
        // whole point of replacing it, so do it here rather than leaving it as a checkbox to miss.
        CharacterController legacy = GetComponent<CharacterController>();

        if (legacy != null && legacy.enabled)
        {
            legacy.enabled = false;
            Debug.LogWarning("[CharacterMotor] Disabled the CharacterController on this object — " +
                             "it and the motor cannot both own the transform.");
        }

        UpdateGrounding(body.position);
    }

    /// <summary>
    /// Move by `velocity` for `dt`, returning the velocity that survived collision.
    ///
    /// Velocity is clipped against every plane the sweep hits, so running into a wall costs you
    /// the component INTO the wall and nothing else — the along-wall component is kept, which is
    /// what makes a controller feel like it slides rather than sticks.
    ///
    /// Grounding is refreshed at the end, so IsGrounded read at the top of the next step is
    /// current without probing twice.
    /// </summary>
    public Vector3 Move(Vector3 velocity, float dt, bool allowGroundSnap, Quaternion rotation)
    {
        bool wasGrounded = IsGrounded;
        Vector3 position = body.position;
        Vector3 remaining = velocity * dt;

        for (int i = 0; i < maxSlideIterations; i++)
        {
            float distance = remaining.magnitude;
            if (distance < MinMoveDistance) break;

            Vector3 direction = remaining / distance;

            if (!Cast(position, CapsuleRotation, direction, distance + skinWidth, out RaycastHit hit))
            {
                position += remaining;
                break;
            }

            float travel = Mathf.Max(hit.distance - skinWidth, 0f);
            position += direction * travel;
            remaining -= direction * travel;

            if (wasGrounded && TryStepUp(ref position, ref remaining, hit)) continue;

            remaining = ClipVelocity(remaining, hit.normal);
            velocity = ClipVelocity(velocity, hit.normal);
        }

        position = Depenetrate(position);

        if (allowGroundSnap && wasGrounded && velocity.y <= 0f)
            position = SnapToGround(position);

        // Position AND rotation both go through the Rigidbody, and this is the only place either
        // is written. Assigning transform.rotation anywhere else re-syncs the body to the
        // transform and throws away the MovePosition queued here — the move is computed
        // correctly, written, and silently discarded before the physics step applies it.
        body.MovePosition(position);
        body.MoveRotation(rotation);
        UpdateGrounding(position);

        return velocity;
    }

    /// <summary>
    /// Remove the component of `velocity` heading into `normal`, keeping everything along it.
    ///
    /// The overbounce leaves the result a hair off the surface. Clipping exactly to the plane
    /// lets floating point put the next frame's start point marginally inside it, and the capsule
    /// creeps into walls it is sliding along.
    /// </summary>
    static Vector3 ClipVelocity(Vector3 velocity, Vector3 normal, float overbounce = 1.001f)
    {
        return velocity - normal * (Vector3.Dot(velocity, normal) * overbounce);
    }

    /// <summary>
    /// Place the body without sweeping. For teleports and spawns — anything that must not slide
    /// through the geometry between where it was and where it is going.
    /// </summary>
    public void Teleport(Vector3 position)
    {
        // Interpolation is dropped across the jump. Left on, the body would be smeared between
        // where it was and where it now is, and the character visibly streaks across the level.
        RigidbodyInterpolation previous = body.interpolation;

        body.interpolation = RigidbodyInterpolation.None;
        body.position = position;
        body.interpolation = previous;

        UpdateGrounding(position);
    }

    /// <summary>
    /// Downward probe, always run from the UPRIGHT reference regardless of CapsuleRotation.
    ///
    /// A rolled capsule shifts where a rolled probe would point, so a wall run would report itself
    /// falsely grounded and the wall state would exit the instant it should hold. Roll is a visual
    /// and collision concern; what counts as ground is not allowed to move with it.
    /// </summary>
    public void UpdateGrounding(Vector3 position)
    {
        if (!Cast(position, Quaternion.identity, Vector3.down, groundProbeDistance + skinWidth, out RaycastHit hit))
        {
            IsGrounded = false;
            GroundNormal = Vector3.up;
            GroundDistance = Mathf.Infinity;
            return;
        }

        GroundDistance = Mathf.Max(hit.distance - skinWidth, 0f);
        GroundNormal = hit.normal;
        // Losing grip uses the strict angle; regaining it needs a shallower one. See the field.
        float limit = IsGrounded ? maxSlopeAngle : maxSlopeAngle - slopeGripHysteresis;

        IsGrounded = Vector3.Angle(hit.normal, Vector3.up) <= limit;
    }

    /// <summary>
    /// Pull the body back down onto ground it is about to leave.
    ///
    /// Without this, cresting a hill or walking down stairs launches the capsule into a short
    /// airborne arc, which costs ground friction, ground acceleration and the grounded animation
    /// state for a few frames each time. Only walkable surfaces qualify, so this can never hold
    /// the player onto a wall.
    /// </summary>
    Vector3 SnapToGround(Vector3 position)
    {
        if (!Cast(position, Quaternion.identity, Vector3.down, snapDistance, out RaycastHit hit))
            return position;

        if (Vector3.Angle(hit.normal, Vector3.up) > maxSlopeAngle)
            return position;

        return position + Vector3.down * Mathf.Max(hit.distance - skinWidth, 0f);
    }

    /// <summary>
    /// Walk up a lip rather than colliding with it: lift by stepHeight, check the way ahead is
    /// clear, then drop back onto whatever is under the raised position.
    ///
    /// Only called against surfaces too steep to walk on — a walkable slope is already handled by
    /// clipping, and stepping up it would fight the slide.
    /// </summary>
    bool TryStepUp(ref Vector3 position, ref Vector3 remaining, RaycastHit blocking)
    {
        if (stepHeight <= 0f) return false;
        if (Vector3.Angle(blocking.normal, Vector3.up) <= maxSlopeAngle) return false;

        Vector3 forward = new Vector3(remaining.x, 0f, remaining.z);
        if (forward.sqrMagnitude < MinMoveDistance) return false;
        forward.Normalize();

        float clearance = capsule.radius + skinWidth * 2f;

        // Lift clear of the tallest step we may climb PLUS margin, then decide from what is
        // actually found rather than from how far we lifted.
        //
        // Lifting exactly stepHeight makes the probe marginal at the limit: a capsule resting on
        // the ground already sits a skin width above it, so the real clearance over a maximum
        // step is a skin width or less. Any frame where the body rests a hair lower — after
        // depenetration, a different snap result, a seam between two mesh colliders — the forward
        // probe clips the step surface and step-up refuses. That is a staircase that works
        // "sometimes", and it gets worse the closer the riser is to stepHeight.
        //
        // The same free skin width moves the LIMIT the other way: a step slightly taller than
        // stepHeight still clears the probe and gets climbed, so the inspector value reads about
        // a skin width lower than the truth.
        float lift = stepHeight + skinWidth * 2f;
        Vector3 lifted = position + Vector3.up * lift;

        if (Cast(lifted, Quaternion.identity, forward, clearance, out _)) return false;

        // The body advances the FULL clearance, even when this step had less motion budget than
        // that. It reads as a forward lurch at walking pace — 0.19 m against a 0.05 m budget —
        // and vanishes near sprint speed, which is why one staircase feels jittery slowly and
        // flawless fast.
        //
        // ⚠ Do not "fix" it by advancing only as far as the budget allows. The capsule centre has
        // to finish PAST the riser face — radius + skinWidth = 0.17 m, essentially this same
        // distance — or the down-cast catches the step's top EDGE instead of its surface and
        // leaves the body perched on the corner, 0.16 m or more above the floor and outside the
        // ground probe. IsGrounded then goes false, and this method requires wasGrounded, so it
        // never runs again: the character falls back and retries forever. Only a lip shorter than
        // the ground probe still works, which is exactly how that failure presents.
        //
        // The overshoot is structural to a teleporting step-up. Hide it in the camera and the
        // model, not here.
        Vector3 ahead = lifted + forward * clearance;

        if (!Cast(ahead, Quaternion.identity, Vector3.down, lift + skinWidth * 2f, out RaycastHit ground)) return false;
        if (Vector3.Angle(ground.normal, Vector3.up) > maxSlopeAngle) return false;

        float drop = Mathf.Max(ground.distance - skinWidth, 0f);

        // MEASURED rise, not the lift. This is what stepHeight actually limits, so the boundary
        // sits where the inspector says it does instead of a skin width above it. Accurate to
        // roughly one skin width, since it reads the surface through the same offset every cast
        // leaves behind.
        if (lift - drop > stepHeight) return false;

        position = ahead + Vector3.down * drop;
        remaining -= forward * Mathf.Min(clearance, remaining.magnitude);
        return true;
    }

    /// <summary>
    /// Push out of anything the capsule ended up inside. Sweeps cannot catch everything —
    /// geometry moves, prefabs spawn overlapping, and skin width is finite.
    /// </summary>
    Vector3 Depenetrate(Vector3 position)
    {
        Quaternion rotation = CapsuleRotation * transform.rotation;
        CapsulePoints(position, CapsuleRotation, out Vector3 top, out Vector3 bottom);

        int count = Physics.OverlapCapsuleNonAlloc(top, bottom, capsule.radius, overlaps,
                                                   collisionLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];
            if (other == capsule) continue;

            bool overlapping = Physics.ComputePenetration(
                capsule, position, rotation,
                other, other.transform.position, other.transform.rotation,
                out Vector3 direction, out float depth);

            if (!overlapping) continue;

            position += direction * (depth + skinWidth * 0.5f);
        }

        // The overlap set is not re-queried as the position moves. One pass converges for the
        // shallow overlaps sweeping leaves behind, and anything deeper resolves next step.

        return position;
    }

    bool Cast(Vector3 position, Quaternion rotation, Vector3 direction, float distance, out RaycastHit hit)
    {
        CapsulePoints(position, rotation, out Vector3 top, out Vector3 bottom);
        return Physics.CapsuleCast(top, bottom, capsule.radius, direction, out hit, distance,
                                   collisionLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// The capsule's two sphere centres in world space, for a given position and roll.
    ///
    /// Assumes the collider's centre offset lies on its own up axis, which is true of every
    /// upright character capsule and is what lets roll be a single rotation of that axis rather
    /// than a full transform rebuild.
    /// </summary>
    void CapsulePoints(Vector3 position, Quaternion rotation, out Vector3 top, out Vector3 bottom)
    {
        Vector3 up = rotation * Vector3.up;
        Vector3 centre = position + up * capsule.center.y;
        float half = Mathf.Max(capsule.height * 0.5f - capsule.radius, 0f);

        top = centre + up * half;
        bottom = centre - up * half;
    }
}
