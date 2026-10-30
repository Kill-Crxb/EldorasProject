using Unity.Collections;
using UnityEngine;

// The main-thread half of foot planting (FootPlantJob). When the gait cycle plants a foot, rays find the ground under
// it and that point is locked until the cycle lifts the foot again, so the foot stays put while the body moves over it.
// Weights fade in and out so a plant or lift never pops.
// With contact bones (Heel.L/R, Ball.L/R as children of the foot) a ray goes under each, and the job puts the lower
// one on its ground in the animated foot's angle. The lock is on the contact the foot pivots on: whichever is lower at
// touch-down, handed from heel to ball once the heel rises, so a rolling foot's ankle moves while the floor contact
// stays put. Without them the ankle's height is the ground under it plus the
// foot's sole height (Animator.leftFeetBottomHeight and right) plus the cycle's baked lift.
// Standing steps (gait build step 6): while standing, a foot that drifts StepDistance from where the animated pose wants
// it (after a stop, or as the body turns on the spot) lifts and steps there over StepSeconds, one foot at a time,
// farthest first. The animated ankles come back from FootPlantJob's state a frame late. Standing uses a wide slip
// window so steps, not slip, take up the drift.
// Plain class inside ProceduralPoseModule, like PostureLayer.

public class FootPlanter
{
    private class Foot
    {
        public readonly int Offset;
        public readonly string Side;
        public Transform Bone;
        public Transform Heel;
        public Transform Ball;
        public float Sole;
        public bool Locked;
        public Vector3 Position;
        public Vector3 Lock;
        public bool OnBall;
        public float HeelGround;
        public float BallGround;
        public Quaternion Tilt = Quaternion.identity;
        public float Weight;
        public bool Stepping;
        public Vector3 StepFrom;
        public Vector3 StepTo;
        public float StepTime;
        public float Lift;

        public Foot(int offset, string side)
        {
            Offset = offset;
            Side = side;
        }
    }

    public LayerMask Ground = ~0;
    public float BodySmoothingSeconds = 0.15f;
    public float SlipStart = 0.06f;
    public float SlipEnd = 0.18f;
    public float ProbeUp = 0.5f;
    public float ProbeDown = 0.6f;
    public float FadeSeconds = 0.08f;
    public float MaxBodyDrop = 0.4f;
    public float RollMargin = 0.005f;
    public float StepDistance = 0.12f;
    public float StepSeconds = 0.25f;
    public float StepHeight = 0.08f;
    public float StandingSlipStart = 0.3f;
    public float StandingSlipEnd = 0.5f;

    private readonly Foot left = new Foot(FootPlantJob.Left, "L");
    private readonly Foot right = new Foot(FootPlantJob.Right, "R");

    public void Bind(Animator animator)
    {
        BindFoot(left, animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.leftFeetBottomHeight);
        BindFoot(right, animator.GetBoneTransform(HumanBodyBones.RightFoot), animator.rightFeetBottomHeight);
    }

    public void Tick(NativeArray<float> control, NativeArray<float> state, float active, float dt, bool standing,
                     bool leftPlanted, float leftLift, bool rightPlanted, float rightLift)
    {
        if (standing) Step(state, dt);
        if (!standing) EndStep(left);
        if (!standing) EndStep(right);

        control[0] = MaxBodyDrop;
        control[1] = standing ? StandingSlipStart : SlipStart;
        control[2] = Mathf.Max(standing ? StandingSlipEnd : SlipEnd, control[1] + 0.001f);
        control[3] = BodySmoothingSeconds > 0f ? 1f - Mathf.Exp(-dt / BodySmoothingSeconds) : 1f;
        control[4] = active;
        TickFoot(control, left, leftLift, leftPlanted && active > 0f, active, dt);
        TickFoot(control, right, rightLift, rightPlanted && active > 0f, active, dt);
    }

    private void BindFoot(Foot foot, Transform bone, float sole)
    {
        foot.Bone = bone;
        foot.Heel = bone != null ? bone.Find("Heel." + foot.Side) : null;
        foot.Ball = bone != null ? bone.Find("Ball." + foot.Side) : null;
        foot.Sole = sole;
        foot.Locked = false;
        foot.Weight = 0f;
        EndStep(foot);
    }

    private void TickFoot(NativeArray<float> control, Foot foot, float lift, bool planted, float active, float dt)
    {
        if (!planted) foot.Locked = false;
        if (!planted) EndStep(foot);
        if (planted && !foot.Locked && foot.Bone != null) foot.Locked = Probe(foot);
        bool contacts = foot.Heel != null && foot.Ball != null;
        bool rolled = contacts && foot.Ball.position.y < foot.Heel.position.y - RollMargin;
        if (foot.Locked && rolled && !foot.OnBall && !foot.Stepping)
        {
            foot.OnBall = true;
            foot.Lock = foot.Ball.position;
        }

        float step = FadeSeconds > 0f ? dt / FadeSeconds : 1f;
        foot.Weight = Mathf.MoveTowards(foot.Weight, foot.Locked ? 1f : 0f, step);

        int o = foot.Offset;
        control[o] = foot.Weight * active;
        control[o + 1] = foot.Lock.x;
        control[o + 2] = foot.Position.y + foot.Sole + lift;
        control[o + 3] = foot.Lock.z;
        control[o + 4] = foot.Tilt.x;
        control[o + 5] = foot.Tilt.y;
        control[o + 6] = foot.Tilt.z;
        control[o + 7] = foot.Tilt.w;
        control[o + 8] = foot.HeelGround;
        control[o + 9] = foot.BallGround;
        control[o + 16] = contacts ? 1f : 0f;
        control[o + 17] = foot.OnBall ? 1f : 0f;
        control[o + 18] = foot.Lift;
        if (!contacts) return;

        Quaternion inverse = Quaternion.Inverse(foot.Bone.rotation);
        Vector3 heel = inverse * (foot.Heel.position - foot.Bone.position);
        Vector3 ball = inverse * (foot.Ball.position - foot.Bone.position);
        control[o + 10] = heel.x;
        control[o + 11] = heel.y;
        control[o + 12] = heel.z;
        control[o + 13] = ball.x;
        control[o + 14] = ball.y;
        control[o + 15] = ball.z;
    }

    // Starts a step for the foot that drifted farthest, once neither is mid-step.
    private void Step(NativeArray<float> state, float dt)
    {
        AdvanceStep(left, dt);
        AdvanceStep(right, dt);
        if (left.Stepping || right.Stepping) return;

        Vector3 leftDrift = Drift(left, state, 1);
        Vector3 rightDrift = Drift(right, state, 4);
        bool leftFirst = leftDrift.sqrMagnitude >= rightDrift.sqrMagnitude;
        Foot foot = leftFirst ? left : right;
        Vector3 drift = leftFirst ? leftDrift : rightDrift;
        if (!foot.Locked || drift.magnitude < StepDistance) return;

        foot.Stepping = true;
        foot.StepTime = 0f;
        foot.StepFrom = foot.Lock;
        foot.StepTo = foot.Lock + drift;
        ProbeGround(foot, drift);
    }

    private void AdvanceStep(Foot foot, float dt)
    {
        if (!foot.Stepping) return;

        foot.StepTime = Mathf.Min(foot.StepTime + dt / Mathf.Max(StepSeconds, 0.01f), 1f);
        foot.Lock = Vector3.Lerp(foot.StepFrom, foot.StepTo, Mathf.SmoothStep(0f, 1f, foot.StepTime));
        foot.Lift = Mathf.Sin(foot.StepTime * Mathf.PI) * StepHeight;
        if (foot.StepTime >= 1f) EndStep(foot);
    }

    private static void EndStep(Foot foot)
    {
        foot.Stepping = false;
        foot.Lift = 0f;
    }

    // Sideways gap from where the foot is held to where the animated pose puts its ankle. Zero before the first
    // evaluation has written the animated ankles.
    private static Vector3 Drift(Foot foot, NativeArray<float> state, int slot)
    {
        var animated = new Vector3(state[slot], state[slot + 1], state[slot + 2]);
        if (foot.Bone == null || animated == Vector3.zero) return Vector3.zero;

        Vector3 drift = animated - foot.Bone.position;
        drift.y = 0f;
        return drift;
    }

    // Locks the foot where it is: on the lower contact with contacts, else on the ankle's ground point.
    private bool Probe(Foot foot)
    {
        if (!ProbeGround(foot, Vector3.zero)) return false;

        foot.Lock = foot.Position;
        foot.OnBall = false;
        if (foot.Heel == null || foot.Ball == null) return true;

        foot.OnBall = foot.Ball.position.y < foot.Heel.position.y;
        foot.Lock = foot.OnBall ? foot.Ball.position : foot.Heel.position;
        return true;
    }

    // The ground under the ankle (shifted by offset); with contacts, also under the heel and ball, and their average
    // normal as the tilt.
    private bool ProbeGround(Foot foot, Vector3 offset)
    {
        if (!Ray(foot.Bone.position + offset, out RaycastHit ankle)) return false;

        foot.Position = ankle.point;
        Vector3 normal = ankle.normal;
        foot.HeelGround = ankle.point.y;
        foot.BallGround = ankle.point.y;

        if (foot.Heel != null && Ray(foot.Heel.position + offset, out RaycastHit heel))
        {
            foot.HeelGround = heel.point.y;
            normal += heel.normal;
        }

        if (foot.Ball != null && Ray(foot.Ball.position + offset, out RaycastHit ball))
        {
            foot.BallGround = ball.point.y;
            normal += ball.normal;
        }

        foot.Tilt = Quaternion.FromToRotation(Vector3.up, normal.normalized);
        return true;
    }

    private bool Ray(Vector3 from, out RaycastHit hit)
    {
        return Physics.Raycast(from + Vector3.up * ProbeUp, Vector3.down, out hit, ProbeUp + ProbeDown, Ground,
                               QueryTriggerInteraction.Ignore);
    }
}
