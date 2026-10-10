using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Progression
{
    // TargetHasStatus is Exploit (+1d6 vs Prone); SelfHasStatus is Thrive (while Hasted).
    public enum KeywordCondition { Always, FromBehind, TargetHasStatus, SelfHasStatus }

    [Serializable]
    public struct ResourceGain
    {
        public ResourceDefinition resource;
        public float amount;
    }

    // A keyword an ability can carry — Sinister, Ambushing (Crxb, 10 Oct). The keyword is a tag: an ability has it
    // when its asset lists it, or when a socket or a talent grants it to that ability for one character. While an
    // ability carries it, the keyword's on-hit gains fire on every hit it lands, and its hit bonuses apply when the
    // condition holds. Talents shape a keyword through ordinary rewards: AbilityModifierReward on the tag (cost,
    // cooldown) and KeywordBonusReward (accuracy, dice, flat damage).
    //
    // Keywords live in a Resources folder named Keywords so a character can look one up by id.
    [CreateAssetMenu(fileName = "Keyword_", menuName = "NinjaGame/Talents/Keyword")]
    public class KeywordDefinition : ScriptableObject
    {
        [Tooltip("Lower case; also the tag abilities carry, e.g. sinister.")]
        public string keywordId;
        public string displayName;
        public Sprite icon;
        [TextArea(1, 4)] public string description;

        [Tooltip("Tree that owns it. Empty = shared by any tree.")]
        public TalentTree ownerTree;

        [Header("On hit")]
        [Tooltip("Granted to the attacker every time an ability carrying this keyword lands a hit — a generator.")]
        public List<ResourceGain> onHit = new();

        [Header("Condition (hit bonus and inflict)")]
        public KeywordCondition condition = KeywordCondition.Always;
        [Tooltip("FromBehind: the attacker counts as behind outside this many degrees of the defender's facing.")]
        [Range(90f, 180f)] public float behindAngle = 120f;
        [Tooltip("TargetHasStatus / SelfHasStatus: the status that must be on the target, or on you.")]
        public StatusDefinition conditionStatus;

        [Header("Inflict")]
        [Tooltip("Applied to the target on a hit that isn't guarded, when the condition holds. Talents add more.")]
        public List<StatusDefinition> inflict = new();

        [Header("Hit bonus")]
        [Tooltip("Added to the hit's to-hit when the condition holds.")]
        public int accuracy;
        [Tooltip("Roll the hit with advantage when the condition holds.")]
        public bool advantage;
        [Tooltip("Bonus dice rolled into the hit's damage when the condition holds; talents add more.")]
        public int bonusDice;
        public int bonusDieFaces = 4;

        public string Label => string.IsNullOrEmpty(displayName) ? keywordId : displayName;
    }
}
