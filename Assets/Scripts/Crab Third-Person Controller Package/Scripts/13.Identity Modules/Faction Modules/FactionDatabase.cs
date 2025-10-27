using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Factions
{
    /// <summary>
    /// Central database for all faction definitions.
    /// Provides fast lookup and validation for faction data.
    /// </summary>
    [CreateAssetMenu(fileName = "FactionDatabase", menuName = "RPG/Factions/Faction Database")]
    public class FactionDatabase : ScriptableObject
    {
        [Header("Database")]
        [Tooltip("All faction definitions in the game")]
        public List<FactionDefinition> factions = new List<FactionDefinition>();

        [Header("Debug")]
        [Tooltip("Enable detailed logging for faction lookups")]
        public bool debugMode = false;

        // Cache for fast lookups
        private Dictionary<FactionType, FactionDefinition> factionCache;

        /// <summary>
        /// Initialize the database cache.
        /// Call this on game start or when database changes.
        /// </summary>
        public void Initialize()
        {
            BuildCache();

            if (debugMode)
            {
                Debug.Log($"[FactionDatabase] Initialized with {factions.Count} factions");
            }
        }

        /// <summary>
        /// Build internal cache for fast lookups.
        /// </summary>
        private void BuildCache()
        {
            factionCache = new Dictionary<FactionType, FactionDefinition>();

            foreach (var faction in factions)
            {
                if (faction == null)
                {
                    Debug.LogWarning("[FactionDatabase] Null faction found in database!");
                    continue;
                }

                if (factionCache.ContainsKey(faction.factionType))
                {
                    Debug.LogWarning($"[FactionDatabase] Duplicate faction type {faction.factionType}! Using first occurrence.");
                    continue;
                }

                factionCache[faction.factionType] = faction;
            }
        }

        // ==================== FACTION LOOKUP ====================

        /// <summary>
        /// Get faction definition by type.
        /// </summary>
        public FactionDefinition GetFaction(FactionType type)
        {
            // Lazy initialization
            if (factionCache == null || factionCache.Count == 0)
            {
                BuildCache();
            }

            if (factionCache.TryGetValue(type, out FactionDefinition faction))
            {
                return faction;
            }

            if (debugMode)
            {
                Debug.LogWarning($"[FactionDatabase] No definition found for faction type: {type}");
            }

            return null;
        }

        /// <summary>
        /// Get display name for a faction type.
        /// </summary>
        public string GetFactionName(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.displayName : type.ToString();
        }

        /// <summary>
        /// Get nameplate color for a faction type (generic, not player-specific).
        /// </summary>
        public Color GetNameplateColor(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.nameplateColor : Color.white;
        }

        // ==================== RELATIONSHIP QUERIES ====================

        /// <summary>
        /// Get relationship between two factions.
        /// </summary>
        public FactionRelationship GetRelationship(FactionType faction1, FactionType faction2)
        {
            // Same faction is always friendly
            if (faction1 == faction2)
                return FactionRelationship.Friendly;

            var factionDef = GetFaction(faction1);
            if (factionDef == null)
                return FactionRelationship.Neutral;

            return factionDef.GetRelationshipWith(faction2);
        }

        /// <summary>
        /// Check if two factions are hostile.
        /// </summary>
        public bool AreFactionsHostile(FactionType faction1, FactionType faction2)
        {
            return GetRelationship(faction1, faction2) == FactionRelationship.Hostile;
        }

        /// <summary>
        /// Check if two factions are allied.
        /// </summary>
        public bool AreFactionsAllied(FactionType faction1, FactionType faction2)
        {
            return GetRelationship(faction1, faction2) == FactionRelationship.Friendly;
        }

        // ==================== PLAYER REPUTATION ====================

        /// <summary>
        /// Get player-specific nameplate color based on reputation.
        /// </summary>
        public Color GetPlayerNameplateColor(FactionType factionType, int playerReputation)
        {
            var faction = GetFaction(factionType);
            if (faction == null)
                return Color.white;

            return faction.GetPlayerNameplateColor(playerReputation);
        }

        /// <summary>
        /// Get default player reputation for a faction.
        /// </summary>
        public int GetDefaultPlayerReputation(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.defaultPlayerReputation : 0;
        }

        /// <summary>
        /// Get faction rank for reputation value.
        /// </summary>
        public FactionRank GetRankForReputation(FactionType type, int reputation)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.GetRankForReputation(reputation) : FactionRank.Neutral;
        }

        // ==================== FACTION MEMBERSHIP ====================

        /// <summary>
        /// Check if player can join a faction.
        /// </summary>
        public bool CanJoinFaction(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null && faction.canJoinFaction;
        }

        /// <summary>
        /// Get all factions that player can join.
        /// </summary>
        public List<FactionDefinition> GetJoinableFactions()
        {
            return factions.Where(f => f != null && f.canJoinFaction).ToList();
        }

        // ==================== REWARDS ====================

        /// <summary>
        /// Get available rewards for a faction at specific reputation.
        /// </summary>
        public List<FactionRankReward> GetAvailableRewards(FactionType type, int reputation)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.GetAvailableRewards(reputation) : new List<FactionRankReward>();
        }

        // ==================== VENDOR & SERVICES ====================

        /// <summary>
        /// Check if faction has a vendor.
        /// </summary>
        public bool HasVendor(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null && faction.hasVendor;
        }

        /// <summary>
        /// Get vendor NPC ID for faction.
        /// </summary>
        public string GetVendorNpcId(FactionType type)
        {
            var faction = GetFaction(type);
            return faction != null ? faction.vendorNpcId : "";
        }

        // ==================== QUERY UTILITIES ====================

        /// <summary>
        /// Get all factions with specific relationship type to target faction.
        /// </summary>
        public List<FactionDefinition> GetFactionsWithRelationship(FactionType targetFaction, FactionRelationship relationshipType)
        {
            var results = new List<FactionDefinition>();

            foreach (var faction in factions)
            {
                if (faction == null) continue;

                if (faction.GetRelationshipWith(targetFaction) == relationshipType)
                {
                    results.Add(faction);
                }
            }

            return results;
        }

        /// <summary>
        /// Get all enemies of a faction.
        /// </summary>
        public List<FactionDefinition> GetEnemiesOf(FactionType factionType)
        {
            return GetFactionsWithRelationship(factionType, FactionRelationship.Hostile);
        }

        /// <summary>
        /// Get all allies of a faction.
        /// </summary>
        public List<FactionDefinition> GetAlliesOf(FactionType factionType)
        {
            return GetFactionsWithRelationship(factionType, FactionRelationship.Friendly);
        }

        // ==================== VALIDATION & DEBUG ====================

#if UNITY_EDITOR
        [ContextMenu("Validate Database")]
        private void ValidateDatabase()
        {
            Debug.Log("=== Faction Database Validation ===");
            
            // Check for nulls
            int nullCount = factions.Count(f => f == null);
            if (nullCount > 0)
            {
                Debug.LogWarning($"Found {nullCount} null faction entries!");
            }
            
            // Check for missing definitions
            var allEnumValues = System.Enum.GetValues(typeof(FactionType)).Cast<FactionType>();
            var definedTypes = factions.Where(f => f != null).Select(f => f.factionType).ToHashSet();
            
            foreach (var enumValue in allEnumValues)
            {
                if (enumValue == FactionType.None) continue;
                
                if (!definedTypes.Contains(enumValue))
                {
                    Debug.LogWarning($"Missing definition for faction type: {enumValue}");
                }
            }
            
            // Check for duplicates
            var duplicates = factions
                .Where(f => f != null)
                .GroupBy(f => f.factionType)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            
            foreach (var duplicate in duplicates)
            {
                Debug.LogWarning($"Duplicate definitions found for: {duplicate}");
            }
            
            Debug.Log($"Validation complete. Total factions: {factions.Count - nullCount}");
        }
        
        [ContextMenu("Debug: Print All Relationships")]
        private void DebugPrintRelationships()
        {
            Debug.Log("=== All Faction Relationships ===");
            
            foreach (var faction in factions)
            {
                if (faction == null) continue;
                
                Debug.Log($"\n{faction.displayName} ({faction.factionType}):");
                
                if (faction.relationships.Count == 0)
                {
                    Debug.Log("  No relationships defined");
                    continue;
                }
                
                foreach (var rel in faction.relationships)
                {
                    string color = rel.relationshipType switch
                    {
                        FactionRelationship.Friendly => "green",
                        FactionRelationship.Hostile => "red",
                        _ => "yellow"
                    };
                    
                    Debug.Log($"  <color={color}>→ {rel.targetFaction}: {rel.relationshipType}</color>");
                }
            }
        }
        
        [ContextMenu("Debug: Print Faction Summary")]
        private void DebugPrintSummary()
        {
            BuildCache();
            
            Debug.Log("=== Faction Database Summary ===");
            Debug.Log($"Total Factions: {factions.Count}");
            Debug.Log($"Cached Factions: {factionCache.Count}");
            Debug.Log($"Joinable Factions: {GetJoinableFactions().Count}");
            Debug.Log($"Factions with Vendors: {factions.Count(f => f != null && f.hasVendor)}");
        }
#endif
    }
}