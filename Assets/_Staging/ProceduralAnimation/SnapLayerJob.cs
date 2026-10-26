using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Snap: animating on twos. Every step (1 / fps) the job keeps the pose it is given; in between it holds that pose,
// so clips and procedural motion alike read as held key poses (decided 2026-10-25: one quantisation step at the end
// of the graph). Only muscles are held: the body position and rotation (root, hips) stay continuous, or the third
// person camera would judder. The step clock runs at the Animator's speed, so a hit-stop (speed 0) keeps the held
// pose held. control: [0] step seconds, [1] weight, [2] Animator speed. state: [0] time since the last key, [1] primed.

public struct SnapLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<MuscleHandle> muscles;
    [ReadOnly] public NativeArray<float> control;
    public NativeArray<float> held;
    public NativeArray<float> state;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        if (!stream.isHumanStream) return;

        AnimationHumanStream human = stream.AsHuman();
        float step = control[0];
        state[0] += stream.deltaTime * control[2];

        if (state[1] == 0f || state[0] >= step)
        {
            Key(human, step);
            return;
        }

        float weight = control[1];
        if (weight <= 0f) return;

        for (int i = 0; i < muscles.Length; i++)
            human.SetMuscle(muscles[i], Mathf.Lerp(human.GetMuscle(muscles[i]), held[i], weight));
    }

    // A long frame (a hitch) would leave the clock several steps behind; start the next step fresh instead.
    private void Key(AnimationHumanStream human, float step)
    {
        state[0] = state[0] >= step * 2f ? 0f : Mathf.Max(0f, state[0] - step);
        state[1] = 1f;

        for (int i = 0; i < muscles.Length; i++)
            held[i] = human.GetMuscle(muscles[i]);
    }
}
