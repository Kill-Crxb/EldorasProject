using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads the geometry immediately around the character and reports what it found. It decides
/// nothing. Whether a wall means a wall-run, a vault or a mantle is the locomotion system's
/// business.
///
/// SHAPE: one oversized trigger capsule around the body. Ground is NOT probed here — CharacterMotor
/// owns grounding and this module mirrors its answer, so there is exactly one authority.
///
/// The trigger is a BROADPHASE ONLY. Enter/Exit maintain a candidate set; every frame each
/// candidate is re-confirmed with a query before it counts. That matters because trigger events
/// lie in three known ways: a collider destroyed or disabled inside the volume never fires Exit,
/// a teleport can skip Enter entirely, and answering "is there a wall RIGHT NOW" from
/// accumulated history means trusting that every event arrived. Confirming each candidate makes
/// a stale entry cost one failed query and then drop itself, while still letting the physics
/// engine do the broadphase work.
///
/// SIZING IS DERIVED, NOT TUNED. At speed the sensor has to lead the body by however far the
/// body travels in one step, or contact arrives the frame after you needed it. The reach is
/// maxExpectedSpeed * fixedDeltaTime, which is a number you can compute rather than eyeball —
/// 15 m/s at 50 Hz is 0.3 m. Hand-placed probes cannot get this right across a speed range.
///
/// FILTER BY ANGLE, NOT BY SHAPE. A capsule sees behind you as readily as in front. Shrinking
/// it to exclude the rear also blinds it to the sides, which is where wall-running lives. The
/// rear cone is rejected by dot product and the surface normal decides floor/wall/ceiling, at
/// any sensor size.
/// </summary>
public class ParkourAssistant : MonoBehaviour, IBrainModule
{
    #region Inspector

    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;

    [Header("What Counts As Geometry")]
    [Tooltip("Layers the sensor is allowed to see. This is a BACKSTOP — the cheap fix is the " +
             "physics layer collision matrix, which stops the trigger events firing at all " +
             "instead of filtering them after the fact. Keep this in step with the motor's " +
             "collisionLayers: the two disagreeing is a wall you can touch but cannot sense.")]
    [SerializeField] private LayerMask probeLayers = ~0;

    [Header("Sensor Capsule")]
    [Tooltip("Match the sensor capsule to the character's own capsule at startup, then grow it " +
             "by the reach below. Turn this off to author the trigger capsule by hand.")]
    [SerializeField] private bool autoFitToBody = true;

    [Tooltip("Derive reach from speed: how far the body travels in one physics step. Leave on " +
             "unless you have a reason — a hand-set reach goes stale the moment speeds change.")]
    [SerializeField] private bool reachFromSpeed = true;

    [Tooltip("Top speed the character is expected to reach, m/s. Reach becomes " +
             "maxExpectedSpeed * fixedDeltaTime, so contact is reported the frame BEFORE the " +
             "body arrives rather than the frame after. 15 is the motor's honest ceiling: above " +
             "it ground snapping stops holding crests and this sensor grows fat enough to grab " +
             "walls the body is not near.")]
    [SerializeField] private float maxExpectedSpeed = 15f;

    [Tooltip("Reach used when reachFromSpeed is off. Metres beyond the body's own radius.")]
    [SerializeField] private float manualReach = 0.4f;

    [Tooltip("Rear blind cone, in degrees from the body's forward. 180 sees everything; 135 " +
             "keeps the sides (where wall-running happens) and drops walls directly behind.")]
    [Range(45f, 180f)]
    [SerializeField] private float maxContactAngle = 135f;

    [Header("Surface Classification")]
    [Tooltip("Steepest surface that still counts as ground rather than wall. A vertical face " +
             "reads 90 degrees. Shared by every classification here so nothing can disagree " +
             "about what a floor is.")]
    [Range(0f, 89f)]
    [SerializeField] private float maxGroundAngle = 50f;

    [Tooltip("How head-on a wall must be to fill the FRONT slot. 1 is perfectly square to the " +
             "face, 0.7 is within about 45 degrees. Side is signed left/right and reads near " +
             "ZERO for a wall you are facing, so a head-on contact has no side to be nearest " +
             "on — climbing and mantling need this slot or they have no discriminator at all.")]
    [Range(0f, 1f)]
    [SerializeField] private float frontApproachThreshold = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region State

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private const float MinAimLength = 1e-6f;

    private ControllerBrain brain;
    private Transform selfRoot;
    private Transform body;
    private CapsuleCollider sensor;
    private CapsuleCollider bodyCapsule;
    private CharacterMotor motor;

    private float reach;
    private float rearCutoff;

    private readonly HashSet<Collider> candidates = new HashSet<Collider>();
    private readonly List<Collider> staleBuffer = new List<Collider>(4);
    private readonly List<ParkourContact> walls = new List<ParkourContact>(8);

    private ParkourContact left;
    private ParkourContact right;
    private ParkourContact front;
    private ParkourContact ceiling;
    private ParkourContact ground;

    /// <summary>Every confirmed wall contact this frame, nearest first is NOT guaranteed.</summary>
    public IReadOnlyList<ParkourContact> Walls => walls;

    /// <summary>Nearest wall on the character's left, as of this frame.</summary>
    public ParkourContact Left => left;

    /// <summary>Nearest wall on the character's right, as of this frame.</summary>
    public ParkourContact Right => right;

    /// <summary>
    /// Nearest wall the character is FACING, as of this frame. A wall can legitimately fill this
    /// slot and a side slot at once — an inside corner is two contacts, not a choice between two.
    /// </summary>
    public ParkourContact Front => front;

    /// <summary>Nearest overhead surface. Head clearance for mantle and vault.</summary>
    public ParkourContact Ceiling => ceiling;

    /// <summary>
    /// Floor under the feet, MIRRORED FROM CharacterMotor rather than probed here.
    ///
    /// This module used to run its own downward sweep, which made it a second answer to "am I
    /// grounded" alongside the motor's — the two disagreed on stairs, slopes and anything moving,
    /// and the probeLayers / groundLayers split was the same bug wearing a different hat. The
    /// motor sweeps every step and knows the normal and the distance, so it wins; this stays only
    /// so existing callers keep compiling and keep getting a truthful answer.
    ///
    /// Collider and Point are not carried: the motor reports what it stands on, not what object
    /// it belongs to. Ask the motor directly if you need more than normal and distance.
    /// </summary>
    public ParkourContact Ground => ground;

    public bool HasWallLeft => left.Detected;
    public bool HasWallRight => right.Detected;
    public bool HasWallFront => front.Detected;
    public bool HasCeiling => ceiling.Detected;
    public bool HasWall => walls.Count > 0;

    /// <summary>Nothing under the feet — airborne, or over a drop. The motor's answer.</summary>
    public bool OverDrop => !ground.Detected;

    /// <summary>How far the sensor currently leads the body, in metres.</summary>
    public float Reach => reach;

    /// <summary>
    /// Fired when a probe changes between detecting and not. Saves a locomotion state machine
    /// polling every frame for a transition it only cares about once.
    /// </summary>
    public event Action<ParkourSurface, ParkourContact> OnContactChanged;

    #endregion

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        selfRoot = brain != null ? brain.transform.root : transform.root;
        body = brain != null && brain.EntityRoot != null ? brain.EntityRoot : selfRoot;

        sensor = GetComponent<CapsuleCollider>();
        motor = body != null ? body.GetComponent<CharacterMotor>() : null;
        bodyCapsule = ResolveBodyCapsule();

        rearCutoff = Mathf.Cos(maxContactAngle * Mathf.Deg2Rad);

        ValidateSetup();
        ConfigureSensor();
    }

    /// <summary>
    /// The capsule the MOTOR sweeps, never any other capsule on the character.
    ///
    /// GetComponentInChildren&lt;CapsuleCollider&gt;() is the obvious way to do this and it is
    /// wrong twice over on a real prefab: it can return this module's OWN trigger capsule, which
    /// makes the sensor fit to itself and grow by the reach every startup, and it can return a
    /// hurtbox capsule, which is sized for damage rather than for movement. Both failures look
    /// entirely correct in the inspector.
    ///
    /// The motor's collider is the authority because it is the one collision actually uses. The
    /// entity root is the fallback for a character with no motor — the ARPG handler's prefabs.
    /// </summary>
    private CapsuleCollider ResolveBodyCapsule()
    {
        if (motor != null && motor.Capsule != null) return motor.Capsule;
        if (body == null) return null;

        CapsuleCollider onRoot = body.GetComponent<CapsuleCollider>();

        return onRoot != sensor ? onRoot : null;
    }

    /// <summary>
    /// Driven from ControllerBrain.Update today because the whole locomotion stack is. Refresh
    /// is public and side-effect-free so a FixedUpdate-driven controller can call it directly
    /// without waiting for the brain — contacts want to be as fresh as the movement that reads
    /// them.
    /// </summary>
    public void UpdateModule()
    {
        if (!IsEnabled) return;
        Refresh();
    }

    #endregion

    #region Refresh

    public void Refresh()
    {
        ResolveContacts();
        Set(ParkourSurface.Ground, ref ground, GroundFromMotor());
    }

    /// <summary>
    /// Confirm every candidate the trigger handed us and pick each slot in the SAME pass.
    ///
    /// Anything that fails — destroyed, disabled, wrong layer, behind us — is dropped for this
    /// frame, and anything Unity has already destroyed is dropped from the set for good. The
    /// slots are chosen inline rather than by re-walking the wall list once per slot: four extra
    /// loops over the same handful of contacts, to answer questions the confirm pass already had
    /// the numbers for.
    /// </summary>
    private void ResolveContacts()
    {
        walls.Clear();
        staleBuffer.Clear();

        ParkourContact bestLeft = default;
        ParkourContact bestRight = default;
        ParkourContact bestFront = default;
        ParkourContact bestCeiling = default;

        float leftDistance = float.MaxValue;
        float rightDistance = float.MaxValue;
        float frontDistance = float.MaxValue;
        float ceilingDistance = float.MaxValue;

        Vector3 center = BodyCenter();
        Vector3 facing = body != null ? body.forward : transform.forward;

        foreach (Collider candidate in candidates)
        {
            if (candidate == null)
            {
                staleBuffer.Add(candidate);
                continue;
            }

            if (!TryConfirm(candidate, center, out ParkourContact contact)) continue;

            if (contact.Kind == ParkourSurfaceKind.Ceiling && contact.Distance < ceilingDistance)
            {
                bestCeiling = contact;
                ceilingDistance = contact.Distance;
            }

            if (contact.Kind != ParkourSurfaceKind.Wall) continue;

            walls.Add(contact);

            if (contact.Side < 0f && contact.Distance < leftDistance)
            {
                bestLeft = contact;
                leftDistance = contact.Distance;
            }

            if (contact.Side > 0f && contact.Distance < rightDistance)
            {
                bestRight = contact;
                rightDistance = contact.Distance;
            }

            // Front is decided by the NORMAL, not by Side. Side is Dot(toContact, body.right) and
            // reads near zero for a wall square in front of you, so a head-on contact is neither
            // left nor right and would otherwise fall through every slot.
            if (contact.Approach(facing) < frontApproachThreshold) continue;
            if (contact.Distance >= frontDistance) continue;

            bestFront = contact;
            frontDistance = contact.Distance;
        }

        for (int i = 0; i < staleBuffer.Count; i++)
            candidates.Remove(staleBuffer[i]);

        Set(ParkourSurface.Left, ref left, bestLeft);
        Set(ParkourSurface.Right, ref right, bestRight);
        Set(ParkourSurface.Front, ref front, bestFront);
        Set(ParkourSurface.Ceiling, ref ceiling, bestCeiling);
    }

    private bool TryConfirm(Collider col, Vector3 center, out ParkourContact contact)
    {
        contact = default;

        if (!col.enabled) return false;
        if (!col.gameObject.activeInHierarchy) return false;
        if (col.transform.root == selfRoot) return false;
        if (!InMask(col.gameObject.layer)) return false;

        if (!TryResolveSurface(col, center, out Vector3 point, out Vector3 normal)) return false;

        Vector3 toContact = point - center;
        float distance = toContact.magnitude;
        if (distance > reach + MinAimLength) return false;

        Vector3 direction = distance > MinAimLength ? toContact / distance : body.forward;
        if (Vector3.Dot(direction, body.forward) < rearCutoff) return false;

        contact = new ParkourContact
        {
            Detected = true,
            Collider = col,
            Point = point,
            Normal = normal,
            Distance = distance,
            Side = Vector3.Dot(direction, body.right),
            Kind = Classify(normal),
        };

        return true;
    }

    /// <summary>
    /// Aim at the surface, then measure it.
    ///
    /// ClosestPoint gives an exact nearest point on convex shapes, which is the right thing to
    /// aim a ray at — a collider's transform.position is wherever the level artist's pivot
    /// happens to be, and on merged environment geometry that is frequently world origin.
    /// Concave meshes and terrain do not support ClosestPoint at all, so those aim at the bounds
    /// instead, which is coarse but only has to point the ray in roughly the right direction.
    ///
    /// The ray is what produces the NORMAL, and the normal is what everything downstream
    /// actually wants — the cross product for a wall-run direction, the push-off for a wall
    /// jump, the floor/wall/ceiling verdict. Collider.Raycast targets this one collider, so no
    /// layer mask is needed and nothing in between can steal the hit.
    ///
    /// A ray that starts inside the shape reports nothing, so deep overlap falls back to the
    /// direction from the aim point. Less accurate at an edge, but it never drops a contact the
    /// body is currently buried in — which is exactly when a controller needs one most.
    /// </summary>
    private bool TryResolveSurface(Collider col, Vector3 center, out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.zero;

        Vector3 aim = SupportsClosestPoint(col)
            ? col.ClosestPoint(center)
            : col.ClosestPointOnBounds(center);

        Vector3 offset = aim - center;
        float length = offset.magnitude;
        if (length < MinAimLength) return false;

        Vector3 direction = offset / length;
        Ray ray = new Ray(center, direction);

        if (col.Raycast(ray, out RaycastHit hit, reach + MinAimLength))
        {
            point = hit.point;
            normal = hit.normal;
            return true;
        }

        point = aim;
        normal = -direction;
        return true;
    }

    /// <summary>
    /// SIZE IS NOT WHAT SEPARATES A FLOOR FROM A WALL — ANGLE IS. A vertical face reads 90
    /// degrees however large the sensor is, and a ramp reads its slope. Floors are classified but
    /// dropped by the caller, because the motor owns that question and carries distance with it.
    /// </summary>
    private ParkourSurfaceKind Classify(Vector3 normal)
    {
        float angle = Vector3.Angle(normal, Vector3.up);

        if (angle <= maxGroundAngle) return ParkourSurfaceKind.Floor;
        if (angle >= 180f - maxGroundAngle) return ParkourSurfaceKind.Ceiling;

        return ParkourSurfaceKind.Wall;
    }

    /// <summary>
    /// Grounding, borrowed from the single authority rather than measured a second time here.
    ///
    /// The motor runs a downward capsule sweep every physics step from the UPRIGHT reference, so
    /// it stays honest through a wall-run roll — which a probe hung off this module's own
    /// transform would not.
    /// </summary>
    private ParkourContact GroundFromMotor()
    {
        if (motor == null) return default;
        if (!motor.IsGrounded) return default;

        return new ParkourContact
        {
            Detected = true,
            Normal = motor.GroundNormal,
            Distance = motor.GroundDistance,
            Kind = ParkourSurfaceKind.Floor,
        };
    }

    #endregion

    #region Trigger broadphase

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        if (other.transform.root == selfRoot) return;

        candidates.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;

        candidates.Remove(other);
    }

    /// <summary>
    /// OnTriggerStay is deliberately absent. It is the expensive callback, and the per-frame
    /// confirmation pass already answers the question it would answer, from a source that cannot
    /// go stale.
    /// </summary>
    private void OnDisable()
    {
        // Reset silently rather than through ClearContacts. On scene teardown the subscribers
        // to OnContactChanged may already be destroyed, and announcing "wall lost" into a dying
        // hierarchy is how a MissingReferenceException gets thrown from a shutdown path.
        candidates.Clear();
        walls.Clear();

        left = default;
        right = default;
        front = default;
        ceiling = default;
        ground = default;
    }

    /// <summary>
    /// Drop everything. Call after a teleport, or anything else that moves the character without
    /// the physics engine watching — Enter and Exit are both skipped by a hard position write.
    /// </summary>
    public void ClearContacts()
    {
        candidates.Clear();
        walls.Clear();

        Set(ParkourSurface.Left, ref left, default);
        Set(ParkourSurface.Right, ref right, default);
        Set(ParkourSurface.Front, ref front, default);
        Set(ParkourSurface.Ceiling, ref ceiling, default);
        Set(ParkourSurface.Ground, ref ground, default);
    }

    #endregion

    #region Setup

    private void ValidateSetup()
    {
        if (sensor == null)
        {
            Debug.LogError($"[ParkourAssistant] {name} has no CapsuleCollider. The sensor must " +
                           "sit on THIS object — trigger callbacks only reach the component " +
                           "sharing a GameObject with the collider. Nothing will be detected.");
            isEnabled = false;
            return;
        }

        if (!sensor.isTrigger)
        {
            Debug.LogError($"[ParkourAssistant] {name}'s CapsuleCollider is not a trigger. It " +
                           "will push the character around instead of sensing. Forcing trigger.");
            sensor.isTrigger = true;
        }

        if (selfRoot != null && selfRoot.GetComponentInChildren<Rigidbody>() == null)
        {
            Debug.LogWarning($"[ParkourAssistant] {name} found no Rigidbody on the character. " +
                             "Trigger events against STATIC level geometry need one (kinematic " +
                             "is fine) or they never fire at all.");
        }

        if (bodyCapsule == null && autoFitToBody)
        {
            Debug.LogWarning($"[ParkourAssistant] {name} could not find the character's " +
                             "CapsuleCollider — no CharacterMotor, and none on the entity root. " +
                             "Author the sensor capsule by hand and turn autoFitToBody off.");
        }

        if (motor == null)
        {
            Debug.LogWarning($"[ParkourAssistant] {name} found no CharacterMotor on the entity " +
                             "root. Ground and OverDrop will always read 'no floor' — this module " +
                             "no longer probes for ground itself.");
        }
    }

    /// <summary>
    /// Grow the sensor from the body rather than from authored numbers, so it stays correct when
    /// the character capsule is resized and across every entity that uses this prefab.
    /// </summary>
    private void ConfigureSensor()
    {
        if (sensor == null) return;

        reach = reachFromSpeed ? maxExpectedSpeed * Time.fixedDeltaTime : manualReach;

        if (!autoFitToBody || bodyCapsule == null)
        {
            reach = Mathf.Max(reach, sensor.radius);
            return;
        }

        // Collider height and radius are LOCAL units. If this object and the body are scaled
        // differently, copying the numbers across produces a sensor of the wrong size — and it
        // looks correct in the inspector, which is the worst kind of wrong.
        float scaleRatio = transform.lossyScale.x / Mathf.Max(0.0001f, bodyCapsule.transform.lossyScale.x);
        if (Mathf.Abs(scaleRatio - 1f) > 0.01f)
        {
            Debug.LogWarning($"[ParkourAssistant] {name} is scaled differently to the character " +
                             $"capsule (ratio {scaleRatio:F2}). Sensor size will be off by that " +
                             "factor — put the sensor on an unscaled object.");
        }

        Vector3 worldCenter = bodyCapsule.transform.TransformPoint(bodyCapsule.center);

        sensor.direction = bodyCapsule.direction;
        sensor.center = transform.InverseTransformPoint(worldCenter);
        sensor.height = bodyCapsule.height;
        sensor.radius = bodyCapsule.radius + reach;

        if (debugLogging)
        {
            Debug.Log($"[ParkourAssistant] Sensor fitted — body radius {bodyCapsule.radius:F2} " +
                      $"+ reach {reach:F2} = {sensor.radius:F2} " +
                      $"({maxExpectedSpeed:F0} m/s at {1f / Time.fixedDeltaTime:F0} Hz)");
        }
    }

    private bool InMask(int layer)
    {
        return (probeLayers.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// ClosestPoint only supports box, sphere, capsule and CONVEX mesh colliders. Concave meshes
    /// — most static level geometry — and terrain silently return the query point instead, which
    /// would aim every ray at nothing.
    /// </summary>
    private static bool SupportsClosestPoint(Collider col)
    {
        if (col is MeshCollider mesh) return mesh.convex;
        if (col is TerrainCollider) return false;

        return true;
    }

    private Vector3 BodyCenter()
    {
        if (bodyCapsule != null)
            return bodyCapsule.transform.TransformPoint(bodyCapsule.center);

        if (sensor != null)
            return transform.TransformPoint(sensor.center);

        return transform.position;
    }

    #endregion

    #region Change tracking

    /// <summary>
    /// Store the new reading and announce it only when the probe crossed between detecting and
    /// not. A wall that is simply still there is not news.
    /// </summary>
    private void Set(ParkourSurface surface, ref ParkourContact slot, ParkourContact next)
    {
        bool changed = slot.Detected != next.Detected;
        slot = next;

        if (!changed) return;

        if (debugLogging)
            Debug.Log($"[ParkourAssistant] {surface} → {(next.Detected ? next.Tag : "clear")}");

        OnContactChanged?.Invoke(surface, next);
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        DrawSensor();
        DrawContacts();
    }

    private void DrawSensor()
    {
        CapsuleCollider capsule = sensor != null ? sensor : GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        Vector3 center = transform.TransformPoint(capsule.center);
        float half = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);

        Gizmos.color = walls.Count > 0 ? Color.green : Color.grey;
        Gizmos.DrawWireSphere(center + Vector3.up * half, capsule.radius);
        Gizmos.DrawWireSphere(center - Vector3.up * half, capsule.radius);
    }

    private void DrawContacts()
    {
        for (int i = 0; i < walls.Count; i++)
        {
            ParkourContact contact = walls[i];

            Gizmos.color = contact.Side > 0f ? Color.cyan : Color.magenta;
            Gizmos.DrawSphere(contact.Point, 0.05f);
            Gizmos.DrawRay(contact.Point, contact.Normal * 0.5f);
        }

        if (front.Detected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(front.Point, 0.09f);
        }

        if (ceiling.Detected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(ceiling.Point, 0.05f);
            Gizmos.DrawRay(ceiling.Point, ceiling.Normal * 0.5f);
        }
    }
#endif

    #endregion
}

/// <summary>
/// Which slot a reading came from.
///
/// New members are APPENDED, never inserted. Unity serializes an enum by its integer value, so
/// inserting Front ahead of Ground would silently renumber Ground from 2 to 4 in every asset
/// that already stored it — a change with no compile error and no visible cause.
/// </summary>
public enum ParkourSurface
{
    Left,
    Right,
    Ground,
    Front,
    Ceiling,
}

/// <summary>What a surface is, decided by its normal rather than by which probe found it.</summary>
public enum ParkourSurfaceKind
{
    None,
    Floor,
    Wall,
    Ceiling,
}

/// <summary>
/// One contact for one frame. A snapshot, not a subscription — read it, act on it, let it go.
/// Holding one across frames means holding a Collider reference that may since have been
/// destroyed.
/// </summary>
public struct ParkourContact
{
    /// <summary>Something is there.</summary>
    public bool Detected;

    /// <summary>What was hit. Null when nothing was, and for ground, which comes from the motor.</summary>
    public Collider Collider;

    /// <summary>Where the surface is.</summary>
    public Vector3 Point;

    /// <summary>The surface normal, pointing away from the surface.</summary>
    public Vector3 Normal;

    /// <summary>Distance from the body centre to the surface.</summary>
    public float Distance;

    /// <summary>
    /// Positive to the character's right, negative to the left. Ground reads 0 — and so does a
    /// wall you are squarely FACING, which is why Front is its own slot rather than a threshold
    /// on this number.
    /// </summary>
    public float Side;

    /// <summary>Floor, wall or ceiling, by normal angle.</summary>
    public ParkourSurfaceKind Kind;

    /// <summary>
    /// The hit object's tag, or empty when nothing was hit. Read on demand rather than cached
    /// per frame, because Collider.tag allocates and most frames nobody asks.
    /// </summary>
    public string Tag => Collider != null ? Collider.tag : string.Empty;

    /// <summary>
    /// Tag test. Prefer this to comparing Tag — CompareTag does not allocate, and it throws on
    /// a tag that is not in the tag manager, which turns a silent typo into a visible error.
    /// </summary>
    public bool Is(string tag) => Detected && Collider != null && Collider.CompareTag(tag);

    /// <summary>
    /// How head-on the approach is: 1 is straight into the surface, 0 is sliding along it,
    /// negative is moving away. This is the number that separates a vault from a wall-run.
    /// Pass a normalized direction — usually velocity, sometimes look.
    /// </summary>
    public float Approach(Vector3 direction) => Detected ? Vector3.Dot(direction, -Normal) : 0f;

    /// <summary>
    /// The run direction along the surface, signed to agree with the way you are already
    /// travelling. Taking this from VELOCITY rather than the camera is what lets a wall-run
    /// continue while the player looks somewhere else.
    /// </summary>
    public Vector3 AlongWall(Vector3 travelDirection)
    {
        if (!Detected) return Vector3.zero;

        Vector3 along = Vector3.Cross(Normal, Vector3.up).normalized;

        return Vector3.Dot(along, travelDirection) < 0f ? -along : along;
    }

    /// <summary>
    /// The climb direction up the surface: the wall tangent's vertical axis. Wall running
    /// discards this component and climbing does not — that discard is the only difference
    /// between the two moves.
    /// </summary>
    public Vector3 UpWall()
    {
        if (!Detected) return Vector3.zero;

        Vector3 along = Vector3.Cross(Normal, Vector3.up);
        if (along.sqrMagnitude < 1e-8f) return Vector3.zero;

        return Vector3.Cross(along.normalized, Normal).normalized;
    }
}
