using System;
using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using CrabSystem.Factions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > NPC Setup. Staging: graduates with WizardTab.
//
// Every NPC is data on one base prefab (Base_NPC): an NPCArchetype says who it is, and the base swaps model, looks and
// gear by id, so a base change never leaves NPC prefabs to fix.
//   Who:   named (one of a kind, placed in a scene with a fixed entity id so quests and dialogue find it) or a spawnable
//          generic (a spawn config any NPCSpawner can use); name, ids, faction, base prefab, and an existing archetype
//          to copy abilities, AI goals, perception and patrol from.
//   Body:  model and creation looks (hair, hair colour, body sliders). Named picks one of each; a generic ticks pools
//          and every spawn picks from them.
//   Gear:  one pick (named) or a pool (generic) per equipment slot, or a whole armour set at once by its look.
//   Stats: dice per stat with a gain per level. A generic gets a StatPackage rolled on every spawn; a named NPC is
//          rolled once here and keeps the numbers, since nothing saves an NPC between sessions yet.
// Writes go to Database/Characters/NPCs/<Name>/.
// A named NPC can be saved as a Base_NPC prefab variant, and is placed from it. Either way it carries a copy of its
// model (a child named Model under Model_System) so it shows while editing; at runtime the archetype's model replaces it.
// Refresh placed NPCs rebuilds those copies from their archetypes after an archetype changes.
// The stage above the steps shows the NPC as picked; for a generic it shows one spawn's picks, and Reroll draws again.

public class NpcSetupTab : WizardTab, IDisposable
{
    public override string Title => "NPC Setup";
    public override int Order => 6;

    protected override string[] StepNames => new[] { "Who", "Body", "Gear", "Stats" };
    protected override string WriteLabel => "Create";
    protected override string RailNote => "Named NPCs get exactly what you pick. Generics pick from what you tick, every spawn.";

    private const string OutputRoot = "Assets/Database/Characters/NPCs";
    private const string BasePrefabName = "Base_NPC";
    private const string Nothing = "(nothing)";
    private const string NoChoice = "(none)";
    private const string CoreStatPrefix = "core.";
    private const float StageHeight = 340f;
    private const string BakedModelName = "Model";

    private enum Kind { Named, Generic }

    private Kind kind = Kind.Generic;
    private string displayName = "";
    private string archetypeId = "";
    private string entityId = "";
    private FactionDefinition faction;
    private NPCType npcType;
    private NPCImportance importance = NPCImportance.Soldier;
    private GameObject basePrefab;
    private NPCArchetype behaviour;
    private bool placeInScene = true;
    private bool saveVariant = true;
    private int level = 1;

    private readonly List<string> models = new List<string>();
    private readonly Dictionary<string, List<string>> looks = new Dictionary<string, List<string>>();
    private readonly Dictionary<string, List<string>> gear = new Dictionary<string, List<string>>();
    private readonly HashSet<string> canBeEmpty = new HashSet<string>();
    private List<StatPackage.StatRoll> statRows = new List<StatPackage.StatRoll>();
    private StatBaseOverride[] namedRoll;

    private ModelDatabase database;
    private List<ItemDefinition> allItems = new List<ItemDefinition>();
    private List<EquipmentSlotDefinition> slots = new List<EquipmentSlotDefinition>();
    private List<NPCArchetype> archetypes = new List<NPCArchetype>();
    private List<StatPackage> packages = new List<StatPackage>();

    private CharacterStage stage;
    private Button reroll;
    private int seed;
    private string shownKey;

    private string AssetName => ItemDefinitionGenerator.ToAssetName(displayName);
    private string Folder => $"{OutputRoot}/{AssetName}";
    private string ArchetypePath => $"{Folder}/Archetype_{AssetName}.asset";
    private string PackagePath => $"{Folder}/StatPackage_{AssetName}.asset";
    private string SpawnConfigPath => $"{Folder}/SpawnConfig_{AssetName}.asset";
    private string VariantPath => $"{Folder}/NPC_{AssetName}.prefab";
    private bool Named => kind == Kind.Named;

    protected override void OnBuild()
    {
        Load();
        statRows = DefaultRows();
    }

    protected override VisualElement Stage()
    {
        stage = new CharacterStage(StageHeight);
        reroll = new Button(() => { seed++; RefreshChecks(); }) { text = "Reroll" };
        stage.Toolbar.Add(reroll);
        return stage.Element;
    }

    public void Dispose()
    {
        if (stage != null) stage.Dispose();
    }

    protected override VisualElement StepContent(int step)
    {
        if (step == 0) return WhoStep();
        if (step == 1) return BodyStep();
        return step == 2 ? GearStep() : StatsStep();
    }

    private void Load()
    {
        database = AssetFinder.LoadAll<ModelDatabase>().FirstOrDefault();
        allItems = AssetFinder.LoadAll<ItemDefinition>();
        slots = AssetFinder.LoadAll<EquipmentSlotDefinition>().OrderBy(slot => slot.displayOrder).ToList();
        archetypes = AssetFinder.LoadAll<NPCArchetype>().OrderBy(a => a.name).ToList();
        packages = AssetFinder.LoadAll<StatPackage>().OrderBy(p => p.name).ToList();
        if (basePrefab == null) basePrefab = AssetFinder.LoadAll<GameObject>($"{BasePrefabName} t:Prefab").FirstOrDefault(p => p.name == BasePrefabName);
    }

    // A start that tunes on: core stats from 3 plus a d4, about +1 every two levels, capped at 20 (Core_Foundations).
    private static List<StatPackage.StatRoll> DefaultRows()
    {
        var rows = new List<StatPackage.StatRoll>();
        foreach (string statId in IdSources.Get(IdKind.Stat))
            if (statId.StartsWith(CoreStatPrefix))
                rows.Add(new StatPackage.StatRoll { statId = statId, flat = 3f, dice = DiceRoll.D4(), perLevel = 0.4f, max = 20f });

        return rows;
    }

    private VisualElement WhoStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Who"));
        pane.Add(DashboardLayout.Note("Named: one of a kind, placed in the open scene with a fixed entity id so quests and dialogue can find it. Generic: a spawn config for NPCSpawners; every spawn rolls its own looks, gear and stats."));

        var kindField = new EnumField("Kind", kind);
        kindField.RegisterValueChangedCallback(e => SetKind((Kind)e.newValue));
        pane.Add(kindField);

        var idField = new TextField("Archetype id") { value = archetypeId, isDelayed = true };
        idField.RegisterValueChangedCallback(e => { archetypeId = e.newValue.Trim(); RefreshChecks(); });
        var entityField = new TextField("Entity id") { value = entityId, isDelayed = true };
        entityField.RegisterValueChangedCallback(e => { entityId = e.newValue.Trim(); RefreshChecks(); });

        var nameField = new TextField("Name") { value = displayName, isDelayed = true };
        nameField.RegisterValueChangedCallback(e => Rename(e.newValue, idField, entityField));
        pane.Add(nameField);
        pane.Add(idField);
        if (Named) pane.Add(entityField);

        pane.Add(Field(new ObjectField("Faction") { objectType = typeof(FactionDefinition), allowSceneObjects = false, value = faction }, v => faction = v as FactionDefinition));

        var typeField = new EnumField("Type", npcType);
        typeField.RegisterValueChangedCallback(e => npcType = (NPCType)e.newValue);
        pane.Add(typeField);

        var importanceField = new EnumField("Importance", importance);
        importanceField.RegisterValueChangedCallback(e => importance = (NPCImportance)e.newValue);
        pane.Add(importanceField);

        pane.Add(Field(new ObjectField("Base prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = basePrefab }, v => basePrefab = v as GameObject));
        pane.Add(BehaviourPopup());

        if (!Named) return pane;

        var variant = new Toggle("Save as prefab variant") { value = saveVariant };
        variant.RegisterValueChangedCallback(e => { saveVariant = e.newValue; RefreshChecks(); });
        pane.Add(variant);

        var place = new Toggle("Place in open scene") { value = placeInScene };
        place.RegisterValueChangedCallback(e => { placeInScene = e.newValue; RefreshChecks(); });
        pane.Add(place);
        pane.Add(new Button(RefreshPlaced) { text = "Refresh placed NPCs" });
        return pane;
    }

    // Ids follow the name until one is typed by hand.
    private void Rename(string newName, TextField idField, TextField entityField)
    {
        bool autoArchetype = archetypeId == AutoId(displayName);
        bool autoEntity = entityId == AutoId(displayName);
        displayName = newName.Trim();
        if (autoArchetype) archetypeId = AutoId(displayName);
        if (autoEntity) entityId = AutoId(displayName);

        idField.SetValueWithoutNotify(archetypeId);
        entityField.SetValueWithoutNotify(entityId);
        RefreshChecks();
    }

    private static string AutoId(string name) => name == "" ? "" : "npc_" + ItemDefinitionGenerator.ToItemId(name);

    // Turning a generic into a named NPC keeps only the first pick of every pool.
    private void SetKind(Kind newKind)
    {
        kind = newKind;
        importance = Named ? NPCImportance.Hero : NPCImportance.Soldier;
        if (Named) TrimPools();

        ShowStep(0);
    }

    private void TrimPools()
    {
        if (models.Count > 1) models.RemoveRange(1, models.Count - 1);
        foreach (var picks in looks.Values.Concat(gear.Values))
            if (picks.Count > 1) picks.RemoveRange(1, picks.Count - 1);

        canBeEmpty.Clear();
    }

    private PopupField<string> BehaviourPopup()
    {
        var names = new List<string> { NoChoice };
        names.AddRange(archetypes.Select(a => a.name));

        var popup = new PopupField<string>("Copy behaviour from", names, behaviour != null ? behaviour.name : NoChoice);
        popup.tooltip = "Abilities, AI goals, combat personality, perception and patrol come from this archetype.";
        popup.RegisterValueChangedCallback(e => { behaviour = archetypes.Find(a => a.name == e.newValue); RefreshChecks(); });
        return popup;
    }

    private VisualElement BodyStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Model"));
        if (database == null)
        {
            pane.Add(DashboardLayout.Note("No ModelDatabase asset found; the NPC wears the base prefab's model."));
            return pane;
        }

        var ids = database.AllModels.Where(m => m.IsValid()).Select(m => m.modelId).ToList();
        pane.Add(Named ? SinglePick("Model", ids, models, null) : PoolToggles(ids, models, null));

        var appearance = FirstAppearance();
        pane.Add(DashboardLayout.Heading("Looks"));
        if (appearance == null)
        {
            pane.Add(DashboardLayout.Note("Pick a model with a ModelAppearance to choose its looks."));
            return pane;
        }

        if (!Named) pane.Add(new Button(() => { TickAllLooks(appearance); ShowStep(1); }) { text = "Random everything" });
        foreach (var group in appearance.Groups.Where(g => g.chosenAtCreation))
            pane.Add(LookRow(group));

        return pane;
    }

    private VisualElement LookRow(ModelAppearance.Group group)
    {
        var picks = Picks(looks, group.groupId);
        var options = group.options.Select(o => o.optionId).ToList();
        string label = string.IsNullOrEmpty(group.displayName) ? group.groupId : group.displayName;
        if (Named) return SinglePick(label, options, picks, null);

        var box = new VisualElement { style = { marginBottom = 6 } };
        box.Add(new Label(label) { style = { unityFontStyleAndWeight = FontStyle.Bold } });
        box.Add(PoolToggles(options, picks, null));
        return box;
    }

    private void TickAllLooks(ModelAppearance appearance)
    {
        foreach (var group in appearance.Groups.Where(g => g.chosenAtCreation))
        {
            var picks = Picks(looks, group.groupId);
            picks.Clear();
            picks.AddRange(group.options.Select(o => o.optionId));
        }
    }

    private VisualElement GearStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Gear"));
        pane.Add(DashboardLayout.Note(Named ? "One piece per slot." : "Tick the pieces each slot picks from. Can be empty adds wearing nothing there as one more choice."));
        pane.Add(SetRow());

        foreach (var slot in slots)
        {
            var fits = ItemsFor(slot);
            if (fits.Count > 0) pane.Add(SlotRow(slot, fits));
        }

        return pane;
    }

    // Batch: every item drawing one look (an armour set made in Item Setup) goes into its slot at once.
    private VisualElement SetRow()
    {
        var sets = allItems.Where(i => !string.IsNullOrEmpty(i.appearanceOption)).Select(i => i.appearanceOption).Distinct().OrderBy(s => s).ToList();
        var row = Row();
        if (sets.Count == 0) return row;

        var popup = new PopupField<string>("Wear a set", sets, sets[0]);
        row.Add(popup);
        row.Add(new Button(() => { AddSet(popup.value); ShowStep(2); }) { text = "Add" });
        return row;
    }

    private void AddSet(string option)
    {
        foreach (var item in allItems.Where(i => i.appearanceOption == option))
        {
            var slot = SlotOf(item);
            if (slot == null) continue;

            var picks = Picks(gear, slot.slotId);
            if (Named) picks.Clear();
            if (!picks.Contains(item.itemId)) picks.Add(item.itemId);
        }
    }

    private VisualElement SlotRow(EquipmentSlotDefinition slot, List<ItemDefinition> fits)
    {
        var picks = Picks(gear, slot.slotId);
        var ids = fits.Select(i => i.itemId).ToList();
        string label = string.IsNullOrEmpty(slot.displayName) ? slot.slotId : slot.displayName;
        Func<string, string> name = id => ItemName(id);
        if (Named) return SinglePick(label, ids, picks, name);

        var box = new VisualElement { style = { marginBottom = 6 } };
        var header = Row();
        header.Add(new Label(label) { style = { unityFontStyleAndWeight = FontStyle.Bold, width = 140 } });
        var empty = new Toggle("Can be empty") { value = canBeEmpty.Contains(slot.slotId) };
        empty.RegisterValueChangedCallback(e => { if (e.newValue) canBeEmpty.Add(slot.slotId); else canBeEmpty.Remove(slot.slotId); });
        header.Add(empty);
        box.Add(header);
        box.Add(PoolToggles(ids, picks, name));
        return box;
    }

    private VisualElement StatsStep()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Stats"));
        pane.Add(DashboardLayout.Note("Each stat is flat + dice + a gain for every level above 1, floored, then capped (0 = no cap). Stats with no row come from the stat schema."));

        var levelField = new IntegerField(Named ? "Level" : "Spawn level") { value = level, isDelayed = true };
        levelField.RegisterValueChangedCallback(e => { level = Mathf.Max(1, e.newValue); RefreshChecks(); });
        pane.Add(levelField);
        pane.Add(LoadRowsPopup());

        var rows = new VisualElement();
        FillRows(rows);
        pane.Add(rows);

        var result = DashboardLayout.Note(Named ? RolledText(namedRoll) : "");
        var roll = new Button(() => RollPressed(result)) { text = Named ? "Roll (kept)" : "Sample a spawn" };
        pane.Add(roll);
        pane.Add(result);
        return pane;
    }

    private void RollPressed(Label result)
    {
        var rolled = RollRows();
        if (Named) namedRoll = rolled;

        result.text = RolledText(rolled);
        RefreshChecks();
    }

    private PopupField<string> LoadRowsPopup()
    {
        var names = new List<string> { NoChoice };
        names.AddRange(packages.Select(p => p.name));

        var popup = new PopupField<string>("Load rows from", names, NoChoice);
        popup.RegisterValueChangedCallback(e => LoadRows(packages.Find(p => p.name == e.newValue)));
        return popup;
    }

    private void LoadRows(StatPackage package)
    {
        if (package == null) return;

        statRows = package.stats.Select(CopyRow).ToList();
        namedRoll = null;
        ShowStep(3);
    }

    private void FillRows(VisualElement box)
    {
        box.Clear();
        var statIds = IdSources.Get(IdKind.Stat).ToList();
        for (int i = 0; i < statRows.Count; i++)
            box.Add(StatRow(box, statRows[i], statIds));

        box.Add(new Button(() => { statRows.Add(new StatPackage.StatRoll { dice = DiceRoll.D4() }); FillRows(box); RefreshChecks(); }) { text = "Add stat" });
    }

    private VisualElement StatRow(VisualElement box, StatPackage.StatRoll row, List<string> statIds)
    {
        var line = Row();
        string shown = statIds.Contains(row.statId) ? row.statId : NoChoice;
        var stat = new PopupField<string>(new List<string> { NoChoice }.Concat(statIds).ToList(), shown) { style = { width = 160 } };
        stat.RegisterValueChangedCallback(e => { row.statId = e.newValue == NoChoice ? "" : e.newValue; RefreshChecks(); });
        line.Add(stat);

        line.Add(Number("flat", row.flat, v => row.flat = v));
        line.Add(Whole("dice", row.dice.diceCount, v => row.dice.diceCount = v));
        line.Add(Whole("d", row.dice.diceFaces, v => row.dice.diceFaces = v));
        line.Add(Number("per level", row.perLevel, v => row.perLevel = v));
        line.Add(Number("max", row.max, v => row.max = v));
        line.Add(new Button(() => { statRows.Remove(row); FillRows(box); RefreshChecks(); }) { text = "✕" });
        return line;
    }

    private FloatField Number(string label, float value, Action<float> set)
    {
        var field = new FloatField(label) { value = value, style = { width = 110 } };
        field.labelElement.style.minWidth = 0;
        field.RegisterValueChangedCallback(e => { set(e.newValue); RefreshChecks(); });
        return field;
    }

    private IntegerField Whole(string label, int value, Action<int> set)
    {
        var field = new IntegerField(label) { value = value, style = { width = 70 } };
        field.labelElement.style.minWidth = 0;
        field.RegisterValueChangedCallback(e => { set(e.newValue); RefreshChecks(); });
        return field;
    }

    // Rolled through a throwaway package, so the wizard and a spawn use the same sum.
    private StatBaseOverride[] RollRows()
    {
        var package = ScriptableObject.CreateInstance<StatPackage>();
        package.stats = statRows.Select(CopyRow).ToList();
        var rolled = package.Roll(level);
        UnityEngine.Object.DestroyImmediate(package);
        return rolled;
    }

    private static string RolledText(StatBaseOverride[] rolled)
    {
        if (rolled == null) return "Not rolled yet.";
        return string.Join("  ·  ", rolled.Select(s => $"{s.statId} {s.baseValue}"));
    }

    private static StatPackage.StatRoll CopyRow(StatPackage.StatRoll row)
    {
        return new StatPackage.StatRoll { statId = row.statId, flat = row.flat, dice = row.dice, perLevel = row.perLevel, max = row.max };
    }

    private VisualElement SinglePick(string label, List<string> choices, List<string> picks, Func<string, string> name)
    {
        var options = new List<string> { Nothing };
        options.AddRange(choices);
        string shown = picks.Count > 0 && choices.Contains(picks[0]) ? picks[0] : Nothing;

        Func<string, string> format = id => id == Nothing || name == null ? id : name(id);
        var popup = new PopupField<string>(label, options, shown, format, format);
        popup.RegisterValueChangedCallback(e =>
        {
            picks.Clear();
            if (e.newValue != Nothing) picks.Add(e.newValue);
            RefreshChecks();
        });
        return popup;
    }

    private VisualElement PoolToggles(List<string> choices, List<string> picks, Func<string, string> name)
    {
        var row = Row();
        row.style.marginLeft = 12;
        foreach (string choice in choices)
        {
            string id = choice;
            var toggle = new Toggle(name != null ? name(id) : id) { value = picks.Contains(id) };
            toggle.labelElement.style.minWidth = 0;
            toggle.style.marginRight = 10;
            toggle.RegisterValueChangedCallback(e => { picks.Remove(id); if (e.newValue) picks.Add(id); RefreshChecks(); });
            row.Add(toggle);
        }

        return row;
    }

    private ObjectField Field(ObjectField field, Action<UnityEngine.Object> set)
    {
        field.RegisterValueChangedCallback(e => { set(e.newValue); RefreshChecks(); });
        return field;
    }

    protected override void CollectIssues(List<WizardIssue> issues)
    {
        if (displayName == "") Error(issues, "Name the NPC (Who).");
        if (archetypeId == "") Error(issues, "It needs an archetype id (Who).");
        if (archetypeId != "" && IdSources.Exists(IdKind.Archetype, archetypeId)) Error(issues, $"Archetype id {archetypeId} is already used.");
        if (displayName != "" && AssetDatabase.IsValidFolder(Folder)) Error(issues, $"{Folder} already exists. Rename the NPC or move that folder.");
        BaseIssues(issues);
        if (Named) NamedIssues(issues);

        if (models.Count == 0) Warn(issues, "No model picked; it wears the base prefab's own.");
        if (behaviour == null) Warn(issues, "No behaviour copied: no abilities or AI goals, so it will stand still.");
        foreach (var row in statRows)
            RowIssues(issues, row);

        GearIssues(issues);
    }

    private void BaseIssues(List<WizardIssue> issues)
    {
        if (basePrefab == null) Error(issues, $"Pick a base prefab ({BasePrefabName}).");
        else if (basePrefab.GetComponentInChildren<ControllerBrain>(true) == null) Error(issues, $"{basePrefab.name} has no ControllerBrain.");
    }

    private void NamedIssues(List<WizardIssue> issues)
    {
        if (entityId == "") Error(issues, "A named NPC needs an entity id (Who).");
        if (entityId != "" && EntityIdsInScene().Contains(entityId)) Error(issues, $"Entity id {entityId} is already used in the open scene.");
        if (statRows.Count > 0 && namedRoll == null) Error(issues, "Roll the named NPC's stats (Stats).");
        if (!placeInScene && !saveVariant) Warn(issues, "Not placed or saved: add a PersistentNPCConfigurator with this archetype and entity id where it should stand.");
    }

    private static void RowIssues(List<WizardIssue> issues, StatPackage.StatRoll row)
    {
        if (string.IsNullOrEmpty(row.statId)) Error(issues, "A stat row has no stat.");
        else if (!IdSources.Exists(IdKind.Stat, row.statId)) Error(issues, $"No stat {row.statId} in the schema.");
        if (row.dice.diceCount > 0 && row.dice.diceFaces < 2) Error(issues, $"{row.statId}: dice need at least 2 faces.");
    }

    private void GearIssues(List<WizardIssue> issues)
    {
        var onBase = BaseSlotIds();
        foreach (var pair in gear)
            if (pair.Value.Count > 0 && onBase != null && !onBase.Contains(pair.Key))
                Warn(issues, $"{basePrefab.name} has no {pair.Key} slot, so those pieces won't go on.");
    }

    // The slots the base prefab's EquipmentSystem lists; null when it has none to read.
    private HashSet<string> BaseSlotIds()
    {
        var equipment = basePrefab != null ? basePrefab.GetComponentInChildren<EquipmentSystem>(true) : null;
        if (equipment == null) return null;

        var list = new SerializedObject(equipment).FindProperty("slotDefinitions");
        var ids = new HashSet<string>();
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue is EquipmentSlotDefinition slot) ids.Add(slot.slotId);

        return ids;
    }

    private static HashSet<string> EntityIdsInScene()
    {
        var ids = new HashSet<string>();
        foreach (var configurator in UnityEngine.Object.FindObjectsByType<PersistentNPCConfigurator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ids.Add(new SerializedObject(configurator).FindProperty("entityId").stringValue);

        return ids;
    }

    protected override List<string> PlanLines()
    {
        var lines = new List<string>();
        if (displayName == "") return lines;

        if (!Named) lines.Add($"Create {PackagePath}, rolled every spawn at level {level}.");
        lines.Add($"Create {ArchetypePath}{(behaviour != null ? $", behaviour from {behaviour.name}" : "")}.");
        if (!Named) lines.Add($"Create {SpawnConfigPath} on {(basePrefab != null ? basePrefab.name : "?")}.");
        if (Named && saveVariant) lines.Add($"Save {VariantPath}, a {(basePrefab != null ? basePrefab.name : "?")} variant wearing its model.");
        if (Named && placeInScene) lines.Add($"Place {displayName} ({entityId}) at the scene view's centre, level {level}.");
        return lines;
    }

    protected override void Write()
    {
        EnsureFolder(Folder);
        var package = Named ? null : CreatePackage();
        var archetype = CreateArchetype(package);
        if (!Named) CreateSpawnConfig(archetype);
        if (Named) Place(archetype);

        AssetDatabase.SaveAssets();
        IdSources.Invalidate();
        Context.Log($"NPC Setup: {displayName} written to {Folder}.");
        Reset();
    }

    private StatPackage CreatePackage()
    {
        var package = ScriptableObject.CreateInstance<StatPackage>();
        package.packageId = archetypeId;
        package.stats = statRows.Select(CopyRow).ToList();
        AssetDatabase.CreateAsset(package, PackagePath);
        return package;
    }

    // A copy of the behaviour archetype keeps its abilities, goals, perception and patrol; everything the wizard
    // decides is written over it.
    private NPCArchetype CreateArchetype(StatPackage package)
    {
        var archetype = behaviour != null ? UnityEngine.Object.Instantiate(behaviour) : ScriptableObject.CreateInstance<NPCArchetype>();
        archetype.archetypeId = archetypeId;
        archetype.archetypeName = displayName;
        archetype.faction = faction;
        archetype.npcType = npcType;
        archetype.importance = importance;
        archetype.baseLevel = level;
        archetype.baseStatOverrides = Named && namedRoll != null ? namedRoll : new StatBaseOverride[0];
        archetype.statPackage = package;
        archetype.modelPool = new List<string>(models);
        archetype.randomizeModel = models.Count > 1;
        archetype.looks = looks.Where(p => p.Value.Count > 0).Select(p => new LookPool { groupId = p.Key, optionIds = new List<string>(p.Value) }).ToList();
        archetype.gear = gear.Where(p => p.Value.Count > 0).Select(p => new GearPool { itemIds = new List<string>(p.Value), canBeEmpty = canBeEmpty.Contains(p.Key) }).ToList();
        AssetDatabase.CreateAsset(archetype, ArchetypePath);
        return archetype;
    }

    private void CreateSpawnConfig(NPCArchetype archetype)
    {
        var config = ScriptableObject.CreateInstance<NPCSpawnConfig>();
        config.spawnConfigId = archetypeId;
        config.displayName = displayName;
        config.prefab = basePrefab;
        config.archetype = archetype;
        config.spawnLevel = level;
        AssetDatabase.CreateAsset(config, SpawnConfigPath);
    }

    // Built at the origin and saved first, so the variant's root keeps the base's position.
    private void Place(NPCArchetype archetype)
    {
        if (!saveVariant && !placeInScene) return;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        instance.name = displayName;
        Configure(instance, archetype);
        Bake(instance, archetype.Roll(level));

        if (saveVariant && placeInScene) PrefabUtility.SaveAsPrefabAssetAndConnect(instance, VariantPath, InteractionMode.AutomatedAction);
        if (saveVariant && !placeInScene) PrefabUtility.SaveAsPrefabAsset(instance, VariantPath);
        if (!placeInScene)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Place NPC");
        if (SceneView.lastActiveSceneView != null) instance.transform.position = SceneView.lastActiveSceneView.pivot;
        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
    }

    // The brain sits on a child (Component_Brain); it is set as well, for a CrabSystem from before the configurator
    // looked below itself.
    private void Configure(GameObject instance, NPCArchetype archetype)
    {
        var configurator = instance.GetComponentInChildren<PersistentNPCConfigurator>(true) ?? instance.AddComponent<PersistentNPCConfigurator>();
        var form = new SerializedObject(configurator);
        form.FindProperty("brain").objectReferenceValue = instance.GetComponentInChildren<ControllerBrain>(true);
        form.FindProperty("entityId").stringValue = entityId;
        form.FindProperty("archetype").objectReferenceValue = archetype;
        form.FindProperty("level").intValue = level;
        form.ApplyModifiedPropertiesWithoutUndo();
    }

    // The copy shown while editing: the model the archetype rolls, dressed as it will be (ModelModule finds a child
    // named Model and replaces it at runtime). A named archetype lists one choice per pool, so the roll is its picks.
    private void Bake(GameObject root, CharacterConfigData data)
    {
        var module = root.GetComponentInChildren<ModelModule>(true);
        if (module == null) return;

        var old = module.transform.Find(BakedModelName);
        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

        var prefab = CharacterDresser.ModelPrefab(database, data.modelId);
        if (prefab == null) return;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, module.transform);
        model.name = BakedModelName;
        CharacterDresser.Dress(model, data.appearance, CharacterDresser.Items(allItems, data.equipmentItemIds));
    }

    // A placed variant's copy lives in the variant, so the variant is rebuilt; any other placed NPC is rebuilt in the
    // scene. Not undoable.
    private void RefreshPlaced()
    {
        Load();
        int count = 0;
        foreach (var configurator in UnityEngine.Object.FindObjectsByType<PersistentNPCConfigurator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Rebake(configurator)) count++;

        Context.Log($"NPC Setup: refreshed {count} placed NPC model(s).");
    }

    private bool Rebake(PersistentNPCConfigurator configurator)
    {
        var form = new SerializedObject(configurator);
        var archetype = form.FindProperty("archetype").objectReferenceValue as NPCArchetype;
        if (archetype == null) return false;

        var data = archetype.Roll(form.FindProperty("level").intValue);
        var module = configurator.GetComponentInChildren<ModelModule>(true);
        var old = module != null ? module.transform.Find(BakedModelName) : null;
        bool inVariant = old != null && PrefabUtility.IsPartOfPrefabInstance(old) && !PrefabUtility.IsAddedGameObjectOverride(old.gameObject);
        if (!inVariant)
        {
            Bake(configurator.gameObject, data);
            EditorSceneManager.MarkSceneDirty(configurator.gameObject.scene);
            return true;
        }

        string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(configurator.gameObject);
        var contents = PrefabUtility.LoadPrefabContents(path);
        Bake(contents, data);
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return true;
    }

    private void Reset()
    {
        displayName = "";
        archetypeId = "";
        entityId = "";
        models.Clear();
        looks.Clear();
        gear.Clear();
        canBeEmpty.Clear();
        namedRoll = null;
        Load();
        ShowStep(0);
    }

    // Drawn with a fixed seed, so a refresh redraws the same spawn until Reroll; only a change in what it draws rebuilds.
    protected override void OnRefresh()
    {
        reroll.SetEnabled(!Named);
        var random = new System.Random(seed);
        string modelId = Sample(models, random, false);
        var choices = SampleLooks(random);
        var wear = SampleGear(random);

        string key = $"{modelId}|{string.Join(",", choices.Select(c => c.groupId + "=" + c.optionId))}|{string.Join(",", wear.Select(i => i.itemId))}";
        if (key == shownKey) return;

        shownKey = key;
        stage.Show(CharacterDresser.ModelPrefab(database, modelId), choices, wear);
    }

    private List<AppearanceChoice> SampleLooks(System.Random random)
    {
        var choices = new List<AppearanceChoice>();
        foreach (var pair in looks)
        {
            string optionId = Sample(pair.Value, random, false);
            if (optionId != null) choices.Add(new AppearanceChoice { groupId = pair.Key, optionId = optionId });
        }

        return choices;
    }

    private List<ItemDefinition> SampleGear(System.Random random)
    {
        var wear = new List<ItemDefinition>();
        foreach (var pair in gear)
        {
            string itemId = Sample(pair.Value, random, canBeEmpty.Contains(pair.Key));
            var item = allItems.Find(i => i.itemId == itemId);
            if (item != null) wear.Add(item);
        }

        return wear;
    }

    // As a spawn draws (NPCArchetype): can be empty is one more choice.
    private static string Sample(List<string> pool, System.Random random, bool canBeEmpty)
    {
        int choices = pool.Count + (canBeEmpty ? 1 : 0);
        if (choices == 0) return null;

        int index = random.Next(choices);
        return index < pool.Count ? pool[index] : null;
    }

    private ModelAppearance FirstAppearance()
    {
        var prefab = models.Count > 0 ? CharacterDresser.ModelPrefab(database, models[0]) : null;
        return prefab != null ? prefab.GetComponent<ModelAppearance>() : null;
    }

    private List<ItemDefinition> ItemsFor(EquipmentSlotDefinition slot) => allItems.Where(i => SlotOf(i) == slot).OrderBy(i => i.displayName).ToList();

    private static EquipmentSlotDefinition SlotOf(ItemDefinition item) => item.subType != null ? item.subType.equipmentSlot : null;

    private string ItemName(string itemId)
    {
        var item = allItems.Find(i => i.itemId == itemId);
        return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : itemId;
    }

    private static List<string> Picks(Dictionary<string, List<string>> pools, string key)
    {
        if (!pools.TryGetValue(key, out var picks)) pools[key] = picks = new List<string>();
        return picks;
    }
}
