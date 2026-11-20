using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrabSystem;
using MagicaCloth2;
using UnityEditor;
using UnityEditor.SceneManagement;
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
//   Hair colour: a shared palette (Palette_Hair) instead of a look. Write turns it into the hair_colour tint group on
//           every model that has hair styles, one option per colour in palette order, so the creation panel and NPC
//           Setup offer it like any look. Edit the palette, Write again to resync.
//   Look:   the category, the model, and the option, new or one already on the model. Reworking an option rebinds its
//           meshes and replaces its layers; its source defaults to the folder it was moved into.
//   Source: the staging folder for a mesh look (an FBX, or a prefab beside it carrying its material), or the layers
//           for a painted one.
//   Cloth:  for a written look with cloth: which of its own bones are Magica roots, and the main cloth parameters.
//           Edits go straight onto the model prefab. Roots stay fixed to the head and the bones under them swing; a
//           bone under no root just follows the head. Changing roots clears Magica's cached selection and init data,
//           so it rebuilds them as roots fixed, everything under them moving. Presets save and load the parameters.
// Remove (Look step, on an existing option) takes the option off the model with its meshes, cloth and the bones only
// it used, and can bin its folder too.
// The stage shows the model wearing the picked option. Write binds through ClothingSetup, one run per file, then moves
// the staging folder to Database/Characters/Customisation/<Category>/<Option>.

public class CustomisationTab : WizardTab, System.IDisposable
{
    public override string Title => "Customisation";
    public override int Order => 7;

    protected override string[] StepNames => new[] { "Look", "Source", "Cloth" };
    protected override string WriteLabel => "Write look";
    protected override string RailNote => "Drop a look's files in a staging folder, pick its category and name, then Write. Checks on the right block Write while anything is wrong.";

    private const string LooksRoot = "Assets/Database/Characters/Customisation";
    private const string ModelPref = "CrabDashboard.Customisation.Model";
    private const string NewOption = "(new)";
    private const float StageHeight = 300f;
    private const string ClothPresets = "Assets/Database/Config/Cloth";
    private const string HairPreset = ClothPresets + "/Hair_Laggy.json";
    private const string HairPalette = "Assets/Database/Config/Customisation/Palette_Hair.asset";
    private const string TintProperty = "_BaseColor";

    // The Magica parameters worth tuning per look; the rest stay on the preset or in the Magica inspector.
    private static readonly string[] ClothParameters =
    {
        "serializeData.gravity",
        "serializeData.gravityFalloff",
        "serializeData.damping",
        "serializeData.radius",
        "serializeData.rootRotation",
        "serializeData.rotationalInterpolation",
        "serializeData.angleRestorationConstraint.useAngleRestoration",
        "serializeData.angleRestorationConstraint.stiffness",
        "serializeData.angleLimitConstraint.useAngleLimit",
        "serializeData.angleLimitConstraint.limitAngle",
        "serializeData.inertiaConstraint.worldInertia",
        "serializeData.inertiaConstraint.localInertia",
        "serializeData.inertiaConstraint.depthInertia",
        "serializeData.inertiaConstraint.particleSpeedLimit",
        "serializeData.colliderCollisionConstraint.mode",
    };

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
        public string tints;
        public string palette;
        public CharacterStage.Framing framing;
    }

    private static readonly Category[] Categories =
    {
        new Category { name = "Hair", groupId = "hair_style", meshes = true, bonesParent = "Head", colliderBones = new[] { "Head" }, boneCloth = true, preset = HairPreset, framing = CharacterStage.Framing.Face },
        new Category { name = "Face", groupId = "face", meshes = true, bonesParent = "Head", colliderBones = new string[0], framing = CharacterStage.Framing.Face },
        new Category { name = "Makeup", groupId = "makeup", emptyOption = true, framing = CharacterStage.Framing.Face },
        new Category { name = "Tattoo", groupId = "tattoo", emptyOption = true, framing = CharacterStage.Framing.Body },
        new Category { name = "Hair colour", groupId = "hair_colour", tints = "hair_style", palette = HairPalette, framing = CharacterStage.Framing.Face },
    };

    private int categoryIndex;
    private GameObject model;
    private string picked = NewOption;
    private string optionName = "";
    private DefaultAsset folder;
    private readonly List<Material> layers = new List<Material>();
    private readonly List<GameObject> sources = new List<GameObject>();
    private CharacterStage stage;
    private TextAsset loadPreset;

    private Category Current => Categories[categoryIndex];
    private bool Reworking => picked != NewOption;
    private string OptionId => Reworking ? picked : ItemDefinitionGenerator.ToItemId(optionName);
    private string Destination => $"{LooksRoot}/{Current.name}/{ItemDefinitionGenerator.ToAssetName(OptionId)}";
    private ModelAppearance Appearance() => model != null ? model.GetComponent<ModelAppearance>() : null;
    private SkinnedMeshRenderer BodyRenderer() => model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
    private bool Coloured => !string.IsNullOrEmpty(Current.palette);
    private ColourPalette Palette() => Coloured ? AssetDatabase.LoadAssetAtPath<ColourPalette>(Current.palette) : null;

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
        if (step == 0) return LookStep();
        return step == 1 ? SourceStep() : ClothStep();
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
        if (Coloured) return pane;

        var options = new List<string> { NewOption };
        options.AddRange(ExistingOptions());
        var optionField = new PopupField<string>("Option", options, Mathf.Max(0, options.IndexOf(picked)));
        optionField.RegisterValueChangedCallback(e => OnOptionPicked(e.newValue));
        pane.Add(optionField);

        if (Reworking)
        {
            pane.Add(new Button(RemoveLook) { text = $"Remove {picked}" });
            return pane;
        }

        var nameField = new TextField("Name") { value = optionName, isDelayed = true };
        nameField.RegisterValueChangedCallback(e => { optionName = e.newValue.Trim(); RefreshChecks(); });
        pane.Add(nameField);
        return pane;
    }

    private VisualElement SourceStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Source"));
        if (Coloured) return PaletteSource(pane);

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

    // The palette is edited in place: it is the one list every model's group is written from.
    private VisualElement PaletteSource(VisualElement pane)
    {
        pane.Add(DashboardLayout.Note($"{Current.name} is a shared palette. Each colour becomes an option that tints every {Current.tints} mesh's {TintProperty}; the first is the default. Keep an id once saves use it."));
        var palette = Palette();
        if (palette == null)
        {
            pane.Add(new Button(CreatePalette) { text = $"Create {Path.GetFileNameWithoutExtension(Current.palette)}" });
            return pane;
        }

        var so = new SerializedObject(palette);
        pane.Add(new PropertyField(so.FindProperty("colours")));
        pane.Bind(so);
        pane.TrackSerializedObjectValue(so, _ => RefreshChecks());
        return pane;
    }

    private void CreatePalette()
    {
        EnsureFolder(Path.GetDirectoryName(Current.palette));
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ColourPalette>(), Current.palette);
        AssetDatabase.SaveAssets();
        ShowStep(Step);
    }

    // Every model the game lists, and the picked one, that has the group this palette tints.
    private List<ModelAppearance> TintedModels()
    {
        var prefabs = AssetFinder.LoadAll<ModelDatabase>().SelectMany(d => d.AllModels).Select(m => m.prefab).ToList();
        prefabs.Add(model);

        return prefabs.Where(p => p != null).Distinct().Select(p => p.GetComponent<ModelAppearance>())
            .Where(a => a != null && a.Groups.Any(g => g.groupId == Current.tints)).ToList();
    }

    // The palette is the truth: the group's options are rewritten in its order, so a removed colour goes too.
    private void SyncPalette(ModelAppearance appearance, ColourPalette palette)
    {
        var so = new SerializedObject(appearance);
        var groups = so.FindProperty("groups");
        int g = IndexOf(groups, "groupId", Current.groupId);
        if (g < 0)
        {
            groups.arraySize++;
            g = groups.arraySize - 1;
        }

        var group = groups.GetArrayElementAtIndex(g);
        group.FindPropertyRelative("groupId").stringValue = Current.groupId;
        group.FindPropertyRelative("displayName").stringValue = Current.name;
        group.FindPropertyRelative("chosenAtCreation").boolValue = true;
        group.FindPropertyRelative("slider").boolValue = false;
        group.FindPropertyRelative("unequippedOptionId").stringValue = "";
        group.FindPropertyRelative("tints").stringValue = Current.tints;
        group.FindPropertyRelative("tintProperty").stringValue = TintProperty;

        var options = group.FindPropertyRelative("options");
        options.arraySize = palette.colours.Count;
        for (int i = 0; i < palette.colours.Count; i++)
        {
            var entry = palette.colours[i];
            var option = options.GetArrayElementAtIndex(i);
            option.FindPropertyRelative("optionId").stringValue = entry.id;
            ClothingSetup.ResetOption(option, entry.displayName);
            option.FindPropertyRelative("tint").colorValue = entry.colour;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private void PaletteIssues(List<WizardIssue> issues)
    {
        var palette = Palette();
        if (palette == null)
        {
            Error(issues, $"No palette at {Current.palette}. Create it in step 2.");
            return;
        }

        if (palette.colours.Count == 0) Error(issues, "Add at least one colour to the palette (step 2).");
        if (palette.colours.Any(c => string.IsNullOrEmpty(c.id))) Error(issues, "Every colour needs an id (step 2).");
        if (palette.colours.Any(c => string.IsNullOrEmpty(c.displayName))) Warn(issues, "A colour has no display name; the creation panel will show a blank.");

        var repeated = palette.colours.GroupBy(c => c.id).Where(ids => ids.Count() > 1).Select(ids => ids.Key).ToList();
        if (repeated.Count > 0) Error(issues, $"Colour ids used twice: {string.Join(", ", repeated)}.");

        var group = CurrentGroup();
        if (group != null && !group.chosenAtCreation) Error(issues, $"{Current.groupId} on {model.name} is an equipment group, not a creation group.");
        if (TintedModels().Count == 0) Error(issues, $"No model has a {Current.tints} group to colour. Add a hair style first.");
    }

    private VisualElement ClothStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Cloth"));
        if (!Current.boneCloth)
        {
            pane.Add(DashboardLayout.Note($"{Current.name} has no cloth."));
            return pane;
        }

        var cloths = LookCloths();
        if (cloths.Count == 0)
        {
            pane.Add(DashboardLayout.Note("Write the look first, then pick it as the option to tune its cloth here."));
            return pane;
        }

        pane.Add(DashboardLayout.Note("Ticked bones are roots: they stay fixed to the head and the bones under them swing. An unticked bone under no root just follows the head. A root with nothing under it never moves. Edits go straight onto the model prefab; Save keeps them."));
        foreach (var cloth in cloths)
            pane.Add(ClothBox(cloth));

        pane.Add(new Button(AssetDatabase.SaveAssets) { text = "Save" });
        return pane;
    }

    private VisualElement ClothBox(MagicaCloth cloth)
    {
        var box = new VisualElement { style = { marginBottom = 8 } };
        var renderer = ClothRenderer(cloth);
        box.Add(DashboardLayout.Heading(renderer != null ? renderer.name : cloth.name));

        box.Add(new Label("Roots"));
        foreach (var (bone, depth) in OwnBones(renderer))
            box.Add(RootToggle(cloth, bone, depth));

        box.Add(new Label("Parameters"));
        var so = new SerializedObject(cloth);
        foreach (var property in ClothParameters.Select(so.FindProperty).Where(p => p != null))
            box.Add(new PropertyField(property));
        box.Bind(so);

        box.Add(PresetRow(cloth));
        box.Add(new Button(() => OpenInPrefab(cloth)) { text = "Open in Magica inspector" });
        return box;
    }

    private Toggle RootToggle(MagicaCloth cloth, Transform bone, int depth)
    {
        var toggle = new Toggle(bone.name) { value = cloth.SerializeData.rootBones.Contains(bone) };
        toggle.style.marginLeft = depth * 16;
        toggle.RegisterValueChangedCallback(e => SetRoot(cloth, bone, e.newValue));
        return toggle;
    }

    private VisualElement PresetRow(MagicaCloth cloth)
    {
        var row = Row();
        var field = new ObjectField { objectType = typeof(TextAsset), allowSceneObjects = false, value = loadPreset, style = { flexGrow = 1 } };
        field.RegisterValueChangedCallback(e => loadPreset = e.newValue as TextAsset);
        row.Add(field);
        row.Add(new Button(() => LoadPreset(cloth)) { text = "Load preset" });
        row.Add(new Button(() => SavePreset(cloth)) { text = "Save as preset" });
        return row;
    }

    // Rebuilt in hierarchy order from the look's own bones, so no empty slot survives (Magica throws on one). The
    // cached selection and init data were made for the old roots; emptied, Magica remakes them from the new ones.
    private void SetRoot(MagicaCloth cloth, Transform bone, bool on)
    {
        var current = cloth.SerializeData.rootBones;
        var wanted = OwnBones(ClothRenderer(cloth)).Select(b => b.bone).Where(b => b == bone ? on : current.Contains(b)).ToList();

        var so = new SerializedObject(cloth);
        var roots = so.FindProperty("serializeData.rootBones");
        roots.arraySize = wanted.Count;
        for (int i = 0; i < wanted.Count; i++)
            roots.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];

        so.FindProperty("serializeData2.selectionData.positions").arraySize = 0;
        so.FindProperty("serializeData2.selectionData.attributes").arraySize = 0;
        so.FindProperty("serializeData2.initData.initVersion").intValue = 0;
        so.ApplyModifiedProperties();
        RefreshChecks();
    }

    // Magica's import keeps the roots, renderers and colliders, so only the parameters change.
    private void LoadPreset(MagicaCloth cloth)
    {
        if (loadPreset == null) return;

        Undo.RecordObject(cloth, "Load cloth preset");
        if (!cloth.SerializeData.ImportJson(loadPreset.text)) Debug.LogError($"[CustomisationTab] {loadPreset.name} isn't a Magica preset.", loadPreset);
        EditorUtility.SetDirty(cloth);
        ShowStep(Step);
    }

    private void SavePreset(MagicaCloth cloth)
    {
        string path = EditorUtility.SaveFilePanelInProject("Save cloth preset", $"{Current.name}_{ItemDefinitionGenerator.ToAssetName(OptionId)}", "json", "", ClothPresets);
        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, cloth.SerializeData.ExportJson());
        AssetDatabase.ImportAsset(path);
        loadPreset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        Context.Log($"Customisation: cloth preset saved to {path}.");
    }

    // For what the tab doesn't show: painting, curves in full, the rest of Magica's settings.
    private void OpenInPrefab(MagicaCloth cloth)
    {
        string path = AnimationUtility.CalculateTransformPath(cloth.transform, model.transform);
        var prefabStage = PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(model));
        if (prefabStage == null) return;

        var found = prefabStage.prefabContentsRoot.transform.Find(path);
        if (found != null) Selection.activeGameObject = found.gameObject;
    }

    private static SkinnedMeshRenderer ClothRenderer(MagicaCloth cloth)
    {
        return cloth.transform.parent != null ? cloth.transform.parent.GetComponent<SkinnedMeshRenderer>() : null;
    }

    // Every bone the look brought, each chain top first and its children under it.
    private List<(Transform bone, int depth)> OwnBones(SkinnedMeshRenderer renderer)
    {
        var bones = new List<(Transform, int)>();
        var body = BodyRenderer();
        if (renderer == null || body == null) return bones;

        foreach (var top in ClothingSetup.OwnRoots(renderer, ClothingSetup.Skeleton(body)))
            AddBranch(bones, top, 0);

        return bones;
    }

    private static void AddBranch(List<(Transform, int)> bones, Transform bone, int depth)
    {
        bones.Add((bone, depth));
        foreach (Transform child in bone)
            if (!child.TryGetComponent<ColliderComponent>(out _)) AddBranch(bones, child, depth + 1);
    }

    private List<MagicaCloth> LookCloths()
    {
        var cloths = new List<MagicaCloth>();
        var appearance = Appearance();
        if (appearance == null || !Reworking) return cloths;

        var option = OptionProperty(new SerializedObject(appearance));
        if (option == null) return cloths;

        foreach (var shown in Shown(option))
            cloths.AddRange(shown.GetComponentsInChildren<MagicaCloth>(true));

        return cloths;
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

    private SerializedProperty OptionProperty(SerializedObject appearance)
    {
        var groups = appearance.FindProperty("groups");
        int g = IndexOf(groups, "groupId", Current.groupId);
        if (g < 0) return null;

        var options = groups.GetArrayElementAtIndex(g).FindPropertyRelative("options");
        int o = IndexOf(options, "optionId", picked);
        return o >= 0 ? options.GetArrayElementAtIndex(o) : null;
    }

    private static int IndexOf(SerializedProperty array, string idField, string id)
    {
        for (int i = 0; i < array.arraySize; i++)
            if (array.GetArrayElementAtIndex(i).FindPropertyRelative(idField).stringValue == id) return i;

        return -1;
    }

    // What the option shows, on every step of the groups it fits.
    private static List<GameObject> Shown(SerializedProperty option)
    {
        var shown = Objects(option.FindPropertyRelative("show"));
        var variants = option.FindPropertyRelative("variants");
        for (int i = 0; i < variants.arraySize; i++)
            shown.AddRange(Objects(variants.GetArrayElementAtIndex(i).FindPropertyRelative("show")));

        return shown;
    }

    private static List<GameObject> Objects(SerializedProperty list)
    {
        var objects = new List<GameObject>();
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go) objects.Add(go);

        return objects;
    }

    private void RemoveLook()
    {
        int choice = EditorUtility.DisplayDialogComplex("Remove look",
            $"Remove {picked} from {Current.groupId} on {model.name}? Its meshes, cloth and the bones only it uses go with it.",
            "Remove, bin its folder", "Cancel", "Remove, keep its folder");
        if (choice == 1) return;

        string modelPath = AssetDatabase.GetAssetPath(model);
        var root = PrefabUtility.LoadPrefabContents(modelPath);
        bool removed = RemoveFrom(root);
        if (removed) PrefabUtility.SaveAsPrefabAsset(root, modelPath);
        PrefabUtility.UnloadPrefabContents(root);
        if (!removed) return;

        // The OS trash, not a delete, so a wrong click can be undone.
        if (choice == 0 && AssetDatabase.IsValidFolder(Destination)) AssetDatabase.MoveAssetToTrash(Destination);

        Context.Log($"Customisation: {Current.name} {picked} removed from {model.name}.");
        ResetLook();
    }

    // The option goes first, then its objects and bones; references the rest of the appearance held to them drop out.
    // A group left with no options goes too.
    private bool RemoveFrom(GameObject root)
    {
        var appearance = root.GetComponent<ModelAppearance>();
        var body = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
        var so = new SerializedObject(appearance);
        var groups = so.FindProperty("groups");
        int g = IndexOf(groups, "groupId", Current.groupId);
        var options = g >= 0 ? groups.GetArrayElementAtIndex(g).FindPropertyRelative("options") : null;
        int o = options != null ? IndexOf(options, "optionId", picked) : -1;
        if (o < 0)
        {
            Debug.LogError($"[CustomisationTab] {model.name} has no {picked} in {Current.groupId}.", model);
            return false;
        }

        var shown = Shown(options.GetArrayElementAtIndex(o));
        var renderers = shown.SelectMany(s => s.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToList();
        var bones = UnusedBones(root, ClothingSetup.Skeleton(body), renderers);

        options.DeleteArrayElementAtIndex(o);
        if (options.arraySize == 0) groups.DeleteArrayElementAtIndex(g);
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (var go in shown)
            Object.DestroyImmediate(go);
        foreach (var bone in bones)
            Object.DestroyImmediate(bone.gameObject);

        so.Update();
        for (int i = 0; i < groups.arraySize; i++)
            DropMissingIn(groups.GetArrayElementAtIndex(i).FindPropertyRelative("options"));
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void DropMissingIn(SerializedProperty options)
    {
        for (int i = 0; i < options.arraySize; i++)
            ClothingSetup.DropMissing(options.GetArrayElementAtIndex(i));
    }

    // The tops of the look's own chains that no other mesh is weighted to anywhere below; each goes with its branch.
    private static List<Transform> UnusedBones(GameObject root, HashSet<string> skeleton, List<SkinnedMeshRenderer> removed)
    {
        var used = new HashSet<Transform>(root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(r => !removed.Contains(r)).SelectMany(r => r.bones).Where(b => b != null));

        return removed.SelectMany(r => ClothingSetup.OwnRoots(r, skeleton)).Distinct()
            .Where(top => !top.GetComponentsInChildren<Transform>(true).Any(used.Contains)).ToList();
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

        if (Coloured)
        {
            PaletteIssues(issues);
            return;
        }

        if (!Reworking && OptionId == "") Error(issues, "Name the look (step 1).");
        if (!Reworking && ExistingOptions().Contains(OptionId)) Error(issues, $"{Current.name} already has {OptionId}. Pick it to rework it, or rename.");

        var group = CurrentGroup();
        if (group != null && !group.chosenAtCreation) Error(issues, $"{Current.groupId} on {model.name} is an equipment group, not a creation group.");

        if (Current.meshes) MeshIssues(issues);
        else if (!layers.Exists(l => l != null)) Error(issues, "Add at least one layer material (step 2).");

        foreach (var cloth in LookCloths())
            ClothIssues(issues, cloth);
    }

    private static void ClothIssues(List<WizardIssue> issues, MagicaCloth cloth)
    {
        var renderer = ClothRenderer(cloth);
        string owner = renderer != null ? renderer.name : cloth.name;
        var roots = cloth.SerializeData.rootBones;
        if (roots.Exists(r => r == null)) Warn(issues, $"{owner}: a cloth root slot is empty, so Magica can't build the cloth and nothing on it moves. Tick or untick any root in step 3 to rewrite the list.");
        if (!roots.Exists(r => r != null)) Warn(issues, $"{owner}: no cloth roots, so nothing on it moves. Tick roots in step 3.");

        foreach (var root in roots.Where(r => r != null && r.childCount == 0))
            Warn(issues, $"{owner}: {root.name} is a root with no bone under it. Roots stay fixed, so it never moves. In Blender, parent it under a short anchor bone (the way Bangs1 sits above Bangs2) and tick the anchor instead.");
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

        var palette = Palette();
        if (Coloured && palette != null)
        {
            lines.Add($"Write {palette.colours.Count} colour(s) as {Current.groupId}, tinting {Current.tints}, on: {string.Join(", ", TintedModels().Select(a => a.name))}.");
            return lines;
        }

        string kind = CurrentGroup() == null ? $"new creation group {Current.groupId}" : Current.groupId;
        string verb = Reworking ? "Rework" : "Add";
        lines.Add($"{verb} {OptionId} in {kind} on {model.name}.");
        if (Current.meshes) lines.Add($"Bind {sources.Count} file(s); bones of their own hang off {Current.bonesParent}.");
        if (Current.boneCloth) lines.Add($"BoneCloth on those bones, colliding with {string.Join(", ", Current.colliderBones)}.");
        if (Current.boneCloth && LookCloths().Count > 0) lines.Add($"The cloth is rebuilt: every chain top is a root again and {Path.GetFileNameWithoutExtension(Current.preset)} is reapplied, so step 3 changes go. Save them as a preset first to load back after.");
        if (!Current.meshes) lines.Add($"Draw {layers.Count(l => l != null)} layer(s) over the body.");
        if (folder != null && AssetDatabase.GetAssetPath(folder) != Destination) lines.Add($"Move {AssetDatabase.GetAssetPath(folder)} to {Destination}.");
        return lines;
    }

    // Binding goes first, so a failed run moves nothing; the folder moves last with its .meta files, so references hold.
    protected override void Write()
    {
        if (Coloured)
        {
            var palette = Palette();
            var models = TintedModels();
            foreach (var appearance in models)
                SyncPalette(appearance, palette);

            AssetDatabase.SaveAssets();
            Context.Log($"Customisation: {Current.name} written to {models.Count} model(s).");
            ShowOnStage();
            RefreshChecks();
            return;
        }

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
