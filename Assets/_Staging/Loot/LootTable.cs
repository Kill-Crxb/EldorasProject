using System;
using System.Collections.Generic;
using UnityEngine;

// An OSRS-style loot table (Loot_Build.md §3). Always drops on every kill. Main is rolled mainRolls times on a
// weighted list, where an entry with no item and no sub-table is "Nothing". Tertiary rolls each line on its own,
// 1 in N. A Main entry can point at another table: that table's Main list is rolled once and its Always and
// Tertiary are skipped, so a gem table or a rare drop table is authored once and shared. Weights are whole
// numbers out of the list's total, never percentages. Quantities are dice.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "LootTable_New", menuName = "CrabSystem/Loot/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Tooltip("Drops on every kill.")]
        public List<LootLine> always = new List<LootLine>();

        [Tooltip("How many times the Main list is rolled per kill.")]
        [Min(0)] public int mainRolls = 1;

        [Tooltip("Weighted. Odds are weight out of the list's total. Leave item and sub-table empty for Nothing.")]
        public List<LootEntry> main = new List<LootEntry>();

        [Tooltip("Rolled one by one on top of Main: 1 in N each.")]
        public List<LootChance> tertiary = new List<LootChance>();

        public int MainWeight()
        {
            int total = 0;
            foreach (LootEntry entry in main)
                if (entry.weight > 0) total += entry.weight;
            return total;
        }
    }

    [Serializable]
    public class LootEntry
    {
        [Min(0)] public int weight = 1;
        [IdRef(IdKind.Item)] public string itemId;
        [Tooltip("Rolled once, Main list only, in place of an item.")]
        public LootTable subTable;
        public LootQuantity quantity = new LootQuantity();

        public bool IsNothing => string.IsNullOrEmpty(itemId) && subTable == null;
    }

    [Serializable]
    public class LootLine
    {
        [IdRef(IdKind.Item)] public string itemId;
        public LootQuantity quantity = new LootQuantity();
    }

    [Serializable]
    public class LootChance
    {
        [Tooltip("Drops on a 1 rolled on a d(oneIn).")]
        [Min(1)] public int oneIn = 100;
        [IdRef(IdKind.Item)] public string itemId;
        public LootQuantity quantity = new LootQuantity();
    }

    // dice d faces + flat, at least 1. The default is a flat 1; "3d6" is dice 3, faces 6, flat 0.
    [Serializable]
    public class LootQuantity
    {
        [Min(0)] public int dice;
        [Min(2)] public int faces = 6;
        public int flat = 1;

        public int Roll()
        {
            int total = flat;
            for (int i = 0; i < dice; i++) total += DiceRoll.Die(faces);
            return Mathf.Max(1, total);
        }

        public float Average() => Mathf.Max(1f, flat + dice * (faces + 1) * 0.5f);

        public string Label()
        {
            if (dice <= 0) return Mathf.Max(1, flat).ToString();
            string roll = $"{dice}d{faces}";
            if (flat == 0) return roll;
            return flat > 0 ? $"{roll}+{flat}" : $"{roll}{flat}";
        }
    }

    public readonly struct LootDrop
    {
        public readonly string itemId;
        public readonly int quantity;

        public LootDrop(string itemId, int quantity)
        {
            this.itemId = itemId;
            this.quantity = quantity;
        }
    }
}
