using System.Collections.Generic;
using CrabSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// HEv2 setup (staging, one-off; delete the HEv2/Editor folder after use).
// Tools → Characters → Set Up HEv2 Model.
//
// Builds the High Elf base from scratch on the HEv2 export:
//   1. FBX import: humanoid Avatar with five-finger hands (Pinky maps as Little), toon smooth normals.
//   2. HighElfBase.prefab (variant of the FBX): HumanoidAnimator_v2, the HighElfSkinV2 material, ModelPosture.
//   3. The model database's highelf_base entry points at the new prefab.
//   4. The existing model tools, in order: Copy Model Parts From Katana Girl (sockets, event forwarder, root-motion and
//      socket relays), Copy Porphi Jiggle (breast and butt cloths), Body Springs (their tuning and limb colliders),
//      Body Shapes (slider groups for every shape key).
// Clothing is added by hand afterwards (Tools → Characters → Add Clothing To Model).
// The old model is in Assets/_Export~/HighElf_v1_removed/.

public static class HEv2Setup
{
    const string ModelPath = "Assets/_Staging/HEv2/HighElfBasev1.1.fbx";
    const string PrefabPath = "Assets/_Staging/HEv2/HighElfBase.prefab";
    const string MaterialPath = "Assets/_Staging/HEv2/HighElfSkinV2.mat";
    const string ControllerPath = "Assets/Database/Animation/Humanoid/HumanoidAnimator_v2.controller";
    const string DatabasePath = "Assets/Database/Characters/EldoraModelDatabase.asset";
    const string ModelId = "highelf_base";
    const string BodyName = "HighElfBaseMesh";
    const string SmoothNormalsFlag = "toonSmoothNormals";

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

    [MenuItem("Tools/Characters/Set Up HEv2 Model")]
    static void SetUp()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.LogError($"[HEv2Setup] {PrefabPath} already exists; delete it to build again.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogError($"[HEv2Setup] No model at {ModelPath}.");
            return;
        }

        ConfigureImporter();
        GameObject prefab = BuildPrefab();
        if (prefab == null) return;

        PointDatabase(prefab);

        Selection.activeObject = prefab;
        foreach (string tool in Tools)
            if (!EditorApplication.ExecuteMenuItem(tool))
                Debug.LogError($"[HEv2Setup] Couldn't run '{tool}'; run it by hand with HighElfBase selected.");

        Debug.Log($"[HEv2Setup] HighElfBase built at {PrefabPath}. Add clothing next.");
    }

    // After re-exporting over the FBX: rebuilds the Avatar's bone list from the new file (keeps the mapping).
    [MenuItem("Tools/Characters/Refresh HEv2 Avatar")]
    static void RefreshAvatar()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogError($"[HEv2Setup] No model at {ModelPath}.");
            return;
        }

        ConfigureImporter();
        Debug.Log($"[HEv2Setup] Avatar refreshed for {ModelPath}.");
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
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (material == null || controller == null)
        {
            Debug.LogError($"[HEv2Setup] Missing {(material == null ? MaterialPath : ControllerPath)}.");
            return null;
        }

        var scene = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = "HighElfBase";
        instance.GetComponent<Animator>().runtimeAnimatorController = controller;
        instance.AddComponent<ModelPosture>();

        SkinnedMeshRenderer body = null;
        foreach (SkinnedMeshRenderer r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (r.name == BodyName) body = r;

        if (body == null) Debug.LogError($"[HEv2Setup] No '{BodyName}' renderer in the FBX; set the skin material by hand.");
        else body.sharedMaterial = material;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        EditorSceneManager.ClosePreviewScene(scene);
        return prefab;
    }

    // The existing highelf_base entry takes the new prefab, so character creation and saves keep the same model id.
    static void PointDatabase(GameObject prefab)
    {
        var database = AssetDatabase.LoadAssetAtPath<ModelDatabase>(DatabasePath);
        var so = new SerializedObject(database);
        SerializedProperty models = so.FindProperty("models");

        for (int i = 0; i < models.arraySize; i++)
        {
            SerializedProperty entry = models.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("modelId").stringValue != ModelId) continue;

            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            return;
        }

        Debug.LogError($"[HEv2Setup] No '{ModelId}' entry in {DatabasePath}; Body Shapes will list the model under a new id.");
    }

    static HumanBone Bone(string bone, string humanName)
    {
        var b = new HumanBone { boneName = bone, humanName = humanName };
        b.limit.useDefaultValues = true;
        return b;
    }
}
