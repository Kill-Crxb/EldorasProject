using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;

// Tools → Characters → Posture → Capture From Clip. Staging: graduates with the posture layer.
//
// The one-button posture authoring from Procedural_Animation_Theory.md: pose a posture in Cascadeur as a
// single-frame clip, then capture its spine, chest, upper chest, neck and shoulder muscles into a new PostureProfile.
// A posture is added on top of whatever the Animator plays, so it should be the difference from a neutral stance:
// give a reference clip (a neutral standing frame) and the capture is pose minus reference. Without one, the pose is
// taken against the Avatar's zero-muscle stance. The model is any humanoid prefab; it is sampled on a hidden copy.

public class PostureCaptureWindow : EditorWindow
{
    private ObjectField modelField;
    private ObjectField poseField;
    private ObjectField referenceField;
    private FloatField timeField;

    [MenuItem("Tools/Characters/Posture/Capture From Clip")]
    private static void Open()
    {
        GetWindow<PostureCaptureWindow>("Posture Capture");
    }

    private void CreateGUI()
    {
        modelField = new ObjectField("Model") { objectType = typeof(GameObject), allowSceneObjects = false };
        poseField = new ObjectField("Posture clip") { objectType = typeof(AnimationClip), allowSceneObjects = false };
        referenceField = new ObjectField("Neutral clip (optional)") { objectType = typeof(AnimationClip), allowSceneObjects = false };
        timeField = new FloatField("Frame time (s)");

        rootVisualElement.Add(modelField);
        rootVisualElement.Add(poseField);
        rootVisualElement.Add(referenceField);
        rootVisualElement.Add(timeField);
        rootVisualElement.Add(new Button(Capture) { text = "Capture to new Posture Profile" });
    }

    private void Capture()
    {
        var model = modelField.value as GameObject;
        var pose = poseField.value as AnimationClip;
        if (model == null || pose == null)
        {
            Debug.LogError("[PostureCapture] Set a model and a posture clip.");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("Save Posture Profile", "PostureProfile_" + pose.name, "asset", "Where to save the captured posture.");
        if (string.IsNullOrEmpty(path)) return;

        var copy = Instantiate(model);
        copy.hideFlags = HideFlags.HideAndDontSave;
        var animator = copy.GetComponentInChildren<Animator>(true);

        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            Debug.LogError($"[PostureCapture] {model.name} has no humanoid Avatar.", model);
            DestroyImmediate(copy);
            return;
        }

        float[] captured = Sample(copy, animator, pose, timeField.value);
        float[] neutral = referenceField.value is AnimationClip reference ? Sample(copy, animator, reference, 0f) : new float[captured.Length];
        DestroyImmediate(copy);

        for (int i = 0; i < captured.Length; i++)
            captured[i] -= neutral[i];

        var profile = CreateInstance<PostureProfile>();
        profile.Set(captured);
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
    }

    // The posture muscles of one clip frame, in PostureProfile.Muscles order.
    private static float[] Sample(GameObject copy, Animator animator, AnimationClip clip, float time)
    {
        clip.SampleAnimation(copy, time);

        var handler = new HumanPoseHandler(animator.avatar, animator.transform);
        var pose = new HumanPose();
        handler.GetHumanPose(ref pose);
        handler.Dispose();

        MuscleHandle[] muscles = PostureProfile.Muscles();
        var values = new float[muscles.Length];
        for (int i = 0; i < muscles.Length; i++)
        {
            int index = Array.IndexOf(HumanTrait.MuscleName, muscles[i].name);
            if (index < 0) Debug.LogError($"[PostureCapture] No muscle named '{muscles[i].name}'; left at 0.");
            if (index >= 0) values[i] = pose.muscles[index];
        }

        return values;
    }
}
