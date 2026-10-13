using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Progression
{
    // What a talent gives. Shared by every tree that places it (generics live in the library tree), so position
    // and prerequisites are on the placement, not here. Held under talent:{nodeId}, so taking a generic in a
    // second slot is refused rather than stacked (Talent_Trees §7).
    [CreateAssetMenu(fileName = "TalentNode", menuName = "NinjaGame/Talents/Node")]
    public class TalentNode : ScriptableObject
    {
        public string nodeId;
        public string displayName;
        public Sprite icon;
        public TalentCategory category;

        [TextArea(1, 4)] public string description;

        [Min(1)] public int maxRank = 1;
        [Min(1)] public int costPerRank = 1;

        [Tooltip("Applied while at least one rank is held. Rank is the reward's scale.")]
        public List<Reward> rewards = new();

        public List<KeywordUse> keywords = new();

        public string Label => string.IsNullOrEmpty(displayName) ? nodeId : displayName;
    }
}
