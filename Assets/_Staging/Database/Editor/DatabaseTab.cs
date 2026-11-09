using System;
using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Dashboard > Content > Database. Staging: graduates with WizardTab.
//
// The game's content as cards: pick a category, filter by name, click a card to edit it. The right pane shows the entry
// on the character stage above its own inspector, and an edit redraws both the stage and the card.
//   NPCs:  every NPCArchetype, in the model, looks and gear it rolls. A generic shows one spawn; Reroll draws again.
//   Items: every ItemDefinition, by equipment slot. The card is its icon. The stage shows its look on the first model
//          that has that look's group, or else its equipped prefab.
// Cards render one per tick and are kept until Refresh. Unlike the standard layout, the entry stays beside the cards at
// any window width and shares the room with them equally past a slim category rail.

public class DatabaseTab : IDashboardTab, IDisposable
{
    public string Title => "Database";
    public DashboardGroup Group => DashboardGroup.Content;
    public int Order => 1;

    private enum Category { NPCs, Items }

    private class Card
    {
        public VisualElement element;
        public Image image;
    }

    private const int CardSize = 120;
    private const int ThumbnailSize = 256;
    private const float StageHeight = 300f;
    private const float RailWidth = 160f;
    private const int RenderEveryMs = 30;
    private const string NoSlot = "Other";

    private static readonly Color SelectedColour = new Color(0.31f, 0.55f, 0.86f);

    private Category category = Category.NPCs;
    private string filter = "";
    private UnityEngine.Object selected;

    private ModelDatabase database;
    private List<NPCArchetype> archetypes = new List<NPCArchetype>();
    private List<ItemDefinition> items = new List<ItemDefinition>();

    private readonly Dictionary<UnityEngine.Object, Texture2D> thumbnails = new Dictionary<UnityEngine.Object, Texture2D>();
    private readonly Dictionary<UnityEngine.Object, Card> cards = new Dictionary<UnityEngine.Object, Card>();
    private readonly Queue<UnityEngine.Object> toRender = new Queue<UnityEngine.Object>();
    private readonly List<Button> categoryButtons = new List<Button>();

    private CharacterStage stage;
    private CharacterStage snapper;
    private Button reroll;
    private VisualElement grid;
    private VisualElement entry;

    public VisualElement Build(DashboardContext context)
    {
        Load();
        stage = new CharacterStage(StageHeight);
        snapper = new CharacterStage(ThumbnailSize);
        reroll = new Button(ShowSelected) { text = "Reroll" };
        stage.Toolbar.Add(reroll);

        grid = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
        var gridScroll = new ScrollView { style = { flexGrow = 1 } };
        gridScroll.Add(grid);

        entry = new VisualElement();
        var entryScroll = new ScrollView { style = { flexGrow = 1 } };
        entryScroll.Add(stage.Element);
        entryScroll.Add(entry);

        var layout = DashboardLayout.Columns(Categories(), gridScroll, entryScroll);
        Widen(layout);
        layout.schedule.Execute(RenderNext).Every(RenderEveryMs);
        ShowCategory(category);
        ShowSelected();
        return layout;
    }

    public void Dispose()
    {
        ClearThumbnails();
        if (stage != null) stage.Dispose();
        if (snapper != null) snapper.Dispose();
    }

    // Inline styles win over Dashboard.uss, including its .narrow rule that drops the third pane underneath.
    private static void Widen(VisualElement layout)
    {
        layout.Q(className: "pane-list").style.width = RailWidth;
        layout.Q(className: "columns-main").style.flexDirection = FlexDirection.Row;

        var side = layout.Q(className: "pane-checks").style;
        side.width = StyleKeyword.Auto;
        side.height = StyleKeyword.Auto;
        side.flexGrow = 1;
        side.flexShrink = 1;
        side.flexBasis = 0f;
    }

    private void Load()
    {
        database = AssetFinder.LoadAll<ModelDatabase>().FirstOrDefault();
        archetypes = AssetFinder.LoadAll<NPCArchetype>();
        items = AssetFinder.LoadAll<ItemDefinition>();
    }

    private VisualElement Categories()
    {
        var pane = new VisualElement();
        pane.Add(DashboardLayout.Heading("Categories"));
        foreach (Category value in Enum.GetValues(typeof(Category)))
        {
            Category shown = value;
            var button = new Button(() => ShowCategory(shown)) { text = value.ToString() };
            button.AddToClassList("rail-tab");
            categoryButtons.Add(button);
            pane.Add(button);
        }

        var search = new ToolbarSearchField { style = { marginTop = 10, width = StyleKeyword.Auto } };
        search.RegisterValueChangedCallback(e => { filter = e.newValue.Trim(); FillGrid(); });
        pane.Add(search);
        pane.Add(new Button(Refresh) { text = "Refresh" });
        pane.Add(DashboardLayout.Note("Click a card to edit it. Refresh after adding or deleting assets."));
        return pane;
    }

    private void ShowCategory(Category shown)
    {
        category = shown;
        for (int i = 0; i < categoryButtons.Count; i++)
            categoryButtons[i].EnableInClassList("selected", i == (int)category);

        FillGrid();
    }

    private void FillGrid()
    {
        grid.Clear();
        cards.Clear();
        toRender.Clear();

        if (category == Category.NPCs)
        {
            foreach (var archetype in Matching(archetypes))
                grid.Add(MakeCard(archetype));
            return;
        }

        foreach (var slot in Matching(items).GroupBy(SlotName).OrderBy(g => g.Key == NoSlot).ThenBy(g => g.Key))
        {
            grid.Add(new Label(slot.Key) { style = { width = Length.Percent(100), unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });
            foreach (var item in slot)
                grid.Add(MakeCard(item));
        }
    }

    private IEnumerable<T> Matching<T>(List<T> assets) where T : UnityEngine.Object
    {
        return assets.Where(a => filter == "" || Name(a).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(Name);
    }

    private VisualElement MakeCard(UnityEngine.Object asset)
    {
        var element = new Button(() => Select(asset)) { style = { width = CardSize, height = CardSize + 22, marginLeft = 3, marginRight = 3, marginTop = 3, marginBottom = 3 } };
        var image = new Image { scaleMode = ScaleMode.ScaleToFit, style = { width = CardSize - 8, height = CardSize - 8 } };
        element.Add(image);
        element.Add(new Label(Name(asset)) { style = { unityTextAlign = TextAnchor.MiddleCenter, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis, whiteSpace = WhiteSpace.NoWrap } });

        var card = new Card { element = element, image = image };
        cards[asset] = card;
        Highlight(card, asset == selected);
        Picture(asset, card);
        return element;
    }

    // An item with an icon shows it; anything else waits its turn on the stage, or falls back to Unity's mini thumbnail.
    private void Picture(UnityEngine.Object asset, Card card)
    {
        var item = asset as ItemDefinition;
        if (item != null && item.icon != null)
        {
            card.image.sprite = item.icon;
            return;
        }

        if (thumbnails.TryGetValue(asset, out var texture))
        {
            card.image.image = texture;
            return;
        }

        card.image.image = AssetPreview.GetMiniThumbnail(asset);
        toRender.Enqueue(asset);
    }

    private void RenderNext()
    {
        if (toRender.Count == 0) return;

        var asset = toRender.Dequeue();
        if (asset == null || !cards.TryGetValue(asset, out var card)) return;
        if (!Dress(snapper, asset)) return;

        var texture = snapper.Snapshot(ThumbnailSize, ThumbnailSize);
        Forget(asset);
        thumbnails[asset] = texture;
        card.image.image = texture;
    }

    // What the stage shows for an entry. False when there is nothing to show.
    private bool Dress(CharacterStage target, UnityEngine.Object asset)
    {
        var none = new List<AppearanceChoice>();
        var nothing = new List<ItemDefinition>();

        if (asset is NPCArchetype archetype)
        {
            var data = archetype.Roll(archetype.baseLevel);
            var model = CharacterDresser.ModelPrefab(database, data.modelId);
            target.Show(model, data.appearance, CharacterDresser.Items(items, data.equipmentItemIds));
            return model != null;
        }

        var item = asset as ItemDefinition;
        var wearer = item != null ? ModelWithLook(item.appearanceGroup) : null;
        if (wearer != null)
        {
            target.Show(wearer, none, new List<ItemDefinition> { item });
            return true;
        }

        var prop = item != null ? item.equippedPrefab : null;
        target.Show(prop, none, nothing);
        return prop != null;
    }

    private GameObject ModelWithLook(string groupId)
    {
        if (string.IsNullOrEmpty(groupId) || database == null) return null;

        foreach (var model in database.AllModels)
        {
            var appearance = model.prefab != null ? model.prefab.GetComponent<ModelAppearance>() : null;
            if (appearance != null && appearance.Groups.Any(g => g.groupId == groupId)) return model.prefab;
        }

        return null;
    }

    private void Select(UnityEngine.Object asset)
    {
        if (selected != null && cards.TryGetValue(selected, out var previous)) Highlight(previous, false);
        selected = asset;
        if (cards.TryGetValue(asset, out var card)) Highlight(card, true);

        entry.Clear();
        entry.Add(DashboardLayout.Heading(Name(asset)));
        var inspector = new InspectorElement(asset);
        inspector.TrackSerializedObjectValue(new SerializedObject(asset), _ => Edited(asset));
        entry.Add(inspector);
        ShowSelected();
    }

    private void ShowSelected()
    {
        reroll.SetEnabled(selected is NPCArchetype);
        if (selected == null)
        {
            stage.Show(null, null, null);
            return;
        }

        Dress(stage, selected);
    }

    private void Edited(UnityEngine.Object asset)
    {
        ShowSelected();
        if (!cards.TryGetValue(asset, out var card)) return;

        Forget(asset);
        Picture(asset, card);
    }

    private void Refresh()
    {
        Load();
        ClearThumbnails();
        if (selected == null) entry.Clear();
        FillGrid();
        ShowSelected();
    }

    private void Forget(UnityEngine.Object asset)
    {
        if (!thumbnails.TryGetValue(asset, out var texture)) return;

        thumbnails.Remove(asset);
        if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
    }

    private void ClearThumbnails()
    {
        foreach (var texture in thumbnails.Values)
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);

        thumbnails.Clear();
    }

    private static void Highlight(Card card, bool on)
    {
        var colour = on ? SelectedColour : Color.clear;
        card.element.style.borderTopColor = colour;
        card.element.style.borderBottomColor = colour;
        card.element.style.borderLeftColor = colour;
        card.element.style.borderRightColor = colour;
        float width = on ? 2f : 0f;
        card.element.style.borderTopWidth = width;
        card.element.style.borderBottomWidth = width;
        card.element.style.borderLeftWidth = width;
        card.element.style.borderRightWidth = width;
    }

    private static string Name(UnityEngine.Object asset)
    {
        if (asset is NPCArchetype archetype && !string.IsNullOrEmpty(archetype.archetypeName)) return archetype.archetypeName;
        if (asset is ItemDefinition item && !string.IsNullOrEmpty(item.displayName)) return item.displayName;
        return asset.name;
    }

    private static string SlotName(ItemDefinition item)
    {
        var slot = item.subType != null ? item.subType.equipmentSlot : null;
        if (slot == null) return NoSlot;
        return string.IsNullOrEmpty(slot.displayName) ? slot.slotId : slot.displayName;
    }
}
