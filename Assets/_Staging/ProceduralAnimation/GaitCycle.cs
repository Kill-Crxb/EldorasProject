using UnityEngine;

// One baked locomotion loop (Procedural_Gait_Design.md, approach B). A looping walk or run clip sampled at evenly spaced
// points of its cycle: every humanoid bone's local rotation and the hips' local position, plus which foot is planted.
// The gait layer plays it by step phase, driven by distance travelled, so the style comes from the animator's keys and
// the feet keep pace with the body whatever the speed.
//
// Phase 0 is the left foot's touch-down. One cycle is two steps; its length on the ground is strideLegLengths × the
// character's leg length (upper leg + lower leg), so one bake serves every race's proportions.
// Made by Tools → Characters → Gait → Bake Cycle. Never written at runtime.

[CreateAssetMenu(menuName = "CrabSystem/Animation/Gait Cycle", fileName = "GaitCycle_")]
public class GaitCycle : ScriptableObject
{
    [SerializeField] private AnimationClip source;

    [Tooltip("Ground covered by one cycle (two steps), in leg lengths.")]
    [SerializeField] private float strideLegLengths;

    [Tooltip("Leg length of the model the cycle was baked on, metres. Scales the hips' baked position to other bodies.")]
    [SerializeField] private float bakedLegLength;

    [Tooltip("The bones in the order the poses store them. The hips are first.")]
    [SerializeField] private HumanBodyBones[] bones = new HumanBodyBones[0];

    [Tooltip("Sample-major: sample s, bone b is [s × bones + b].")]
    [SerializeField] private Quaternion[] rotations = new Quaternion[0];

    [SerializeField] private Vector3[] hipsPositions = new Vector3[0];
    [SerializeField] private bool[] leftPlanted = new bool[0];
    [SerializeField] private bool[] rightPlanted = new bool[0];

    public AnimationClip Source => source;
    public float StrideLegLengths => strideLegLengths;
    public float BakedLegLength => bakedLegLength;
    public int Samples => hipsPositions.Length;
    public HumanBodyBones[] Bones => bones;
    public Quaternion[] Rotations => rotations;
    public Vector3[] HipsPositions => hipsPositions;
    public bool[] LeftPlanted => leftPlanted;
    public bool[] RightPlanted => rightPlanted;

    // Editor bake only (GaitBakeWindow); SOs are never written at runtime.
    public void Set(AnimationClip clip, float stride, float legLength, HumanBodyBones[] boneOrder, Quaternion[] poses,
                    Vector3[] hips, bool[] left, bool[] right)
    {
        source = clip;
        strideLegLengths = stride;
        bakedLegLength = legLength;
        bones = boneOrder;
        rotations = poses;
        hipsPositions = hips;
        leftPlanted = left;
        rightPlanted = right;
    }

    // Upper leg plus lower leg, in world metres. The same measure the bake used, so strides and hips heights scale by it.
    public static float LegLength(Animator animator)
    {
        Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        if (upper == null || lower == null || foot == null) return 0f;

        return Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, foot.position);
    }
}
