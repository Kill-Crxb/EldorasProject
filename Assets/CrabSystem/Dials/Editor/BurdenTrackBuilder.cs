using System;
using System.Collections.Generic;
using NinjaGame.Progression;
using NinjaGame.Stats;
using UnityEditor;
using UnityEngine;

// Tools → Crab → Dials → Build Burden Track (10 Oct; CF6d, Tracks_Plan §1). Heavy gear costs freedom: every point of
// equip.load passed pays a penalty onto a dial, cumulatively, on CoreDerivation.asset — a stat track like any
// other, taken back the moment the load drops. Light set ≈ 3, medium ≈ 8, heavy ≈ 11.
//
// Wall run, recovery frames and i-frame penalties wait on systems that don't exist yet; their slots use dials that
// do (Tracks_Plan †). Techniques pay their +1 in stamina, the resource they list. Rerunnable: the equip.load entry
// and the Burden rewards are rebuilt from this table each run, so tune here or on the reward assets.
public static class BurdenTrackBuilder
{
    const string Tag = "BurdenTrack";
    const string TablePath = "Assets/Database/Resources/StatDatabase/CoreDerivation.asset";
    const string RewardFolder = "Assets/Database/Resources/StatDatabase/Rewards/Burden";
    const string StaminaPath = "Assets/CrabSystem/Resources/ResourceDefinitions/StaminaDefinition.asset";
    const string LoadStat = "equip.load";

    enum Kind { Stat, Cost }

    struct Penalty
    {
        public int Load;
        public Kind Kind;
        public string StatId;
        public float Amount;
        public string Name;
        public Func<AbilityFilter> Filter;
    }

    static Penalty Stat(int load, string statId, float amount, string name) =>
        new Penalty { Load = load, Kind = Kind.Stat, StatId = statId, Amount = amount, Name = name };

    static Penalty Cost(int load, float amount, string name, Func<AbilityFilter> filter) =>
        new Penalty { Load = load, Kind = Kind.Cost, Amount = amount, Name = name, Filter = filter };

    static readonly Penalty[] Track =
    {
        Stat(1, DialIds.AirTurn, -15f, "AirTurn"),
        Stat(1, DialIds.AirStrafe, -0.5f, "AirStrafe"),
        Stat(2, DialIds.DashSpeed, -1f, "DashSpeed"),
        Stat(3, DialIds.StaminaRegenDelay, 0.25f, "StaminaDelay"),
        Stat(4, DialIds.JumpSpeed, -0.5f, "JumpSpeed"),
        Stat(4, DialIds.AirJumpSpeed, -0.5f, "AirJumpSpeed"),
        Stat(5, DialIds.DashSpeed, -1f, "DashSpeed"),
        Cost(6, 1f, "DodgeStamina", () => new AbilityFilter { tag = "dodge" }),
        Stat(7, DialIds.SprintSpeed, -1f, "SprintSpeed"),
        Cost(8, 1f, "AttackStamina", () => new AbilityFilter { matchType = true, type = AbilityType.Offensive, matchCategory = true, category = AbilityCategory.Physical }),
        Stat(9, DialIds.StaminaRegenDelay, 0.25f, "StaminaDelay"),
        Stat(10, DialIds.Accel, -2f, "Accel"),
        Stat(10, DialIds.Friction, -1f, "Friction"),
        Stat(10, DialIds.AirTurn, -15f, "AirTurn"),
        Stat(11, DialIds.DashSpeed, -2f, "DashSpeed"),
        Cost(12, 1f, "TechniqueStamina", () => new AbilityFilter { matchSpeedClass = true, speedClass = SpeedClass.Technique }),
    };

    [MenuItem("Tools/Crab/Dials/Build Burden Track")]
    public static void Build()
    {
        var table = AssetDatabase.LoadAssetAtPath<StatDerivationTable>(TablePath);
        var stamina = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(StaminaPath);
        if (table == null || stamina == null)
        {
            Debug.LogError($"[{Tag}] Missing {(table == null ? TablePath : StaminaPath)}.");
            return;
        }

        CrabWizardGUI.EnsureFolder(RewardFolder);
        var byLoad = new SortedDictionary<int, List<Reward>>();
        foreach (Penalty penalty in Track)
        {
            if (!byLoad.ContainsKey(penalty.Load)) byLoad[penalty.Load] = new List<Reward>();
            byLoad[penalty.Load].Add(MakeReward(penalty, stamina));
        }

        WriteDerivation(table, byLoad);
        AssetDatabase.SaveAssets();
        Debug.Log($"[{Tag}] Done. {Track.Length} penalties over loads 1–12 on {LoadStat}. Put on a heavy set and watch the dials in the Stat inspector.");
    }

    static Reward MakeReward(Penalty penalty, ResourceDefinition stamina)
    {
        string path = $"{RewardFolder}/Burden_{penalty.Load:00}_{penalty.Name}.asset";
        return penalty.Kind == Kind.Stat ? StatReward(path, penalty) : CostReward(path, penalty, stamina);
    }

    static Reward StatReward(string path, Penalty penalty)
    {
        var reward = Load<StatBonusReward>(path);
        var so = new SerializedObject(reward);
        SerializedProperty ids = so.FindProperty("targetStatIds");
        ids.arraySize = 1;
        ids.GetArrayElementAtIndex(0).stringValue = penalty.StatId;
        so.FindProperty("amount").floatValue = penalty.Amount;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static Reward CostReward(string path, Penalty penalty, ResourceDefinition stamina)
    {
        var reward = Load<AbilityModifierReward>(path);
        AbilityFilter filter = penalty.Filter();

        var so = new SerializedObject(reward);
        SerializedProperty f = so.FindProperty("filter");
        f.FindPropertyRelative("abilityId").stringValue = filter.abilityId ?? "";
        f.FindPropertyRelative("matchType").boolValue = filter.matchType;
        f.FindPropertyRelative("type").enumValueIndex = (int)filter.type;
        f.FindPropertyRelative("matchCategory").boolValue = filter.matchCategory;
        f.FindPropertyRelative("category").enumValueIndex = (int)filter.category;
        f.FindPropertyRelative("matchSpeedClass").boolValue = filter.matchSpeedClass;
        f.FindPropertyRelative("speedClass").enumValueIndex = (int)filter.speedClass;
        f.FindPropertyRelative("tag").stringValue = filter.tag ?? "";
        so.FindProperty("resource").objectReferenceValue = stamina;
        so.FindProperty("costDelta").floatValue = penalty.Amount;
        so.FindProperty("perRank").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    static T Load<T>(string path) where T : Reward
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;

        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    // Replaces the equip.load entry (or adds one) with a breakpoint per load.
    static void WriteDerivation(StatDerivationTable table, SortedDictionary<int, List<Reward>> byLoad)
    {
        var so = new SerializedObject(table);
        SerializedProperty derivations = so.FindProperty("derivations");

        SerializedProperty entry = null;
        for (int i = 0; i < derivations.arraySize; i++)
        {
            SerializedProperty each = derivations.GetArrayElementAtIndex(i);
            if (each.FindPropertyRelative("sourceStatId").stringValue == LoadStat) entry = each;
        }

        if (entry == null)
        {
            derivations.arraySize++;
            entry = derivations.GetArrayElementAtIndex(derivations.arraySize - 1);
            entry.FindPropertyRelative("sourceStatId").stringValue = LoadStat;
        }

        SerializedProperty breakpoints = entry.FindPropertyRelative("breakpoints");
        breakpoints.arraySize = byLoad.Count;

        int index = 0;
        foreach (KeyValuePair<int, List<Reward>> step in byLoad)
        {
            SerializedProperty breakpoint = breakpoints.GetArrayElementAtIndex(index++);
            breakpoint.FindPropertyRelative("threshold").floatValue = step.Key;

            SerializedProperty rewards = breakpoint.FindPropertyRelative("rewards");
            rewards.arraySize = step.Value.Count;
            for (int r = 0; r < step.Value.Count; r++) rewards.GetArrayElementAtIndex(r).objectReferenceValue = step.Value[r];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[{Tag}] table: {LoadStat} has {byLoad.Count} breakpoints on {table.name}.");
    }
}
