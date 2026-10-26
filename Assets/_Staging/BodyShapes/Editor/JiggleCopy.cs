using MagicaCloth2;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Jiggle → Copy Porphi Jiggle To Selected Model. Staging: retire once the new body graduates.
//
// Copies Porphi's two BoneSpring cloths (BreastCloth, ButtCloth) onto the selected model prefab with their
// settings unchanged, and points them at the new rig's bones. Only the settings block is copied: Porphi's
// cached init data belongs to Porphi's skeleton, so Magica rebuilds it for the new one. Bones are found by
// name on the new rig; the inertia anchor is matched through the humanoid avatar, so it lands on the same
// body part whatever either rig calls it. Rerunning overwrites both cloths with Porphi's current settings.

public static class JiggleCopy
{
    private const string PorphiPath = "Assets/Database/Characters/MC/Porphi.prefab";

    private static readonly string[] BreastBones = { "Breast1.L", "Breast1.R" };
    private static readonly string[] ButtBones = { "Butt1.L", "Butt1.R" };

    [MenuItem("Tools/Characters/Jiggle/Copy Porphi Jiggle To Selected Model")]
    private static void CopyToSelectedModel()
    {
        string path = Selection.activeGameObject != null ? AssetDatabase.GetAssetPath(Selection.activeGameObject) : "";
        if (!path.EndsWith(".prefab"))
        {
            Debug.LogError("[JiggleCopy] Select a model prefab in the Project window.");
            return;
        }

        var porphi = AssetDatabase.LoadAssetAtPath<GameObject>(PorphiPath);
        if (porphi == null)
        {
            Debug.LogError($"[JiggleCopy] Porphi prefab not found at {PorphiPath}.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        var sourceAnimator = porphi.GetComponentInChildren<Animator>(true);
        var targetAnimator = root.GetComponentInChildren<Animator>(true);

        Copy(Find(porphi, "BreastCloth"), root, BreastBones, sourceAnimator, targetAnimator);
        Copy(Find(porphi, "ButtCloth"), root, ButtBones, sourceAnimator, targetAnimator);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void Copy(MagicaCloth source, GameObject root, string[] boneNames, Animator sourceAnimator, Animator targetAnimator)
    {
        if (source == null)
        {
            Debug.LogError("[JiggleCopy] Porphi is missing a cloth this copy expects.");
            return;
        }

        var cloth = ClothOn(root, source.gameObject.name);
        var target = new SerializedObject(cloth);
        var from = new SerializedObject(source);

        target.CopyFromSerializedProperty(from.FindProperty("serializeData"));
        foreach (string property in new[] { "animationPoseRatioProperty", "gravityProperty", "dampingProperty", "worldInertiaProperty", "localInertiaProperty", "windInfluenceProperty", "blendWeightProperty" })
            target.CopyFromSerializedProperty(from.FindProperty(property));

        var bones = FindBones(root.transform, boneNames);
        var data = target.FindProperty("serializeData");
        SetTransforms(data.FindPropertyRelative("rootBones"), bones);
        SetTransforms(data.FindPropertyRelative("colliderCollisionConstraint.collisionBones"), bones);
        data.FindPropertyRelative("colliderCollisionConstraint.colliderList").arraySize = 0;
        data.FindPropertyRelative("inertiaConstraint.anchor").objectReferenceValue =
            MatchAnchor(source.SerializeData.inertiaConstraint.anchor, sourceAnimator, targetAnimator);

        target.ApplyModifiedPropertiesWithoutUndo();
    }

    // Porphi's collision bones are its root bones, so the copy keeps that pairing only when it had any.
    private static void SetTransforms(SerializedProperty list, Transform[] bones)
    {
        if (list.arraySize == 0) return;

        list.arraySize = bones.Length;
        for (int i = 0; i < bones.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = bones[i];
    }

    private static Transform MatchAnchor(Transform anchor, Animator sourceAnimator, Animator targetAnimator)
    {
        if (anchor == null || sourceAnimator == null || targetAnimator == null) return null;

        for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
            if (sourceAnimator.GetBoneTransform(bone) == anchor) return targetAnimator.GetBoneTransform(bone);

        Debug.LogError($"[JiggleCopy] Porphi's anchor {anchor.name} is not a humanoid bone. Set the anchor by hand.");
        return null;
    }

    private static MagicaCloth ClothOn(GameObject root, string name)
    {
        Transform child = root.transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
        }

        var cloth = child.GetComponent<MagicaCloth>();
        return cloth != null ? cloth : child.gameObject.AddComponent<MagicaCloth>();
    }

    private static Transform[] FindBones(Transform root, string[] names)
    {
        var bones = new Transform[names.Length];
        for (int i = 0; i < names.Length; i++)
            bones[i] = FindDeep(root, names[i]);

        foreach (var bone in bones)
            if (bone == null) Debug.LogError($"[JiggleCopy] {root.name} is missing a jiggle bone ({string.Join(", ", names)}).");

        return bones;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;

        return null;
    }

    private static MagicaCloth Find(GameObject prefab, string name)
    {
        foreach (var cloth in prefab.GetComponentsInChildren<MagicaCloth>(true))
            if (cloth.gameObject.name == name) return cloth;

        return null;
    }
}
