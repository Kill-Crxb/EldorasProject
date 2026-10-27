using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Tools → Characters → Gait → Bake Cycle. Staging: graduates with the gait layer (Procedural_Gait_Design.md, step 1).
//
// Bakes a looping locomotion clip into a GaitCycle: the clip is sampled at evenly spaced points on a hidden copy of
// the model, and each sample stores every humanoid bone's local rotation and the hips' local position.
// From the same samples it finds:
//   planted feet  a foot is planted while it is near its lowest point in the cycle;
//   stride        while a foot is planted it slides back relative to the hips at the walking speed, so speed × the
//                 clip's length is the ground one cycle covers, stored in leg lengths;
//   phase 0       the samples are rotated so the cycle starts at the left foot's touch-down;
//   travel        a root-motion clip carries the hips forward over the loop; that drift is taken out of the hips
//                 positions, since the character's movement already moves the body.
// Bake on the base body; races share its skeleton.

public class GaitBakeWindow : EditorWindow
{
    private const int DefaultSamples = 32;

    // A foot within this share of its height range above its lowest point counts as planted.
    private const float PlantedShare = 0.15f;

    private ObjectField modelField;
    private ObjectField clipField;
    private IntegerField samplesField;

    [MenuItem("Tools/Characters/Gait/Bake Cycle")]
    private static void Open()
    {
        GetWindow<GaitBakeWindow>("Gait Bake");
    }

    private void CreateGUI()
    {
        modelField = new ObjectField("Model") { objectType = typeof(GameObject), allowSceneObjects = false };
        clipField = new ObjectField("Looping clip") { objectType = typeof(AnimationClip), allowSceneObjects = false };
        samplesField = new IntegerField("Samples per cycle") { value = DefaultSamples };

        rootVisualElement.Add(modelField);
        rootVisualElement.Add(clipField);
        rootVisualElement.Add(samplesField);
        rootVisualElement.Add(new Button(Bake) { text = "Bake to new Gait Cycle" });
    }

    private void Bake()
    {
        var model = modelField.value as GameObject;
        var clip = clipField.value as AnimationClip;
        int samples = Mathf.Max(4, samplesField.value);
        if (model == null || clip == null)
        {
            Debug.LogError("[GaitBake] Set a model and a looping clip.");
            return;
        }

        var copy = Instantiate(model);
        copy.hideFlags = HideFlags.HideAndDontSave;
        var animator = copy.GetComponentInChildren<Animator>(true);

        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError($"[GaitBake] {model.name} has no humanoid Avatar.", model);
            DestroyImmediate(copy);
            return;
        }

        var cycle = Sample(copy, animator, clip, samples);
        DestroyImmediate(copy);
        if (cycle == null) return;

        string path = EditorUtility.SaveFilePanelInProject("Save Gait Cycle", "GaitCycle_" + clip.name, "asset", "Where to save the baked cycle.");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(cycle, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cycle;
    }

    private static GaitCycle Sample(GameObject copy, Animator animator, AnimationClip clip, int samples)
    {
        HumanBodyBones[] boneOrder = BoneOrder(animator);
        Transform[] bones = Array.ConvertAll(boneOrder, animator.GetBoneTransform);
        Transform root = copy.transform;
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        var rotations = new Quaternion[samples * bones.Length];
        var hips = new Vector3[samples];
        var left = new Vector3[samples];
        var right = new Vector3[samples];

        for (int s = 0; s < samples; s++)
        {
            clip.SampleAnimation(copy, clip.length * s / samples);

            for (int b = 0; b < bones.Length; b++)
                rotations[s * bones.Length + b] = bones[b].localRotation;

            hips[s] = bones[0].localPosition;
            left[s] = root.InverseTransformPoint(leftFoot.position) - root.InverseTransformPoint(bones[0].position);
            right[s] = root.InverseTransformPoint(rightFoot.position) - root.InverseTransformPoint(bones[0].position);
        }

        clip.SampleAnimation(copy, clip.length);
        Vector3 drift = bones[0].localPosition - hips[0];
        for (int s = 0; s < samples; s++)
            hips[s] -= drift * s / samples;

        bool[] leftPlanted = Planted(left);
        bool[] rightPlanted = Planted(right);
        float step = clip.length / samples;
        float speed = (PlantedSpeed(left, leftPlanted, step) + PlantedSpeed(right, rightPlanted, step)) * 0.5f;
        float legLength = GaitCycle.LegLength(animator);

        if (speed <= 0f || legLength <= 0f)
        {
            Debug.LogError($"[GaitBake] Couldn't measure a stride from {clip.name}. Is it a looping forward walk or run?", clip);
            return null;
        }

        int start = TouchDown(leftPlanted);
        var cycle = CreateInstance<GaitCycle>();
        cycle.Set(clip, speed * clip.length / legLength, legLength, boneOrder,
                  Rotate(rotations, start * bones.Length), Rotate(hips, start), Rotate(leftPlanted, start), Rotate(rightPlanted, start));
        return cycle;
    }

    // The hips first (its position is baked too), then every other mapped humanoid bone.
    private static HumanBodyBones[] BoneOrder(Animator animator)
    {
        var order = new List<HumanBodyBones> { HumanBodyBones.Hips };
        for (var bone = HumanBodyBones.Spine; bone < HumanBodyBones.LastBone; bone++)
            if (animator.GetBoneTransform(bone) != null) order.Add(bone);

        return order.ToArray();
    }

    private static bool[] Planted(Vector3[] foot)
    {
        float lowest = float.MaxValue;
        float highest = float.MinValue;
        foreach (var p in foot)
        {
            lowest = Mathf.Min(lowest, p.y);
            highest = Mathf.Max(highest, p.y);
        }

        float limit = lowest + (highest - lowest) * PlantedShare;
        return Array.ConvertAll(foot, p => p.y <= limit);
    }

    // A planted foot slides backward relative to the hips at the walking speed (forward is the model's +z).
    private static float PlantedSpeed(Vector3[] foot, bool[] planted, float step)
    {
        float distance = 0f;
        int pairs = 0;

        for (int s = 0; s < foot.Length; s++)
        {
            int next = (s + 1) % foot.Length;
            if (!planted[s] || !planted[next]) continue;

            distance += foot[s].z - foot[next].z;
            pairs++;
        }

        return pairs > 0 ? distance / (pairs * step) : 0f;
    }

    // The first sample where the foot lands after being in the air; 0 if it never lifts.
    private static int TouchDown(bool[] planted)
    {
        for (int s = 0; s < planted.Length; s++)
            if (planted[s] && !planted[(s + planted.Length - 1) % planted.Length]) return s;

        return 0;
    }

    private static T[] Rotate<T>(T[] values, int start)
    {
        var rotated = new T[values.Length];
        for (int i = 0; i < values.Length; i++)
            rotated[i] = values[(i + start) % values.Length];

        return rotated;
    }
}
