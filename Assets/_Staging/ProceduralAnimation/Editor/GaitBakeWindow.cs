using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UIElements;

// Tools → Characters → Gait → Bake Cycle. Staging: graduates with the gait layer (Procedural_Gait_Design.md, step 1).
//
// Bakes a looping locomotion clip into a GaitCycle: the clip is sampled at evenly spaced points on a hidden copy of
// the model through a clip playable with Foot IK on (the same correction a state's Foot IK applies: retargeted feet
// pulled back to the clip's own foot positions, which removes the toe dip on bodies proportioned unlike the source),
// and each sample stores every humanoid bone's local rotation and the hips' local position.
// From the same samples it finds:
//   planted feet  a foot is planted while it moves backward relative to the hips and its lowest contact (Heel / Ball
//                 bones, or the ankle without them) is on the ground: a running foot keeps moving back after it
//                 lifts, and those samples are swing;
//   stride        the clip's root-motion speed × its length is the ground one cycle covers, stored in leg lengths
//                 (in-place clips fall back to how fast a planted foot slides back relative to the hips);
//   lift          each ankle's height above the cycle's lowest ankle point, per sample (heel-up landings);
//   phase 0       the samples are rotated so the cycle starts at the left foot's touch-down;
//   travel        a root-motion clip carries the hips forward over the loop; that drift is taken out of the hips
//                 positions, since the character's movement already moves the body.
// Bake on the base body; races share its skeleton.

public class GaitBakeWindow : EditorWindow
{
    private const int DefaultSamples = 32;

    // A foot counts as on the ground while its lowest contact is within this of that contact's lowest point in the
    // cycle, metres.
    private const float GroundTolerance = 0.03f;

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

        // Baking over an existing cycle keeps its GUID, so prefabs that point at it stay assigned.
        var existing = AssetDatabase.LoadAssetAtPath<GaitCycle>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(cycle, existing);
            existing.name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorUtility.SetDirty(existing);
            DestroyImmediate(cycle);
            cycle = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(cycle, path);
        }

        AssetDatabase.SaveAssets();
        Selection.activeObject = cycle;
    }

    // Poses the copy at a time. Setting the time twice zeroes the delta, so nothing blends from the previous sample.
    private static void SampleAt(PlayableGraph graph, AnimationClipPlayable playable, float time)
    {
        playable.SetTime(time);
        playable.SetTime(time);
        graph.Evaluate();
    }

    private static GaitCycle Sample(GameObject copy, Animator animator, AnimationClip clip, int samples)
    {
        HumanBodyBones[] boneOrder = BoneOrder(animator);
        Transform[] bones = Array.ConvertAll(boneOrder, animator.GetBoneTransform);
        Transform root = copy.transform;
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        PlayableGraph graph = PlayableGraph.Create("GaitBake");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(true);
        AnimationPlayableOutput.Create(graph, "GaitBake", animator).SetSourcePlayable(playable);

        var rotations = new Quaternion[samples * bones.Length];
        var hips = new Vector3[samples];
        var left = new Vector3[samples];
        var right = new Vector3[samples];
        var leftLow = new float[samples];
        var rightLow = new float[samples];

        for (int s = 0; s < samples; s++)
        {
            SampleAt(graph, playable, clip.length * s / samples);

            for (int b = 0; b < bones.Length; b++)
                rotations[s * bones.Length + b] = bones[b].localRotation;

            hips[s] = bones[0].localPosition;
            left[s] = root.InverseTransformPoint(leftFoot.position) - root.InverseTransformPoint(bones[0].position);
            right[s] = root.InverseTransformPoint(rightFoot.position) - root.InverseTransformPoint(bones[0].position);
            leftLow[s] = ContactHeight(root, leftFoot, "L");
            rightLow[s] = ContactHeight(root, rightFoot, "R");
        }

        // A looping clip's playable wraps at its full length, so the end is read just before it and scaled up.
        float end = clip.length * 0.9999f;
        SampleAt(graph, playable, end);
        graph.Destroy();
        Vector3 drift = (bones[0].localPosition - hips[0]) * (clip.length / end);
        for (int s = 0; s < samples; s++)
            hips[s] -= drift * s / samples;

        bool[] leftPlanted = Planted(left, leftLow);
        bool[] rightPlanted = Planted(right, rightLow);
        float step = clip.length / samples;
        // Root-motion travel matched Crxb's test (walk at 2.36 leg lengths looked right); the planted-foot measure read
        // about 0.6 of it on every clip. Feet are the fallback for in-place clips (no RootT curves).
        float speed = RootTravel(clip) * animator.humanScale / clip.length;
        if (speed <= 0f)
            speed = (PlantedSpeed(left, leftPlanted, step) + PlantedSpeed(right, rightPlanted, step)) * 0.5f;

        float legLength = GaitCycle.LegLength(animator);

        if (speed <= 0f || legLength <= 0f)
        {
            Debug.LogError($"[GaitBake] Couldn't measure a stride from {clip.name}. Is it a looping forward walk or run?", clip);
            return null;
        }

        int start = TouchDown(leftPlanted);
        var cycle = CreateInstance<GaitCycle>();
        float ground = Mathf.Min(Lowest(left), Lowest(right));
        float[] leftLift = Array.ConvertAll(left, p => p.y - ground);
        float[] rightLift = Array.ConvertAll(right, p => p.y - ground);

        cycle.Set(clip, speed * clip.length / legLength, legLength, boneOrder,
                  Rotate(rotations, start * bones.Length), Rotate(hips, start), Rotate(leftPlanted, start), Rotate(rightPlanted, start),
                  Rotate(leftLift, start), Rotate(rightLift, start));
        return cycle;
    }

    // Ground covered by the clip's humanoid root motion over one loop (normalised units; × humanScale for metres).
    // Read from the RootT curves: clip.averageSpeed came back 0 on some bakes.
    private static float RootTravel(AnimationClip clip)
    {
        AnimationCurve x = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
        AnimationCurve z = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"));
        if (x == null || z == null) return 0f;

        float dx = x.Evaluate(clip.length) - x.Evaluate(0f);
        float dz = z.Evaluate(clip.length) - z.Evaluate(0f);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static float Lowest(Vector3[] foot)
    {
        float lowest = float.MaxValue;
        foreach (var p in foot) lowest = Mathf.Min(lowest, p.y);
        return lowest;
    }

    // The hips first (its position is baked too), then every other mapped humanoid bone.
    private static HumanBodyBones[] BoneOrder(Animator animator)
    {
        var order = new List<HumanBodyBones> { HumanBodyBones.Hips };
        for (var bone = HumanBodyBones.LeftUpperLeg; bone < HumanBodyBones.LastBone; bone++)
            if (animator.GetBoneTransform(bone) != null) order.Add(bone);

        return order.ToArray();
    }

    // A foot is planted while it moves backward relative to the hips (the body passing over it) and its lowest contact
    // is on the ground. Direction alone counts a lifted running foot as planted; ankle height alone misreads heel-off.
    private static bool[] Planted(Vector3[] foot, float[] low)
    {
        float ground = Mathf.Min(low) + GroundTolerance;
        var planted = new bool[foot.Length];
        for (int s = 0; s < foot.Length; s++)
            planted[s] = foot[(s + 1) % foot.Length].z < foot[s].z && low[s] <= ground;

        return planted;
    }

    // Height of the foot's lowest contact (Heel / Ball bone, or the ankle without them) in the model's space.
    private static float ContactHeight(Transform root, Transform foot, string side)
    {
        Transform heel = foot.Find("Heel." + side);
        Transform ball = foot.Find("Ball." + side);
        if (heel == null || ball == null) return root.InverseTransformPoint(foot.position).y;

        return Mathf.Min(root.InverseTransformPoint(heel.position).y, root.InverseTransformPoint(ball.position).y);
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
