using System.Collections.Generic;
using CrabSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// High Elf new body setup (staging, one-off; delete this Editor folder once the new body replaces HighElfBase,
// along with HEv2/Editor, whose bone map this copies).
// Tools → Characters → Set Up High Elf New Body.
//
// Builds HighElfNewBody.prefab beside the old HighElfBase, which is left alone until the new body is signed off:
//   1. FBX import: humanoid Avatar with HEv2's bone map (Hip.L/R sit between Hips and the thighs, so the auto
//      mapping isn't trusted), toon smooth normals.
//   2. HighElfNewBody.prefab, a variant of BaseMesh-HE-F2.prefab so its materials carry over: HumanoidAnimator_v2,
//      ModelPosture, and only the body, ears and face plane. The FBX's clothing and hair come off: clothing goes
//      back on as equipment through Add Clothing To Model, the hair stays the old one until its rig is exported.
//   3. The existing model tools, in order (as HEv2). Body Shapes lists it in the model database as highelfnewbody.

public static class HighElfNewBodySetup
{
    const string ModelPath = "Assets/_Staging/HighElfNewBody/BaseMesh-HE-F2.fbx";
    const string MaterialsPrefabPath = "Assets/_Staging/HighElfNewBody/BaseMesh-HE-F2.prefab";
    const string PrefabPath = "Assets/_Staging/HighElfNewBody/HighElfNewBody.prefab";
    const string ControllerPath = "Assets/Database/Animation/Humanoid/HumanoidAnimator_v2.controller";
    const string HairRig = "arm.SpacebunsBangs-HighElf-Hair";
    const string SmoothNormalsFlag = "toonSmoothNormals";

    static readonly string[] Keep = { "HighElfBaseMesh", "Ears", "FacePane" };

    static readonly string[] Human =
    {
        "Hips", "Hips", "Spine", "Spine", "Chest", "Chest", "UpperChest", "UpperChest", "Neck", "Neck", "Head", "Head",
        "Thigh.L", "LeftUpperLeg", "Shin.L", "LeftLowerLeg", "Foot.L", "LeftFoot", "Toe.L", "LeftToes",
        "Thigh.R", "RightUpperLeg", "Shin.R", "RightLowerLeg", "Foot.R", "RightFoot", "Toe.R", "RightToes",
        "Shoulder.L", "LeftShoulder", "Bicep.L", "LeftUpperArm", "Forearm.L", "LeftLowerArm", "Hand.L", "LeftHand",
        "Shoulder.R", "RightShoulder", "Bicep.R", "RightUpperArm", "Forearm.R", "RightLowerArm", "Hand.R", "RightHand",
    };

    static readonly string[] Fingers = { "Thumb", "Thumb", "Index", "Index", "Middle", "Middle", "Ring", "Ring", "Pinky", "Little" };
    static readonly string[] Joints = { "Proximal", "Intermediate", "Distal" };

    static readonly string[] Tools =
    {
        "Tools/Characters/Copy Model Parts From Katana Girl",
        "Tools/Characters/Jiggle/Copy Porphi Jiggle To Selected Model",
        "Tools/Characters/Body Springs/Set Up Selected Model",
        "Tools/Characters/Body Shapes/Set Up Selected Model",
    };

    [MenuItem("Tools/Characters/Set Up High Elf New Body")]
    static void SetUp()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogError($"[HighElfNewBodySetup] {PrefabPath} already exists; delete it to build again.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(MaterialsPrefabPath) == null)
        {
            Debug.LogError($"[HighElfNewBodySetup] Needs {ModelPath} and {MaterialsPrefabPath}.");
            return;
        }

        ConfigureImporter();
        GameObject prefab = BuildPrefab();
        if (prefab == null) return;

        Selection.activeObject = prefab;
        foreach (string tool in Tools)
            if (!EditorApplication.ExecuteMenuItem(tool))
                Debug.LogError($"[HighElfNewBodySetup] Couldn't run '{tool}'; run it by hand with HighElfNewBody selected.");

        Debug.Log($"[HighElfNewBodySetup] HighElfNewBody built at {PrefabPath}. Add clothing next.");
    }

    // After re-exporting over the FBX: rebuilds the Avatar's bone list from the new file (keeps the mapping).
    [MenuItem("Tools/Characters/Refresh High Elf New Body Avatar")]
    static void RefreshAvatar()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogError($"[HighElfNewBodySetup] No model at {ModelPath}.");
            return;
        }

        ConfigureImporter();
        Debug.Log($"[HighElfNewBodySetup] Avatar refreshed for {ModelPath}.");
    }

    static void ConfigureImporter()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        var skeleton = new List<SkeletonBone>();
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
        {
            skeleton.Add(new SkeletonBone
            {
                name = t == model.transform ? model.name + "(Clone)" : t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
        }

        var human = new List<HumanBone>();
        for (int i = 0; i < Human.Length; i += 2)
            human.Add(Bone(Human[i], Human[i + 1]));

        foreach (string side in new[] { "L", "R" })
        {
            string prefix = side == "L" ? "Left " : "Right ";
            for (int f = 0; f < Fingers.Length; f += 2)
                for (int j = 0; j < 3; j++)
                    human.Add(Bone($"{Fingers[f]}{j + 1}.{side}", $"{prefix}{Fingers[f + 1]} {Joints[j]}"));
        }

        HumanDescription description = importer.humanDescription;
        description.human = human.ToArray();
        description.skeleton = skeleton.ToArray();
        importer.humanDescription = description;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        if (!importer.userData.Contains(SmoothNormalsFlag)) importer.userData += SmoothNormalsFlag;
        importer.SaveAndReimport();
    }

    static GameObject BuildPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MaterialsPrefabPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[HighElfNewBodySetup] Missing {ControllerPath}.");
            return null;
        }

        var scene = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        instance.name = "HighElfNewBody";
        instance.GetComponent<Animator>().runtimeAnimatorController = controller;
        instance.AddComponent<ModelPosture>();

        var strip = new List<GameObject>();
        foreach (SkinnedMeshRenderer r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (System.Array.IndexOf(Keep, r.name) < 0) strip.Add(r.gameObject);

        foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            if (t.name == HairRig) strip.Add(t.gameObject);

        foreach (GameObject go in strip)
            Object.DestroyImmediate(go);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        EditorSceneManager.ClosePreviewScene(scene);
        return prefab;
    }

    static HumanBone Bone(string bone, string humanName)
    {
        var b = new HumanBone { boneName = bone, humanName = humanName };
        b.limit.useDefaultValues = true;
        return b;
    }
}
