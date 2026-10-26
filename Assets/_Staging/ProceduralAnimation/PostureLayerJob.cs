using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Adds the posture's muscle offsets to the pose the Animator produced. Offsets arrive already weighted, so at
// weight 0 the pose passes through untouched. Burst-compiled.

[BurstCompile]
public struct PostureLayerJob : IAnimationJob
{
    [ReadOnly] public NativeArray<MuscleHandle> muscles;
    [ReadOnly] public NativeArray<float> offsets;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        if (!stream.isHumanStream) return;

        AnimationHumanStream human = stream.AsHuman();
        for (int i = 0; i < muscles.Length; i++)
            human.SetMuscle(muscles[i], Mathf.Clamp(human.GetMuscle(muscles[i]) + offsets[i], -1f, 1f));
    }
}
