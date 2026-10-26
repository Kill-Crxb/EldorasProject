using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// A posture: a partial pose of the spine, chest, upper chest, neck and shoulders as humanoid muscle offsets (-1..1),
// added on top of whatever the Animator plays. Postures stack additively (decided 2026-10-22); the job clamps the
// sum to the muscle range. Humanoid muscles are mirrored, so one shoulder value moves both sides the same way.
// Signs are Unity's muscle space: front-back positive bends backward (negative hunches), and the neck likewise.
// Authored by hand for now; the capture tool (a Cascadeur single-frame clip) comes with the posture layer.

[CreateAssetMenu(menuName = "CrabSystem/Animation/Posture Profile", fileName = "PostureProfile_")]
public class PostureProfile : ScriptableObject
{
    [Header("Spine")]
    [Range(-1f, 1f)] [SerializeField] private float spineFrontBack;
    [Range(-1f, 1f)] [SerializeField] private float spineLeftRight;
    [Range(-1f, 1f)] [SerializeField] private float spineTwist;

    [Header("Chest")]
    [Range(-1f, 1f)] [SerializeField] private float chestFrontBack;
    [Range(-1f, 1f)] [SerializeField] private float chestLeftRight;
    [Range(-1f, 1f)] [SerializeField] private float chestTwist;

    [Header("Upper Chest")]
    [Range(-1f, 1f)] [SerializeField] private float upperChestFrontBack;
    [Range(-1f, 1f)] [SerializeField] private float upperChestLeftRight;
    [Range(-1f, 1f)] [SerializeField] private float upperChestTwist;

    [Header("Neck")]
    [Range(-1f, 1f)] [SerializeField] private float neckFrontBack;
    [Range(-1f, 1f)] [SerializeField] private float neckLeftRight;
    [Range(-1f, 1f)] [SerializeField] private float neckTwist;

    [Header("Shoulders (both)")]
    [Range(-1f, 1f)] [SerializeField] private float shoulderDownUp;
    [Range(-1f, 1f)] [SerializeField] private float shoulderFrontBack;

    // The order Write fills. Keep the two in step.
    public static MuscleHandle[] Muscles()
    {
        return new[]
        {
            new MuscleHandle(BodyDof.SpineFrontBack),
            new MuscleHandle(BodyDof.SpineLeftRight),
            new MuscleHandle(BodyDof.SpineRollLeftRight),
            new MuscleHandle(BodyDof.ChestFrontBack),
            new MuscleHandle(BodyDof.ChestLeftRight),
            new MuscleHandle(BodyDof.ChestRollLeftRight),
            new MuscleHandle(BodyDof.UpperChestFrontBack),
            new MuscleHandle(BodyDof.UpperChestLeftRight),
            new MuscleHandle(BodyDof.UpperChestRollLeftRight),
            new MuscleHandle(HeadDof.NeckFrontBack),
            new MuscleHandle(HeadDof.NeckLeftRight),
            new MuscleHandle(HeadDof.NeckRollLeftRight),
            new MuscleHandle(HumanPartDof.LeftArm, ArmDof.ShoulderDownUp),
            new MuscleHandle(HumanPartDof.RightArm, ArmDof.ShoulderDownUp),
            new MuscleHandle(HumanPartDof.LeftArm, ArmDof.ShoulderFrontBack),
            new MuscleHandle(HumanPartDof.RightArm, ArmDof.ShoulderFrontBack)
        };
    }

    public void Write(NativeArray<float> offsets, float weight)
    {
        offsets[0] = spineFrontBack * weight;
        offsets[1] = spineLeftRight * weight;
        offsets[2] = spineTwist * weight;
        offsets[3] = chestFrontBack * weight;
        offsets[4] = chestLeftRight * weight;
        offsets[5] = chestTwist * weight;
        offsets[6] = upperChestFrontBack * weight;
        offsets[7] = upperChestLeftRight * weight;
        offsets[8] = upperChestTwist * weight;
        offsets[9] = neckFrontBack * weight;
        offsets[10] = neckLeftRight * weight;
        offsets[11] = neckTwist * weight;
        offsets[12] = shoulderDownUp * weight;
        offsets[13] = shoulderDownUp * weight;
        offsets[14] = shoulderFrontBack * weight;
        offsets[15] = shoulderFrontBack * weight;
    }
}
