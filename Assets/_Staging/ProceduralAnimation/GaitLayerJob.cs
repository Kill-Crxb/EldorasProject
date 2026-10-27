using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Plays a GaitCycle by step phase: the two baked poses either side of the phase are blended and written over the
// Animator's pose. Bones, not muscles, for the same reason as snap (muscle reads are unreliable in this graph).
// Two weights: lower body (hips and legs) and upper body (spine, arms, head), so an action playing on the upper body
// (a block or cast on the move) keeps it while the gait drives the legs.
// Foot planting is not here yet (gait design, build step 3): planted feet follow the cycle, so they can slide on slopes.
// control: [0] phase 0..1, [1] lower weight, [2] upper weight, [3] hips scale (leg length ÷ baked leg length).
// bones[0] is the hips; upper[b] is 1 for upper-body bones.

[BurstCompile]
public struct GaitLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<TransformStreamHandle> bones;
    [ReadOnly] public NativeArray<byte> upper;
    [ReadOnly] public NativeArray<Quaternion> rotations;
    [ReadOnly] public NativeArray<Vector3> hipsPositions;
    [ReadOnly] public NativeArray<float> control;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        int samples = hipsPositions.Length;
        if (samples == 0 || bones.Length == 0) return;

        float lowerWeight = control[1];
        float upperWeight = control[2];
        if (lowerWeight <= 0f && upperWeight <= 0f) return;

        float position = control[0] * samples;
        int from = (int)position % samples;
        int to = (from + 1) % samples;
        float t = position - (int)position;

        int count = bones.Length;
        for (int b = 0; b < count; b++)
        {
            float weight = upper[b] > 0 ? upperWeight : lowerWeight;
            if (weight <= 0f) continue;

            Quaternion pose = PoseMath.Nlerp(rotations[from * count + b], rotations[to * count + b], t);
            bones[b].SetLocalRotation(stream, PoseMath.Nlerp(bones[b].GetLocalRotation(stream), pose, weight));
        }

        if (lowerWeight <= 0f) return;

        Vector3 hips = Vector3.Lerp(hipsPositions[from], hipsPositions[to], t) * control[3];
        bones[0].SetLocalPosition(stream, Vector3.Lerp(bones[0].GetLocalPosition(stream), hips, lowerWeight));
    }
}
