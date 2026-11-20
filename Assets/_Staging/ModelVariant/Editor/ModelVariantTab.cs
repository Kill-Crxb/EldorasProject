using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > Model Variant. Staging: graduates with WizardTab.
//
// Copies an existing model and swaps in the meshes from a re-export, keeping the existing model's skeleton. Each
// swapped mesh's weights move onto the model's bones by name and are rebound where those bones stand, so everything
// built on the model's bones (baked gait and posture rotations, jiggle, sockets, hair cloth, colliders) keeps working
// even when the re-export's bones are rolled another way.
//   Source: the model to copy, the new export (a prefab beside the FBX keeps its materials) and the variant's name.
//   Meshes: every skinned mesh in the export. A ticked one replaces the model's mesh of the same name and takes the
//           export's materials. It can only swap when every bone it is weighted to is on the model, in the same place.
// Write copies the model prefab beside it, saves the rebound meshes in <Name>Meshes, drops the slider groups whose
// shape keys the new meshes no longer have (clothing linked to their steps goes with them), adds groups for the new
// keys through BodyShapeSetup, puts every mesh carrying a slider's keys on that slider so the clothes follow the body,
// and lists the variant in the model database.

public class ModelVariantTab : WizardTab
{
    public override string Title => "Model Variant";
    public override int Order => 8;

    protected override string[] StepNames => new[] { "Source", "Meshes" };
    protected override string WriteLabel => "Make variant";
    protected override string RailNote => "Pick the model to copy, the new export and a name, check the meshes, then Make variant. Checks on the right block it while anything is wrong.";

    private const string ModelPref = "CrabDashboard.ModelVariant.Model";
    private const string ShapeGroupPrefix = "shape_";

    // Joints further apart than this between the export and the model, metres, mean the skeletons don't match.
    private const float JointTolerance = 0.002f;

    private class Swap
    {
        public string name;
        public bool onModel;
        public bool ticked;
        public readonly HashSet<string> shapes = new HashSet<string>();
        public readonly List<string> missingBones = new List<string>();
        public readonly List<string> movedBones = new List<string>();
        public readonly List<string> ambiguousBones = new List<string>();
    }

    private class ShapeUse
    {
        public string groupId;
        public string renderer;
        public string shape;
    }

    private GameObject model;
    private GameObject source;
    private string variantName = "";
    private readonly List<Swap> swaps = new List<Swap>();
    private readonly List<ShapeUse> shapeUses = new List<ShapeUse>();

    private string ModelFolder => Path.GetDirectoryName(AssetDatabase.GetAssetPath(model)).Replace('\\', '/');
    private string PrefabPath => $"{ModelFolder}/{variantName}.prefab";
    private string MeshFolder => $"{ModelFolder}/{variantName}Meshes";
    private List<Swap> Ticked => swaps.Where(s => s.ticked).ToList();

    protected override void OnBuild()
    {
        model = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString(ModelPref, ""));
        Scan();
    }

    protected override VisualElement StepContent(int step)
    {
        return step == 0 ? SourceStep() : MeshesStep();
    }

    // ── Steps ─────────────────────────────────────────────────────────────────

    private VisualElement SourceStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Source"));
        pane.Add(DashboardLayout.Note("The variant keeps the model's skeleton and everything on it, and swaps in the export's meshes. Use the prefab beside the export's FBX when you've set materials on it."));

        var modelField = new ObjectField("Model to copy") { objectType = typeof(GameObject), allowSceneObjects = false, value = model };
        modelField.RegisterValueChangedCallback(e => OnModelPicked(e.newValue as GameObject));
        pane.Add(modelField);

        var sourceField = new ObjectField("New export") { objectType = typeof(GameObject), allowSceneObjects = false, value = source };
        sourceField.RegisterValueChangedCallback(e => { source = e.newValue as GameObject; Scan(); ShowStep(0); });
        pane.Add(sourceField);

        var nameField = new TextField("Variant name") { value = variantName, isDelayed = true };
        nameField.RegisterValueChangedCallback(e => { variantName = e.newValue.Trim(); RefreshChecks(); });
        pane.Add(nameField);

        pane.Add(new Button(() => { Scan(); ShowStep(0); }) { text = "Rescan" });
        return pane;
    }

    private void OnModelPicked(GameObject picked)
    {
        model = picked;
        EditorPrefs.SetString(ModelPref, model != null ? AssetDatabase.GetAssetPath(model) : "");
        Scan();
        ShowStep(0);
    }

    private VisualElement MeshesStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Meshes"));
        pane.Add(DashboardLayout.Note("One row per skinned mesh in the export. A ticked mesh replaces the model's mesh of the same name. A mesh with no match on the model can't be ticked."));

        if (swaps.Count == 0) pane.Add(DashboardLayout.Note("No skinned meshes yet. Pick the model and the new export on step 1."));

        foreach (var swap in swaps)
            pane.Add(SwapRow(swap));

        return pane;
    }

    private VisualElement SwapRow(Swap swap)
    {
        var row = Row();
        var toggle = new Toggle { value = swap.ticked, style = { width = 20 } };
        toggle.SetEnabled(swap.onModel);
        toggle.RegisterValueChangedCallback(e => { swap.ticked = e.newValue; RefreshChecks(); });

        row.Add(toggle);
        row.Add(new Label(swap.name) { style = { width = 220 } });
        row.Add(new Label(Status(swap)) { style = { whiteSpace = WhiteSpace.Normal, flexShrink = 1 } });
        return row;
    }

    private static string Status(Swap swap)
    {
        if (!swap.onModel) return "not on the model";
        if (swap.missingBones.Count > 0) return $"{swap.missingBones.Count} bones not on the model";
        if (swap.ambiguousBones.Count > 0) return $"{swap.ambiguousBones.Count} bones named twice on the model";
        if (swap.movedBones.Count > 0) return $"{swap.movedBones.Count} joints in another place";

        return $"matches, {swap.shapes.Count} shape keys";
    }

    // ── Checks ────────────────────────────────────────────────────────────────

    protected override void CollectIssues(List<WizardIssue> issues)
    {
        if (model == null) Error(issues, "Pick the model to copy.");
        if (source == null) Error(issues, "Pick the new export.");
        if (model == null || source == null) return;

        if (variantName == "") Error(issues, "Name the variant.");
        if (variantName != "" && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) Error(issues, $"{PrefabPath} already exists.");
        if (variantName != "" && AssetDatabase.IsValidFolder(MeshFolder)) Error(issues, $"{MeshFolder} already exists.");
        if (Ticked.Count == 0) Error(issues, "Tick at least one mesh to swap.");

        foreach (var swap in Ticked)
            SwapIssues(issues, swap);

        var unmatched = swaps.Where(s => !s.onModel).Select(s => s.name).ToList();
        if (unmatched.Count > 0) Warn(issues, $"Not on the model, left out: {string.Join(", ", unmatched)}.");

        var stale = StaleGroups();
        if (stale.Count > 0) Warn(issues, $"Replaces {stale.Count} slider groups whose keys the new meshes don't have ({string.Join(", ", stale)}). Clothing linked to their steps has to be set up again.");
    }

    private static void SwapIssues(List<WizardIssue> issues, Swap swap)
    {
        if (swap.missingBones.Count > 0) Error(issues, $"{swap.name} is weighted to bones the model doesn't have: {Sample(swap.missingBones)}.");
        if (swap.ambiguousBones.Count > 0) Error(issues, $"{swap.name} is weighted to bones the model has twice: {Sample(swap.ambiguousBones)}.");
        if (swap.movedBones.Count > 0) Error(issues, $"{swap.name}: {swap.movedBones.Count} joints sit somewhere else on the model ({Sample(swap.movedBones)}). The skeletons don't match, so it can't keep the model's.");
    }

    private static string Sample(List<string> names)
    {
        return names.Count <= 5 ? string.Join(", ", names) : string.Join(", ", names.Take(5)) + $" and {names.Count - 5} more";
    }

    // A slider group is stale when it drives a key on a mesh being swapped that the new mesh doesn't have.
    private List<string> StaleGroups()
    {
        var ticked = new Dictionary<string, Swap>();
        foreach (var swap in Ticked)
            ticked.TryAdd(swap.name, swap);

        var stale = new List<string>();

        foreach (var use in shapeUses)
        {
            if (stale.Contains(use.groupId) || !ticked.TryGetValue(use.renderer, out Swap swap)) continue;
            if (!swap.shapes.Contains(use.shape)) stale.Add(use.groupId);
        }

        return stale;
    }

    protected override List<string> PlanLines()
    {
        var lines = new List<string>();
        if (model == null || source == null || variantName == "") return lines;

        lines.Add($"Copy {model.name} to {PrefabPath}, keeping its skeleton and everything on it.");
        lines.Add($"Swap in {Ticked.Count} meshes from {source.name}, rebound to the model's bones, saved in {MeshFolder}, with the export's materials.");

        var stale = StaleGroups();
        if (stale.Count > 0) lines.Add($"Drop {stale.Count} stale slider groups.");

        lines.Add("Add slider groups for the new shape keys, with every mesh that carries them.");
        lines.Add($"List it in the model database as {variantName.ToLowerInvariant()}.");
        return lines;
    }

    // ── Scan ──────────────────────────────────────────────────────────────────

    private void Scan()
    {
        swaps.Clear();
        shapeUses.Clear();
        if (model == null || source == null) return;

        ReadShapeUses();
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(model));
        var export = Object.Instantiate(source);
        export.hideFlags = HideFlags.HideAndDontSave;

        var targets = RenderersByName(root);
        var duplicates = new HashSet<string>();
        var bones = BonesByName(root.transform, duplicates);

        foreach (var renderer in export.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer.sharedMesh == null) continue;
            swaps.Add(Measure(renderer, targets.ContainsKey(renderer.name), bones, duplicates, export.transform, root.transform));
        }

        Object.DestroyImmediate(export);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Swap Measure(SkinnedMeshRenderer renderer, bool onModel, Dictionary<string, Transform> bones,
                                HashSet<string> duplicates, Transform exportRoot, Transform modelRoot)
    {
        var swap = new Swap { name = renderer.name, onModel = onModel, ticked = onModel };
        Mesh mesh = renderer.sharedMesh;

        for (int i = 0; i < mesh.blendShapeCount; i++)
            swap.shapes.Add(mesh.GetBlendShapeName(i));

        foreach (int index in WeightedBones(mesh))
            MeasureBone(swap, renderer.bones[index], bones, duplicates, exportRoot, modelRoot);

        return swap;
    }

    private static void MeasureBone(Swap swap, Transform bone, Dictionary<string, Transform> bones, HashSet<string> duplicates,
                                    Transform exportRoot, Transform modelRoot)
    {
        if (bone == null) return;
        if (duplicates.Contains(bone.name)) swap.ambiguousBones.Add(bone.name);
        if (!bones.TryGetValue(bone.name, out Transform match))
        {
            swap.missingBones.Add(bone.name);
            return;
        }

        Vector3 there = modelRoot.InverseTransformPoint(match.position);
        Vector3 here = exportRoot.InverseTransformPoint(bone.position);
        if (Vector3.Distance(there, here) > JointTolerance) swap.movedBones.Add(bone.name);
    }

    private static SortedSet<int> WeightedBones(Mesh mesh)
    {
        var used = new SortedSet<int>();
        foreach (var weight in mesh.GetAllBoneWeights())
            if (weight.weight > 0f) used.Add(weight.boneIndex);

        return used;
    }

    // Every shape key the model's slider groups drive, by group and renderer name.
    private void ReadShapeUses()
    {
        var appearance = model.GetComponent<ModelAppearance>();
        if (appearance == null) return;

        var groups = new SerializedObject(appearance).FindProperty("groups");
        for (int g = 0; g < groups.arraySize; g++)
        {
            var group = groups.GetArrayElementAtIndex(g);
            string groupId = group.FindPropertyRelative("groupId").stringValue;
            if (groupId.StartsWith(ShapeGroupPrefix)) ReadGroupShapes(groupId, group.FindPropertyRelative("options"));
        }
    }

    private void ReadGroupShapes(string groupId, SerializedProperty options)
    {
        for (int o = 0; o < options.arraySize; o++)
        {
            var shapes = options.GetArrayElementAtIndex(o).FindPropertyRelative("shapes");
            for (int s = 0; s < shapes.arraySize; s++)
                shapeUses.Add(ReadShapeUse(groupId, shapes.GetArrayElementAtIndex(s)));
        }
    }

    private static ShapeUse ReadShapeUse(string groupId, SerializedProperty entry)
    {
        var renderer = entry.FindPropertyRelative("renderer").objectReferenceValue;
        return new ShapeUse
        {
            groupId = groupId,
            renderer = renderer != null ? renderer.name : "",
            shape = entry.FindPropertyRelative("shape").stringValue,
        };
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    protected override void Write()
    {
        var stale = StaleGroups();
        var ticked = new HashSet<string>(Ticked.Select(s => s.name));
        string meshFolder = MeshFolder;
        string prefabPath = PrefabPath;

        if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(model), prefabPath))
        {
            Debug.LogError($"[ModelVariant] Couldn't copy {model.name} to {prefabPath}.");
            return;
        }

        EnsureFolder(meshFolder);
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var export = Object.Instantiate(source);
        export.hideFlags = HideFlags.HideAndDontSave;

        var targets = RenderersByName(root);
        var bones = BonesByName(root.transform, new HashSet<string>());
        foreach (var renderer in export.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!ticked.Contains(renderer.name)) continue;
            SwapIn(renderer, targets[renderer.name], bones, export.transform, root.transform, meshFolder);
        }

        RemoveGroups(root, stale);
        BodyShapeSetup.AddShapeGroups(root);
        BodyShapeSetup.FollowShapes(root);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Object.DestroyImmediate(export);
        AssetDatabase.SaveAssets();

        BodyShapeSetup.RegisterModel(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        variantName = "";
        Scan();
        ShowStep(0);
    }

    // The export's mesh, weighted to the model's bones of the same names. Each bindpose is taken from where the
    // model's bone stands, so the model's own bone axes are the ones the mesh is bound to.
    private static void SwapIn(SkinnedMeshRenderer from, SkinnedMeshRenderer to, Dictionary<string, Transform> bones,
                               Transform exportRoot, Transform modelRoot, string meshFolder)
    {
        var mesh = Object.Instantiate(from.sharedMesh);
        mesh.name = from.sharedMesh.name;

        Matrix4x4 meshToWorld = modelRoot.localToWorldMatrix * exportRoot.worldToLocalMatrix * from.transform.localToWorldMatrix;
        var mapped = new Transform[from.bones.Length];
        var binds = new Matrix4x4[from.bones.Length];

        for (int i = 0; i < mapped.Length; i++)
        {
            mapped[i] = Lookup(bones, from.bones[i]);
            binds[i] = mapped[i] != null ? mapped[i].worldToLocalMatrix * meshToWorld : Matrix4x4.identity;
        }

        mesh.bindposes = binds;
        AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{meshFolder}/{from.name}.asset"));

        to.sharedMesh = mesh;
        to.bones = mapped;
        to.sharedMaterials = from.sharedMaterials;
        for (int i = 0; i < mesh.blendShapeCount; i++)
            to.SetBlendShapeWeight(i, 0f);
    }

    private static void RemoveGroups(GameObject root, List<string> groupIds)
    {
        var appearance = root.GetComponent<ModelAppearance>();
        if (appearance == null || groupIds.Count == 0) return;

        var so = new SerializedObject(appearance);
        var groups = so.FindProperty("groups");
        for (int g = groups.arraySize - 1; g >= 0; g--)
            if (groupIds.Contains(groups.GetArrayElementAtIndex(g).FindPropertyRelative("groupId").stringValue))
                groups.DeleteArrayElementAtIndex(g);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ── Lookups ───────────────────────────────────────────────────────────────

    private static Dictionary<string, SkinnedMeshRenderer> RenderersByName(GameObject root)
    {
        var found = new Dictionary<string, SkinnedMeshRenderer>();
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            found.TryAdd(renderer.name, renderer);

        return found;
    }

    // First transform of each name; names met more than once go in duplicates.
    private static Dictionary<string, Transform> BonesByName(Transform root, HashSet<string> duplicates)
    {
        var found = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (!found.TryAdd(t.name, t)) duplicates.Add(t.name);

        return found;
    }

    private static Transform Lookup(Dictionary<string, Transform> bones, Transform bone)
    {
        return bone != null && bones.TryGetValue(bone.name, out Transform match) ? match : null;
    }
}
