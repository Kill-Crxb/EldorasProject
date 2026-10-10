using NinjaGame.Progression;
using UnityEditor;
using UnityEngine;

// Tools → Crab → Talents → Create Test Trees (10 Oct). Two small trees to play the system with before real ones
// are authored: Test Ninja (stats, Sprint and Double Jump grants, all seven rows, a capstone pair, a Sinister
// socket and Ambushing talents) and Test Sage (a mana tree to swap to). Also the two test keywords and the "basic"
// tag on the katana's light string. Test content — delete when real trees exist. Rerunnable: rebuilds the trees.
public static class TalentTestTrees
{
    const string Tag = "TalentTestTrees";
    const string Folder = "Assets/Database/Talents/Test";
    const string KeywordFolder = "Assets/Database/Resources/Keywords";
    const string StaminaPath = "Assets/CrabSystem/Resources/ResourceDefinitions/StaminaDefinition.asset";
    const string AttackFolder = "Assets/Database/Resources/AbilityDatabase/KatanaAbilities";

    [MenuItem("Tools/Crab/Talents/Create Test Trees")]
    public static void Build()
    {
        TalentRules rules = TalentSetupBuilder.Rules();
        CrabWizardGUI.EnsureFolder(Folder);
        CrabWizardGUI.EnsureFolder(KeywordFolder);
        TagBasicAttacks();

        TalentTree ninja = Ninja();
        TalentTree sage = Sage();
        Register(rules, ninja);
        Register(rules, sage);

        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. Both trees are known at start; open the player menu's Talents page.");
    }

    static TalentTree Ninja()
    {
        TalentTree tree = NewTree("test_ninja", "Test Ninja", "Stamina and movement.");

        // Row contents 12 / 7 / 6 / 5 / 3 / 3 points, so every gate (0, 5 … 30) can be reached.
        TalentNode breath = Node(tree, "test_deep_breath", "Deep Breath", TalentCategory.Generic, 1, 0, 5, Rate(tree, "character.max_stamina", 5));
        Node(tree, "test_tough_skin", "Tough Skin", TalentCategory.Generic, 1, 2, 5, Rate(tree, "character.max_health", 5));
        Node(tree, "test_light_feet", "Light Feet", TalentCategory.Capability, 1, 4, 1, Flag(tree, "DoubleJumpGranted"), 2);

        TalentNode sprinter = Node(tree, "test_sprinter", "Sprinter", TalentCategory.Capability, 2, 0, 1, Flag(tree, "SprintGranted"));
        tree.Find(sprinter).requires.Add(breath);
        TalentNode iron = Node(tree, "test_iron_body", "Iron Body", TalentCategory.Modifier, 2, 2, 1, Bonus(tree, "core.body", 2));
        Node(tree, "test_steady", "Steady", TalentCategory.Generic, 2, 4, 5, Rate(tree, "character.max_recovery", 2));

        TalentNode focus = Node(tree, "test_focus", "Focus", TalentCategory.Modifier, 3, 2, 1, Bonus(tree, "core.insight", 2));
        tree.Find(focus).requires.Add(iron);
        Node(tree, "test_endurance", "Endurance", TalentCategory.Generic, 3, 0, 5, Rate(tree, "character.max_stamina", 3));

        Node(tree, "test_second_wind", "Second Wind", TalentCategory.Generic, 4, 2, 5, Rate(tree, "character.max_health", 4));
        Node(tree, "test_vigor", "Vigor", TalentCategory.Generic, 5, 1, 3, Rate(tree, "character.max_stamina", 3));
        Node(tree, "test_hardened", "Hardened", TalentCategory.Generic, 6, 3, 3, Rate(tree, "character.max_health", 4));

        // Keywords: a Sinister socket in the drawer, Ambushing on basic attacks, and talents that shape both.
        KeywordDefinition sinister = Keyword("sinister", "Sinister", "On hit, regain 3 stamina (stand-in for a generator).", KeywordCondition.Always, 0, false);
        KeywordDefinition ambushing = Keyword("ambushing", "Ambushing", "From behind: +2 to hit and advantage.", KeywordCondition.FromBehind, 2, true);
        tree.sockets.Add(new KeywordSocket { keyword = sinister });

        TalentNode opportunist = Node(tree, "test_opportunist", "Opportunist", TalentCategory.Keyword, 1, 1, 1, Grant(tree, ambushing, "basic"));
        Node(tree, "test_cheap_shot", "Cheap Shot", TalentCategory.Modifier, 1, 3, 1, CostCut(tree, "sinister", -2f));
        TalentNode knifesEdge = Node(tree, "test_knifes_edge", "Knife's Edge", TalentCategory.Keyword, 2, 1, 3, Dice(tree, ambushing, 1, 4));
        tree.Find(knifesEdge).requires.Add(opportunist);

        TalentNode shadow = Node(tree, "test_shadow", "Shadow", TalentCategory.Capstone, 7, 1, 1, Bonus(tree, "character.max_movement_charges", 1), 3);
        TalentNode stone = Node(tree, "test_stone", "Stone", TalentCategory.Capstone, 7, 3, 1, Bonus(tree, "character.max_health", 20), 3);
        tree.Find(shadow).exclusiveWith = stone;
        tree.Find(stone).exclusiveWith = shadow;

        EditorUtility.SetDirty(tree);
        return tree;
    }

    static TalentTree Sage()
    {
        TalentTree tree = NewTree("test_sage", "Test Sage", "Mana and insight.");

        TalentNode calm = Node(tree, "test_calm_mind", "Calm Mind", TalentCategory.Generic, 1, 1, 5, Rate(tree, "character.max_mana", 5));
        Node(tree, "test_clarity", "Clarity", TalentCategory.Generic, 1, 3, 3, Rate(tree, "character.max_recollection", 2));
        TalentNode insight = Node(tree, "test_insight", "Insight", TalentCategory.Modifier, 2, 1, 1, Bonus(tree, "core.insight", 1));
        tree.Find(insight).requires.Add(calm);

        EditorUtility.SetDirty(tree);
        return tree;
    }

    static KeywordDefinition Keyword(string id, string name, string description, KeywordCondition condition, int accuracy, bool advantage)
    {
        string path = $"{KeywordFolder}/Keyword_{name}.asset";
        var keyword = AssetDatabase.LoadAssetAtPath<KeywordDefinition>(path);
        if (keyword == null)
        {
            keyword = ScriptableObject.CreateInstance<KeywordDefinition>();
            AssetDatabase.CreateAsset(keyword, path);
        }

        keyword.keywordId = id;
        keyword.displayName = name;
        keyword.description = description;
        keyword.condition = condition;
        keyword.accuracy = accuracy;
        keyword.advantage = advantage;
        keyword.onHit.Clear();
        if (id == "sinister")
            keyword.onHit.Add(new ResourceGain { resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(StaminaPath), amount = 3f });
        EditorUtility.SetDirty(keyword);
        return keyword;
    }

    static void TagBasicAttacks()
    {
        foreach (string name in new[] { "BasicAttack1", "BasicAttack2", "BasicAttack3" })
        {
            var attack = AssetDatabase.LoadAssetAtPath<AbilityDefinition>($"{AttackFolder}/{name}.asset");
            if (attack == null || attack.HasTag("basic")) continue;

            attack.tags.Add("basic");
            EditorUtility.SetDirty(attack);
        }
    }

    static Reward Grant(TalentTree tree, KeywordDefinition keyword, string tag)
    {
        var reward = Inline<KeywordGrantReward>(tree, $"Grant_{keyword.keywordId}");
        var so = new SerializedObject(reward);
        so.FindProperty("keyword").objectReferenceValue = keyword;
        so.FindProperty("filter").FindPropertyRelative("tag").stringValue = tag;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static Reward CostCut(TalentTree tree, string tag, float stamina)
    {
        var reward = Inline<AbilityModifierReward>(tree, $"Cost_{tag}");
        var so = new SerializedObject(reward);
        so.FindProperty("filter").FindPropertyRelative("tag").stringValue = tag;
        so.FindProperty("resource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(StaminaPath);
        so.FindProperty("costDelta").floatValue = stamina;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static Reward Dice(TalentTree tree, KeywordDefinition keyword, int dice, int faces)
    {
        var reward = Inline<KeywordBonusReward>(tree, $"Dice_{keyword.keywordId}");
        var so = new SerializedObject(reward);
        so.FindProperty("keyword").objectReferenceValue = keyword;
        so.FindProperty("dice").intValue = dice;
        so.FindProperty("dieFaces").intValue = faces;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static TalentTree NewTree(string treeId, string displayName, string description)
    {
        string path = $"{Folder}/TalentTree_{CrabWizardGUI.ToAssetName(displayName)}.asset";
        AssetDatabase.DeleteAsset(path);

        TalentTree tree = ScriptableObject.CreateInstance<TalentTree>();
        tree.treeId = treeId;
        tree.displayName = displayName;
        tree.description = description;
        AssetDatabase.CreateAsset(tree, path);
        Debug.Log($"[{Tag}] tree: {displayName} made.");
        return tree;
    }

    static void Register(TalentRules rules, TalentTree tree)
    {
        rules.trees.RemoveAll(t => t == null);
        rules.knownAtStart.RemoveAll(t => t == null);
        if (!rules.trees.Contains(tree)) rules.trees.Add(tree);
        if (!rules.knownAtStart.Contains(tree)) rules.knownAtStart.Add(tree);
        EditorUtility.SetDirty(rules);
    }

    static TalentNode Node(TalentTree tree, string nodeId, string displayName, TalentCategory category, int tier, int column, int maxRank, Reward reward, int cost = 1)
    {
        TalentNode node = ScriptableObject.CreateInstance<TalentNode>();
        node.name = nodeId;
        node.nodeId = nodeId;
        node.displayName = displayName;
        node.category = category;
        node.maxRank = maxRank;
        node.costPerRank = cost;
        node.rewards.Add(reward);
        AssetDatabase.AddObjectToAsset(node, tree);

        tree.placements.Add(new TalentPlacement { node = node, tier = tier, column = column });
        return node;
    }

    static Reward Rate(TalentTree tree, string statId, float perRank)
    {
        var reward = Inline<StatRateReward>(tree, $"Rate_{statId}");
        var so = new SerializedObject(reward);
        SetIds(so, statId);
        so.FindProperty("perPoint").floatValue = perRank;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static Reward Bonus(TalentTree tree, string statId, float amount)
    {
        var reward = Inline<StatBonusReward>(tree, $"Bonus_{statId}");
        var so = new SerializedObject(reward);
        SetIds(so, statId);
        so.FindProperty("amount").floatValue = amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static Reward Flag(TalentTree tree, string fact)
    {
        var reward = Inline<FlagReward>(tree, $"Flag_{fact}");
        var so = new SerializedObject(reward);
        so.FindProperty("fact").stringValue = fact;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static T Inline<T>(TalentTree tree, string name) where T : Reward
    {
        T reward = ScriptableObject.CreateInstance<T>();
        reward.name = name;
        AssetDatabase.AddObjectToAsset(reward, tree);
        return reward;
    }

    static void SetIds(SerializedObject so, string statId)
    {
        SerializedProperty ids = so.FindProperty("targetStatIds");
        ids.arraySize = 1;
        ids.GetArrayElementAtIndex(0).stringValue = statId;
    }
}
