using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Progression
{
    // Every talent number in one place, read by TalentModule at runtime and by the validator and wizard in the
    // editor, so the tool and the game can't disagree (Talent_Wizard §1). Also the catalogue of trees, so a
    // save can name a tree by id.
    //
    // 10 Oct (Crxb): character level is the XP level again; each level pays points into one pool spent across
    // all slotted trees. Trees are 5 wide by 7 tall, each row opening after 5 more points in that tree.
    [CreateAssetMenu(fileName = "TalentRules", menuName = "NinjaGame/Talents/Rules")]
    public class TalentRules : ScriptableObject
    {
        [Header("Points")]
        public int slots = 3;
        [Tooltip("Talent points per character level, spent in any slotted tree.")]
        public int pointsPerLevel = 2;
        [Tooltip("Character level cap; the most points a character can hold is this × points per level.")]
        public int levelCap = 30;

        [Header("Grid")]
        [Tooltip("Points that must be spent in lower rows of the tree before each row opens, row 1 first.")]
        public int[] tierGates = { 0, 5, 10, 15, 20, 25, 30 };
        public int columns = 5;

        [Header("Trees")]
        [Tooltip("Every tree a save can name. The generic library is not slotted, so it isn't listed here.")]
        public List<TalentTree> trees = new();

        [Tooltip("Trees every new character already knows.")]
        public List<TalentTree> knownAtStart = new();

        public int Tiers => tierGates.Length;

        public int MaxPoints => pointsPerLevel * levelCap;

        public int Gate(int tier) => tier < 1 || tier > tierGates.Length ? 0 : tierGates[tier - 1];

        public TalentTree FindTree(string treeId)
        {
            if (string.IsNullOrEmpty(treeId)) return null;
            foreach (TalentTree tree in trees)
            {
                if (tree != null && tree.treeId == treeId) return tree;
            }
            return null;
        }
    }
}
