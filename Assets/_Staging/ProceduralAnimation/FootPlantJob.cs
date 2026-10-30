using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Foot planting (Procedural_Gait_Design.md, build step 3). Runs last in the graph, after snap, so a planted foot stays
// on its ground point even while snap holds the rest of the pose.
// Each leg is a two-bone IK (upper leg, lower leg, foot) pulled toward the foot's locked ground point by its weight;
// the foot keeps its animated rotation, tilted to the ground normal.
// Lock: with contact bones the locked point is the contact the foot pivots on (the heel from a heel strike, handed to
// the ball as the foot rolls), and the ankle goes wherever the animated foot's angle puts it from there, so the roll
// moves the ankle as it should. Without them the ankle itself is locked.
// Height: with contact bones (Heel, Ball) the ankle sits where the lower of heel and ball, in the animated foot's
// angle, rests on the ground found under it, so heel strike, toe-off and the roll between come from the animation.
// Without them it falls back to the ground under the ankle plus sole height and the cycle's baked lift.
// Slip: a planted foot lets go as the cycle's own foot moves away from where the lock puts it (stride stretch, turns), from
// slip start to slip end, so the foot slides a little rather than the leg stretching to reach it.
// Body height: the hips move up or down so the cycle's planted foot lands on its ground point (the lower foot wins, so
// stairs and slopes work both ways), smoothed and capped. Held through flight; fades out when planting stops.
// Snap: with snap holding the legs, the targets (lock, lift), weights and body offset are held between snap keys, so a
// standing step or a height change moves in held poses like the rest of the body. The clock copies SnapLayerJob's,
// fed the same numbers each frame, so both key on the same frames. Planted feet stay where they are on the ground.
// control: globals [0] max body offset, [1] slip start, [2] slip end, [3] body smoothing blend, [4] planting active,
// [5] snap step seconds, [6] snap holds the legs, [7] Animator speed, [8] key now (written by ProceduralPoseModule);
// per foot from Left / Right: +0 weight, +1 lock x, +2 fallback y, +3 lock z (lock x, z: the pivot contact's, or the
// ankle's without contacts), +4..7 tilt, +8 ground y under the heel,
// +9 ground y under the ball, +10..12 heel offset and +13..15 ball offset (foot's rotation frame, world scale),
// +16 has contacts, +17 pivot on the ball (else the heel), +18 step lift (added to the target height).
// state: [0] the smoothed body offset; [1..3] left and [4..6] right ankle as animated (before planting, world), read
// back by FootPlanter next frame to decide standing steps; [7] time since the last snap key, [8] primed, [9] held body
// offset, [10..14] left held lock x, lock z, lift, weight, pivot on the ball, [15..19] right.
// legs: [0] hips, [1..3] left upper leg, lower leg, foot, [4..6] right upper leg, lower leg, foot.

[BurstCompile]
public struct FootPlantJob : IAnimationJob
{
    public const int Left = 9;
    public const int Right = 28;
    public const int Size = 47;
    public const int StateSize = 20;

    [ReadOnly] public NativeArray<TransformStreamHandle> legs;
    [ReadOnly] public NativeArray<float> control;
    public NativeArray<float> state;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        if (legs.Length < 7) return;

        WriteAnimated(stream, 3, 1);
        WriteAnimated(stream, 6, 4);

        bool key = Key(stream);
        if (key) HoldTarget(Left);
        if (key) HoldTarget(Right);

        float leftWeight = Weight(stream, 3, Left);
        float rightWeight = Weight(stream, 6, Right);

        float need = float.MaxValue;
        if (leftWeight > 0f) need = Mathf.Min(need, Target(stream, 3, Left).y - legs[3].GetPosition(stream).y);
        if (rightWeight > 0f) need = Mathf.Min(need, Target(stream, 6, Right).y - legs[6].GetPosition(stream).y);

        float goal = need < float.MaxValue ? Mathf.Clamp(need, -control[0], control[0]) : state[0];
        state[0] = Mathf.Lerp(state[0], control[4] > 0f ? goal : 0f, control[3]);

        if (key)
        {
            state[9] = state[0];
            state[Held(Left) + 3] = leftWeight;
            state[Held(Right) + 3] = rightWeight;
        }

        float offset = state[9];
        if (offset != 0f) legs[0].SetPosition(stream, legs[0].GetPosition(stream) + Vector3.up * offset);

        float heldLeft = state[Held(Left) + 3];
        float heldRight = state[Held(Right) + 3];
        if (heldLeft > 0f) Solve(stream, 1, Left, heldLeft);
        if (heldRight > 0f) Solve(stream, 4, Right, heldRight);
    }

    // SnapLayerJob's clock on the same inputs. Every frame is a key when snap isn't holding the legs.
    private bool Key(AnimationStream stream)
    {
        if (control[6] <= 0f) return true;

        float step = control[5];
        state[7] += stream.deltaTime * control[7];
        if (state[8] != 0f && state[7] < step && control[8] <= 0f) return false;

        state[7] = state[7] >= step * 2f ? 0f : Mathf.Max(0f, state[7] - step);
        state[8] = 1f;
        return true;
    }

    private static int Held(int o)
    {
        return o == Left ? 10 : 15;
    }

    private void HoldTarget(int o)
    {
        int h = Held(o);
        state[h] = control[o + 1];
        state[h + 1] = control[o + 3];
        state[h + 2] = control[o + 18];
        state[h + 4] = control[o + 17];
    }

    private void WriteAnimated(AnimationStream stream, int foot, int slot)
    {
        Vector3 p = legs[foot].GetPosition(stream);
        state[slot] = p.x;
        state[slot + 1] = p.y;
        state[slot + 2] = p.z;
    }

    // The foot's weight, faded out as the cycle's foot slips away from the locked point.
    private float Weight(AnimationStream stream, int foot, int o)
    {
        float weight = control[o];
        if (weight <= 0f) return 0f;

        Vector3 offsetToTarget = Target(stream, foot, o) - legs[foot].GetPosition(stream);
        offsetToTarget.y = 0f;
        return weight * Mathf.InverseLerp(control[2], control[1], offsetToTarget.magnitude);
    }

    private void Solve(AnimationStream stream, int upperIndex, int o, float weight)
    {
        TransformStreamHandle upper = legs[upperIndex];
        TransformStreamHandle lower = legs[upperIndex + 1];
        TransformStreamHandle foot = legs[upperIndex + 2];

        Quaternion footRotation = foot.GetRotation(stream);
        Vector3 target = Target(stream, upperIndex + 2, o);

        Vector3 a = upper.GetPosition(stream);
        Vector3 b = lower.GetPosition(stream);
        Vector3 c = foot.GetPosition(stream);
        Vector3 t = Vector3.Lerp(c, target, weight);

        float ab = (b - a).magnitude;
        float bc = (c - b).magnitude;
        float at = Mathf.Min((t - a).magnitude, (ab + bc) * 0.999f);

        Vector3 axis = Vector3.Cross(b - a, c - b);
        if (axis.sqrMagnitude < 1e-8f) axis = upper.GetRotation(stream) * Vector3.right;

        float bend = Triangle((c - a).magnitude, ab, bc) - Triangle(at, ab, bc);
        lower.SetRotation(stream, PoseMath.AxisAngle(axis.normalized, bend) * lower.GetRotation(stream));

        c = foot.GetPosition(stream);
        upper.SetRotation(stream, PoseMath.FromTo(c - a, t - a) * upper.GetRotation(stream));
        foot.SetRotation(stream, PoseMath.Nlerp(footRotation, Tilt(o) * footRotation, weight));
    }

    // Where the ankle goes: back from the locked pivot along the animated foot's angle, at the height that puts the
    // lower contact on its ground.
    private Vector3 Target(AnimationStream stream, int foot, int o)
    {
        int h = Held(o);
        Vector3 locked = new Vector3(state[h], control[o + 2] + state[h + 2], state[h + 1]);
        if (control[o + 16] <= 0f) return locked;

        Quaternion r = Tilt(o) * legs[foot].GetRotation(stream);
        Vector3 heel = r * new Vector3(control[o + 10], control[o + 11], control[o + 12]);
        Vector3 ball = r * new Vector3(control[o + 13], control[o + 14], control[o + 15]);
        Vector3 pivot = state[h + 4] > 0f ? ball : heel;
        float y = Mathf.Max(control[o + 8] - heel.y, control[o + 9] - ball.y) + state[h + 2];
        return new Vector3(locked.x - pivot.x, y, locked.z - pivot.z);
    }

    private Quaternion Tilt(int o)
    {
        return new Quaternion(control[o + 4], control[o + 5], control[o + 6], control[o + 7]);
    }

    // The angle at the knee for a hip-to-foot distance, from the two leg lengths.
    private static float Triangle(float opposite, float side1, float side2)
    {
        float cos = (side1 * side1 + side2 * side2 - opposite * opposite) / (2f * side1 * side2);
        return Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
    }
}
