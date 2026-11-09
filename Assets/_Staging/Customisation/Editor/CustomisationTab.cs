using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > Customisation. Staging: graduates with WizardTab and ClothingSetup.
//
// Adds or reworks a character-creation look on a model's ModelAppearance. Each category is one creation group:
//   Hair:   a hair FBX; its chains hang off Head with a BoneCloth (the Hair_Laggy preset) that hits a head sphere.
//   Face:   meshes worn on the head (brows, eyes, ears), no cloth.
//   Makeup: body layers only, drawn over the face; the group starts on "none".
//   Tattoo: body layers only, drawn over the body; the group starts on "none".
//   Look:   the category, the model, and the option, new or one already on the model. Reworking an option rebinds its
//           meshes and replaces its layers; its source defaults to the folder it was moved into.
//   Source: the staging folder for a mesh look (an FBX, or a prefab beside it carrying its material), or the layers
//           for a painted one.
// The stage shows the model wearing the picked option. Write binds through ClothingSetup, one run per file, then moves
// the staging folder to Database/Characters/Customisation/<Category>/<Option>.

public class CustomisationTab : WizardTab, System.IDisposable
{
    public override string Title => "Customisation";
    public override int Order => 7;

    protected override string[] StepNames => new[] { "Look", "Source" };
    protected override string WriteLabel => "Write look";
    protected override string RailNote => "Drop a look's files in a staging folder, pick its category and name, then Write. Checks on the right block Write while anything is wrong.";

    private const string LooksRoot = "Assets/Database/Characters/Customisation";
    private const string ModelPref = "CrabDashboard.Customisation.Model";
    private const string NewOption = "(new)";
    private const float StageHeight = 300f;
    private const string HairPreset = "Assets/Database/Config/Cloth/Hair_Laggy.json";

    // Blender's default object names; a mesh still called one of these was never named.
    private static readonly string[] DefaultMeshNames = { "NurbsPath", "BezierCurve", "Cube", "Plane", "Cylinder", "Sphere", "Mesh" };

    private class Category
    {
        public string name;
        public string groupId;
        public bool meshes;
        public bool emptyOption;
        public string bonesParent;
        public string[] colliderBones;
        public bool boneCloth;
        public string preset;
        public CharacterStage.Framing framing;
    }

    private static readonly Category[] Categories =
    {
        new Category { name = "Hair", groupId = "hair_style", meshes = true, bonesParent = "Head", colliderBones = new[] { "Head" }, boneCloth = true, preset = HairPreset, framing = CharacterStage.Framing.Face },
        new Category { name = "Face", groupId = "face", meshes = true, bonesParent = "Head", colliderBones = new string[0], framing = CharacterStage.Framing.Face },
        new Category { name = "Makeup", groupId = "makeup", emptyOption = true, framing = CharacterStage.Framing.Face },
        new Category { name = "Tattoo", groupId = "tattoo", emptyOption = true, framing = CharacterStage.Framing.Body },
    };

    private int categoryIndex;
    private GameObject model;
    private string picked = NewOption;
    private string optionName = "";
    private DefaultAsset folder;
    private readonly List<Material> layers = new List<Material>();
    private readonly List<GameObject> sources = new List<GameObject>();
    private CharacterStage stage;

    private Category Current => Categories[categoryIndex];
    private bool Reworking => picked != NewOption;
    private string OptionId => Reworking ? picked : ItemDefinitionGenerator.ToItemId(optionName);
    private string Destination => $"{LooksRoot}/{Current.name}/{ItemDefinitionGenerator.ToAssetName(OptionId)}";
    private ModelAppearance Appearance() => model != null ? model.GetComponent<ModelAppearance>() : null;
    private SkinnedMeshRenderer BodyRenderer() => model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;

    protected override void OnBuild()
    {
        model = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString(ModelPref, ""));
        Scan();
    }

    protected override VisualElement Stage()
    {
        stage = new CharacterStage(StageHeight);
        ShowOnStage();
        return stage.Element;
    }

    public void Dispose()
    {
        if (stage != null) stage.Dispose();
    }

    protected override VisualElement StepContent(int step)
    {
        return step == 0 ? LookStep() : SourceStep();
    }

    private VisualElement LookStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Look"));
        pane.Add(DashboardLayout.Note("A category is one creation group on the model. Pick (new) to add a look, or an existing one to rework it."));

        var names = Categories.Select(c => c.name).ToList();
        var categoryField = new PopupField<string>("Category", names, categoryIndex);
        categoryField.RegisterValueChangedCallback(e => { categoryIndex = names.IndexOf(e.newValue); ResetLook(); });
        pane.Add(categoryField);

        var modelField = new ObjectField("Model prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = model };
        modelField.RegisterValueChangedCallback(e => OnModelPicked(e.newValue as GameObject));
        pane.Add(modelField);

        var options = new List<string> { NewOption };
        options.AddRange(ExistingOptions());
        var optionField = new PopupField<string>("Option", options, Mathf.Max(0, options.IndexOf(picked)));
        optionField.RegisterValueChangedCallback(e => OnOptionPicked(e.newValue));
        pane.Add(optionField);

        if (Reworking) return pane;

        var nameField = new TextField("Name") { value = optionName, isDelayed = true };
        nameField.RegisterValueChangedCallback(e => { optionName = e.newValue.Trim(); RefreshChecks(); });
        pane.Add(nameField);
        return pane;
    }

    private VisualElement SourceStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Source"));
        if (!Current.meshes)
        {
            pane.Add(DashboardLayout.Note($"{Current.name} is painted on: materials drawn over the body while the look shows, bottom first."));
            var box = new VisualElement();
            FillLayers(box);
            pane.Add(box);
            return pane;
        }

        pane.Add(DashboardLayout.Note($"The folder holds the look's FBX, skinned to its own bones or the base armature. Bones of its own hang off {Current.bonesParent}. A prefab beside an FBX of the same name is used instead, so its material is kept."));
        var folderField = new ObjectField("Staging folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = folder };
        folderField.RegisterValueChangedCallback(e => OnFolderPicked(e.newValue as DefaultAsset));
        pane.Add(folderField);

        foreach (var source in sources)
            pane.Add(new Label($"{source.name}: {string.Join(", ", source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.name))}"));

        if (folder != null && sources.Count == 0) pane.Add(DashboardLayout.Note("No models in this folder."));
        return pane;
    }

    private void FillLayers(VisualElement box)
    {
        box.Clear();
        for (int i = 0; i < layers.Count; i++)
            box.Add(LayerRow(box, i));

        box.Add(new Button(() => { layers.Add(null); FillLayers(box); }) { text = "Add layer" });
    }

    private VisualElement LayerRow(VisualElement box, int index)
    {
        var row = Row();
        var field = new ObjectField { objectType = typeof(Material), allowSceneObjects = false, value = layers[index], style = { flexGrow = 1 } };
        field.RegisterValueChangedCallback(e => { layers[index] = e.newValue as Material; RefreshChecks(); });
        row.Add(field);
        row.Add(new Button(() => { layers.RemoveAt(index); FillLayers(box); RefreshChecks(); }) { text = "✕" });
        return row;
    }

    private void OnModelPicked(GameObject newModel)
    {
        model = newModel;
        EditorPrefs.SetString(ModelPref, model != null ? AssetDatabase.GetAssetPath(model) : "");
        ResetLook();
    }

    // A reworked look starts from the folder it was moved into and the layers it has now.
    private void OnOptionPicked(string option)
    {
        picked = option;
        folder = Reworking ? AssetDatabase.LoadAssetAtPath<DefaultAsset>(Destination) : null;
        layers.Clear();
        if (Reworking) layers.AddRange(CurrentLayers());

        Scan();
        ShowStep(Step);
    }

    private void OnFolderPicked(DefaultAsset newFolder)
    {
        bool isFolder = newFolder != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(newFolder));
        folder = isFolder ? newFolder : null;
        if (folder != null && !Reworking && optionName == "") optionName = ObjectNames.NicifyVariableName(folder.name);

        Scan();
        ShowStep(Step);
    }

    private void ResetLook()
    {
        picked = NewOption;
        optionName = "";
        folder = null;
        layers.Clear();
        Scan();
        ShowStep(0);
    }

    private void Scan()
    {
        sources.Clear();
        if (folder != null)
            foreach (string path in ItemSetupTab.SourcePaths(AssetDatabase.GetAssetPath(folder)))
                sources.Add(AssetDatabase.LoadAssetAtPath<GameObject>(path));

        ShowOnStage();
    }

    private void ShowOnStage()
    {
        if (stage == null) return;

        var looks = new List<AppearanceChoice>();
        if (Reworking) looks.Add(new AppearanceChoice { groupId = Current.groupId, optionId = picked });

        stage.Show(model, looks, new List<ItemDefinition>());
        stage.Frame(Current.framing);
    }

    private ModelAppearance.Group CurrentGroup()
    {
        var appearance = Appearance();
        return appearance != null ? appearance.Groups.FirstOrDefault(g => g.groupId == Current.groupId) : null;
    }

    private List<string> ExistingOptions()
    {
        var group = CurrentGroup();
        if (group == null) return new List<string>();

        return group.options.Select(o => o.optionId).Where(id => !(Current.emptyOption && id == "none")).ToList();
    }

    private List<Material> CurrentLayers()
    {
        var group = CurrentGroup();
        var option = group != null ? group.options.FirstOrDefault(o => o.optionId == picked) : null;
        if (option == null || option.layers.Length == 0) return new List<Material>();

        return option.layers[0].materials.ToList();
    }

    protected override void CollectIssues(List<WizardIssue> issues)
    {
        if (model == null) Error(issues, "Pick the character the look goes on, e.g. HighElfBase (step 1).");
        if (model != null && Appearance() == null) Error(issues, $"{model.name} has no ModelAppearance on its root. Model prefab is the character the look goes on (e.g. HighElfBase), not the look itself (step 1).");
        if (Appearance() == null) return;

        if (!Reworking && OptionId == "") Error(issues, "Name the look (step 1).");
        if (!Reworking && ExistingOptions().Contains(OptionId)) Error(issues, $"{Current.name} already has {OptionId}. Pick it to rework it, or rename.");

        var group = CurrentGroup();
        if (group != null && !group.chosenAtCreation) Error(issues, $"{Current.groupId} on {model.name} is an equipment group, not a creation group.");

        if (Current.meshes) MeshIssues(issues);
        else if (!layers.Exists(l => l != null)) Error(issues, "Add at least one layer material (step 2).");
    }

    private void MeshIssues(List<WizardIssue> issues)
    {
        if (folder == null) Error(issues, "Pick the staging folder (step 2).");
        if (folder != null && sources.Count == 0) Error(issues, "The staging folder has no models (step 2).");
        if (folder != null && !Reworking && AssetDatabase.IsValidFolder(Destination)) Error(issues, $"{Destination} already exists. Rename the look or move that folder.");

        var body = BodyRenderer();
        if (body == null) return;

        var modelBones = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));
        if (!string.IsNullOrEmpty(Current.preset) && AssetDatabase.LoadAssetAtPath<TextAsset>(Current.preset) == null) Error(issues, $"No cloth preset at {Current.preset}.");
        if (!modelBones.Contains(Current.bonesParent)) Error(issues, $"{model.name} has no {Current.bonesParent} bone to hang the look's bones off.");

        var skeleton = ClothingSetup.Skeleton(body);
        foreach (var renderer in sources.SelectMany(s => s.GetComponentsInChildren<SkinnedMeshRenderer>(true)))
            RendererIssues(issues, renderer, body, skeleton, modelBones);
    }

    // A bone the model already has but its skeleton doesn't came with another look; this one would bind to it.
    private void RendererIssues(List<WizardIssue> issues, SkinnedMeshRenderer renderer, SkinnedMeshRenderer body, HashSet<string> skeleton, HashSet<string> modelBones)
    {
        if (renderer.name == body.name) Error(issues, $"{renderer.name} has the body's name. Rename it in Blender.");
        if (DefaultMeshNames.Any(n => renderer.name.StartsWith(n))) Warn(issues, $"{renderer.name} still has Blender's default name, and the look's object takes it. Rename it in Blender.");
        if (Reworking) return;

        var parent = body.transform.parent;
        if (parent.Find(renderer.name) != null) Error(issues, $"{model.name} already has a mesh called {renderer.name} from another look; writing would take it from that look. Rework that look, or rename the mesh in Blender.");

        var taken = renderer.bones.Where(b => b != null && !skeleton.Contains(b.name) && modelBones.Contains(b.name)).Select(b => b.name).ToList();
        if (taken.Count > 0) Warn(issues, $"{renderer.name}: {string.Join(", ", taken)} already on {model.name} from another look; this one would bind to those. Prefix its bones in Blender.");
    }

    protected override List<string> PlanLines()
    {
        var lines = new List<string>();
        if (model == null) return lines;

        string kind = CurrentGroup() == null ? $"new creation group {Current.groupId}" : Current.groupId;
        string verb = Reworking ? "Rework" : "Add";
        lines.Add($"{verb} {OptionId} in {kind} on {model.name}.");
        if (Current.meshes) lines.Add($"Bind {sources.Count} file(s); bones of their own hang off {Current.bonesParent}.");
        if (Current.boneCloth) lines.Add($"BoneCloth on those bones, colliding with {string.Join(", ", Current.colliderBones)}.");
        if (!Current.meshes) lines.Add($"Draw {layers.Count(l => l != null)} layer(s) over the body.");
        if (folder != null && AssetDatabase.GetAssetPath(folder) != Destination) lines.Add($"Move {AssetDatabase.GetAssetPath(folder)} to {Destination}.");
        return lines;
    }

    // Binding goes first, so a failed run moves nothing; the folder moves last with its .meta files, so references hold.
    protected override void Write()
    {
        if (!Bind())
        {
            Context.Log("Customisation: a run failed (see the console). Nothing moved.");
            return;
        }

        AssetDatabase.SaveAssets();
        MoveFolder();
        Context.Log($"Customisation: {Current.name} {OptionId} written on {model.name}.");

        picked = OptionId;
        optionName = "";
        folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Destination);
        Scan();
        ShowStep(0);
    }

    private bool Bind()
    {
        var setup = ScriptableObject.CreateInstance<ClothingSetup>();
        bool ok = Current.meshes ? sources.All(source => Run(setup, source)) : Run(setup, null);
        Object.DestroyImmediate(setup);
        return ok;
    }

    private bool Run(ClothingSetup setup, GameObject source)
    {
        var form = new SerializedObject(setup);
        form.FindProperty("clothing").objectReferenceValue = source;
        form.FindProperty("model").objectReferenceValue = model;
        form.FindProperty("material").objectReferenceValue = null;
        form.FindProperty("groupId").stringValue = Current.groupId;
        form.FindProperty("optionId").stringValue = OptionId;
        form.FindProperty("creation").boolValue = true;
        form.FindProperty("emptyOption").boolValue = Current.emptyOption;
        form.FindProperty("fitsGroup").stringValue = "";
        form.FindProperty("boneCloth").boolValue = Current.boneCloth;
        form.FindProperty("meshCloth").boolValue = false;
        form.FindProperty("preset").objectReferenceValue = string.IsNullOrEmpty(Current.preset) ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(Current.preset);
        form.FindProperty("looseBonesParent").stringValue = Current.bonesParent ?? "";
        form.FindProperty("skippedMeshes").arraySize = 0;
        form.FindProperty("fitsSteps").arraySize = 0;

        var colliders = form.FindProperty("colliderBones");
        var bones = Current.colliderBones ?? new string[0];
        colliders.arraySize = bones.Length;
        for (int i = 0; i < bones.Length; i++)
            colliders.GetArrayElementAtIndex(i).stringValue = bones[i];

        var kept = Current.meshes ? new List<Material>() : layers.FindAll(l => l != null);
        var layerList = form.FindProperty("layers");
        layerList.arraySize = kept.Count;
        for (int i = 0; i < kept.Count; i++)
            layerList.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];

        form.ApplyModifiedPropertiesWithoutUndo();
        return setup.Add();
    }

    private void MoveFolder()
    {
        if (folder == null) return;

        string from = AssetDatabase.GetAssetPath(folder);
        if (from == Destination) return;

        EnsureFolder($"{LooksRoot}/{Current.name}");
        string moveError = AssetDatabase.MoveAsset(from, Destination);
        if (!string.IsNullOrEmpty(moveError)) Debug.LogError($"[CustomisationTab] Couldn't move the folder: {moveError}");
    }
}
