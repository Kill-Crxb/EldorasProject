using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Snap: animating on twos. Every step (1 / fps) the job keys the pose it is given; in between it holds that pose,
// so clips and procedural motion alike read as held key poses (decided 2026-10-25: one quantisation step at the end
// of the graph). The character root stays continuous, or the third person camera would judder.
//
// It holds bones (each humanoid bone's local rotation, and the hips' local position), not muscles: in this layered
// graph the humanoid muscles read back without a masked override layer's contribution, so a held muscle pose put the
// run cycle's arms over a block or cast on the move (2026-10-26). The hips are held with the rest, or the spine's
// counter-rotation stops cancelling them and the chest wobbles.
//
// The step clock runs at the Animator's speed, so a hit-stop (speed 0) holds. A forced key on the frame a freeze
// starts or ends puts the impact pose on screen at once rather than up to a step late.
// control: [0] step seconds, [1] weight, [2] Animator speed, [3] key now.
// state: [0] time since the last key, [1] primed.
// bones: [0] is the hips; the rest are the other humanoid bones being held.
// Burst-compiled, so no Quaternion.Slerp (a native call): rotations blend with Nlerp.

[BurstCompile]
public struct SnapLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<TransformStreamHandle> bones;
    [ReadOnly] public NativeArray<float> control;
    public NativeArray<Quaternion> heldRotation;
    public NativeArray<Vector3> heldHipsPosition;
    public NativeArray<float> state;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        float step = control[0];
        state[0] += stream.deltaTime * control[2];

        if (state[1] == 0f || state[0] >= step || control[3] > 0f)
        {
            Key(stream, step);
            return;
        }

        float weight = control[1];
        if (weight <= 0f) return;

        for (int i = 0; i < bones.Length; i++)
            bones[i].SetLocalRotation(stream, Nlerp(bones[i].GetLocalRotation(stream), heldRotation[i], weight));

        bones[0].SetLocalPosition(stream, Vector3.Lerp(bones[0].GetLocalPosition(stream), heldHipsPosition[0], weight));
    }

    // A long frame (a hitch) would leave the clock several steps behind; start the next step fresh instead.
    private void Key(AnimationStream stream, float step)
    {
        state[0] = state[0] >= step * 2f ? 0f : Mathf.Max(0f, state[0] - step);
        state[1] = 1f;

        heldHipsPosition[0] = bones[0].GetLocalPosition(stream);
        for (int i = 0; i < bones.Length; i++)
            heldRotation[i] = bones[i].GetLocalRotation(stream);
    }

    // Normalised lerp along the shorter arc. Matches slerp at weight 0 and 1, which is where snap normally sits.
    private static Quaternion Nlerp(Quaternion from, Quaternion to, float t)
    {
        float dot = from.x * to.x + from.y * to.y + from.z * to.z + from.w * to.w;
        float sign = dot < 0f ? -1f : 1f;

        float x = from.x + (to.x * sign - from.x) * t;
        float y = from.y + (to.y * sign - from.y) * t;
        float z = from.z + (to.z * sign - from.z) * t;
        float w = from.w + (to.w * sign - from.w) * t;
        float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);

        return new Quaternion(x / length, y / length, z / length, w / length);
    }
}
