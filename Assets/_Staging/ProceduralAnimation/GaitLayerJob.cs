using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Plays a GaitCycle by step phase: the two baked poses either side of the phase are blended and written over the
// Animator's pose. Bones, not muscles, for the same reason as snap (muscle reads are unreliable in this graph).
// Two weights: lower body (hips and legs) and upper body (spine, arms, head), so an action playing on the upper body
// (a block or cast on the move) keeps it while the gait drives the legs.
// Foot planting is not here yet (gait design, build step 3): planted feet follow the cycle, so they can slide on slopes.
// Hips position: the cycle's bob and sway around its own average, placed on the Animator's average hips position
// (smoothed over about half a second). Writing the baked position as is sank the body into the floor: the runtime
// Animator holds the hips at a different height from the baker's still, root-motion-free copy.
// Cycles (walk, run, sprint) sit end to end in rotations and hipsPositions; two neighbours blend by speed. They share
// sample count and bone order and all start at the left foot's touch-down, so they blend phase-matched.
// control: [0] phase 0..1, [1] lower weight, [2] upper weight, [3] hips scale (leg length ÷ baked leg length),
// [4] hips smoothing blend for this frame, [5] blend from cycle [6] to cycle [7], [8] samples per cycle,
// [9..12] lean and bank (world rotation applied to the hips at the lower weight), [13] bob scale (load),
// [14..16] up in the hips' parent space (the axis the bob scale works along), [17] stop momentum pitch in degrees,
// [18..20] its axis (world, the body's right).
// Stop momentum: after the cycle, the torso bones (spine, chest) pitch forward by control[17], split between them. It
// is not scaled by the gait weights, since the gait is fading out as the body stops.
// hipsState: [0] the Animator's smoothed hips position, [1 + c] cycle c's average hips position.
// bones[0] is the hips; upper[b] is 1 for upper-body bones.

[BurstCompile]
public struct GaitLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<TransformStreamHandle> bones;
    [ReadOnly] public NativeArray<byte> upper;
    [ReadOnly] public NativeArray<TransformStreamHandle> torso;
    [ReadOnly] public NativeArray<Quaternion> rotations;
    [ReadOnly] public NativeArray<Vector3> hipsPositions;
    [ReadOnly] public NativeArray<float> control;
    public NativeArray<Vector3> hipsState;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        PlayCycle(stream);
        Momentum(stream);
    }

    private void Momentum(AnimationStream stream)
    {
        float pitch = control[17];
        if (pitch == 0f || torso.Length == 0) return;

        var axis = new Vector3(control[18], control[19], control[20]);
        Quaternion share = PoseMath.AxisAngle(axis, pitch * Mathf.Deg2Rad / torso.Length);
        for (int i = 0; i < torso.Length; i++)
            torso[i].SetRotation(stream, share * torso[i].GetRotation(stream));
    }

    private void PlayCycle(AnimationStream stream)
    {
        int samples = (int)control[8];
        if (samples == 0 || bones.Length == 0) return;

        Vector3 animated = bones[0].GetLocalPosition(stream);
        hipsState[0] = hipsState[0] == Vector3.zero ? animated : Vector3.Lerp(hipsState[0], animated, control[4]);

        float lowerWeight = control[1];
        float upperWeight = control[2];
        if (lowerWeight <= 0f && upperWeight <= 0f) return;

        float position = control[0] * samples;
        int from = (int)position % samples;
        int to = (from + 1) % samples;
        float t = position - (int)position;
        float blend = control[5];
        int a = (int)control[6];
        int b = (int)control[7];

        int count = bones.Length;
        for (int i = 0; i < count; i++)
        {
            float weight = upper[i] > 0 ? upperWeight : lowerWeight;
            if (weight <= 0f) continue;

            Quaternion pose = PoseMath.Nlerp(Sample(a, from, to, t, i, samples), Sample(b, from, to, t, i, samples), blend);
            bones[i].SetLocalRotation(stream, PoseMath.Nlerp(bones[i].GetLocalRotation(stream), pose, weight));
        }

        if (lowerWeight <= 0f) return;

        Vector3 swing = Vector3.Lerp(Swing(a, from, to, t, samples), Swing(b, from, to, t, samples), blend) * control[3];
        var up = new Vector3(control[14], control[15], control[16]);
        swing += up * (Vector3.Dot(swing, up) * (control[13] - 1f));
        bones[0].SetLocalPosition(stream, Vector3.Lerp(animated, hipsState[0] + swing, lowerWeight));

        var leanBank = new Quaternion(control[9], control[10], control[11], control[12]);
        Quaternion lean = PoseMath.Nlerp(Quaternion.identity, leanBank, lowerWeight);
        bones[0].SetRotation(stream, lean * bones[0].GetRotation(stream));
    }

    private Quaternion Sample(int cycle, int from, int to, float t, int bone, int samples)
    {
        int start = cycle * samples * bones.Length;
        return PoseMath.Nlerp(rotations[start + from * bones.Length + bone], rotations[start + to * bones.Length + bone], t);
    }

    private Vector3 Swing(int cycle, int from, int to, float t, int samples)
    {
        int start = cycle * samples;
        return Vector3.Lerp(hipsPositions[start + from], hipsPositions[start + to], t) - hipsState[1 + cycle];
    }
}
