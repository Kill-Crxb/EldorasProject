using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Adds the posture's muscle offsets to the pose the Animator produced. Offsets arrive already weighted, so at
// weight 0 the pose passes through untouched. humanStream reports back whether the stream could be read as humanoid
// (1) or not (-1): a graph layered on the Animator through PreviousInputs is what the layering test is checking.

public struct PostureLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<MuscleHandle> muscles;
    [ReadOnly] public NativeArray<float> offsets;
    public NativeArray<int> humanStream;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        if (!stream.isHumanStream)
        {
            humanStream[0] = -1;
            return;
        }

        humanStream[0] = 1;
        AnimationHumanStream human = stream.AsHuman();

        for (int i = 0; i < muscles.Length; i++)
            human.SetMuscle(muscles[i], Mathf.Clamp(human.GetMuscle(muscles[i]) + offsets[i], -1f, 1f));
    }
}
