using System.Collections.Generic;
using UnityEngine;

// Rolls loot tables (Loot_Build.md §3). Pure: no scene and no MonoBehaviour, every roll through DiceRoll, so it
// can be tested, simulated in the editor and run on a server. Sub-tables stop at MaxDepth, so a loop the checker
// missed can't hang the game.

namespace CrabSystem
{
    public static class LootRoller
    {
        public const int MaxDepth = 4;

        public static List<LootDrop> Roll(LootTable table)
        {
            var drops = new List<LootDrop>();
            Roll(table, drops);
            return drops;
        }

        public static void Roll(LootTable table, List<LootDrop> drops)
        {
            if (table == null) return;

            foreach (LootLine line in table.always)
                Add(drops, line.itemId, line.quantity);

            for (int i = 0; i < table.mainRolls; i++)
                RollMain(table, drops, 0);

            foreach (LootChance chance in table.tertiary)
                if (chance.oneIn >= 1 && DiceRoll.Die(chance.oneIn) == 1) Add(drops, chance.itemId, chance.quantity);
        }

        public static void RollMain(LootTable table, List<LootDrop> drops, int depth)
        {
            if (depth >= MaxDepth)
            {
                Debug.LogError($"[LootRoller] {table.name}: sub-tables nest deeper than {MaxDepth}. Is there a loop?", table);
                return;
            }

            LootEntry entry = Pick(table.main, table.MainWeight());
            if (entry == null) return;

            if (entry.subTable != null)
            {
                RollMain(entry.subTable, drops, depth + 1);
                return;
            }

            Add(drops, entry.itemId, entry.quantity);
        }

        // A d(total) walked down the list; entries with no weight are never picked.
        public static LootEntry Pick(List<LootEntry> entries, int total)
        {
            if (total <= 0) return null;

            int roll = DiceRoll.Die(total);
            foreach (LootEntry entry in entries)
            {
                if (entry.weight <= 0) continue;
                roll -= entry.weight;
                if (roll <= 0) return entry;
            }
            return null;
        }

        private static void Add(List<LootDrop> drops, string itemId, LootQuantity quantity)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            drops.Add(new LootDrop(itemId, quantity != null ? quantity.Roll() : 1));
        }
    }
}
