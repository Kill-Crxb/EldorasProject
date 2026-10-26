using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Snap: animating on twos. Every step (1 / fps) the job keys the pose it is given; in between it holds that pose,
// so clips and procedural motion alike read as held key poses (decided 2026-10-25: one quantisation step at the end
// of the graph). The character root stays continuous, or the third person camera would judder.
//
// Two ways to hold (control[5]): bones (each humanoid bone's local rotation, and the hips' local position) or
// muscles. Bones is the default: in this layered graph the humanoid muscles read back without a masked override
// layer's contribution (Actions Upper's block arms read as the run cycle), so holding muscles wrote the wrong arms
// over the real pose (found 2026-10-26). Bones read the pose the Animator actually produced.
//
// The body (hips) is held with the rest by default. Holding only the limbs left the hips turning smoothly under a
// held spine, so the spine's counter-rotation stopped cancelling the hips and the chest wobbled whenever the hips
// swung (strafing, blocking on the move).
//
// The step clock runs at the Animator's speed, so a hit-stop (speed 0) holds. A forced key on the frame a freeze
// starts or ends puts the impact pose on screen at once rather than up to a step late.
// control: [0] step seconds, [1] weight, [2] Animator speed, [3] hold body, [4] key now, [5] hold bones.
// state: [0] time since the last key, [1] primed.
// bones: [0] is the hips; the rest are the other humanoid bones being held.
// probe (read by SnapTrace): [0] evaluations since last read, [1] keyed, [2] left arm down-up in, [3] out,
// [4] left arm front-back in, [5] out.

public struct SnapLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<MuscleHandle> muscles;
    [ReadOnly] public NativeArray<TransformStreamHandle> bones;
    [ReadOnly] public NativeArray<float> control;
    public NativeArray<float> held;
    public NativeArray<Quaternion> heldBoneRotation;
    public NativeArray<Vector3> heldHipsPosition;
    public NativeArray<Vector3> heldBodyPosition;
    public NativeArray<Quaternion> heldBodyRotation;
    public NativeArray<float> state;
    public NativeArray<float> probe;
    public MuscleHandle probeDownUp;
    public MuscleHandle probeFrontBack;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        if (!stream.isHumanStream) return;

        AnimationHumanStream human = stream.AsHuman();
        probe[0] += 1f;
        probe[2] = human.GetMuscle(probeDownUp);
        probe[4] = human.GetMuscle(probeFrontBack);
        probe[1] = 0f;

        Snap(stream, human);

        probe[3] = human.GetMuscle(probeDownUp);
        probe[5] = human.GetMuscle(probeFrontBack);
    }

    private void Snap(AnimationStream stream, AnimationHumanStream human)
    {
        float step = control[0];
        state[0] += stream.deltaTime * control[2];

        if (state[1] == 0f || state[0] >= step || control[4] > 0f)
        {
            Key(stream, human, step);
            probe[1] = 1f;
            return;
        }

        float weight = control[1];
        if (weight <= 0f) return;

        if (control[5] > 0f)
        {
            HoldBones(stream, weight);
            return;
        }

        HoldMuscles(human, weight);
    }

    // A long frame (a hitch) would leave the clock several steps behind; start the next step fresh instead.
    private void Key(AnimationStream stream, AnimationHumanStream human, float step)
    {
        state[0] = state[0] >= step * 2f ? 0f : Mathf.Max(0f, state[0] - step);
        state[1] = 1f;

        for (int i = 0; i < muscles.Length; i++)
            held[i] = human.GetMuscle(muscles[i]);

        heldBodyPosition[0] = human.bodyLocalPosition;
        heldBodyRotation[0] = human.bodyLocalRotation;

        if (bones.Length == 0) return;

        heldHipsPosition[0] = bones[0].GetLocalPosition(stream);
        for (int i = 0; i < bones.Length; i++)
            heldBoneRotation[i] = bones[i].GetLocalRotation(stream);
    }

    // The hips (bones[0]) hold position and rotation only when the body is held; the rest always hold rotation.
    private void HoldBones(AnimationStream stream, float weight)
    {
        int first = control[3] > 0f ? 0 : 1;
        for (int i = first; i < bones.Length; i++)
            bones[i].SetLocalRotation(stream, Quaternion.Slerp(bones[i].GetLocalRotation(stream), heldBoneRotation[i], weight));

        if (first > 0 || bones.Length == 0) return;
        bones[0].SetLocalPosition(stream, Vector3.Lerp(bones[0].GetLocalPosition(stream), heldHipsPosition[0], weight));
    }

    private void HoldMuscles(AnimationHumanStream human, float weight)
    {
        for (int i = 0; i < muscles.Length; i++)
            human.SetMuscle(muscles[i], Mathf.Lerp(human.GetMuscle(muscles[i]), held[i], weight));

        if (control[3] <= 0f) return;

        human.bodyLocalPosition = Vector3.Lerp(human.bodyLocalPosition, heldBodyPosition[0], weight);
        human.bodyLocalRotation = Quaternion.Slerp(human.bodyLocalRotation, heldBodyRotation[0], weight);
    }
}
