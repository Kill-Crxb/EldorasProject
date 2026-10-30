using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// Head-look inside the procedural graph, after gait and before snap, so the head is held with the rest of the body
// and the gait can't overwrite it (the Animator's look-at IK is solved after the whole graph, so it moved smoothly
// over the held poses). HeadLookSystem still decides where to look and how strongly; ProceduralPoseModule switches its
// own IK off (HeadLookSystem.ExternalSolver) and passes the direction here.
// The look is the turn from the body's facing to the look direction (how HeadLookSystem works it out), added over
// whatever the animation does with the head. Chest, neck and head each take a share of the turn; the shares add up
// to the whole turn at the head.
// control: [0] weight, [1..3] look direction (world), [4..6] the body's facing (world, flat), [7 + i] bone i's share.
// bones: chest (upper chest if the model has one), neck (if any), head last.

[BurstCompile]
public struct LookLayerJob : IAnimationJob
{
    public const int Size = 10;

    [ReadOnly] public NativeArray<TransformStreamHandle> bones;
    [ReadOnly] public NativeArray<float> control;

    public void ProcessRootMotion(AnimationStream stream) { }

    public void ProcessAnimation(AnimationStream stream)
    {
        float weight = control[0];
        if (weight <= 0f || bones.Length == 0) return;

        var look = new Vector3(control[1], control[2], control[3]);
        var facing = new Vector3(control[4], control[5], control[6]);
        Quaternion turn = PoseMath.FromTo(facing, look);

        for (int i = 0; i < bones.Length; i++)
        {
            Quaternion share = PoseMath.Nlerp(Quaternion.identity, turn, weight * control[7 + i]);
            bones[i].SetRotation(stream, share * bones[i].GetRotation(stream));
        }
    }
}
