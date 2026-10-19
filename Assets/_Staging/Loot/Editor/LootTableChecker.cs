using System.Collections.Generic;
using UnityEditor;

// The loot table rules (Loot_Build.md §3). Errors: an entry with both an item and a sub-table, a missing or
// unknown item id, a sub-table chain that loops. Warnings: a weight of 0, an empty Main list that is rolled,
// and a table used as a sub-table that carries Always or Tertiary lines (those are skipped when it's a sub-table).

namespace CrabSystem
{
    public static class LootTableChecker
    {
        public static void Check(LootTable table, List<string> errors, List<string> warnings)
        {
            CheckAlways(table, errors);
            CheckMain(table, errors, warnings);
            CheckTertiary(table, errors);

            if (Loops(table, table, new HashSet<LootTable>()))
                errors.Add("A sub-table chain leads back to this table. Rolls stop at the depth cap.");

            if (UsedAsSubTable(table) && (table.always.Count > 0 || table.tertiary.Count > 0))
                warnings.Add("Used as a sub-table elsewhere: its Always and Tertiary lines are skipped there.");
        }

        private static void CheckAlways(LootTable table, List<string> errors)
        {
            for (int i = 0; i < table.always.Count; i++)
                CheckItem(table.always[i].itemId, $"Always {i + 1}", errors);
        }

        private static void CheckTertiary(LootTable table, List<string> errors)
        {
            for (int i = 0; i < table.tertiary.Count; i++)
                CheckItem(table.tertiary[i].itemId, $"Tertiary {i + 1}", errors);
        }

        private static void CheckMain(LootTable table, List<string> errors, List<string> warnings)
        {
            if (table.mainRolls > 0 && table.MainWeight() == 0)
                warnings.Add("Main is rolled but has no weight, so it never drops anything.");

            for (int i = 0; i < table.main.Count; i++)
            {
                LootEntry entry = table.main[i];
                string where = $"Main {i + 1}";

                if (entry.weight == 0) warnings.Add($"{where} has weight 0 and never drops.");
                if (!string.IsNullOrEmpty(entry.itemId) && entry.subTable != null)
                    errors.Add($"{where} has both an item and a sub-table. Pick one.");
                if (entry.subTable == null && !string.IsNullOrEmpty(entry.itemId))
                    CheckItem(entry.itemId, where, errors);
            }
        }

        private static void CheckItem(string itemId, string where, List<string> errors)
        {
            if (string.IsNullOrEmpty(itemId)) errors.Add($"{where} has no item.");
            else if (!IdSources.Exists(IdKind.Item, itemId)) errors.Add($"{where}: no item with id '{itemId}'.");
        }

        private static bool Loops(LootTable start, LootTable current, HashSet<LootTable> seen)
        {
            if (!seen.Add(current)) return false;

            foreach (LootEntry entry in current.main)
            {
                if (entry.subTable == null) continue;
                if (entry.subTable == start) return true;
                if (Loops(start, entry.subTable, seen)) return true;
            }
            return false;
        }

        private static bool UsedAsSubTable(LootTable table)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:LootTable"))
            {
                var other = AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other == table) continue;
                foreach (LootEntry entry in other.main)
                    if (entry.subTable == table) return true;
            }
            return false;
        }

        [MenuItem("Tools/Loot/Check Loot Tables")]
        private static void CheckAll()
        {
            int problems = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:LootTable"))
            {
                var table = AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (table == null) continue;

                var errors = new List<string>();
                var warnings = new List<string>();
                Check(table, errors, warnings);
                foreach (string error in errors) UnityEngine.Debug.LogError($"[Loot] {table.name}: {error}", table);
                foreach (string warning in warnings) UnityEngine.Debug.LogWarning($"[Loot] {table.name}: {warning}", table);
                problems += errors.Count + warnings.Count;
            }
            UnityEngine.Debug.Log($"[Loot] Checked every loot table: {problems} problems.");
        }
    }
}
