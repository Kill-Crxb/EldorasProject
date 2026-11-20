using System.Collections.Generic;
using CrabSystem;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Body Shapes. Staging: retire once body shapes graduate.
//
// Set Up Selected Model: one creation group per shape key on the selected model prefab's skinned meshes, with
// an option per step and marked as a slider. Shape keys are locked to the steps so clothing can be made per
// step and shown by the same option. The model is then listed in the model database as playable. Groups that
// already exist are left alone, so clothing added to their options survives a rerun.
//
// Follow Shapes On Selected Model: every mesh on the model carrying a key a slider drives joins that slider, so clothing
// follows the body. Model Variant runs it after rebuilding the sliders.
//
// A staged family ("Chest 10%" … "Chest 100%", see StagedShapeKeys) becomes one group: Basis is step 0 and each
// key is a step, pushed to 100 with the rest of the family at 0.

public static class BodyShapeSetup
{
    // The locked body steps (decision 2026-10-25).
    private static readonly int[] Steps = { 0, 25, 50, 75, 100 };

    private const string GroupPrefix = "shape_";

    [MenuItem("Tools/Characters/Body Shapes/Set Up Selected Model")]
    private static void SetUpSelectedModel()
    {
        string path = SelectedPrefabPath();
        if (path == null) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        int added = AddShapeGroups(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        if (added == 0)
            Debug.LogError($"[BodyShapeSetup] No new shape keys on {path}. Check the FBX was exported with shape keys.");

        RegisterModel(AssetDatabase.LoadAssetAtPath<GameObject>(path));
    }

    // Puts every mesh on the model that carries a key a slider drives into that slider, so clothing follows the body.
    [MenuItem("Tools/Characters/Body Shapes/Follow Shapes On Selected Model")]
    private static void FollowShapesOnSelectedModel()
    {
        string path = SelectedPrefabPath();
        if (path == null) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        FollowShapes(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static string SelectedPrefabPath()
    {
        string path = Selection.activeGameObject != null ? AssetDatabase.GetAssetPath(Selection.activeGameObject) : "";
        if (path.EndsWith(".prefab")) return path;

        Debug.LogError("[BodyShapeSetup] Select a model prefab in the Project window.");
        return null;
    }

    // ── Shape groups ──────────────────────────────────────────────────────────

    public static void FollowShapes(GameObject root)
    {
        var appearance = root.GetComponent<ModelAppearance>();
        if (appearance == null) return;

        var so = new SerializedObject(appearance);
        var renderers = new List<SkinnedMeshRenderer>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        ClothingSetup.FollowBodyShapes(so.FindProperty("groups"), renderers);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static int AddShapeGroups(GameObject root)
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
            string family = StagedShapeKeys.Family(shape);
            string groupId = GroupPrefix + (family ?? shape).ToLowerInvariant().Replace(' ', '_');
            if (HasGroup(groups, groupId)) continue;

            if (family == null) WriteGroup(Append(groups), groupId, shape, renderer);
            else WriteStagedGroup(Append(groups), groupId, family, renderer);
            added++;
        }

        return added;
    }

    private static void WriteGroup(SerializedProperty group, string groupId, string shape, SkinnedMeshRenderer renderer)
    {
        var options = WriteGroupHeader(group, groupId, DisplayName(shape), Steps.Length);

        for (int i = 0; i < Steps.Length; i++)
        {
            var shapes = WriteOptionHeader(options.GetArrayElementAtIndex(i), Steps[i], 1);
            WriteShape(shapes.GetArrayElementAtIndex(0), renderer, shape, Steps[i]);
        }
    }

    private static void WriteStagedGroup(SerializedProperty group, string groupId, string family, SkinnedMeshRenderer renderer)
    {
        List<int> percents = StagedPercents(renderer.sharedMesh, family);
        var options = WriteGroupHeader(group, groupId, DisplayName(family), percents.Count + 1);

        WriteStagedOption(options.GetArrayElementAtIndex(0), 0, family, percents, renderer);
        for (int i = 0; i < percents.Count; i++)
            WriteStagedOption(options.GetArrayElementAtIndex(i + 1), percents[i], family, percents, renderer);
    }

    private static void WriteStagedOption(SerializedProperty option, int step, string family, List<int> percents, SkinnedMeshRenderer renderer)
    {
        var shapes = WriteOptionHeader(option, step, percents.Count);

        for (int i = 0; i < percents.Count; i++)
            WriteShape(shapes.GetArrayElementAtIndex(i), renderer, StagedShapeKeys.Name(family, percents[i]), percents[i] == step ? 100 : 0);
    }

    private static List<int> StagedPercents(Mesh mesh, string family)
    {
        var percents = new List<int>();
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string shape = mesh.GetBlendShapeName(i);
            if (StagedShapeKeys.Family(shape) == family) percents.Add(StagedShapeKeys.Percent(shape));
        }

        percents.Sort();
        return percents;
    }

    private static SerializedProperty WriteGroupHeader(SerializedProperty group, string groupId, string displayName, int optionCount)
    {
        group.FindPropertyRelative("groupId").stringValue = groupId;
        group.FindPropertyRelative("displayName").stringValue = displayName;
        group.FindPropertyRelative("chosenAtCreation").boolValue = true;
        group.FindPropertyRelative("slider").boolValue = true;
        group.FindPropertyRelative("unequippedOptionId").stringValue = "";

        var options = group.FindPropertyRelative("options");
        options.arraySize = optionCount;
        return options;
    }

    private static SerializedProperty WriteOptionHeader(SerializedProperty option, int step, int shapeCount)
    {
        option.FindPropertyRelative("optionId").stringValue = step.ToString();
        option.FindPropertyRelative("displayName").stringValue = step.ToString();
        option.FindPropertyRelative("show").arraySize = 0;
        option.FindPropertyRelative("swaps").arraySize = 0;

        var shapes = option.FindPropertyRelative("shapes");
        shapes.arraySize = shapeCount;
        return shapes;
    }

    private static void WriteShape(SerializedProperty entry, SkinnedMeshRenderer renderer, string shape, int weight)
    {
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

    public static void RegisterModel(GameObject prefab)
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
