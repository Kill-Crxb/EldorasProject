using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Factions
{
    /// <summary>
    /// Asset-keyed faction relationship matrix.
    ///
    /// Entries reference FactionDefinition assets directly — no enum, no string
    /// matching. Adding a faction to the game requires zero code changes:
    /// create the FactionDefinition asset and author its rows here.
    ///
    /// Usage:
    /// 1. Create asset: Right-click → Create → RPG/Factions/Faction Relationships
    /// 2. Add entries: drag two FactionDefinition assets + pick a stance
    /// 3. Assign to FactionManager on Manager_Brain
    ///
    /// Unlisted pairs fall back to defaultRelationship.
    /// Same faction on both sides is always Friendly.
    /// </summary>
    [CreateAssetMenu(fileName = "FactionRelationships", menuName = "RPG/Factions/Faction Relationships")]
    public class FactionRelationshipConfig : ScriptableObject
    {
        [Header("Faction Relationships")]
        [Tooltip("Define how each faction pair relates. Entries are bidirectional.")]
        public List<FactionRelationshipEntry> relationships = new List<FactionRelationshipEntry>();

        [Header("Default Behavior")]
        [Tooltip("Relationship used when no entry exists for a pair.")]
        public FactionRelationship defaultRelationship = FactionRelationship.Neutral;

        [Header("Debug")]
        public bool showDebugLogs = false;

        private Dictionary<(FactionDefinition, FactionDefinition), FactionRelationshipEntry> entryCache;

        /// <summary>Initialize the lookup cache. Called by FactionManager on startup.</summary>
        public void Initialize()
        {
            BuildCache();

            if (showDebugLogs)
                Debug.Log($"[FactionRelationshipConfig] Initialized with {relationships.Count} relationship entries");
        }

        private void BuildCache()
        {
            entryCache = new Dictionary<(FactionDefinition, FactionDefinition), FactionRelationshipEntry>();

            foreach (var entry in relationships)
            {
                if (entry == null || entry.faction1 == null || entry.faction2 == null)
                    continue;

                entryCache[(entry.faction1, entry.faction2)] = entry;
                entryCache[(entry.faction2, entry.faction1)] = entry;
            }
        }

        /// <summary>Stance between two factions. Null faction on either side → Neutral.</summary>
        public FactionRelationship GetRelationship(FactionDefinition a, FactionDefinition b)
        {
            if (a == null || b == null)
                return FactionRelationship.Neutral;

            if (a == b)
                return FactionRelationship.Friendly;

            var entry = GetEntry(a, b);
            if (entry != null)
                return entry.relationship;

            if (showDebugLogs)
                Debug.LogWarning($"[FactionRelationshipConfig] No relationship defined between {a.name} and {b.name}. Using default: {defaultRelationship}");

            return defaultRelationship;
        }

        /// <summary>Full entry for a pair (stance + modifiers), or null if unlisted.</summary>
        public FactionRelationshipEntry GetEntry(FactionDefinition a, FactionDefinition b)
        {
            if (a == null || b == null)
                return null;

            if (entryCache == null || entryCache.Count == 0)
                BuildCache();

            entryCache.TryGetValue((a, b), out var entry);
            return entry;
        }

        /// <summary>Damage multiplier for attacker → defender. 1.0 when unlisted or same faction.</summary>
        public float GetDamageMultiplier(FactionDefinition attacker, FactionDefinition defender)
        {
            var entry = GetEntry(attacker, defender);
            return entry != null ? entry.damageMultiplier : 1f;
        }

        public bool HasRelationship(FactionDefinition a, FactionDefinition b)
            => GetEntry(a, b) != null;

        // ==================== EDITOR UTILITIES ====================

#if UNITY_EDITOR
        [ContextMenu("Validate Relationships")]
        private void ValidateRelationships()
        {
            Debug.Log("=== Faction Relationship Validation ===");

            int nullRefs = relationships.Count(r => r == null || r.faction1 == null || r.faction2 == null);
            if (nullRefs > 0)
                Debug.LogWarning($"{nullRefs} entries have missing FactionDefinition references");

            var duplicates = relationships
                .Where(r => r != null && r.faction1 != null && r.faction2 != null)
                .GroupBy(r => r.faction1.GetInstanceID() < r.faction2.GetInstanceID()
                    ? (r.faction1, r.faction2)
                    : (r.faction2, r.faction1))
                .Where(g => g.Count() > 1);

            foreach (var dup in duplicates)
                Debug.LogWarning($"Duplicate relationship: {dup.Key.Item1.name} ↔ {dup.Key.Item2.name}");

            var selfRefs = relationships.Count(r => r != null && r.faction1 != null && r.faction1 == r.faction2);
            if (selfRefs > 0)
                Debug.LogWarning($"Found {selfRefs} self-referencing entries (same faction always Friendly — remove them)");

            Debug.Log($"Total relationships: {relationships.Count}");
        }

        [ContextMenu("Debug: Print All Relationships")]
        private void DebugPrintAllRelationships()
        {
            BuildCache();

            Debug.Log("=== ALL FACTION RELATIONSHIPS ===");
            foreach (var entry in relationships)
            {
                if (entry == null || entry.faction1 == null || entry.faction2 == null) continue;

                string color = entry.relationship switch
                {
                    FactionRelationship.Friendly => "green",
                    FactionRelationship.Hostile => "red",
                    _ => "yellow"
                };
                Debug.Log($"<color={color}>{entry.faction1.name} ↔ {entry.faction2.name}: {entry.relationship}</color> (dmg ×{entry.damageMultiplier})");
            }
        }
#endif
    }

    [System.Serializable]
    public class FactionRelationshipEntry
    {
        public FactionDefinition faction1;
        public FactionDefinition faction2;
        public FactionRelationship relationship = FactionRelationship.Neutral;

        [Tooltip("Damage multiplier applied when these factions fight each other. 1 = normal.")]
        public float damageMultiplier = 1f;

        [Tooltip("Will members of one faction assist the other when it is attacked?")]
        public bool canAssist = false;

        public string notes;
    }
}
