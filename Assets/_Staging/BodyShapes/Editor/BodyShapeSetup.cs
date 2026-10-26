using CrabSystem;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Body Shapes. Staging: retire once body shapes graduate.
//
// Set Up Selected Model: one creation group per shape key on the selected model prefab's skinned meshes, with
// an option per step and marked as a slider. Shape keys are locked to the steps so clothing can be made per
// step and shown by the same option. The model is then listed in the model database as playable. Groups that
// already exist are left alone, so clothing added to their options survives a rerun.

public static class BodyShapeSetup
{
    // The locked body steps (decision 2026-10-25).
    private static readonly int[] Steps = { 0, 25, 50, 75, 100 };

    private const string GroupPrefix = "shape_";

    [MenuItem("Tools/Characters/Body Shapes/Set Up Selected Model")]
    private static void SetUpSelectedModel()
    {
        string path = Selection.activeGameObject != null ? AssetDatabase.GetAssetPath(Selection.activeGameObject) : "";
        if (!path.EndsWith(".prefab"))
        {
            Debug.LogError("[BodyShapeSetup] Select a model prefab in the Project window.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        int added = AddShapeGroups(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        if (added == 0)
            Debug.LogError($"[BodyShapeSetup] No new shape keys on {path}. Check the FBX was exported with shape keys.");

        RegisterModel(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    // ── Shape groups ──────────────────────────────────────────────────────────

    private static int AddShapeGroups(GameObject root)
    {
        var appearance = root.GetComponent<ModelAppearance>();
        if (appearance == null) appearance = root.AddComponent<ModelAppearance>();

        var so = new SerializedObject(appearance);
        var groups = so.FindProperty("groups");
        int added = 0;

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            added += AddRendererGroups(groups, renderer);

        so.ApplyModifiedPropertiesWithoutUndo();
        return added;
    }

    private static int AddRendererGroups(SerializedProperty groups, SkinnedMeshRenderer renderer)
    {
        Mesh mesh = renderer.sharedMesh;
        if (mesh == null) return 0;

        int added = 0;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shape = mesh.GetBlendShapeName(i);
            string groupId = GroupPrefix + shape.ToLowerInvariant().Replace(' ', '_');
            if (HasGroup(groups, groupId)) continue;

            WriteGroup(Append(groups), groupId, shape, renderer);
            added++;
        }

        return added;
    }

    private static void WriteGroup(SerializedProperty group, string groupId, string shape, SkinnedMeshRenderer renderer)
    {
        group.FindPropertyRelative("groupId").stringValue = groupId;
        group.FindPropertyRelative("displayName").stringValue = DisplayName(shape);
        group.FindPropertyRelative("chosenAtCreation").boolValue = true;
        group.FindPropertyRelative("slider").boolValue = true;
        group.FindPropertyRelative("unequippedOptionId").stringValue = "";

        var options = group.FindPropertyRelative("options");
        options.arraySize = Steps.Length;

        for (int i = 0; i < Steps.Length; i++)
            WriteOption(options.GetArrayElementAtIndex(i), Steps[i], shape, renderer);
    }

    private static void WriteOption(SerializedProperty option, int weight, string shape, SkinnedMeshRenderer renderer)
    {
        option.FindPropertyRelative("optionId").stringValue = weight.ToString();
        option.FindPropertyRelative("displayName").stringValue = weight.ToString();
        option.FindPropertyRelative("show").arraySize = 0;
        option.FindPropertyRelative("swaps").arraySize = 0;

        var shapes = option.FindPropertyRelative("shapes");
        shapes.arraySize = 1;

        var entry = shapes.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("renderer").objectReferenceValue = renderer;
        entry.FindPropertyRelative("shape").stringValue = shape;
        entry.FindPropertyRelative("weight").floatValue = weight;
    }

    private static bool HasGroup(SerializedProperty groups, string groupId)
    {
        for (int i = 0; i < groups.arraySize; i++)
            if (groups.GetArrayElementAtIndex(i).FindPropertyRelative("groupId").stringValue == groupId) return true;

        return false;
    }

    // "BreastSlider" reads as "Breast".
    private static string DisplayName(string shape)
    {
        string name = shape.EndsWith("Slider") ? shape.Substring(0, shape.Length - "Slider".Length) : shape;
        return ObjectNames.NicifyVariableName(name);
    }

    // ── Model database ────────────────────────────────────────────────────────

    private static void RegisterModel(GameObject prefab)
    {
        string[] guids = AssetDatabase.FindAssets("t:ModelDatabase", new[] { "Assets" });
        if (guids.Length != 1)
        {
            Debug.LogError($"[BodyShapeSetup] Expected one ModelDatabase under Assets, found {guids.Length}. List {prefab.name} by hand.");
            return;
        }

        var database = AssetDatabase.LoadAssetAtPath<ModelDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        foreach (var variant in database.AllModels)
            if (variant.prefab == prefab) return;

        var so = new SerializedObject(database);
        var entry = Append(so.FindProperty("models"));
        entry.FindPropertyRelative("modelId").stringValue = prefab.name.ToLowerInvariant();
        entry.FindPropertyRelative("displayName").stringValue = ObjectNames.NicifyVariableName(prefab.name);
        entry.FindPropertyRelative("playable").boolValue = true;
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("thumbnailSprite").objectReferenceValue = null;
        entry.FindPropertyRelative("allowColorCustomization").boolValue = false;
        entry.FindPropertyRelative("defaultColors").arraySize = 0;

        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static SerializedProperty Append(SerializedProperty array)
    {
        array.arraySize++;
        return array.GetArrayElementAtIndex(array.arraySize - 1);
    }
}
