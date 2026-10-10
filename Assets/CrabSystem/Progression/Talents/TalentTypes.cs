using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Progression
{
    // Talent_Trees.md §7–§8 and Talent_Wizard.md §1. A node is *what* (TalentNode), a placement is *where in
    // this tree* (TalentPlacement), a tree is the grid (TalentTree).

    public enum TalentCategory { Generic, Keyword, Modifier, Ability, Capability, Trigger, Capstone }

    public enum KeywordVerb { Inflict, Exploit, Intensify, Spread, Resist, Immune, Grant, Thrive }

    // Declaration, not mechanism: the node's rewards do the work. Lets the validator check keyword ownership
    // and the tooltip say "Inflicts Grappled" without reading rewards.
    [Serializable]
    public struct KeywordUse
    {
        [IdRef(IdKind.Status)] public string statusId;
        public KeywordVerb verb;
    }

    [Serializable]
    public class TalentPlacement
    {
        public TalentNode node;

        [Tooltip("Row, 1 at the bottom. Its gate is the points spent in lower rows of this tree (TalentRules.tierGates).")]
        [Min(1)] public int tier = 1;

        [Tooltip("Cell in the tier's row, 0 on the left.")]
        public int column;

        [Tooltip("Nodes in lower tiers of this tree that must hold at least one rank first.")]
        public List<TalentNode> requires = new();

        [Tooltip("Pick-one partner: taking either locks the other.")]
        public TalentNode exclusiveWith;
    }
}
