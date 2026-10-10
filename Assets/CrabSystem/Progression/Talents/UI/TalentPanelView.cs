using System.Collections.Generic;
using System.Text;
using NinjaGame.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The Talents page of the player menu. Builds its own widgets the first time it shows, so the page needs only
// this component on an empty RectTransform: slot tabs, the selected slot's tree as a row grid (left click
// learns, right click unlearns, hover for detail), and the actions — change the tree, reset it. Everything goes
// through TalentModule; this only shows and asks.
public class TalentPanelView : UIPanelView
{
    [Header("Look")]
    [SerializeField] private Color learnedColor = new Color(0.85f, 0.68f, 0.25f, 1f);
    [SerializeField] private Color availableColor = new Color(0.22f, 0.42f, 0.3f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.16f, 0.16f, 0.18f, 1f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color selectedTabColor = new Color(0.35f, 0.35f, 0.42f, 1f);
    [SerializeField] private Vector2 cellSize = new Vector2(96f, 50f);

    [Tooltip("Shows a +1 level button, for testing. Off in a release build regardless.")]
    [SerializeField] private bool devButtons = true;

    private TalentModule talents;
    private RPGSystem rpg;
    private TalentSlot selected;
    private TalentNode hovered;
    private bool picking;
    private int socketPicking = -1;
    private bool built;

    private readonly List<Button> tabs = new();
    private readonly List<TalentCell> cells = new();
    private readonly List<Button> pickButtons = new();
    private readonly List<Button> socketButtons = new();
    private RectTransform socketRow;
    private TextMeshProUGUI info;
    private TextMeshProUGUI detail;
    private RectTransform grid;
    private RectTransform picker;
    private Button treeButton;
    private Button resetButton;

    protected override void OnShown()
    {
        ControllerBrain player = PlayerBrainAccess.Find();
        TalentModule found = player != null ? player.GetModule<TalentModule>() : null;
        if (found != talents) selected = null;
        talents = found;
        rpg = player != null ? player.RPG : null;

        if (talents == null || talents.Rules == null)
        {
            Debug.LogWarning($"[TalentPanelView] No TalentModule with rules on the player for {name}.", this);
            return;
        }

        if (!built) Build();
        if (selected == null && talents.Slots.Count > 0) SelectSlot(talents.Slots[0]);

        talents.Changed += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (talents != null) talents.Changed -= Refresh;
    }

    protected override void OnHidden()
    {
        if (talents != null) talents.Changed -= Refresh;
        picking = false;
        socketPicking = -1;
        hovered = null;
    }

    // ---- Building ----

    private void Build()
    {
        built = true;
        var root = (RectTransform)transform;

        VerticalLayoutGroup column = gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding = new RectOffset(12, 12, 12, 12);
        column.spacing = 8;
        column.childControlWidth = true;
        column.childControlHeight = true;
        column.childForceExpandHeight = false;

        RectTransform tabRow = Row(root, 46f);
        // By index, so the tabs follow a new player's module after a respawn or reload.
        for (int i = 0; i < talents.Slots.Count; i++)
        {
            int index = i;
            tabs.Add(NewButton(tabRow, "", () => SelectSlot(talents.Slots[index])));
        }

        info = NewText(root, 16, TextAlignmentOptions.Left);
        Fix(info.rectTransform, 44f);

        RectTransform actions = Row(root, 34f);
        treeButton = NewButton(actions, "Change tree", HandleChangeTree);
        resetButton = NewButton(actions, "Reset tree", HandleReset);
        if (devButtons && Debug.isDebugBuild) NewButton(actions, "Dev: +1 level", HandleDevLevel);

        socketRow = Row(root, 32f);
        grid = BuildGrid(root);
        picker = NewRect("TreePicker", root);
        VerticalLayoutGroup pickList = picker.gameObject.AddComponent<VerticalLayoutGroup>();
        pickList.spacing = 4;
        pickList.childControlWidth = true;
        pickList.childControlHeight = true;
        pickList.childForceExpandHeight = false;

        detail = NewText(root, 14, TextAlignmentOptions.TopLeft);
        Fix(detail.rectTransform, 130f);
    }

    private RectTransform BuildGrid(RectTransform root)
    {
        RectTransform rows = NewRect("Grid", root);
        VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        TalentRules rules = talents.Rules;
        FitCells(root.rect.width, rules.columns);
        for (int tier = rules.Tiers; tier >= 1; tier--)
        {
            RectTransform row = Row(rows, cellSize.y);

            TextMeshProUGUI label = NewText(row, 13, TextAlignmentOptions.MidlineLeft);
            label.text = $"Row {tier}\n{rules.Gate(tier)} pts";
            Size(label.rectTransform, 56f, cellSize.y);

            for (int column = 0; column < rules.columns; column++) cells.Add(NewCell(row, tier, column));
        }
        return rows;
    }

    // Narrow the cells to the panel when the authored size wouldn't fit beside the tier labels.
    private void FitCells(float panelWidth, int columns)
    {
        const float margins = 24f + 56f;
        if (panelWidth <= 0f || columns <= 0) return;

        float fits = (panelWidth - margins - 6f * columns) / columns;
        cellSize.x = Mathf.Min(cellSize.x, Mathf.Max(48f, fits));
    }

    private TalentCell NewCell(RectTransform row, int tier, int column)
    {
        Image image = NewImage(row, lockedColor);
        Size(image.rectTransform, cellSize.x, cellSize.y);

        TextMeshProUGUI label = NewText(image.rectTransform, 12, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);

        TalentCell cell = image.gameObject.AddComponent<TalentCell>();
        cell.Tier = tier;
        cell.Column = column;
        cell.Clicked += HandleCellClicked;
        cell.Hovered += HandleCellHovered;
        return cell;
    }

    // ---- Showing ----

    private void Refresh()
    {
        if (talents == null || !built) return;

        RefreshTabs();
        RefreshInfo();
        RefreshGrid();
        RefreshSockets();
        RefreshPicker();
        RefreshDetail();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count && i < talents.Slots.Count; i++)
        {
            TalentSlot slot = talents.Slots[i];
            string tree = slot.Tree != null ? $"{slot.Tree.Label} ({talents.PointsSpent(slot)})" : "—";
            Label(tabs[i]).text = $"Tree {slot.Index + 1}\n<b>{tree}</b>";
            tabs[i].image.color = slot == selected ? selectedTabColor : lockedColor;
        }
    }

    private void RefreshInfo()
    {
        var text = new StringBuilder();
        text.Append($"Level <b>{talents.Level}</b>  ·  <b>{talents.PointsLeft}</b> point{(talents.PointsLeft == 1 ? "" : "s")} to spend of {talents.PointsEarned}");
        if (selected?.Tree != null) text.Append($"\n{selected.Tree.Label}: {talents.PointsSpent(selected)} spent here");
        info.text = text.ToString();

        SetAction(treeButton, selected != null, true);
        SetAction(resetButton, selected?.Tree != null, selected != null && selected.Ranks.Count > 0);
        Label(treeButton).text = picking ? "Back to tree" : selected?.Tree != null ? "Change tree" : "Choose tree";
    }

    private void RefreshGrid()
    {
        bool choosing = picking || socketPicking >= 0;
        grid.gameObject.SetActive(!choosing);
        if (choosing) return;

        foreach (TalentCell cell in cells)
        {
            TalentPlacement placement = selected?.Tree != null ? selected.Tree.At(cell.Tier, cell.Column) : null;
            Image image = cell.GetComponent<Image>();
            TextMeshProUGUI label = cell.GetComponentInChildren<TextMeshProUGUI>();

            image.enabled = placement?.node != null;
            label.enabled = image.enabled;
            if (!image.enabled) continue;

            TalentNode node = placement.node;
            int rank = selected.Rank(node.nodeId);
            label.text = $"{node.Label}\n{rank}/{node.maxRank}";
            image.color = rank > 0 ? learnedColor : talents.WhyNotSpend(selected, node) == null ? availableColor : lockedColor;
        }
    }

    // The selected tree's drawer: one button per keyword socket, showing what's in it.
    private void RefreshSockets()
    {
        foreach (Button button in socketButtons) Destroy(button.gameObject);
        socketButtons.Clear();

        List<KeywordSocket> sockets = selected?.Tree != null ? selected.Tree.sockets : null;
        socketRow.gameObject.SetActive(sockets != null && sockets.Count > 0 && !picking);
        if (!socketRow.gameObject.activeSelf) return;

        for (int i = 0; i < sockets.Count; i++)
        {
            int index = i;
            bool open = talents.SocketOpen(selected, i);
            string held = selected.Sockets.TryGetValue(i, out string abilityId) ? AbilityName(abilityId) : "empty";
            string text = open ? $"<b>{sockets[i].Label}</b>: {held}" : $"<b>{sockets[i].Label}</b>: locked";

            Button button = NewButton(socketRow, text, () => HandleSocketClicked(index));
            button.interactable = open;
            if (index == socketPicking) button.image.color = selectedTabColor;
            socketButtons.Add(button);
        }
    }

    private void RefreshPicker()
    {
        bool choosing = picking || socketPicking >= 0;
        picker.gameObject.SetActive(choosing);
        if (!choosing) return;

        foreach (Button button in pickButtons) Destroy(button.gameObject);
        pickButtons.Clear();

        if (socketPicking >= 0)
        {
            PickAbilities();
            return;
        }

        foreach (TalentTree tree in talents.KnownTrees())
        {
            if (tree.isLibrary) continue;

            TalentTree captured = tree;
            string why = talents.WhyNotSetTree(selected, tree);
            Button button = NewButton(picker, $"<b>{tree.Label}</b>  {tree.description}{(why != null ? $"  <i>({why})</i>" : "")}", () => HandlePickTree(captured));
            button.interactable = why == null;
            Fix((RectTransform)button.transform, 40f);
            pickButtons.Add(button);
        }

        if (pickButtons.Count == 0) Debug.LogWarning("[TalentPanelView] The player knows no trees.", this);
    }

    // Every ability the player knows, plus an entry that empties the socket.
    private void PickAbilities()
    {
        AddPick("<i>Empty the socket</i>", null, () => HandlePickAbility(null));

        RuntimeAbilityManager known = PlayerBrainAccess.Find()?.GetModule<RuntimeAbilityManager>();
        if (known == null) return;

        foreach (AbilityInstance instance in known.GetAllInstances())
        {
            AbilityDefinition ability = instance?.definition;
            if (ability == null) continue;

            string why = talents.WhyNotSocket(selected, socketPicking, ability);
            AddPick($"<b>{ability.abilityName}</b>", why, () => HandlePickAbility(ability));
        }
    }

    private void AddPick(string text, string why, UnityEngine.Events.UnityAction onClick)
    {
        Button button = NewButton(picker, why != null ? $"{text}  <i>({why})</i>" : text, onClick);
        button.interactable = why == null;
        Fix((RectTransform)button.transform, 36f);
        pickButtons.Add(button);
    }

    private static string AbilityName(string abilityId)
    {
        RuntimeAbilityManager known = PlayerBrainAccess.Find()?.GetModule<RuntimeAbilityManager>();
        if (known == null) return abilityId;

        foreach (AbilityInstance instance in known.GetAllInstances())
        {
            if (instance?.definition != null && instance.definition.abilityId == abilityId) return instance.definition.abilityName;
        }
        return abilityId;
    }

    private void HandleSocketClicked(int index)
    {
        socketPicking = socketPicking == index ? -1 : index;
        picking = false;
        Refresh();
    }

    private void HandlePickAbility(AbilityDefinition ability)
    {
        if (ability == null) talents.ClearSocket(selected, socketPicking);
        else talents.SetSocket(selected, socketPicking, ability);
        socketPicking = -1;
        Refresh();
    }

    private void RefreshDetail()
    {
        if (hovered == null || selected?.Tree == null)
        {
            detail.text = "Left click a talent to learn it, right click to unlearn it.";
            return;
        }

        TalentPlacement placement = selected.Tree.Find(hovered);
        var text = new StringBuilder();
        text.Append($"<b>{hovered.Label}</b>  <i>{hovered.category}</i>  ·  {hovered.costPerRank} point{(hovered.costPerRank == 1 ? "" : "s")} per rank, rank {selected.Rank(hovered.nodeId)}/{hovered.maxRank}");
        if (!string.IsNullOrEmpty(hovered.description)) text.Append($"\n{hovered.description}");
        if (hovered.rewards.Count > 0) text.Append($"\n{Summary(hovered.rewards)}");

        foreach (KeywordUse use in hovered.keywords) text.Append($"\n{use.verb} {use.statusId}");
        if (placement?.exclusiveWith != null) text.Append($"\nPick one: {placement.exclusiveWith.Label}");

        string why = talents.WhyNotSpend(selected, hovered);
        if (why != null && selected.Rank(hovered.nodeId) < hovered.maxRank) text.Append($"\n<color=#C06050>{why}</color>");

        detail.text = text.ToString();
    }

    private static string Summary(List<Reward> rewards)
    {
        var text = new StringBuilder();
        foreach (Reward reward in rewards)
        {
            if (reward == null) continue;
            if (text.Length > 0) text.Append(", ");
            text.Append(reward.DisplayName);
        }
        return text.ToString();
    }

    // ---- Input ----

    private void SelectSlot(TalentSlot slot)
    {
        selected = slot;
        picking = slot.Tree == null;
        socketPicking = -1;
        hovered = null;
        Refresh();
    }

    private void HandleCellClicked(TalentCell cell, PointerEventData.InputButton button)
    {
        TalentPlacement placement = selected?.Tree != null ? selected.Tree.At(cell.Tier, cell.Column) : null;
        if (placement?.node == null) return;

        if (button == PointerEventData.InputButton.Left) talents.Spend(selected, placement.node);
        if (button == PointerEventData.InputButton.Right) talents.Refund(selected, placement.node);
        RefreshDetail();
    }

    private void HandleCellHovered(TalentCell cell, bool entered)
    {
        TalentPlacement placement = selected?.Tree != null ? selected.Tree.At(cell.Tier, cell.Column) : null;
        hovered = entered ? placement?.node : null;
        RefreshDetail();
    }

    private void HandleReset() => talents.ResetTree(selected);

    private void HandleChangeTree()
    {
        picking = !picking;
        socketPicking = -1;
        Refresh();
    }

    private void HandlePickTree(TalentTree tree)
    {
        picking = !talents.SetTree(selected, tree);
        Refresh();
    }

    private void HandleDevLevel()
    {
        if (rpg != null) rpg.SetLevel(rpg.CurrentLevel + 1);
    }

    // ---- Widgets ----

    private static void SetAction(Button button, bool shown, bool usable)
    {
        button.gameObject.SetActive(shown);
        button.interactable = usable;
    }

    private static TextMeshProUGUI Label(Button button) => button.GetComponentInChildren<TextMeshProUGUI>();

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private RectTransform Row(Transform parent, float height)
    {
        RectTransform row = NewRect("Row", parent);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        Fix(row, height);
        return row;
    }

    private static TextMeshProUGUI NewText(Transform parent, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Image NewImage(Transform parent, Color color)
    {
        Image image = NewRect("Cell", parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private Button NewButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Image image = NewImage(parent, lockedColor);
        image.gameObject.name = "Button";
        Button button = image.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);

        TextMeshProUGUI text = NewText(image.rectTransform, 14, TextAlignmentOptions.Center);
        text.text = label;
        Stretch(text.rectTransform);

        LayoutElement element = image.gameObject.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minHeight = 30f;
        return button;
    }

    private static void Fix(RectTransform rect, float height)
    {
        if (!rect.TryGetComponent(out LayoutElement element)) element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static void Size(RectTransform rect, float width, float height)
    {
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.minWidth = width;
        element.preferredHeight = height;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
    }
}
