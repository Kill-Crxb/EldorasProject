using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;

// A posture: a partial pose of the spine, chest, upper chest, neck and shoulders as humanoid muscle offsets (-1..1),
// added on top of whatever the Animator plays. Postures stack additively (decided 2026-10-22); the job clamps the
// sum to the muscle range. Humanoid muscles are mirrored, so one shoulder value moves both sides the same way.
// Signs are Unity's muscle space: front-back positive bends backward (negative hunches), and the neck likewise.
// Authored by hand or captured from a clip frame (Tools → Characters → Posture → Capture From Clip).

[CreateAssetMenu(menuName = "CrabSystem/Animation/Posture Profile", fileName = "PostureProfile_")]
public class PostureProfile : ScriptableObject
{
    [Tooltip("Seconds to blend in when the posture starts and out when it ends.")]
    [Min(0.01f)] [SerializeField] private float fadeSeconds = 0.25f;

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

    public float FadeSeconds => fadeSeconds;

    // The order Add and Set use. Keep the three in step.
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

    public void Add(NativeArray<float> sums, float weight)
    {
        sums[0] += spineFrontBack * weight;
        sums[1] += spineLeftRight * weight;
        sums[2] += spineTwist * weight;
        sums[3] += chestFrontBack * weight;
        sums[4] += chestLeftRight * weight;
        sums[5] += chestTwist * weight;
        sums[6] += upperChestFrontBack * weight;
        sums[7] += upperChestLeftRight * weight;
        sums[8] += upperChestTwist * weight;
        sums[9] += neckFrontBack * weight;
        sums[10] += neckLeftRight * weight;
        sums[11] += neckTwist * weight;
        sums[12] += shoulderDownUp * weight;
        sums[13] += shoulderDownUp * weight;
        sums[14] += shoulderFrontBack * weight;
        sums[15] += shoulderFrontBack * weight;
    }

    // Editor capture only (PostureCaptureWindow); SOs are never written at runtime. Shoulders take the mean of both sides.
    public void Set(float[] muscles)
    {
        spineFrontBack = muscles[0];
        spineLeftRight = muscles[1];
        spineTwist = muscles[2];
        chestFrontBack = muscles[3];
        chestLeftRight = muscles[4];
        chestTwist = muscles[5];
        upperChestFrontBack = muscles[6];
        upperChestLeftRight = muscles[7];
        upperChestTwist = muscles[8];
        neckFrontBack = muscles[9];
        neckLeftRight = muscles[10];
        neckTwist = muscles[11];
        shoulderDownUp = (muscles[12] + muscles[13]) * 0.5f;
        shoulderFrontBack = (muscles[14] + muscles[15]) * 0.5f;
    }
}
