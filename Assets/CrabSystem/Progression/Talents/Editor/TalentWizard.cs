using System;
using System.Collections.Generic;
using NinjaGame.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Tools → Crab → Wizards → Talents (Talent_Wizard.md). Authors trees on the grid the player will see.
//
//   Tree      right-click a cell: new talent, place a generic. Drag a card to move it (onto another card swaps).
//             Select a card, then "Add requirement" / "Exclusive with" and click the partner.
//   Simulate  spend a number of points by the game's own rules (TalentChecks).
//
// A tree's own talents and any reward made here are sub-assets of the tree's file, so a tree is one file and
// deleting it can't orphan anything. Generics live in the library tree and are placed by reference. Every edit
// goes through Undo. Issues come from TalentTreeValidator; clicking one selects its talent.
public class TalentWizard : EditorWindow
{
    const string TreeFolder = "Assets/Database/Talents";
    const float CellWidth = 124f;
    const float CellHeight = 66f;
    const float Gap = 16f;
    const float TierLabelWidth = 72f;
    const float ListWidth = 200f;
    const float InspectorWidth = 360f;

    enum Tab { Tree, Simulate }
    enum LinkMode { None, Requires, Exclusive }

    static readonly string[] TabNames = { "Tree", "Simulate" };
    static readonly Color[] CategoryColours =
    {
        new Color(0.55f, 0.55f, 0.55f), new Color(0.75f, 0.35f, 0.35f), new Color(0.35f, 0.55f, 0.8f),
        new Color(0.45f, 0.75f, 0.45f), new Color(0.8f, 0.65f, 0.3f), new Color(0.65f, 0.45f, 0.8f), new Color(0.95f, 0.85f, 0.4f)
    };

    TalentRules rules;
    List<TalentTree> trees = new();
    TalentTree tree;
    TalentNode selectedNode;
    Tab tab;
    LinkMode linkMode;
    TalentPlacement dragging;
    string newTreeName = "";

    List<TalentTreeValidator.Issue> issues = new();
    bool issuesDirty = true;

    readonly Dictionary<string, int> simRanks = new();
    int simPoints = 30;
    string simHover = "";

    readonly Dictionary<Object, Editor> rewardEditors = new();
    readonly HashSet<Object> openRewards = new();

    Vector2 listScroll;
    Vector2 gridScroll;
    Vector2 inspectorScroll;
    Vector2 issueScroll;

    // By node, not by placement object: an undo or a serialized edit rebuilds the placement list.
    TalentPlacement selected
    {
        get => tree != null ? tree.Find(selectedNode) : null;
        set => selectedNode = value?.node;
    }

    [MenuItem("Tools/Crab/Wizards/Talents…")]
    static void Open()
    {
        TalentWizard window = GetWindow<TalentWizard>("Talents");
        window.minSize = new Vector2(1180, 660);
    }

    void OnEnable()
    {
        wantsMouseMove = true;
        Undo.undoRedoPerformed += HandleUndo;
        Reload();
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= HandleUndo;
        foreach (Editor editor in rewardEditors.Values) DestroyImmediate(editor);
        rewardEditors.Clear();
    }

    void OnProjectChange() => Reload();

    void HandleUndo()
    {
        issuesDirty = true;
        Repaint();
    }

    void Reload()
    {
        rules = TalentTreeValidator.FindRules();
        trees = CrabWizardGUI.LoadAll<TalentTree>();
        trees.Sort((a, b) => a.isLibrary != b.isLibrary ? (a.isLibrary ? 1 : -1) : string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));

        if (tree == null || !trees.Contains(tree)) tree = trees.Count > 0 ? trees[0] : null;
        if (selected == null) selectedNode = null;
        issuesDirty = true;
        Repaint();
    }

    void OnGUI()
    {
        if (rules == null)
        {
            EditorGUILayout.HelpBox("No TalentRules asset. Run Tools → Crab → Talents → Set Up.", MessageType.Info);
            return;
        }

        if (issuesDirty) Revalidate();

        EditorGUILayout.BeginHorizontal();
        DrawTreeList();

        EditorGUILayout.BeginVertical();
        tab = (Tab)GUILayout.Toolbar((int)tab, TabNames);
        DrawTab();
        DrawIssues();
        EditorGUILayout.EndVertical();

        DrawInspector();
        EditorGUILayout.EndHorizontal();
    }

    void DrawTab()
    {
        if (tree == null)
        {
            EditorGUILayout.HelpBox("Make a tree on the left to start.", MessageType.Info);
            GUILayout.FlexibleSpace();
            return;
        }

        if (tab == Tab.Tree) DrawTreeTab();
        if (tab == Tab.Simulate) DrawSimulateTab();
    }

    void Revalidate()
    {
        issues = TalentTreeValidator.Validate(tree, rules);
        issuesDirty = false;
    }

    void Changed(Object target)
    {
        EditorUtility.SetDirty(target);
        issuesDirty = true;
    }

    // ---- Tree list ----

    void DrawTreeList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
        CrabWizardGUI.Section("Trees");

        listScroll = EditorGUILayout.BeginScrollView(listScroll);
        foreach (TalentTree each in trees)
        {
            string label = each.isLibrary ? $"{each.Label}  (library)" : each.Label;
            bool on = GUILayout.Toggle(each == tree, label, "Button");
            if (on && each != tree) SelectTree(each);
        }
        EditorGUILayout.EndScrollView();

        newTreeName = EditorGUILayout.TextField(newTreeName);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newTreeName)))
        {
            if (GUILayout.Button("+ Tree")) CreateTree(newTreeName, false);
            if (GUILayout.Button("+ Library")) CreateTree(newTreeName, true);
        }
        EditorGUILayout.EndHorizontal();

        DrawCatalogue();
        EditorGUILayout.EndVertical();
    }

    void DrawCatalogue()
    {
        CrabWizardGUI.Section("Rules");
        EditorGUILayout.ObjectField(rules, typeof(TalentRules), false);
        if (tree == null || tree.isLibrary) return;

        bool listed = rules.trees.Contains(tree);
        bool known = rules.knownAtStart.Contains(tree);
        bool nowListed = EditorGUILayout.ToggleLeft("In the catalogue", listed);
        bool nowKnown = EditorGUILayout.ToggleLeft("Known at start", known);
        if (nowListed == listed && nowKnown == known) return;

        Undo.RecordObject(rules, "Talent catalogue");
        SetMember(rules.trees, tree, nowListed || nowKnown);
        SetMember(rules.knownAtStart, tree, nowKnown);
        EditorUtility.SetDirty(rules);
    }

    static void SetMember(List<TalentTree> list, TalentTree item, bool member)
    {
        if (member && !list.Contains(item)) list.Add(item);
        if (!member) list.Remove(item);
    }

    void SelectTree(TalentTree next)
    {
        tree = next;
        selected = null;
        linkMode = LinkMode.None;
        simRanks.Clear();
        issuesDirty = true;
        GUI.FocusControl(null);
    }

    void CreateTree(string displayName, bool library)
    {
        string assetName = CrabWizardGUI.ToAssetName(displayName);
        string folder = $"{TreeFolder}/{assetName}";
        CrabWizardGUI.EnsureFolder(folder);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/TalentTree_{assetName}.asset");
        TalentTree created = CreateInstance<TalentTree>();
        created.treeId = CrabWizardGUI.ToItemId(displayName);
        created.displayName = displayName.Trim();
        created.isLibrary = library;

        AssetDatabase.CreateAsset(created, path);
        Undo.RegisterCreatedObjectUndo(created, "New talent tree");

        if (!library)
        {
            Undo.RecordObject(rules, "New talent tree");
            rules.trees.Add(created);
            EditorUtility.SetDirty(rules);
        }

        AssetDatabase.SaveAssets();
        newTreeName = "";
        Reload();
        SelectTree(created);
    }

    // ---- Grid (Tree and Simulate) ----

    Rect GridArea()
    {
        float width = TierLabelWidth + rules.columns * (CellWidth + Gap);
        float height = rules.Tiers * (CellHeight + Gap);
        return GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
    }

    Rect CellRect(Rect area, int tier, int column) =>
        new Rect(area.x + TierLabelWidth + column * (CellWidth + Gap), area.y + (rules.Tiers - tier) * (CellHeight + Gap), CellWidth, CellHeight);

    bool CellAt(Rect area, Vector2 point, out int tier, out int column)
    {
        column = Mathf.FloorToInt((point.x - area.x - TierLabelWidth) / (CellWidth + Gap));
        tier = rules.Tiers - Mathf.FloorToInt((point.y - area.y) / (CellHeight + Gap));
        bool inside = column >= 0 && column < rules.columns && tier >= 1 && tier <= rules.Tiers;
        return inside && CellRect(area, tier, column).Contains(point);
    }

    void DrawGrid(Rect area, bool simulate)
    {
        for (int tier = 1; tier <= rules.Tiers; tier++)
        {
            Rect row = CellRect(area, tier, 0);
            GUI.Label(new Rect(area.x, row.y, TierLabelWidth, CellHeight), $"Row {tier}\n{rules.Gate(tier)} pts", EditorStyles.miniBoldLabel);
        }

        if (Event.current.type == EventType.Repaint) DrawLinks(area);

        for (int tier = 1; tier <= rules.Tiers; tier++)
        {
            for (int column = 0; column < rules.columns; column++)
            {
                Rect rect = CellRect(area, tier, column);
                TalentPlacement placement = tree.At(tier, column);
                if (placement?.node == null) EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, simulate ? 0.05f : 0.15f));
                else DrawCard(rect, placement, simulate);
            }
        }
    }

    void DrawLinks(Rect area)
    {
        foreach (TalentPlacement placement in tree.placements)
        {
            if (placement?.node == null) continue;
            Rect to = CellRect(area, placement.tier, placement.column);

            Handles.color = new Color(1f, 1f, 1f, 0.7f);
            foreach (TalentNode required in placement.requires)
            {
                TalentPlacement from = tree.Find(required);
                if (from == null) continue;
                Rect start = CellRect(area, from.tier, from.column);
                Handles.DrawAAPolyLine(3f, new Vector3(start.center.x, start.yMin), new Vector3(to.center.x, to.yMax));
            }

            TalentPlacement partner = tree.Find(placement.exclusiveWith);
            if (partner == null) continue;
            Handles.color = new Color(1f, 0.6f, 0.3f, 0.9f);
            Handles.DrawDottedLine(to.center, CellRect(area, partner.tier, partner.column).center, 4f);
        }
    }

    void DrawCard(Rect rect, TalentPlacement placement, bool simulate)
    {
        TalentNode node = placement.node;
        Color category = CategoryColours[Mathf.Clamp((int)node.category, 0, CategoryColours.Length - 1)];

        EditorGUI.DrawRect(rect, simulate ? SimColour(node) : new Color(0.2f, 0.2f, 0.22f));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), category);

        if (placement == selected && !simulate) Outline(rect, new Color(1f, 0.9f, 0.3f), 2f);
        else if (HasError(node)) Outline(rect, new Color(0.9f, 0.25f, 0.25f), 2f);

        bool shared = AssetDatabase.GetAssetPath(node) != AssetDatabase.GetAssetPath(tree);
        string ranks = simulate ? $"{TalentChecks.Rank(simRanks, node)}/{node.maxRank}" : $"{node.costPerRank} × {node.maxRank}";
        string text = $"<b>{node.Label}</b>\n{node.category}{(shared ? " · generic" : "")}\n{ranks}";
        GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 10f, rect.height - 4f), text, CardStyle);
    }

    static GUIStyle cardStyle;
    static GUIStyle CardStyle => cardStyle ??= new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true, normal = { textColor = Color.white } };

    static void Outline(Rect rect, Color colour, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), colour);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), colour);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), colour);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), colour);
    }

    bool HasError(TalentNode node)
    {
        foreach (TalentTreeValidator.Issue issue in issues)
        {
            if (issue.IsError && issue.Node == node) return true;
        }
        return false;
    }

    // ---- Tree tab ----

    void DrawTreeTab()
    {
        string hint = linkMode == LinkMode.Requires ? "Click the talent it requires (a lower row). Esc cancels."
            : linkMode == LinkMode.Exclusive ? "Click its pick-one partner. Esc cancels."
            : "Right-click a cell to add. Drag to move. Select a card to edit it on the right.";
        EditorGUILayout.LabelField(hint, EditorStyles.miniLabel);

        gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));
        Rect area = GridArea();
        DrawGrid(area, false);
        HandleTreeEvents(area);
        EditorGUILayout.EndScrollView();
    }

    void HandleTreeEvents(Rect area)
    {
        Event e = Event.current;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            linkMode = LinkMode.None;
            e.Use();
            return;
        }

        if (!CellAt(area, e.mousePosition, out int tier, out int column)) return;
        TalentPlacement placement = tree.At(tier, column);

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (placement?.node != null) CardMenu(placement);
            else CellMenu(tier, column);
            e.Use();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0) PressCell(placement);
        if (e.type == EventType.MouseUp && e.button == 0) ReleaseCell(tier, column);
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) e.Use();
    }

    void PressCell(TalentPlacement placement)
    {
        GUI.FocusControl(null);
        if (linkMode != LinkMode.None && placement?.node != null && selected != null)
        {
            Link(placement);
            return;
        }

        selected = placement?.node != null ? placement : null;
        dragging = selected;
        linkMode = LinkMode.None;
    }

    void ReleaseCell(int tier, int column)
    {
        TalentPlacement moved = dragging;
        dragging = null;
        if (moved == null || (moved.tier == tier && moved.column == column)) return;

        Undo.RecordObject(tree, "Move talent");
        TalentPlacement there = tree.At(tier, column);
        if (there != null)
        {
            there.tier = moved.tier;
            there.column = moved.column;
        }
        moved.tier = tier;
        moved.column = column;
        Changed(tree);
    }

    void CellMenu(int tier, int column)
    {
        var menu = new GenericMenu();
        foreach (TalentCategory category in Enum.GetValues(typeof(TalentCategory)))
        {
            TalentCategory captured = category;
            menu.AddItem(new GUIContent($"New talent/{category}"), false, () => NewNode(tier, column, captured));
        }

        foreach (TalentTree library in trees)
        {
            if (!library.isLibrary || library == tree) continue;
            foreach (TalentPlacement generic in library.placements)
            {
                if (generic?.node == null) continue;
                TalentNode node = generic.node;
                bool placed = tree.Find(node) != null;
                if (placed) menu.AddDisabledItem(new GUIContent($"Place generic/{library.Label}/{node.Label}"));
                else menu.AddItem(new GUIContent($"Place generic/{library.Label}/{node.Label}"), false, () => Place(node, tier, column));
            }
        }
        menu.ShowAsContext();
    }

    void CardMenu(TalentPlacement placement)
    {
        selected = placement;
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("Add requirement…"), false, () => linkMode = LinkMode.Requires);
        menu.AddItem(new GUIContent("Exclusive with…"), false, () => linkMode = LinkMode.Exclusive);
        menu.AddItem(new GUIContent("Clear links"), false, () => ClearLinks(placement));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Duplicate"), false, () => Duplicate(placement));
        bool own = IsOwn(placement.node);
        menu.AddItem(new GUIContent(own ? "Delete talent" : "Remove from this tree"), false, () => Delete(placement));
        menu.ShowAsContext();
    }

    bool IsOwn(Object asset) => asset != null && AssetDatabase.GetAssetPath(asset) == AssetDatabase.GetAssetPath(tree);

    void NewNode(int tier, int column, TalentCategory category)
    {
        Undo.RecordObject(tree, "New talent");
        TalentNode node = CreateInstance<TalentNode>();
        node.nodeId = UniqueNodeId($"{tree.treeId}_{category.ToString().ToLowerInvariant()}");
        node.name = node.nodeId;
        node.displayName = $"New {category}";
        node.category = category;
        node.costPerRank = category == TalentCategory.Capstone ? 3 : 1;

        AssetDatabase.AddObjectToAsset(node, tree);
        Undo.RegisterCreatedObjectUndo(node, "New talent");
        Place(node, tier, column);
        AssetDatabase.SaveAssets();
    }

    void Place(TalentNode node, int tier, int column)
    {
        Undo.RecordObject(tree, "Place talent");
        var placement = new TalentPlacement { node = node, tier = tier, column = column };
        tree.placements.Add(placement);
        selected = placement;
        Changed(tree);
    }

    static string UniqueNodeId(string stem)
    {
        // Talents are sub-assets of their tree, so every object in every tree file is read, not just the first.
        var used = new HashSet<string>();
        foreach (TalentTree each in CrabWizardGUI.LoadAll<TalentTree>())
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(each)))
            {
                if (asset is TalentNode node) used.Add(node.nodeId);
            }
        }

        int n = 1;
        while (used.Contains($"{stem}_{n}")) n++;
        return $"{stem}_{n}";
    }

    void Link(TalentPlacement target)
    {
        Undo.RecordObject(tree, "Link talents");
        if (linkMode == LinkMode.Requires) ToggleRequirement(selected, target);
        if (linkMode == LinkMode.Exclusive) PairExclusive(selected, target);
        linkMode = LinkMode.None;
        Changed(tree);
    }

    void ToggleRequirement(TalentPlacement placement, TalentPlacement required)
    {
        if (required.tier >= placement.tier)
        {
            ShowNotification(new GUIContent("A requirement must be in a lower row."));
            return;
        }

        if (!placement.requires.Remove(required.node)) placement.requires.Add(required.node);
    }

    void PairExclusive(TalentPlacement a, TalentPlacement b)
    {
        if (a == b) return;
        bool unpair = a.exclusiveWith == b.node;

        ClearExclusive(a);
        ClearExclusive(b);
        if (unpair) return;

        a.exclusiveWith = b.node;
        b.exclusiveWith = a.node;
    }

    void ClearExclusive(TalentPlacement placement)
    {
        TalentPlacement partner = tree.Find(placement.exclusiveWith);
        if (partner != null && partner.exclusiveWith == placement.node) partner.exclusiveWith = null;
        placement.exclusiveWith = null;
    }

    void ClearLinks(TalentPlacement placement)
    {
        Undo.RecordObject(tree, "Clear links");
        placement.requires.Clear();
        ClearExclusive(placement);
        Changed(tree);
    }

    void Duplicate(TalentPlacement placement)
    {
        int column = FreeColumn(placement.tier);
        if (column < 0)
        {
            ShowNotification(new GUIContent($"Row {placement.tier} is full."));
            return;
        }

        Undo.RecordObject(tree, "Duplicate talent");
        TalentNode copy = Instantiate(placement.node);
        copy.nodeId = UniqueNodeId(placement.node.nodeId);
        copy.name = copy.nodeId;
        AssetDatabase.AddObjectToAsset(copy, tree);
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate talent");

        for (int i = 0; i < copy.rewards.Count; i++)
        {
            if (!IsOwn(copy.rewards[i])) continue;
            Reward reward = Instantiate(copy.rewards[i]);
            reward.name = copy.rewards[i].name;
            AssetDatabase.AddObjectToAsset(reward, tree);
            Undo.RegisterCreatedObjectUndo(reward, "Duplicate talent");
            copy.rewards[i] = reward;
        }

        Place(copy, placement.tier, column);
        AssetDatabase.SaveAssets();
    }

    int FreeColumn(int tier)
    {
        for (int column = 0; column < rules.columns; column++)
        {
            if (tree.At(tier, column) == null) return column;
        }
        return -1;
    }

    // A tree's own talent is destroyed with its inline rewards; a generic is only taken off this tree. A
    // library talent still placed elsewhere isn't deleted, or every tree using it would hold a null.
    void Delete(TalentPlacement placement)
    {
        TalentNode node = placement.node;
        string users = UsersOutside(node);
        if (tree.isLibrary && users.Length > 0)
        {
            EditorUtility.DisplayDialog("Still in use", $"{node.Label} is placed in: {users}. Remove it there first.", "OK");
            return;
        }

        Undo.RecordObject(tree, "Delete talent");
        tree.placements.Remove(placement);
        foreach (TalentPlacement other in tree.placements)
        {
            other.requires.Remove(node);
            if (other.exclusiveWith == node) other.exclusiveWith = null;
        }

        if (IsOwn(node)) DestroyNode(node);
        selected = null;
        Changed(tree);
        AssetDatabase.SaveAssets();
    }

    void DestroyNode(TalentNode node)
    {
        foreach (Reward reward in node.rewards)
        {
            if (IsOwn(reward)) Undo.DestroyObjectImmediate(reward);
        }
        Undo.DestroyObjectImmediate(node);
    }

    string UsersOutside(TalentNode node)
    {
        var names = new List<string>();
        foreach (TalentTree other in trees)
        {
            if (other != tree && other.Find(node) != null) names.Add(other.Label);
        }
        return string.Join(", ", names);
    }

    // ---- Rewards ----

    // `owner` holds the list; inline rewards go into the owner's asset file.
    void DrawRewardList(Object owner, List<Reward> rewards)
    {
        for (int i = 0; i < rewards.Count; i++)
        {
            Reward reward = rewards[i];
            EditorGUILayout.BeginHorizontal();
            bool open = reward != null && openRewards.Contains(reward);
            bool nowOpen = EditorGUILayout.Foldout(open, reward != null ? $"{reward.DisplayName}  ({reward.GetType().Name})" : "(missing)", true);
            bool remove = GUILayout.Button("×", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            if (reward != null && nowOpen) openRewards.Add(reward);
            if (reward != null && !nowOpen) openRewards.Remove(reward);
            if (reward != null && nowOpen) DrawRewardEditor(reward);

            if (!remove) continue;
            RemoveReward(owner, rewards, i);
            break;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("New reward ▾", GUILayout.Width(110))) RewardMenu(owner, rewards);
        Reward existing = (Reward)EditorGUILayout.ObjectField(null, typeof(Reward), false);
        EditorGUILayout.EndHorizontal();
        if (existing == null) return;

        Undo.RecordObject(owner, "Add reward");
        rewards.Add(existing);
        Changed(owner);
    }

    void DrawRewardEditor(Reward reward)
    {
        rewardEditors.TryGetValue(reward, out Editor editor);
        Editor.CreateCachedEditor(reward, null, ref editor);
        rewardEditors[reward] = editor;

        EditorGUI.indentLevel++;
        EditorGUI.BeginChangeCheck();
        editor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck()) issuesDirty = true;
        EditorGUI.indentLevel--;
    }

    void RewardMenu(Object owner, List<Reward> rewards)
    {
        var menu = new GenericMenu();
        foreach (Type type in TypeCache.GetTypesDerivedFrom<Reward>())
        {
            if (type.IsAbstract) continue;
            Type captured = type;
            menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () => CreateReward(owner, rewards, captured));
        }
        menu.ShowAsContext();
    }

    void CreateReward(Object owner, List<Reward> rewards, Type type)
    {
        Undo.RecordObject(owner, "New reward");
        var reward = (Reward)CreateInstance(type);
        reward.name = $"{(owner is TalentNode node ? node.nodeId : tree.treeId)}_{type.Name}";
        AssetDatabase.AddObjectToAsset(reward, AssetDatabase.GetAssetPath(owner));
        Undo.RegisterCreatedObjectUndo(reward, "New reward");

        rewards.Add(reward);
        openRewards.Add(reward);
        Changed(owner);
        AssetDatabase.SaveAssets();
    }

    // An inline reward is destroyed with its entry; a shared reward asset is only unlisted.
    void RemoveReward(Object owner, List<Reward> rewards, int index)
    {
        Reward reward = rewards[index];
        Undo.RecordObject(owner, "Remove reward");
        rewards.RemoveAt(index);
        Changed(owner);

        bool inline = reward != null && AssetDatabase.GetAssetPath(reward) == AssetDatabase.GetAssetPath(owner);
        if (inline) Undo.DestroyObjectImmediate(reward);
        AssetDatabase.SaveAssets();
    }

    // ---- Simulate tab ----

    void DrawSimulateTab()
    {
        EditorGUILayout.BeginHorizontal();
        simPoints = EditorGUILayout.IntSlider("Points in this tree", simPoints, 0, rules.MaxPoints);
        foreach (int preset in new[] { rules.MaxPoints, 40, 30, 20, 10 })
        {
            if (GUILayout.Button(preset.ToString(), GUILayout.Width(32))) simPoints = preset;
        }

        if (GUILayout.Button("Clear", GUILayout.Width(50))) simRanks.Clear();
        EditorGUILayout.EndHorizontal();

        int spent = TalentChecks.Spent(tree, simRanks);
        EditorGUILayout.LabelField($"{SimAvailable()} points left, {spent} spent.  Open rows: {OpenTiers()}.  Capstone reachable: {(CapstoneReachable() ? "yes" : "no")}.  {simHover}", EditorStyles.wordWrappedMiniLabel);

        gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));
        Rect area = GridArea();
        DrawGrid(area, true);
        HandleSimulateEvents(area);
        EditorGUILayout.EndScrollView();
    }

    int SimAvailable() => simPoints - TalentChecks.Spent(tree, simRanks);

    string OpenTiers()
    {
        var open = new List<string>();
        for (int tier = 1; tier <= rules.Tiers; tier++)
        {
            if (TalentChecks.Spent(tree, simRanks, tier) >= rules.Gate(tier)) open.Add(tier.ToString());
        }
        return string.Join(", ", open);
    }

    // Some capstone could still be bought if the points went there: the gate is reachable with what's left.
    bool CapstoneReachable()
    {
        foreach (TalentPlacement placement in tree.placements)
        {
            if (placement?.node == null || placement.node.category != TalentCategory.Capstone) continue;
            if (TalentChecks.Rank(simRanks, placement.node) > 0) return true;

            int needed = Mathf.Max(0, rules.Gate(placement.tier) - TalentChecks.Spent(tree, simRanks, placement.tier));
            if (SimAvailable() >= needed + placement.node.costPerRank) return true;
        }
        return false;
    }

    Color SimColour(TalentNode node)
    {
        if (TalentChecks.Rank(simRanks, node) > 0) return new Color(0.6f, 0.48f, 0.15f);
        bool open = TalentChecks.WhyNotSpend(tree, rules, simRanks, node, SimAvailable()) == null;
        return open ? new Color(0.18f, 0.38f, 0.26f) : new Color(0.15f, 0.15f, 0.17f);
    }

    void HandleSimulateEvents(Rect area)
    {
        Event e = Event.current;
        if (!CellAt(area, e.mousePosition, out int tier, out int column)) return;

        TalentNode node = tree.At(tier, column)?.node;
        if (node == null) return;

        if (e.type == EventType.MouseMove)
        {
            simHover = $"{node.Label}: {TalentChecks.WhyNotSpend(tree, rules, simRanks, node, SimAvailable()) ?? "can be learned"}";
            Repaint();
            return;
        }

        if (e.type != EventType.MouseDown) return;
        if (e.button == 0 && TalentChecks.WhyNotSpend(tree, rules, simRanks, node, SimAvailable()) == null) simRanks[node.nodeId] = TalentChecks.Rank(simRanks, node) + 1;
        if (e.button == 1 && TalentChecks.WhyNotRefund(tree, rules, simRanks, node) == null) simRanks[node.nodeId] = TalentChecks.Rank(simRanks, node) - 1;
        e.Use();
    }

    // ---- Inspector ----

    void DrawInspector()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(InspectorWidth));
        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);

        if (tree != null && selected?.node != null && tab == Tab.Tree) DrawTalentInspector();
        else if (tree != null) DrawTreeInspector();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawTreeInspector()
    {
        CrabWizardGUI.Section("Tree");
        var so = new SerializedObject(tree);
        foreach (string field in new[] { "treeId", "displayName", "icon", "description", "isLibrary", "ownedKeywords", "sockets" })
            EditorGUILayout.PropertyField(so.FindProperty(field), true);
        if (so.ApplyModifiedProperties()) Changed(tree);

        if (GUILayout.Button("Ping asset")) EditorGUIUtility.PingObject(tree);
    }

    void DrawTalentInspector()
    {
        TalentNode node = selected.node;
        CrabWizardGUI.Section($"Talent — tier {selected.tier}, column {selected.column}");
        if (!IsOwn(node)) EditorGUILayout.HelpBox("A shared generic: edits change it in every tree that places it.", MessageType.Info);

        var so = new SerializedObject(node);
        foreach (string field in new[] { "nodeId", "displayName", "icon", "category", "description", "maxRank", "costPerRank", "keywords" })
            EditorGUILayout.PropertyField(so.FindProperty(field), true);
        if (so.ApplyModifiedProperties()) Changed(node);

        CrabWizardGUI.Section("Rewards — scaled by rank");
        DrawRewardList(node, node.rewards);

        CrabWizardGUI.Section("Placement");
        DrawRequirements();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Exclusive with", selected.exclusiveWith != null ? selected.exclusiveWith.Label : "—");
        if (GUILayout.Button("Pick", GUILayout.Width(44))) linkMode = LinkMode.Exclusive;
        EditorGUILayout.EndHorizontal();
    }

    void DrawRequirements()
    {
        EditorGUILayout.LabelField("Requires", EditorStyles.miniBoldLabel);
        foreach (TalentNode required in selected.requires)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(required != null ? required.Label : "(missing)");
            bool remove = GUILayout.Button("×", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            if (!remove) continue;
            Undo.RecordObject(tree, "Remove requirement");
            selected.requires.Remove(required);
            Changed(tree);
            break;
        }

        if (GUILayout.Button("Add requirement", GUILayout.Width(130))) linkMode = LinkMode.Requires;
    }

    // ---- Issues ----

    void DrawIssues()
    {
        int errors = 0;
        foreach (TalentTreeValidator.Issue issue in issues) errors += issue.IsError ? 1 : 0;
        EditorGUILayout.LabelField($"{errors} errors · {issues.Count - errors} warnings", EditorStyles.boldLabel);

        issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.Height(130));
        foreach (TalentTreeValidator.Issue issue in issues)
        {
            string text = $"{(issue.IsError ? "Error" : "Warning")}:  {(issue.Node != null ? issue.Node.Label + ": " : "")}{issue.Message}";
            if (!GUILayout.Button(text, EditorStyles.label)) continue;

            TalentPlacement placement = tree.Find(issue.Node);
            if (placement == null) continue;
            selected = placement;
            tab = Tab.Tree;
        }
        EditorGUILayout.EndScrollView();
    }
}
