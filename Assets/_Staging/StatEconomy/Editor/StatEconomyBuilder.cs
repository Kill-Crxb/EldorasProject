using System.Collections.Generic;
using System.Linq;
using CrabSystem;
using CrabSystem.Progression;
using CrabSystem.Stats;
using UnityEditor;
using UnityEngine;

// Tools > Crab > Stats > Apply Stat Economy. Staging: graduates with the stat economy (Stat_Economy.md, adopted by
// Crxb 13 Nov night from the 11 Nov proposal). Re-running rewrites the same assets.
//   Pool packages: each core pays its pool at 5 / 10 / 15 / 20 (CoreDerivation). Health is Endurance's now, and
//                  Regeneration is Body's. Sizes from the proposal §6.
//   Heavy armour:  the heavy set pieces shrink from 6d4 + 6 to 4d4 + 2 (about +6 net MP, from +14).
//   Keen Eye:      a common trait gives a secondary, not a core: Insight +1 becomes Cunning +1.

public static class StatEconomyBuilder
{
    private const string DerivationPath = "Assets/Database/Resources/StatDatabase/CoreDerivation.asset";
    private const string PackageFolder = "Assets/Database/Resources/StatDatabase/Rewards/Packages";
    private const string ArmourFolder = "Assets/Database/Resources/ItemDatabase/Armor/Sets";
    private const string KeenEyePath = "Assets/Database/Traits/Rewards/Reward_Trait_KeenEye.asset";

    private class Package
    {
        public string core;
        public string pool;
        public float[] amounts;
    }

    private static readonly float[] Thresholds = { 5, 10, 15, 20 };

    private static readonly Package[] Packages =
    {
        new Package { core = StatIds.Endurance, pool = "character.max_health", amounts = new float[] { 10, 15, 20, 25 } },
        new Package { core = StatIds.Body, pool = "character.max_regeneration", amounts = new float[] { 5, 5, 5, 5 } },
        new Package { core = StatIds.Spirit, pool = "character.max_stamina", amounts = new float[] { 5, 5, 5, 5 } },
        new Package { core = StatIds.Mind, pool = "character.max_mana", amounts = new float[] { 5, 5, 5, 5 } },
        new Package { core = StatIds.Resilience, pool = "character.max_recovery", amounts = new float[] { 5, 5, 5, 5 } },
        new Package { core = StatIds.Insight, pool = "character.max_recollection", amounts = new float[] { 5, 5, 5, 5 } },
    };

    // Heavy set: (item, arm_dice, atr.arm). The other stats on each piece are left alone.
    private static readonly (string file, float dice, float arm)[] HeavySet =
    {
        ("Item_Heavy_Cuirass", 3, 2),
        ("Item_Heavy_Kabuto", 1, 0),
        ("Item_Heavy_Greaves", 0, 0),
    };

    [MenuItem("Tools/Crab/Stats/Apply Stat Economy")]
    public static void Apply()
    {
        StatDerivationTable table = AssetDatabase.LoadAssetAtPath<StatDerivationTable>(DerivationPath);
        if (table == null)
        {
            Debug.LogError("[StatEconomyBuilder] CoreDerivation is missing.");
            return;
        }

        EnsureFolder(PackageFolder);
        int packages = WritePackages(table);
        int armour = WriteHeavyArmour();
        bool keenEye = WriteKeenEye();

        AssetDatabase.SaveAssets();
        Debug.Log($"[StatEconomyBuilder] {packages} pool packages, {armour} heavy pieces, Keen Eye {(keenEye ? "→ Cunning" : "missing")}.");
    }

    // ---- Pool packages ----

    private static int WritePackages(StatDerivationTable table)
    {
        int written = 0;
        foreach (Package package in Packages)
        {
            StatDerivation derivation = table.Derivations.FirstOrDefault(d => d.sourceStatId == package.core);
            if (derivation == null)
            {
                Debug.LogWarning($"[StatEconomyBuilder] No derivation for {package.core}; its packages were skipped.");
                continue;
            }

            for (int i = 0; i < Thresholds.Length; i++)
            {
                StatBonusReward reward = PackageReward(package, i);
                AddOnce(Breakpoint(derivation, Thresholds[i]), reward);
                written++;
            }
            derivation.breakpoints.Sort((a, b) => a.threshold.CompareTo(b.threshold));
        }
        EditorUtility.SetDirty(table);
        return written;
    }

    private static StatBonusReward PackageReward(Package package, int index)
    {
        string coreName = package.core.Substring(package.core.IndexOf('.') + 1);
        string path = $"{PackageFolder}/Pkg_{coreName}_{Thresholds[index]}.asset";
        StatBonusReward reward = LoadOrCreate<StatBonusReward>(path);

        var serialized = new SerializedObject(reward);
        SerializedProperty targets = serialized.FindProperty("targetStatIds");
        targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).stringValue = package.pool;
        serialized.FindProperty("amount").floatValue = package.amounts[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return reward;
    }

    private static StatBreakpoint Breakpoint(StatDerivation derivation, float threshold)
    {
        StatBreakpoint found = derivation.breakpoints.FirstOrDefault(b => Mathf.Approximately(b.threshold, threshold));
        if (found != null) return found;

        var created = new StatBreakpoint { threshold = threshold };
        derivation.breakpoints.Add(created);
        return created;
    }

    private static void AddOnce(StatBreakpoint breakpoint, Reward reward)
    {
        if (!breakpoint.rewards.Contains(reward)) breakpoint.rewards.Add(reward);
    }

    // ---- Armour ----

    private static int WriteHeavyArmour()
    {
        int written = 0;
        foreach ((string file, float dice, float arm) in HeavySet)
        {
            ItemDefinition item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ArmourFolder}/{file}.asset");
            if (item == null) continue;

            SetStat(item, StatIds.ArmourDice, dice);
            SetStat(item, StatIds.Armour, arm);
            EditorUtility.SetDirty(item);
            written++;
        }
        return written;
    }

    // Zero removes the line, so a piece never carries a "+0".
    private static void SetStat(ItemDefinition item, string statId, float amount)
    {
        item.stats.RemoveAll(s => s.statId == statId);
        if (amount != 0f) item.stats.Add(new ItemStatBonus { statId = statId, amount = amount });
    }

    // ---- Traits ----

    private static bool WriteKeenEye()
    {
        StatBonusReward reward = AssetDatabase.LoadAssetAtPath<StatBonusReward>(KeenEyePath);
        if (reward == null) return false;

        var serialized = new SerializedObject(reward);
        SerializedProperty targets = serialized.FindProperty("targetStatIds");
        targets.arraySize = 1;
        targets.GetArrayElementAtIndex(0).stringValue = StatIds.Cunning;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(reward);
        return true;
    }

    // ---- Assets ----

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
