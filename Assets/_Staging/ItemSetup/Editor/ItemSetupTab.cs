using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > Item Setup. Staging: graduates to Database/Scripts/Editor with ClothingSetup.
//
// Turns a folder of staged clothing (FBXs or prefabs, skinned to the base armature) into a finished armour set in
// one pass, so a set is three short steps rather than a ClothingSetup run per mesh plus hand-made items.
//   Source: the folder, the model the set goes on, the set's name and tier, and a vendor to stock it.
//   Pieces: every skinned mesh found, with its slot (guessed from the mesh or file name), cloth and the slider steps
//           it fits. A mesh that brings bones of its own starts on Bone cloth.
//   Items:  one item per slot used, named after the set, stats copied from an existing item of that slot and tier,
//           with its appearance group and the body layers drawn while it is worn.
// Set Up then binds each run of meshes through ClothingSetup (one run per file, slot, cloth and fit), writes the
// items into the item folder, stocks the vendor, and moves the folder under Database/Items/Models so the staging
// folder is left empty. A prefab beside an FBX of the same name is used instead of the FBX, so material choices
// made on it are kept. Every option the set adds uses one option id, the set's name as an id.

public class ItemSetupTab : WizardTab
{
    public override string Title => "Item Setup";
    public override int Order => 5;

    protected override string[] StepNames => new[] { "Source", "Pieces", "Items" };
    protected override string WriteLabel => "Set up";
    protected override string RailNote => "Drop a set's models in a staging folder, walk the steps, then Set up. Checks on the right block Set up while anything is wrong.";

    private const string ModelsRoot = "Assets/Database/Items/Models";
    private const string ModelPref = "CrabDashboard.ItemSetup.Model";
    private const string NoSlot = "(none)";
    private const string EveryStep = "(every step)";
    private const string NoTemplate = "(no stats)";
    private const string LayerShader = "Crab/FlatToon";

    // Appearance groups are named after their slot, except body armour, which drives the existing body group.
    private static readonly Dictionary<string, string> GroupForSlot = new Dictionary<string, string> { { "bodyarmor", "body" } };

    private enum ClothKind { None, Bone, Mesh }
    private enum Tier { Light, Medium, Heavy }

    private class Piece
    {
        public string sourcePath;
        public GameObject source;
        public SkinnedMeshRenderer renderer;
        public bool include = true;
        public string slot;
        public ClothKind cloth;
        public string fitsGroup = "";
        public List<string> fitsSteps = new List<string>();
        public List<string> missingShapes = new List<string>();
        public float bindScale = 1f;
    }

    private class ItemPlan
    {
        public string slot;
        public string displayName;
        public string itemId;
        public string description = "";
        public Sprite icon;
        public ItemDefinition template;
        public string group;
        public List<Material> layers = new List<Material>();
    }

    private DefaultAsset folder;
    private GameObject model;
    private string setName = "";
    private Tier tier = Tier.Light;
    private VendorDefinition vendor;

    private readonly List<Piece> pieces = new List<Piece>();
    private readonly List<ItemPlan> items = new List<ItemPlan>();
    private Dictionary<string, ItemSubType> subTypes = new Dictionary<string, ItemSubType>();
    private List<ItemDefinition> allItems = new List<ItemDefinition>();

    private string OptionId => ItemDefinitionGenerator.ToItemId(setName);
    private string SetAssetName => ItemDefinitionGenerator.ToAssetName(setName);
    private string TierTag => "armour_" + tier.ToString().ToLowerInvariant();
    private string ModelsFolder => $"{ModelsRoot}/{SetAssetName}";

    protected override void OnBuild()
    {
        model = AssetDatabase.LoadAssetAtPath<GameObject>(EditorPrefs.GetString(ModelPref, ""));
        Scan();
    }

    protected override void OnStepShown(int step)
    {
        if (step == 2) SyncItems(false);
    }

    protected override VisualElement StepContent(int step)
    {
        return step == 0 ? SourceStep() : step == 1 ? PiecesStep() : ItemsStep();
    }


    private VisualElement SourceStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Source"));
        pane.Add(DashboardLayout.Note("The folder holds the set's clothing FBXs or prefabs, skinned to the base armature. Changing the name or tier resets the item names and stats on step 3."));

        var folderField = new ObjectField("Staging folder") { objectType = typeof(DefaultAsset), allowSceneObjects = false, value = folder };
        folderField.RegisterValueChangedCallback(e => OnFolderPicked(e.newValue as DefaultAsset));
        pane.Add(folderField);

        var modelField = new ObjectField("Model prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = model };
        modelField.RegisterValueChangedCallback(e => OnModelPicked(e.newValue as GameObject));
        pane.Add(modelField);

        var nameField = new TextField("Set name") { value = setName, isDelayed = true };
        nameField.RegisterValueChangedCallback(e => { setName = e.newValue.Trim(); SyncItems(true); RefreshChecks(); });
        pane.Add(nameField);

        var tierField = new EnumField("Tier", tier);
        tierField.RegisterValueChangedCallback(e => { tier = (Tier)e.newValue; SyncItems(true); RefreshChecks(); });
        pane.Add(tierField);

        var vendorField = new ObjectField("Stock at vendor") { objectType = typeof(VendorDefinition), allowSceneObjects = false, value = vendor };
        vendorField.RegisterValueChangedCallback(e => { vendor = e.newValue as VendorDefinition; RefreshChecks(); });
        pane.Add(vendorField);

        pane.Add(new Button(() => { Scan(); ShowStep(0); }) { text = "Rescan folder" });
        return pane;
    }

    private void OnFolderPicked(DefaultAsset picked)
    {
        bool isFolder = picked != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(picked));
        folder = isFolder ? picked : null;
        if (folder != null && setName == "") setName = ObjectNames.NicifyVariableName(folder.name);

        Scan();
        ShowStep(0);
    }

    private void OnModelPicked(GameObject picked)
    {
        model = picked;
        EditorPrefs.SetString(ModelPref, model != null ? AssetDatabase.GetAssetPath(model) : "");
        Scan();
        ShowStep(0);
    }

    private VisualElement PiecesStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Pieces"));
        pane.Add(DashboardLayout.Note("One row per skinned mesh. Untick a mesh to leave it out. Bone cloth simulates bones the mesh brings (a skirt's chains); Mesh cloth simulates the mesh itself and needs its fixed vertices painted afterwards. Fits: a mesh made for some steps of a slider shows only on those steps."));

        if (pieces.Count == 0)
        {
            pane.Add(DashboardLayout.Note("No skinned meshes yet. Pick a staging folder and a model on step 1."));
            return pane;
        }

        string current = null;
        foreach (var piece in pieces)
        {
            if (piece.sourcePath != current) pane.Add(SourceHeader(piece.sourcePath));
            current = piece.sourcePath;

            var row = new VisualElement { style = { marginLeft = 12, marginBottom = 4 } };
            FillPieceRow(row, piece);
            pane.Add(row);
        }

        return pane;
    }

    // Batch: put every mesh of one file in a slot at once.
    private VisualElement SourceHeader(string path)
    {
        var row = Row();
        row.style.marginTop = 8;
        row.Add(new Label(Path.GetFileName(path)) { style = { unityFontStyleAndWeight = FontStyle.Bold, width = 260 } });
        row.Add(new Label("All to") { style = { width = 40 } });
        row.Add(SlotPopup(null, slot => SetSourceSlot(path, slot)));
        return row;
    }

    private void SetSourceSlot(string path, string slot)
    {
        foreach (var piece in pieces)
            if (piece.sourcePath == path) piece.slot = slot;

        SyncItems(false);
        ShowStep(1);
    }

    private void FillPieceRow(VisualElement row, Piece piece)
    {
        row.Clear();
        var line = Row();

        var include = new Toggle { value = piece.include };
        include.RegisterValueChangedCallback(e => { piece.include = e.newValue; StructureChanged(); });
        line.Add(include);
        line.Add(new Label(piece.renderer.name) { style = { width = 220 } });
        line.Add(SlotPopup(piece.slot, slot => { piece.slot = slot; StructureChanged(); }));

        var cloth = new EnumField(piece.cloth) { style = { width = 70 } };
        cloth.RegisterValueChangedCallback(e => { piece.cloth = (ClothKind)e.newValue; RefreshChecks(); });
        line.Add(cloth);
        line.Add(FitsPopup(row, piece));
        row.Add(line);

        if (piece.fitsGroup != "") row.Add(StepToggles(piece));
        if (piece.missingShapes.Count > 0)
            row.Add(DashboardLayout.Note($"No {string.Join(", ", piece.missingShapes)} shape keys: it may clip when those sliders go up."));
    }

    private PopupField<string> SlotPopup(string current, Action<string> set)
    {
        var choices = new List<string> { NoSlot };
        choices.AddRange(subTypes.Keys.OrderBy(slot => slot));

        string shown = current != null && subTypes.ContainsKey(current) ? current : NoSlot;
        var popup = new PopupField<string>(choices, shown) { style = { width = 110 } };
        popup.RegisterValueChangedCallback(e => set(e.newValue == NoSlot ? null : e.newValue));
        return popup;
    }

    private PopupField<string> FitsPopup(VisualElement row, Piece piece)
    {
        var choices = new List<string> { EveryStep };
        foreach (var group in SliderGroups())
            choices.Add(group.groupId);

        string shown = choices.Contains(piece.fitsGroup) ? piece.fitsGroup : EveryStep;
        var popup = new PopupField<string>(choices, shown) { style = { width = 130 } };
        popup.RegisterValueChangedCallback(e =>
        {
            piece.fitsGroup = e.newValue == EveryStep ? "" : e.newValue;
            piece.fitsSteps.Clear();
            FillPieceRow(row, piece);
            RefreshChecks();
        });
        return popup;
    }

    private VisualElement StepToggles(Piece piece)
    {
        var line = Row();
        line.style.marginLeft = 24;
        line.Add(new Label("Fits steps") { style = { width = 70 } });

        var group = SliderGroups().Find(g => g.groupId == piece.fitsGroup);
        if (group == null) return line;

        foreach (var option in group.options)
        {
            string stepId = option.optionId;
            var toggle = new Toggle(stepId) { value = piece.fitsSteps.Contains(stepId) };
            toggle.labelElement.style.minWidth = 0;
            toggle.style.marginRight = 8;
            toggle.RegisterValueChangedCallback(e => ToggleStep(piece, stepId, e.newValue));
            line.Add(toggle);
        }

        return line;
    }

    private void ToggleStep(Piece piece, string stepId, bool on)
    {
        piece.fitsSteps.Remove(stepId);
        if (on) piece.fitsSteps.Add(stepId);
        piece.fitsSteps.Sort(string.CompareOrdinal);
        RefreshChecks();
    }

    private VisualElement ItemsStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Items"));
        pane.Add(DashboardLayout.Note($"One item per slot. Stats are copied from the item picked under Stats from (the first {TierTag} item of that slot by default). Body layers are drawn over the skin while it's worn, bottom first, and must use {LayerShader}."));

        if (items.Count == 0) pane.Add(DashboardLayout.Note("No pieces have a slot yet (step 2)."));
        foreach (var item in items)
            pane.Add(ItemCard(item));

        return pane;
    }

    private VisualElement ItemCard(ItemPlan item)
    {
        var card = new VisualElement { style = { marginBottom = 12, paddingLeft = 6, borderLeftWidth = 2, borderLeftColor = new Color(0.5f, 0.6f, 0.8f, 0.6f) } };
        card.Add(DashboardLayout.Heading($"{SlotName(item.slot)}  ·  {string.Join(", ", MeshesIn(item.slot))}"));

        var idField = new TextField("Item id") { value = item.itemId };
        idField.RegisterValueChangedCallback(e => { item.itemId = e.newValue.Trim(); RefreshChecks(); });

        var nameField = new TextField("Name") { value = item.displayName };
        nameField.RegisterValueChangedCallback(e => Rename(item, e.newValue, idField));
        card.Add(nameField);
        card.Add(idField);

        var description = new TextField("Description") { value = item.description, multiline = true };
        description.RegisterValueChangedCallback(e => item.description = e.newValue);
        card.Add(description);

        var icon = new ObjectField("Icon") { objectType = typeof(Sprite), allowSceneObjects = false, value = item.icon };
        icon.RegisterValueChangedCallback(e => { item.icon = e.newValue as Sprite; RefreshChecks(); });
        card.Add(icon);

        var stats = DashboardLayout.Note(StatsText(item.template));
        card.Add(TemplatePopup(item, stats));
        card.Add(stats);

        var group = new TextField("Appearance group") { value = item.group, isDelayed = true };
        group.RegisterValueChangedCallback(e => { item.group = e.newValue.Trim(); RefreshChecks(); });
        card.Add(group);

        var layers = new VisualElement();
        FillLayers(layers, item);
        card.Add(layers);
        return card;
    }

    // An id still matching the old name's automatic id follows the new name; one typed by hand is kept.
    private void Rename(ItemPlan item, string newName, TextField idField)
    {
        bool autoId = item.itemId == AutoId(item.displayName);
        item.displayName = newName.Trim();
        if (autoId) item.itemId = AutoId(item.displayName);

        idField.SetValueWithoutNotify(item.itemId);
        RefreshChecks();
    }

    private PopupField<string> TemplatePopup(ItemPlan item, Label stats)
    {
        var templates = Templates(item.slot);
        var names = new List<string> { NoTemplate };
        names.AddRange(templates.Select(t => t.name));

        var popup = new PopupField<string>("Stats from", names, item.template != null ? item.template.name : NoTemplate);
        popup.RegisterValueChangedCallback(e =>
        {
            item.template = templates.Find(t => t.name == e.newValue);
            stats.text = StatsText(item.template);
            RefreshChecks();
        });
        return popup;
    }

    private void FillLayers(VisualElement box, ItemPlan item)
    {
        box.Clear();
        box.Add(new Label("Body layers (bottom first)") { style = { marginTop = 4 } });
        for (int i = 0; i < item.layers.Count; i++)
            box.Add(LayerRow(box, item, i));

        box.Add(new Button(() => { item.layers.Add(null); FillLayers(box, item); }) { text = "Add layer" });
    }

    private VisualElement LayerRow(VisualElement box, ItemPlan item, int index)
    {
        var row = Row();
        var field = new ObjectField { objectType = typeof(Material), allowSceneObjects = false, value = item.layers[index], style = { flexGrow = 1 } };
        field.RegisterValueChangedCallback(e => { item.layers[index] = e.newValue as Material; RefreshChecks(); });
        row.Add(field);
        row.Add(new Button(() => { item.layers.RemoveAt(index); FillLayers(box, item); RefreshChecks(); }) { text = "✕" });
        return row;
    }


    private void Scan()
    {
        pieces.Clear();
        subTypes = ItemDefinitionGenerator.CollectSubTypes();
        allItems = LoadItems();

        if (folder == null || model == null)
        {
            SyncItems(true);
            return;
        }

        var skeleton = Skeleton(BodyRenderer());
        var sliderShapes = SliderShapes();
        foreach (string path in SourcePaths(AssetDatabase.GetAssetPath(folder)))
            AddPieces(path, skeleton, sliderShapes);

        SyncItems(true);
    }

    // Also read by the Customisation tab.
    internal static List<string> SourcePaths(string folderPath)
    {
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folderPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool fbx = path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
            if (fbx && File.Exists(Path.ChangeExtension(path, ".prefab"))) continue;

            paths.Add(path);
        }

        paths.Sort(string.CompareOrdinal);
        return paths;
    }

    private void AddPieces(string path, HashSet<string> skeleton, List<string> sliderShapes)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var piece = new Piece { sourcePath = path, source = source, renderer = renderer };
            piece.slot = GuessSlot(renderer.name, source.name);
            piece.cloth = HasOwnBones(renderer, skeleton) ? ClothKind.Bone : ClothKind.None;
            piece.missingShapes = sliderShapes.FindAll(shape => renderer.sharedMesh == null || renderer.sharedMesh.GetBlendShapeIndex(shape) < 0);
            piece.bindScale = BindScale(renderer, BodyRenderer());
            pieces.Add(piece);
        }
    }

    private string GuessSlot(string meshName, string fileName)
    {
        string slot = ItemDefinitionGenerator.MatchSlot(meshName) ?? ItemDefinitionGenerator.MatchSlot(fileName);
        return slot != null && subTypes.ContainsKey(slot) ? slot : null;
    }

    // How far the piece's bind pose sits from the body's, measured on the shared bone furthest from the origin: 1 when the
    // piece was exported in the body's units. A piece exported in centimetres beside a body in metres binds at a
    // hundredth of its size and can't be seen, whether the importer or the armature's own scale caused it.
    private static float BindScale(SkinnedMeshRenderer piece, SkinnedMeshRenderer body)
    {
        if (piece.sharedMesh == null || body == null || body.sharedMesh == null) return 1f;

        var pieceBinds = piece.sharedMesh.bindposes;
        var bodyBinds = body.sharedMesh.bindposes;
        float reach = 0f;
        float ratio = 1f;
        for (int i = 0; i < piece.bones.Length && i < pieceBinds.Length; i++)
        {
            int j = piece.bones[i] != null ? Array.FindIndex(body.bones, b => b != null && b.name == piece.bones[i].name) : -1;
            if (j < 0 || j >= bodyBinds.Length) continue;

            float bodyReach = ((Vector3)bodyBinds[j].GetColumn(3)).magnitude;
            if (bodyReach <= reach) continue;

            reach = bodyReach;
            ratio = ((Vector3)pieceBinds[i].GetColumn(3)).magnitude / bodyReach;
        }

        return ratio;
    }

    private static bool HasOwnBones(SkinnedMeshRenderer renderer, HashSet<string> skeleton)
    {
        if (skeleton.Count == 0) return false;

        foreach (var bone in renderer.bones)
            if (bone != null && !skeleton.Contains(bone.name)) return true;

        return false;
    }

    // The body's bones and everything above them, as ClothingSetup reads it.
    private static HashSet<string> Skeleton(SkinnedMeshRenderer body)
    {
        var skeleton = new HashSet<string>();
        if (body == null) return skeleton;

        foreach (var bone in body.bones)
            for (Transform t = bone; t != null; t = t.parent)
                skeleton.Add(t.name);

        return skeleton;
    }

    // Shape keys the creation sliders push on the body; clothing needs keys of the same names to follow them.
    private List<string> SliderShapes()
    {
        var shapes = new List<string>();
        var body = BodyRenderer();
        foreach (var group in SliderGroups())
            foreach (var option in group.options)
                foreach (var shape in option.shapes)
                    if (shape.renderer == body && !shapes.Contains(shape.shape)) shapes.Add(shape.shape);

        return shapes;
    }

    private void SyncItems(bool resetDefaults)
    {
        var wanted = new List<string>();
        foreach (var piece in pieces)
            if (piece.include && piece.slot != null && !wanted.Contains(piece.slot)) wanted.Add(piece.slot);

        items.RemoveAll(item => !wanted.Contains(item.slot));
        foreach (string slot in wanted)
        {
            if (FindItem(slot) != null) continue;

            var item = new ItemPlan { slot = slot };
            ResetDefaults(item);
            items.Add(item);
        }

        if (!resetDefaults) return;

        foreach (var item in items)
            ResetDefaults(item);
    }

    private void ResetDefaults(ItemPlan item)
    {
        item.displayName = $"{setName} {SlotName(item.slot)}".Trim();
        item.itemId = AutoId(item.displayName);
        item.group = GroupForSlot.TryGetValue(item.slot, out var group) ? group : item.slot;
        item.template = Templates(item.slot).Find(t => t.HasTag(TierTag));
    }

    private void StructureChanged()
    {
        SyncItems(false);
        RefreshChecks();
    }

    // One ClothingSetup run per file, slot, cloth and fit, since each run takes one of each.
    private List<List<Piece>> Runs()
    {
        var runs = new Dictionary<string, List<Piece>>();
        foreach (var piece in pieces)
        {
            if (!piece.include || piece.slot == null) continue;

            string key = $"{piece.sourcePath}|{piece.slot}|{piece.cloth}|{piece.fitsGroup}|{string.Join(",", piece.fitsSteps)}";
            if (!runs.TryGetValue(key, out var run)) runs[key] = run = new List<Piece>();
            run.Add(piece);
        }

        return runs.Values.ToList();
    }


    protected override void CollectIssues(List<WizardIssue> issues)
    {
        if (folder == null) Error(issues, "Pick a staging folder (step 1).");
        if (Appearance() == null) Error(issues, "Pick a model prefab with a ModelAppearance on its root (step 1).");
        if (setName == "") Error(issues, "Name the set (step 1).");
        if (setName != "" && AssetDatabase.IsValidFolder(ModelsFolder)) Error(issues, $"{ModelsFolder} already exists. Rename the set or move that folder.");
        if (folder != null && !pieces.Exists(p => p.include)) Error(issues, "No meshes ticked (step 2).");

        var bodyName = BodyRenderer() != null ? BodyRenderer().name : "";
        foreach (var piece in pieces)
            if (piece.include) PieceIssues(issues, piece, bodyName);

        foreach (var item in items)
            ItemIssues(issues, item);
    }

    private static void PieceIssues(List<WizardIssue> issues, Piece piece, string bodyName)
    {
        string mesh = piece.renderer.name;
        if (piece.slot == null) Error(issues, $"{mesh}: pick a slot or untick it.");
        if (piece.renderer.sharedMesh == null) Error(issues, $"{mesh}: has no mesh. Re-export it with the armature.");
        if (mesh == bodyName) Error(issues, $"{mesh}: named like the body mesh. Rename it in Blender.");
        if (piece.bindScale < 0.8f || piece.bindScale > 1.25f) Error(issues, $"{mesh}: binds at {piece.bindScale:0.###}x the body's size. Export it in the body's units (Blender: Apply Scalings FBX Units Scale) and reimport.");
        if (piece.fitsGroup != "" && piece.fitsSteps.Count == 0) Error(issues, $"{mesh}: pick the {piece.fitsGroup} steps it fits.");
        if (piece.cloth == ClothKind.Mesh) Warn(issues, $"{mesh}: Mesh cloth won't run until its fixed vertices are painted in the Magica inspector.");
        if (piece.missingShapes.Count > 0) Warn(issues, $"{mesh}: no {string.Join(", ", piece.missingShapes)} shape keys, so it may clip on those sliders.");
    }

    private void ItemIssues(List<WizardIssue> issues, ItemPlan item)
    {
        string name = SlotName(item.slot);
        if (item.displayName == "") Error(issues, $"{name}: needs a name.");
        if (item.itemId == "") Error(issues, $"{name}: needs an item id.");
        if (allItems.Exists(d => d.itemId == item.itemId)) Error(issues, $"{name}: item id {item.itemId} is already used.");
        if (items.Count(other => other.itemId == item.itemId) > 1) Error(issues, $"{name}: two items share the id {item.itemId}.");
        if (item.group == "") Error(issues, $"{name}: needs an appearance group.");
        if (item.displayName != "" && File.Exists(ItemPath(item))) Error(issues, $"{name}: {ItemPath(item)} already exists.");
        GroupIssues(issues, item, name);

        if (item.icon == null) Warn(issues, $"{name}: no icon; it gets the subtype's default icon.");
        if (item.template == null) Warn(issues, $"{name}: no stats to copy, so it gives none.");
        foreach (var layer in item.layers)
            LayerIssues(issues, layer, name);
    }

    private void GroupIssues(List<WizardIssue> issues, ItemPlan item, string name)
    {
        var appearance = Appearance();
        if (appearance == null || item.group == "") return;

        var group = appearance.Groups.FirstOrDefault(g => g.groupId == item.group);
        if (group == null) return;

        if (group.chosenAtCreation) Error(issues, $"{name}: {item.group} is a creation group; clothing needs an equipment group.");
        if (group.options.Any(o => o.optionId == OptionId)) Warn(issues, $"{name}: {item.group} already has the option {OptionId}; its meshes are replaced.");
    }

    private static void LayerIssues(List<WizardIssue> issues, Material layer, string name)
    {
        if (layer == null) Warn(issues, $"{name}: an empty body layer is skipped.");
        else if (layer.shader == null || layer.shader.name != LayerShader) Warn(issues, $"{name}: layer {layer.name} isn't {LayerShader}, so it won't stack on the skin.");
    }

    protected override List<string> PlanLines()
    {
        var lines = new List<string>();
        if (model != null) lines.Add($"Bind {Runs().Count} clothing runs onto {model.name}, option {OptionId}.");
        foreach (var item in items)
            lines.Add($"Create {ItemPath(item)} ({item.group}).");

        if (vendor != null && items.Count > 0) lines.Add($"Stock {items.Count} items at {vendor.name}.");
        if (folder != null) lines.Add($"Move {AssetDatabase.GetAssetPath(folder)} to {ModelsFolder}.");
        return lines;
    }


    // Binding goes first: a failed run stops before any item is made or anything moves, and rerunning replaces
    // the meshes already bound. The folder moves last, with its .meta files, so the bound references survive.
    protected override void Write()
    {
        if (!BindRuns())
        {
            Context.Log("Item Setup: a clothing run failed (see the console). No items made, nothing moved.");
            return;
        }

        var created = items.Select(CreateItem).ToList();
        Stock(created);
        AssetDatabase.SaveAssets();

        EnsureFolder(ModelsRoot);
        string moveError = AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(folder), ModelsFolder);
        if (!string.IsNullOrEmpty(moveError)) Debug.LogError($"[ItemSetupTab] Couldn't move the folder: {moveError}");

        Context.Log($"Item Setup: {setName} done, {created.Count} items, models in {ModelsFolder}.");
        folder = null;
        setName = "";
        Scan();
        ShowStep(0);
    }

    private bool BindRuns()
    {
        var setup = ScriptableObject.CreateInstance<ClothingSetup>();
        var layered = new HashSet<string>();
        bool ok = true;
        foreach (var run in Runs())
        {
            var item = FindItem(run[0].slot);
            Fill(setup, run, item, layered.Add(item.slot) ? item.layers : new List<Material>());
            ok = setup.Add();
            if (!ok) break;
        }

        UnityEngine.Object.DestroyImmediate(setup);
        return ok;
    }

    // Layers go on with the item's first run only; an empty list leaves the option's layers as they are.
    private void Fill(ClothingSetup setup, List<Piece> run, ItemPlan item, List<Material> layers)
    {
        var first = run[0];
        var form = new SerializedObject(setup);
        form.FindProperty("clothing").objectReferenceValue = first.source;
        form.FindProperty("model").objectReferenceValue = model;
        form.FindProperty("material").objectReferenceValue = null;
        form.FindProperty("groupId").stringValue = item.group;
        form.FindProperty("optionId").stringValue = OptionId;
        form.FindProperty("fitsGroup").stringValue = first.fitsGroup;
        form.FindProperty("boneCloth").boolValue = first.cloth == ClothKind.Bone;
        form.FindProperty("meshCloth").boolValue = first.cloth == ClothKind.Mesh;
        SetStrings(form.FindProperty("fitsSteps"), first.fitsSteps);
        SetStrings(form.FindProperty("skippedMeshes"), Skipped(first.source, run));

        var layerList = form.FindProperty("layers");
        var kept = layers.FindAll(layer => layer != null);
        layerList.arraySize = kept.Count;
        for (int i = 0; i < kept.Count; i++)
            layerList.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];

        form.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<string> Skipped(GameObject source, List<Piece> run)
    {
        var skipped = new List<string>();
        foreach (var renderer in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (!run.Exists(piece => piece.renderer == renderer)) skipped.Add(renderer.name);

        return skipped;
    }

    private static void SetStrings(SerializedProperty array, List<string> values)
    {
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            array.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    private ItemDefinition CreateItem(ItemPlan plan)
    {
        var subType = subTypes[plan.slot];
        var grid = ItemDefinitionGenerator.SlotGrid.TryGetValue(plan.slot, out var size) ? size : Vector2Int.one;

        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.itemId = plan.itemId;
        item.displayName = plan.displayName;
        item.description = plan.description;
        item.icon = plan.icon != null ? plan.icon : subType.defaultIcon;
        item.appearanceGroup = plan.group;
        item.appearanceOption = OptionId;
        item.category = subType.parentCategory;
        item.subType = subType;
        item.gridWidth = grid.x;
        item.gridHeight = grid.y;
        item.stats = CopyStats(plan.template);
        item.tags = new List<string> { plan.slot, TierTag };

        string path = ItemPath(plan);
        EnsureFolder(Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(item, path);
        return item;
    }

    private static List<ItemStatBonus> CopyStats(ItemDefinition template)
    {
        var stats = new List<ItemStatBonus>();
        if (template == null) return stats;

        foreach (var stat in template.stats)
            stats.Add(new ItemStatBonus { statId = stat.statId, amount = stat.amount });

        return stats;
    }

    private void Stock(List<ItemDefinition> created)
    {
        if (vendor == null) return;

        foreach (var item in created)
            vendor.stock.Add(new VendorStock { item = item, restocks = true });

        EditorUtility.SetDirty(vendor);
    }


    private ModelAppearance Appearance() => model != null ? model.GetComponent<ModelAppearance>() : null;

    // The first skinned mesh, the same body ClothingSetup binds against.
    private SkinnedMeshRenderer BodyRenderer() => model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;

    private List<ModelAppearance.Group> SliderGroups()
    {
        var appearance = Appearance();
        return appearance == null ? new List<ModelAppearance.Group>() : appearance.Groups.Where(g => g.chosenAtCreation && g.slider).ToList();
    }

    private ItemPlan FindItem(string slot) => items.Find(item => item.slot == slot);

    private List<ItemDefinition> Templates(string slot)
    {
        return subTypes.TryGetValue(slot, out var subType) ? allItems.FindAll(item => item.subType == subType) : new List<ItemDefinition>();
    }

    private List<string> MeshesIn(string slot)
    {
        return pieces.Where(p => p.include && p.slot == slot).Select(p => p.renderer.name).ToList();
    }

    private string SlotName(string slot) => subTypes.TryGetValue(slot, out var subType) && !string.IsNullOrEmpty(subType.displayName) ? subType.displayName : slot;

    private string AutoId(string displayName) => ItemDefinitionGenerator.ToItemId($"{tier} {displayName}");

    private string ItemPath(ItemPlan item)
    {
        string area = ItemDefinitionGenerator.SlotFolder.TryGetValue(item.slot, out var folderName) ? folderName : "Misc";
        string asset = $"Item_{tier}_{ItemDefinitionGenerator.ToAssetName(item.displayName)}";
        return $"{CrabSystemSettings.instance.itemFolder}/{area}/{SetAssetName}/{asset}.asset";
    }

    private static string StatsText(ItemDefinition template)
    {
        if (template == null || template.stats.Count == 0) return "Stats: none";
        return "Stats: " + string.Join("  ·  ", template.stats.Select(s => $"{s.statId} {s.amount}"));
    }

    private static List<ItemDefinition> LoadItems()
    {
        var found = new List<ItemDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) found.Add(item);
        }

        return found;
    }
}
