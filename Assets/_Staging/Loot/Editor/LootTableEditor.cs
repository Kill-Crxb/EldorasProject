using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// The loot table inspector (Loot_Build.md §3): the fields, then the checker's findings, the real odds of every
// item (multiplied through sub-table chains, so a Diamond from a Slime reads about 1/928), and a button that rolls
// the table many times and shows what dropped. Editor-only; the simulation uses the game's own LootRoller.

namespace CrabSystem
{
    [CustomEditor(typeof(LootTable))]
    public class LootTableEditor : Editor
    {
        private int kills = 1000;
        private List<SimRow> simulated;

        private struct SimRow
        {
            public string itemId;
            public int drops;
            public int total;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var table = (LootTable)target;

            DrawChecks(table);
            DrawOdds(table);
            DrawSimulation(table);
        }

        private static void DrawChecks(LootTable table)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            LootTableChecker.Check(table, errors, warnings);

            EditorGUILayout.Space();
            foreach (string error in errors) EditorGUILayout.HelpBox(error, MessageType.Error);
            foreach (string warning in warnings) EditorGUILayout.HelpBox(warning, MessageType.Warning);
        }

        private static void DrawOdds(LootTable table)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Odds", EditorStyles.boldLabel);

            foreach (LootLine line in table.always)
                Row(line.itemId, "always", line.quantity);

            var perRoll = new Dictionary<string, double>();
            double nothing = MainOdds(table, 1d, perRoll, 0);
            string rolls = table.mainRolls == 1 ? "per kill" : $"per roll, {table.mainRolls} rolls";
            foreach (var pair in perRoll.OrderByDescending(p => p.Value))
                Row(pair.Key, $"{OneIn(pair.Value)} {rolls}", null);
            if (nothing > 0d) Row("(nothing)", $"{OneIn(nothing)} {rolls}", null);

            foreach (LootChance chance in table.tertiary)
                Row(chance.itemId, $"1/{chance.oneIn} tertiary", chance.quantity);
        }

        // Adds each item's chance per Main roll, through sub-tables, and returns the chance of Nothing.
        private static double MainOdds(LootTable table, double reach, Dictionary<string, double> odds, int depth)
        {
            int total = table.MainWeight();
            if (total == 0 || depth >= LootRoller.MaxDepth) return reach;

            double nothing = 0d;
            foreach (LootEntry entry in table.main)
            {
                if (entry.weight <= 0) continue;

                double chance = reach * entry.weight / total;
                if (entry.subTable != null) nothing += MainOdds(entry.subTable, chance, odds, depth + 1);
                else if (string.IsNullOrEmpty(entry.itemId)) nothing += chance;
                else odds[entry.itemId] = odds.TryGetValue(entry.itemId, out double had) ? had + chance : chance;
            }
            return nothing;
        }

        private static string OneIn(double chance)
        {
            if (chance <= 0d) return "never";
            double n = 1d / chance;
            return n < 10d ? $"1/{n:0.#}" : $"1/{n:0}";
        }

        private static void Row(string itemId, string odds, LootQuantity quantity)
        {
            string amount = quantity != null ? $"  ×{quantity.Label()}" : "";
            EditorGUILayout.LabelField(string.IsNullOrEmpty(itemId) ? "(no item)" : itemId, odds + amount);
        }

        private void DrawSimulation(LootTable table)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simulate", EditorStyles.boldLabel);
            kills = Mathf.Max(1, EditorGUILayout.IntField("Kills", kills));
            if (GUILayout.Button($"Roll {kills:N0} kills")) simulated = Simulate(table, kills);
            if (simulated == null) return;

            foreach (SimRow row in simulated)
                EditorGUILayout.LabelField(row.itemId, $"{row.drops} drops (1/{(double)kills / row.drops:0.#})  ×{row.total} total");
        }

        private static List<SimRow> Simulate(LootTable table, int kills)
        {
            var rows = new Dictionary<string, SimRow>();
            var drops = new List<LootDrop>();

            for (int i = 0; i < kills; i++)
            {
                drops.Clear();
                LootRoller.Roll(table, drops);
                foreach (LootDrop drop in drops)
                {
                    rows.TryGetValue(drop.itemId, out SimRow row);
                    row.itemId = drop.itemId;
                    row.drops++;
                    row.total += drop.quantity;
                    rows[drop.itemId] = row;
                }
            }
            return rows.Values.OrderByDescending(r => r.drops).ToList();
        }
    }
}
