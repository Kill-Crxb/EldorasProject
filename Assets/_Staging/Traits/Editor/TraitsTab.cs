using System;
using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > Traits. Staging: graduates with the trait module.
//
// Every trait as a card: its icon in its border, with "Lv N" when it starts dormant. Click a card to edit it; the
// right pane shows the card large (toggle ??? to see it the way a dormant roll shows at creation), its checks, then
// its inspector. New makes a trait in the traits folder and adds it to the catalog, making the catalog if there is
// none. A trait outside the catalog can't be rolled or loaded, so it is flagged and can be added from its checks.

public class TraitsTab : IDashboardTab
{
    public string Title => "Traits";
    public DashboardGroup Group => DashboardGroup.Content;
    public int Order => 2;

    private const string Folder = "Assets/Database/Traits";
    private const int CardSize = 96;
    private const int PreviewSize = 160;

    private static readonly Color SelectedColour = new Color(0.31f, 0.55f, 0.86f);
    private static readonly Color ErrorColour = new Color(0.9f, 0.35f, 0.3f);
    private static readonly Color WarningColour = new Color(0.9f, 0.7f, 0.25f);

    private TraitCatalog catalog;
    private List<TraitDefinition> traits = new List<TraitDefinition>();
    private TraitDefinition selected;
    private string filter = "";
    private bool asDormant;

    private readonly Dictionary<TraitDefinition, VisualElement> cards = new Dictionary<TraitDefinition, VisualElement>();

    private DashboardContext context;
    private TextField newName;
    private VisualElement grid;
    private VisualElement preview;
    private VisualElement checks;
    private VisualElement entry;

    public VisualElement Build(DashboardContext dashboard)
    {
        context = dashboard;
        Load();

        grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        var gridScroll = new ScrollView { style = { flexGrow = 1 } };
        gridScroll.Add(grid);

        preview = new VisualElement { style = { alignItems = Align.Center, marginBottom = 8 } };
        checks = new VisualElement { style = { marginBottom = 8 } };
        entry = new VisualElement();
        var side = new ScrollView { style = { flexGrow = 1 } };
        side.Add(preview);
        var dormantToggle = new Toggle("Show as dormant (???)") { value = asDormant };
        dormantToggle.RegisterValueChangedCallback(e => { asDormant = e.newValue; ShowSelected(); });
        side.Add(dormantToggle);
        side.Add(checks);
        side.Add(entry);

        FillGrid();
        ShowSelected();
        return DashboardLayout.Columns(Rail(), gridScroll, side);
    }

    private void Load()
    {
        catalog = AssetFinder.LoadAll<TraitCatalog>().FirstOrDefault();
        traits = AssetFinder.LoadAll<TraitDefinition>();
    }

    private VisualElement Rail()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Traits"));

        var catalogField = new ObjectField("Catalog") { objectType = typeof(TraitCatalog), value = catalog };
        catalogField.RegisterValueChangedCallback(e => { catalog = e.newValue as TraitCatalog; FillGrid(); ShowSelected(); });
        pane.Add(catalogField);

        newName = new TextField("New trait") { value = "" };
        pane.Add(newName);
        pane.Add(new Button(CreateTrait) { text = "New" });

        var search = new ToolbarSearchField { style = { marginTop = 10, width = StyleKeyword.Auto } };
        search.RegisterValueChangedCallback(e => { filter = e.newValue.Trim(); FillGrid(); });
        pane.Add(search);
        pane.Add(new Button(Refresh) { text = "Refresh" });
        pane.Add(DashboardLayout.Note($"New traits go to {Folder}. A trait needs to be in the catalog to be rolled or loaded."));
        return pane;
    }

    private void FillGrid()
    {
        grid.Clear();
        cards.Clear();

        foreach (TraitDefinition trait in traits.Where(Matches).OrderBy(Name))
        {
            TraitDefinition shown = trait;
            var card = new Button(() => Select(shown)) { style = { width = CardSize + 12, height = CardSize + 40, marginLeft = 3, marginRight = 3, marginTop = 3, marginBottom = 3, alignItems = Align.Center } };
            card.Add(Face(trait, CardSize, false));
            card.Add(new Label(Name(trait)) { style = { unityTextAlign = TextAnchor.MiddleCenter, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap, maxWidth = CardSize + 8 } });
            card.Add(new Label(Tagline(trait)) { style = { fontSize = 10, unityTextAlign = TextAnchor.MiddleCenter, color = Problems(trait).Any(p => p.error) ? ErrorColour : new Color(0.6f, 0.6f, 0.6f) } });

            cards[trait] = card;
            Highlight(card, trait == selected);
            grid.Add(card);
        }

        if (cards.Count == 0) grid.Add(DashboardLayout.Note(traits.Count == 0 ? "No traits yet. Name one on the left and press New." : "Nothing matches the search."));
    }

    private bool Matches(TraitDefinition trait) => filter == "" || Name(trait).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || (trait.traitId ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

    private string Tagline(TraitDefinition trait)
    {
        if (catalog == null || !catalog.traits.Contains(trait)) return "not in catalog";
        return trait.revealLevel > 0 ? $"dormant, Lv {trait.revealLevel}" : "active";
    }

    // The icon with its border drawn over it. With no border sprite, a plain outline in the tint stands in.
    private static VisualElement Face(TraitDefinition trait, int size, bool hidden)
    {
        var face = new VisualElement { style = { width = size, height = size, backgroundColor = new Color(0, 0, 0, 0.25f) } };

        if (hidden)
            face.Add(new Label("???") { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleCenter, fontSize = size / 4, unityFontStyleAndWeight = FontStyle.Bold } });
        else if (trait.icon != null)
            face.Add(new Image { sprite = trait.icon, scaleMode = ScaleMode.ScaleToFit, style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 } });

        if (trait.border != null)
        {
            face.Add(new Image { sprite = trait.border, tintColor = trait.borderTint, scaleMode = ScaleMode.StretchToFill, style = { position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0 } });
            return face;
        }

        CardStyle.Outline(face, trait.borderTint, 2f);
        return face;
    }

    private void Select(TraitDefinition trait)
    {
        if (selected != null && cards.TryGetValue(selected, out var previous)) Highlight(previous, false);
        selected = trait;
        if (cards.TryGetValue(trait, out var card)) Highlight(card, true);

        entry.Clear();
        var inspector = new InspectorElement(trait);
        inspector.TrackSerializedObjectValue(new SerializedObject(trait), _ => Edited());
        entry.Add(inspector);
        ShowSelected();
    }

    private void ShowSelected()
    {
        preview.Clear();
        checks.Clear();
        if (selected == null)
        {
            preview.Add(DashboardLayout.Note("Pick a card to edit it."));
            return;
        }

        bool hidden = asDormant && selected.revealLevel > 0;
        preview.Add(Face(selected, PreviewSize, hidden));
        preview.Add(DashboardLayout.Heading(hidden ? "???" : Name(selected)));
        if (!hidden && !string.IsNullOrEmpty(selected.description)) preview.Add(new Label(selected.description) { style = { whiteSpace = WhiteSpace.Normal, maxWidth = PreviewSize * 2 } });
        if (hidden) preview.Add(DashboardLayout.Note($"Reveals at level {selected.revealLevel}."));

        checks.Add(DashboardLayout.Heading("Checks"));
        var problems = Problems(selected);
        if (problems.Count == 0) checks.Add(DashboardLayout.Note("All good."));
        foreach (var problem in problems)
            checks.Add(new Label((problem.error ? "Error: " : "Warning: ") + problem.text) { style = { color = problem.error ? ErrorColour : WarningColour, whiteSpace = WhiteSpace.Normal } });

        if (catalog != null && !catalog.traits.Contains(selected)) checks.Add(new Button(AddSelectedToCatalog) { text = "Add to catalog" });
        if (catalog != null && catalog.traits.Contains(selected)) checks.Add(new Button(RemoveSelectedFromCatalog) { text = "Remove from catalog" });
    }

    private struct Problem
    {
        public bool error;
        public string text;
    }

    private List<Problem> Problems(TraitDefinition trait)
    {
        var found = new List<Problem>();
        if (string.IsNullOrEmpty(trait.traitId)) found.Add(new Problem { error = true, text = "No trait id." });
        if (!string.IsNullOrEmpty(trait.traitId) && traits.Count(t => t.traitId == trait.traitId) > 1) found.Add(new Problem { error = true, text = $"Another trait also uses the id '{trait.traitId}'." });
        if (catalog == null) found.Add(new Problem { error = true, text = "No trait catalog. Press New to make one." });
        else if (!catalog.traits.Contains(trait)) found.Add(new Problem { error = true, text = "Not in the catalog, so it can't be rolled or loaded." });
        if (trait.rewards.Count == 0) found.Add(new Problem { text = "No rewards: the trait does nothing yet." });
        if (trait.rewards.Any(r => r == null)) found.Add(new Problem { text = "An empty reward slot." });
        if (trait.icon == null) found.Add(new Problem { text = "No icon." });
        if (string.IsNullOrEmpty(trait.description)) found.Add(new Problem { text = "No description." });
        return found;
    }

    private void CreateTrait()
    {
        string typed = newName.value.Trim();
        if (typed == "")
        {
            context.Log("Traits: type a name first.");
            return;
        }

        string id = ItemDefinitionGenerator.ToItemId(typed);
        if (traits.Any(t => t.traitId == id))
        {
            context.Log($"Traits: '{id}' is already taken.");
            return;
        }

        EnsureFolder();
        var trait = ScriptableObject.CreateInstance<TraitDefinition>();
        trait.traitId = id;
        trait.displayName = typed;
        string path = AssetDatabase.GenerateUniqueAssetPath($"{Folder}/Trait_{ItemDefinitionGenerator.ToAssetName(typed)}.asset");
        AssetDatabase.CreateAsset(trait, path);

        EnsureCatalog();
        Undo.RecordObject(catalog, "Add trait to catalog");
        catalog.traits.Add(trait);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        newName.value = "";
        context.Log($"Traits: made {path} and added it to {catalog.name}.");
        Refresh();
        Select(trait);
    }

    private void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(Folder)) return;
        AssetDatabase.CreateFolder("Assets/Database", "Traits");
    }

    private void EnsureCatalog()
    {
        if (catalog != null) return;

        catalog = ScriptableObject.CreateInstance<TraitCatalog>();
        AssetDatabase.CreateAsset(catalog, $"{Folder}/TraitCatalog.asset");
        context.Log($"Traits: made {Folder}/TraitCatalog.asset. Give it to TraitModule on Base_PC and Base_NPC.");
    }

    private void AddSelectedToCatalog()
    {
        Undo.RecordObject(catalog, "Add trait to catalog");
        catalog.traits.Add(selected);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Edited();
    }

    private void RemoveSelectedFromCatalog()
    {
        Undo.RecordObject(catalog, "Remove trait from catalog");
        catalog.traits.Remove(selected);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Edited();
    }

    private void Edited()
    {
        FillGrid();
        ShowSelected();
    }

    private void Refresh()
    {
        Load();
        if (selected == null) entry.Clear();
        FillGrid();
        ShowSelected();
    }

    private static void Highlight(VisualElement card, bool on) => CardStyle.Outline(card, on ? SelectedColour : Color.clear, on ? 2f : 0f);

    private static string Name(TraitDefinition trait) => string.IsNullOrEmpty(trait.displayName) ? trait.name : trait.displayName;
}
