using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Progression
{
    // A class: a grid of placed talents the player spends points into (Talent_Trees §6–§8). The resource budget
    // and level track were dropped on 10 Oct (Crxb) — trees are their talents.
    [CreateAssetMenu(fileName = "TalentTree", menuName = "NinjaGame/Talents/Tree")]
    public class TalentTree : ScriptableObject
    {
        public string treeId;
        public string displayName;
        public Sprite icon;
        [TextArea(1, 4)] public string description;

        [Tooltip("The library of generics is placed into other trees and never slotted.")]
        public bool isLibrary;

        [Header("Keywords (§8)")]
        [Tooltip("Two harmful and two beneficial status ids, at most one Hard tier. Only the owner Inflicts and Grants them.")]
        [IdRef(IdKind.Status)] public List<string> ownedKeywords = new();

        [Header("Talents")]
        public List<TalentPlacement> placements = new();

        public string Label => string.IsNullOrEmpty(displayName) ? treeId : displayName;

        public TalentPlacement Find(TalentNode node)
        {
            if (node == null) return null;
            foreach (TalentPlacement placement in placements)
            {
                if (placement != null && placement.node == node) return placement;
            }
            return null;
        }

        public TalentPlacement Find(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            foreach (TalentPlacement placement in placements)
            {
                if (placement?.node != null && placement.node.nodeId == nodeId) return placement;
            }
            return null;
        }

        public TalentPlacement At(int tier, int column)
        {
            foreach (TalentPlacement placement in placements)
            {
                if (placement != null && placement.tier == tier && placement.column == column) return placement;
            }
            return null;
        }
    }
}
