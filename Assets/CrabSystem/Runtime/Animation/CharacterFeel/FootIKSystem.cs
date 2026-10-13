using System;
using UnityEngine;

public class FootIKSystem : AnimatorIKModule
{
    [Header("Ground Probe")]
    [Tooltip("Take the probe mask and slope limit from CharacterMotor so foot planting cannot " +
             "disagree with what the capsule actually collides with.")]
    [SerializeField] private bool useMotorSettings = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float maxGroundAngle = 40f;

    [Tooltip("How far above the animated foot the probe starts. Must clear the tallest step the " +
             "motor walks up, or a foot already inside a stair face finds nothing.")]
    [SerializeField] private float probeUp = 0.5f;
    [SerializeField] private float probeDown = 0.6f;

    [Tooltip("Probe thickness. A zero-radius ray flips between the tread and the riser as it " +
             "crosses a stair nosing; a small sphere rolls over the edge instead. Zero falls " +
             "back to a raycast.")]
    [SerializeField] private float probeRadius = 0.08f;

    [Tooltip("Distance from the foot bone's pivot to the sole. Too small and she sinks; too " +
             "large and she skates above the floor.")]
    [SerializeField] private float footHeight = 0.1f;

    [Header("Body")]
    [Tooltip("How far the hips may drop so the lower foot can reach. This is what makes stairs " +
             "and slopes work — without it the low foot simply fails to stretch.")]
    [SerializeField] private float maxBodyDrop = 0.4f;
    [SerializeField] private float bodySmoothing = 10f;

    [Header("Weights")]
    [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;
    [SerializeField] private float weightFadeSpeed = 6f;

    [Tooltip("Per-foot fade. Without this a foot crossing a gap, a seam or a stair edge loses " +
             "its ground in one frame and the leg snaps straight.")]
    [SerializeField] private float footWeightFadeSpeed = 8f;

    [Tooltip("Smoothing on each foot's target. The probe result jitters across uneven geometry; " +
             "this is what stops that reaching the leg.")]
    [SerializeField] private float targetSmoothing = 15f;

    [Header("Swing Falloff")]
    [Tooltip("A foot this close above its ground is fully planted.")]
    [SerializeField] private float plantDistance = 0.12f;

    [Tooltip("A foot this high above its ground gets no IK at all — it is mid-stride and the " +
             "animation owns it. Between the two the weight ramps. This is a stand-in for real " +
             "foot curves, not a replacement for them.")]
    [SerializeField] private float liftDistance = 0.35f;

    [Header("Speed")]
    [Tooltip("Planting fades out with speed. At a sprint the animation already sells the " +
             "contact and pinning it reads as dragging.")]
    [SerializeField] private float speedFadeStart = 2.5f;
    [SerializeField] private float speedFadeEnd = 5f;

    [Header("Foot Lock")]
    [Tooltip("While the character is turning on the spot, hold each foot where it was planted " +
             "instead of letting it slide with the rotating body.")]
    [SerializeField] private bool lockWhileStationary = true;
    [SerializeField] private float lockSpeedThreshold = 0.15f;

    [Tooltip("Accumulated body yaw, in degrees, before a locked foot gives up and re-plants.")]
    [SerializeField] private float lockReleaseAngle = 50f;

    [Tooltip("How far a locked foot may end up from its animated position before it re-plants. " +
             "This is the leg-stretch guard; the angle limit alone does not cover a foot that " +
             "walks away from its lock.")]
    [SerializeField] private float lockReleaseDistance = 0.35f;

    [Header("Animator Curves")]
    [Tooltip("Optional float curves authored on the locomotion clips, 1 while the foot is " +
             "planted and 0 while it swings. Leave empty until the clips have them. These are " +
             "the real fix for swing-phase feet; the falloff above is the approximation.")]
    [SerializeField] private string leftFootCurve = "";
    [SerializeField] private string rightFootCurve = "";

    private class FootState
    {
        public AvatarIKGoal Goal;
        public bool Grounded;
        public float Weight;
        public float GroundOffset;
        public Vector3 Position;
        public Quaternion Rotation;
        public bool Primed;
        public int CurveHash;
        public bool HasCurve;

        public bool Locked;
        public Vector3 LockedPosition;
        public Quaternion LockedRotation;
        public float LockedYaw;

        public bool Planted;
        public Vector3 GroundNormal;
        public Collider GroundCollider;
    }

    public struct FootContact
    {
        public AvatarIKGoal Goal;
        public Vector3 Position;
        public Vector3 Normal;
        public Collider Collider;
        public float Speed;
    }

    private readonly FootState left = new FootState { Goal = AvatarIKGoal.LeftFoot };
    private readonly FootState right = new FootState { Goal = AvatarIKGoal.RightFoot };

    private CharacterMotor motor;
    private MovementSystem movement;

    private float baseWeight;
    private float bodyOffset;

    // Fired from inside the animator's IK pass when a foot first touches down. Keep handlers
    // cheap — a one-shot and a particle emit, not a raycast.
    public event Action<FootContact> OnFootPlanted;

    public float CurrentWeight => baseWeight;
    public float BodyOffset => bodyOffset;
    public bool LeftGrounded => left.Grounded;
    public bool RightGrounded => right.Grounded;
    public float LeftWeight => left.Weight;
    public float RightWeight => right.Weight;
    public Vector3 LeftTarget => left.Position;
    public Vector3 RightTarget => right.Position;
    public bool LeftLocked => left.Locked;
    public bool RightLocked => right.Locked;

    // First: this is the receiver that moves the body.
    public override int IKOrder => 0;

    private LayerMask ProbeMask => useMotorSettings && motor != null ? motor.CollisionLayers : groundLayers;
    private float SlopeLimit => useMotorSettings && motor != null ? motor.MaxSlopeAngle : maxGroundAngle;

    protected override void ResolveReferences()
    {
        motor = FeelReferences.FindMotor(brain);
        movement = brain?.Movement;

        if (useMotorSettings && motor == null)
            Debug.LogError($"[FootIKSystem] useMotorSettings is on but no CharacterMotor was found on '{(brain != null && brain.EntityRoot != null ? brain.EntityRoot.name : "?")}' or its children. Falling back to the serialized mask, which can disagree with what the capsule collides with.", this);
    }

    protected override void OnRebind(Animator animator)
    {
        BindCurve(animator, left, leftFootCurve);
        BindCurve(animator, right, rightFootCurve);

        left.Primed = false;
        right.Primed = false;
    }

    protected override void Solve(Animator animator)
    {
        float dt = Time.deltaTime;
        float desiredWeight = IsEnabled && IsGrounded() ? SpeedFade() : 0f;
        baseWeight = Mathf.MoveTowards(baseWeight, desiredWeight, weightFadeSpeed * dt);

        float bodyBlend = 1f - Mathf.Exp(-bodySmoothing * dt);

        if (!IsEnabled || !IsGrounded())
        {
            Release(animator, left, dt);
            Release(animator, right, dt);
            bodyOffset = Mathf.Lerp(bodyOffset, 0f, bodyBlend);
            return;
        }

        // Solved even when baseWeight is zero. The speed fade turns the IK off at a sprint, but
        // ground contact is still what footsteps and effects listen to, and those must not stop
        // the moment she runs.
        SolveFoot(animator, left, dt);
        SolveFoot(animator, right, dt);

        if (baseWeight <= 0f)
        {
            bodyOffset = Mathf.Lerp(bodyOffset, 0f, bodyBlend);
            ClearGoal(animator, AvatarIKGoal.LeftFoot);
            ClearGoal(animator, AvatarIKGoal.RightFoot);
            return;
        }

        // The hips follow whichever foot must reach furthest down. Taking the minimum rather than
        // an average is what keeps the high foot planted on a stair instead of both feet splitting
        // the difference and neither touching. Scaling by the foot's own weight means a swinging
        // foot passing over a drop does not yank the body down with it.
        float drop = 0f;
        drop = Mathf.Min(drop, left.GroundOffset * left.Weight);
        drop = Mathf.Min(drop, right.GroundOffset * right.Weight);
        drop = Mathf.Max(drop, -maxBodyDrop);

        bodyOffset = Mathf.Lerp(bodyOffset, drop, bodyBlend);
        animator.bodyPosition += Vector3.up * (bodyOffset * baseWeight);

        ApplyGoal(animator, left);
        ApplyGoal(animator, right);
    }

    private void SolveFoot(Animator animator, FootState foot, float dt)
    {
        Vector3 animated = animator.GetIKPosition(foot.Goal);
        Quaternion animatedRotation = animator.GetIKRotation(foot.Goal);

        Vector3 target = animated;
        Quaternion targetRotation = animatedRotation;
        float desired = 0f;
        float lift = 0f;

        foot.Grounded = Probe(animated, out RaycastHit ground);

        if (foot.Grounded)
        {
            target = ground.point + Vector3.up * footHeight;
            targetRotation = Quaternion.FromToRotation(Vector3.up, ground.normal) * animatedRotation;
            lift = animated.y - target.y;
            desired = PlantFalloff(lift);
            foot.GroundOffset = target.y - animated.y;
            foot.GroundNormal = ground.normal;
            foot.GroundCollider = ground.collider;
        }
        else
        {
            foot.GroundOffset = 0f;
        }

        UpdatePlantedState(foot, lift, target);
        UpdateLock(foot, animated, ref target, ref targetRotation);

        foot.Weight = Mathf.MoveTowards(foot.Weight, desired, footWeightFadeSpeed * dt);

        if (!foot.Primed)
        {
            foot.Position = target;
            foot.Rotation = targetRotation;
            foot.Primed = true;
            return;
        }

        float blend = 1f - Mathf.Exp(-targetSmoothing * dt);
        foot.Position = Vector3.Lerp(foot.Position, target, blend);
        foot.Rotation = Quaternion.Slerp(foot.Rotation, targetRotation, blend);
    }

    // Turn-in-place. With CameraDrivesFacing the body can rotate while the animation holds a
    // standing pose, and the feet skate. Holding the world-space target where it was planted
    // makes the body turn over the feet instead — until the twist gets implausible.
    private void UpdateLock(FootState foot, Vector3 animated, ref Vector3 target, ref Quaternion targetRotation)
    {
        bool eligible = lockWhileStationary && movement != null && foot.Grounded && CurrentSpeed() < lockSpeedThreshold;

        if (!eligible)
        {
            foot.Locked = false;
            return;
        }

        float yaw = FacingYaw();

        if (!foot.Locked)
        {
            foot.Locked = true;
            foot.LockedPosition = target;
            foot.LockedRotation = targetRotation;
            foot.LockedYaw = yaw;
        }
        else if (Mathf.Abs(Mathf.DeltaAngle(foot.LockedYaw, yaw)) > lockReleaseAngle)
        {
            foot.Locked = false;
            return;
        }
        else if (Vector3.Distance(animated, foot.LockedPosition) > lockReleaseDistance)
        {
            foot.Locked = false;
            return;
        }

        target = foot.LockedPosition;
        targetRotation = foot.LockedRotation;
    }

    private void UpdatePlantedState(FootState foot, float lift, Vector3 contactPoint)
    {
        if (!foot.Grounded)
        {
            foot.Planted = false;
            return;
        }

        if (!foot.Planted && lift <= plantDistance)
        {
            foot.Planted = true;
            OnFootPlanted?.Invoke(new FootContact
            {
                Goal = foot.Goal,
                Position = contactPoint,
                Normal = foot.GroundNormal,
                Collider = foot.GroundCollider,
                Speed = CurrentSpeed(),
            });
            return;
        }

        if (foot.Planted && lift >= liftDistance)
            foot.Planted = false;
    }

    private float CurrentSpeed() => movement != null ? movement.Speed : 0f;

    private float FacingYaw()
    {
        if (brain != null && brain.ModelRoot != null) return brain.ModelRoot.eulerAngles.y;
        if (brain != null && brain.EntityRoot != null) return brain.EntityRoot.eulerAngles.y;
        return transform.eulerAngles.y;
    }

    // How much IK a foot deserves, from how far it sits above the ground under it. Negative lift
    // means the animation has driven the foot through the floor, which always wants full weight.
    private float PlantFalloff(float lift)
    {
        if (lift <= plantDistance) return 1f;
        if (lift >= liftDistance || liftDistance <= plantDistance) return 0f;
        return 1f - (lift - plantDistance) / (liftDistance - plantDistance);
    }

    private bool Probe(Vector3 animated, out RaycastHit hit)
    {
        Vector3 origin = animated + Vector3.up * probeUp;
        float distance = probeUp + probeDown;

        bool found = probeRadius > 0f
            ? Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, distance, ProbeMask, QueryTriggerInteraction.Ignore)
            : Physics.Raycast(origin, Vector3.down, out hit, distance, ProbeMask, QueryTriggerInteraction.Ignore);

        // A SphereCast that starts already overlapping returns distance 0 with a zeroed point and
        // a normal facing back up the cast. Unusable, and it happens whenever a foot is inside
        // geometry — which is exactly when IK is most wanted.
        if (found && hit.distance <= 0f)
            found = Physics.Raycast(origin, Vector3.down, out hit, distance, ProbeMask, QueryTriggerInteraction.Ignore);

        return found && Vector3.Angle(hit.normal, Vector3.up) <= SlopeLimit;
    }

    private void ApplyGoal(Animator animator, FootState foot)
    {
        float weight = baseWeight * foot.Weight;
        if (foot.HasCurve) weight *= animator.GetFloat(foot.CurveHash);

        animator.SetIKPositionWeight(foot.Goal, weight * positionWeight);
        animator.SetIKRotationWeight(foot.Goal, weight * rotationWeight);

        if (weight <= 0f) return;

        animator.SetIKPosition(foot.Goal, foot.Position);
        animator.SetIKRotation(foot.Goal, foot.Rotation);
    }

    // Zeroes the animator goal without touching foot state. Used when the speed fade has turned
    // the IK off but the probe results are still wanted — footsteps and effects read contact even
    // while nothing is being planted.
    private static void ClearGoal(Animator animator, AvatarIKGoal goal)
    {
        animator.SetIKPositionWeight(goal, 0f);
        animator.SetIKRotationWeight(goal, 0f);
    }

    private void Release(Animator animator, FootState foot, float dt)
    {
        foot.Weight = Mathf.MoveTowards(foot.Weight, 0f, footWeightFadeSpeed * dt);
        foot.Grounded = false;
        foot.GroundOffset = 0f;
        foot.Primed = false;
        foot.Locked = false;
        foot.Planted = false;

        animator.SetIKPositionWeight(foot.Goal, 0f);
        animator.SetIKRotationWeight(foot.Goal, 0f);
    }

    private bool IsGrounded()
    {
        if (motor != null) return motor.IsGrounded;
        if (movement != null) return movement.IsGrounded;
        return brain != null && brain.IsGrounded;
    }

    private float SpeedFade()
    {
        if (movement == null || speedFadeEnd <= speedFadeStart) return 1f;
        return 1f - Mathf.Clamp01((movement.Speed - speedFadeStart) / (speedFadeEnd - speedFadeStart));
    }

    private static void BindCurve(Animator animator, FootState foot, string parameterName)
    {
        foot.HasCurve = false;

        if (animator == null || string.IsNullOrEmpty(parameterName)) return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float || parameter.name != parameterName) continue;

            foot.CurveHash = Animator.StringToHash(parameterName);
            foot.HasCurve = true;
            return;
        }
    }
}
