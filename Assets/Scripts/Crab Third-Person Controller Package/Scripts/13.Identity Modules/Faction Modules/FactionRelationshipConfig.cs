using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Factions
{
    /// <summary>
    /// Simple ScriptableObject to define faction relationships in the Inspector.
    /// NO reputation, NO rewards, NO vendors - just combat relationships.
    /// 
    /// Usage:
    /// 1. Create asset: Right-click → Create → RPG/Factions/Faction Relationships
    /// 2. Add relationships in Inspector (drag-and-drop friendly)
    /// 3. Assign to FactionManager
    /// </summary>
    [CreateAssetMenu(fileName = "FactionRelationships", menuName = "RPG/Factions/Faction Relationships")]
    public class FactionRelationshipConfig : ScriptableObject
    {
        [Header("Faction Relationships")]
        [Tooltip("Define how each faction views other factions")]
        public List<FactionRelationshipEntry> relationships = new List<FactionRelationshipEntry>();

        [Header("Default Behavior")]
        [Tooltip("What relationship to use if none is defined?")]
        public FactionRelationship defaultRelationship = FactionRelationship.Neutral;

        [Header("Debug")]
        public bool showDebugLogs = false;

        // Cache for fast lookups
        private Dictionary<(FactionType, FactionType), FactionRelationship> relationshipCache;

        /// <summary>
        /// Initialize the cache for fast lookups.
        /// Called by FactionManager on startup.
        /// </summary>
        public void Initialize()
        {
            BuildCache();

            if (showDebugLogs)
            {
                Debug.Log($"[FactionRelationshipConfig] Initialized with {relationships.Count} relationship entries");
            }
        }

        /// <summary>
        /// Build the lookup cache.
        /// </summary>
        private void BuildCache()
        {
            relationshipCache = new Dictionary<(FactionType, FactionType), FactionRelationship>();

            foreach (var entry in relationships)
            {
                if (entry == null) continue;

                // Add bidirectional relationships
                relationshipCache[(entry.faction1, entry.faction2)] = entry.relationship;
                relationshipCache[(entry.faction2, entry.faction1)] = entry.relationship;
            }
        }

        /// <summary>
        /// Get relationship between two factions.
        /// </summary>
        public FactionRelationship GetRelationship(FactionType faction1, FactionType faction2)
        {
            // Lazy initialization
            if (relationshipCache == null || relationshipCache.Count == 0)
            {
                BuildCache();
            }

            // Same faction = always friendly
            if (faction1 == faction2)
                return FactionRelationship.Friendly;

            // Special override factions
            if (faction1 == FactionType.Friendly || faction2 == FactionType.Friendly)
                return FactionRelationship.Friendly;

            if (faction1 == FactionType.Hostile || faction2 == FactionType.Hostile)
                return FactionRelationship.Hostile;

            if (faction1 == FactionType.None || faction2 == FactionType.None)
                return FactionRelationship.Neutral;

            // Lookup in cache
            if (relationshipCache.TryGetValue((faction1, faction2), out FactionRelationship relationship))
            {
                return relationship;
            }

            // Use default if not found
            if (showDebugLogs)
            {
                Debug.LogWarning($"[FactionRelationshipConfig] No relationship defined between {faction1} and {faction2}. Using default: {defaultRelationship}");
            }

            return defaultRelationship;
        }

        /// <summary>
        /// Check if a relationship exists between two factions.
        /// </summary>
        public bool HasRelationship(FactionType faction1, FactionType faction2)
        {
            if (relationshipCache == null || relationshipCache.Count == 0)
            {
                BuildCache();
            }

            return relationshipCache.ContainsKey((faction1, faction2));
        }

        // ==================== EDITOR UTILITIES ====================

#if UNITY_EDITOR
        [ContextMenu("Validate Relationships")]
        private void ValidateRelationships()
        {
            Debug.Log("=== Faction Relationship Validation ===");

            // Check for duplicates
            var duplicates = relationships
                .GroupBy(r => (r.faction1, r.faction2))
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var dup in duplicates)
            {
                Debug.LogWarning($"Duplicate relationship: {dup.faction1} ↔ {dup.faction2}");
            }

            // Check for self-references
            var selfRefs = relationships.Where(r => r.faction1 == r.faction2).ToList();
            if (selfRefs.Count > 0)
            {
                Debug.LogWarning($"Found {selfRefs.Count} self-referencing relationships (factions relating to themselves)");
            }

            Debug.Log($"Total relationships: {relationships.Count}");
            Debug.Log($"Unique faction pairs: {relationships.Select(r => (r.faction1, r.faction2)).Distinct().Count()}");
        }

        [ContextMenu("Debug: Print All Relationships")]
        private void DebugPrintAllRelationships()
        {
            BuildCache();

            Debug.Log("=== ALL FACTION RELATIONSHIPS ===");

            var allFactions = System.Enum.GetValues(typeof(FactionType)).Cast<FactionType>()
                .Where(f => f != FactionType.None).ToList();

            foreach (var faction1 in allFactions)
            {
                Debug.Log($"\n--- {faction1} ---");

                foreach (var faction2 in allFactions)
                {
                    if (faction1 == faction2) continue;

                    var rel = GetRelationship(faction1, faction2);
                    string color = rel switch
                    {
                        FactionRelationship.Friendly => "green",
                        FactionRelationship.Hostile => "red",
                        _ => "yellow"
                    };

                    Debug.Log($"  <color={color}>{faction1} → {faction2}: {rel}</color>");
                }
            }
        }

        [ContextMenu("Quick Setup: Create Default Relationships")]
        private void CreateDefaultRelationships()
        {
            relationships.Clear();

            // Player relationships
            AddEntry(FactionType.Player, FactionType.Elves, FactionRelationship.Friendly);
            AddEntry(FactionType.Player, FactionType.Humans, FactionRelationship.Friendly);
            AddEntry(FactionType.Player, FactionType.Dwarves, FactionRelationship.Friendly);
            AddEntry(FactionType.Player, FactionType.Warlocks, FactionRelationship.Hostile);
            AddEntry(FactionType.Player, FactionType.Undead, FactionRelationship.Hostile);
            AddEntry(FactionType.Player, FactionType.Bandits, FactionRelationship.Hostile);
            AddEntry(FactionType.Player, FactionType.Monsters, FactionRelationship.Hostile);
            AddEntry(FactionType.Player, FactionType.Wildlife, FactionRelationship.Neutral);

            // Civilized factions (allied)
            AddEntry(FactionType.Elves, FactionType.Humans, FactionRelationship.Friendly);
            AddEntry(FactionType.Elves, FactionType.Dwarves, FactionRelationship.Friendly);
            AddEntry(FactionType.Humans, FactionType.Dwarves, FactionRelationship.Friendly);

            // Civilized vs Evil
            AddEntry(FactionType.Elves, FactionType.Warlocks, FactionRelationship.Hostile);
            AddEntry(FactionType.Elves, FactionType.Undead, FactionRelationship.Hostile);
            AddEntry(FactionType.Elves, FactionType.Bandits, FactionRelationship.Hostile);
            AddEntry(FactionType.Humans, FactionType.Warlocks, FactionRelationship.Hostile);
            AddEntry(FactionType.Humans, FactionType.Undead, FactionRelationship.Hostile);
            AddEntry(FactionType.Humans, FactionType.Bandits, FactionRelationship.Hostile);
            AddEntry(FactionType.Dwarves, FactionType.Warlocks, FactionRelationship.Hostile);
            AddEntry(FactionType.Dwarves, FactionType.Undead, FactionRelationship.Hostile);
            AddEntry(FactionType.Dwarves, FactionType.Bandits, FactionRelationship.Hostile);

            // Evil factions (allied)
            AddEntry(FactionType.Warlocks, FactionType.Undead, FactionRelationship.Friendly);
            AddEntry(FactionType.Warlocks, FactionType.Bandits, FactionRelationship.Friendly);
            AddEntry(FactionType.Undead, FactionType.Bandits, FactionRelationship.Friendly);

            Debug.Log($"Created {relationships.Count} default relationships");
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void AddEntry(FactionType faction1, FactionType faction2, FactionRelationship relationship)
        {
            relationships.Add(new FactionRelationshipEntry
            {
                faction1 = faction1,
                faction2 = faction2,
                relationship = relationship
            });
        }
#endif
    }

    /// <summary>
    /// Single relationship entry between two factions.
    /// Displayed in Inspector as a clean line item.
    /// </summary>
    [System.Serializable]
    public class FactionRelationshipEntry
    {
        [Tooltip("First faction in the relationship")]
        public FactionType faction1;

        [Tooltip("Second faction in the relationship")]
        public FactionType faction2;

        [Tooltip("How these factions view each other (bidirectional)")]
        public FactionRelationship relationship = FactionRelationship.Neutral;

        [Tooltip("Optional notes about this relationship")]
        [TextArea(1, 3)]
        public string notes = "";
    }
}