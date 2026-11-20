using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// A feat (Feats.md): a named bundle of rewards, translated from D&D. Static authored data only.
// Feats are never picked from a pool; something grants them through GrantFeatReward (a trait, a talent node, a race,
// and later a status), and the feat lasts as long as that grant does.

namespace CrabSystem
{
    public enum FeatCategory { Origin, General, FightingStyle, Epic, Racial, Weapon }

    [CreateAssetMenu(fileName = "Feat_", menuName = "CrabSystem/Feats/Feat")]
    public class FeatDefinition : ScriptableObject
    {
        public string featId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public FeatCategory category;

        [Tooltip("Granted twice from two sources, a repeatable feat applies twice (Ability Score Improvement). " +
                 "Any other feat applies once however many things grant it.")]
        public bool repeatable;

        [Tooltip("Applied with Scale = character level, so a Stat Rate reward reads as 'per level' (Tough).")]
        public List<Reward> rewards = new();
    }
}
