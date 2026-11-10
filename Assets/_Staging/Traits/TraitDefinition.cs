using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// A birth trait (Character_Roll_Traits.md). Static authored data only; who has it lives in TraitModule's save.
// Its effect is its rewards, applied under trait:{id}:{index}. A trait whose effect Rewards can't express gets a
// Reward subclass of its own, so every trait comes off exactly the same way.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Trait_", menuName = "CrabSystem/Traits/Trait")]
    public class TraitDefinition : ScriptableObject
    {
        public string traitId;
        public string displayName;
        [TextArea] public string description;

        public Sprite icon;
        [Tooltip("Frame drawn over the icon on trait cards. Tint recolours it, so one frame can serve several looks.")]
        public Sprite border;
        public Color borderTint = Color.white;

        [Tooltip("0 = active from level 1. Higher = dormant (shown as ???) until the character reaches this level.")]
        [Min(0)] public int revealLevel;

        public List<Reward> rewards = new();

        public bool IsDormantAt(int level) => revealLevel > 0 && level < revealLevel;
    }
}
