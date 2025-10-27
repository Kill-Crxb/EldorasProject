using UnityEngine;
using System.Collections.Generic;

namespace RPG.Factions
{
    /// <summary>
    /// Singleton manager for the faction system.
    /// NOW ENHANCED: Uses FactionDatabase for data-driven faction management.
    /// Maintains backward compatibility with existing code.
    /// 
    /// Migration from old system:
    /// - Old: Hardcoded relationships in InitializeRelationships()
    /// - New: Relationships loaded from FactionDatabase ScriptableObject
    /// </summary>
    public class FactionManager : MonoBehaviour
    {
        [Header("Database")]
        [Tooltip("The faction database containing all faction definitions")]
        [SerializeField] private FactionDatabase factionDatabase;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Singleton pattern
        private static FactionManager _instance;
        public static FactionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<FactionManager>();
                    if (_instance == null)
                    {
                        Debug.LogWarning("[FactionManager] No FactionManager found in scene! Creating temporary instance.");
                        GameObject go = new GameObject("FactionManager (Auto-Created)");
                        _instance = go.AddComponent<FactionManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // Singleton enforcement
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize database
            InitializeDatabase();
        }

        /// <summary>
        /// Initialize the faction database.
        /// </summary>
        private void InitializeDatabase()
        {
            if (factionDatabase == null)
            {
                Debug.LogError("[FactionManager] No FactionDatabase assigned! Please assign a FactionDatabase in the inspector.");
                Debug.LogWarning("[FactionManager] Faction queries will return default values until database is assigned.");
                return;
            }

            factionDatabase.Initialize();

            if (enableDebugLogs)
            {
                Debug.Log($"[FactionManager] Initialized with FactionDatabase containing {factionDatabase.factions.Count} factions");
            }
        }

        // ==================== PUBLIC API (Static Methods - Backward Compatible) ====================

        /// <summary>
        /// Get the relationship between two factions.
        /// This is the core query method used by AI detection systems.
        /// BACKWARD COMPATIBLE: Same signature as old system.
        /// </summary>
        public static FactionRelationship GetRelationship(FactionType sourceFaction, FactionType targetFaction)
        {
            // Validate instance
            if (Instance == null || Instance.factionDatabase == null)
            {
                // Fallback to sensible defaults if no database
                return GetFallbackRelationship(sourceFaction, targetFaction);
            }

            return Instance.factionDatabase.GetRelationship(sourceFaction, targetFaction);
        }

        /// <summary>
        /// Check if one faction should attack another on sight.
        /// BACKWARD COMPATIBLE: Same as old system.
        /// </summary>
        public static bool IsHostile(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Hostile;
        }

        /// <summary>
        /// Check if two factions are friendly.
        /// BACKWARD COMPATIBLE: Same as old system.
        /// </summary>
        public static bool IsFriendly(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Friendly;
        }

        /// <summary>
        /// Check if two factions are neutral.
        /// BACKWARD COMPATIBLE: Same as old system.
        /// </summary>
        public static bool IsNeutral(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Neutral;
        }

        // ==================== NEW API (Database Access) ====================

        /// <summary>
        /// Get the faction database.
        /// </summary>
        public FactionDatabase GetDatabase()
        {
            return factionDatabase;
        }

        /// <summary>
        /// Get faction definition.
        /// </summary>
        public FactionDefinition GetFaction(FactionType type)
        {
            if (factionDatabase == null) return null;
            return factionDatabase.GetFaction(type);
        }

        /// <summary>
        /// Get display name for faction.
        /// </summary>
        public string GetFactionName(FactionType type)
        {
            if (factionDatabase == null) return type.ToString();
            return factionDatabase.GetFactionName(type);
        }

        /// <summary>
        /// Get generic nameplate color for faction.
        /// </summary>
        public Color GetNameplateColor(FactionType type)
        {
            if (factionDatabase == null) return Color.white;
            return factionDatabase.GetNameplateColor(type);
        }

        /// <summary>
        /// Get player-specific nameplate color based on reputation.
        /// </summary>
        public Color GetPlayerNameplateColor(FactionType factionType, int playerReputation)
        {
            if (factionDatabase == null) return Color.white;
            return factionDatabase.GetPlayerNameplateColor(factionType, playerReputation);
        }

        /// <summary>
        /// Get default player reputation for a faction.
        /// </summary>
        public int GetDefaultPlayerReputation(FactionType type)
        {
            if (factionDatabase == null) return 0;
            return factionDatabase.GetDefaultPlayerReputation(type);
        }

        /// <summary>
        /// Check if player can join a faction.
        /// </summary>
        public bool CanJoinFaction(FactionType type)
        {
            if (factionDatabase == null) return false;
            return factionDatabase.CanJoinFaction(type);
        }

        // ==================== FALLBACK SYSTEM (No Database) ====================

        /// <summary>
        /// Fallback relationship logic when no database is assigned.
        /// Uses sensible defaults based on faction types.
        /// </summary>
        private static FactionRelationship GetFallbackRelationship(FactionType faction1, FactionType faction2)
        {
            // Same faction = always friendly
            if (faction1 == faction2)
                return FactionRelationship.Friendly;

            // Handle special factions
            if (faction1 == FactionType.Neutral || faction2 == FactionType.Neutral)
                return FactionRelationship.Neutral;

            if (faction1 == FactionType.Friendly || faction2 == FactionType.Friendly)
                return FactionRelationship.Friendly;

            if (faction1 == FactionType.Hostile || faction2 == FactionType.Hostile)
                return FactionRelationship.Hostile;

            if (faction1 == FactionType.None || faction2 == FactionType.None)
                return FactionRelationship.Neutral;

            // Evil factions vs good factions
            var evilFactions = new[] { FactionType.Warlocks, FactionType.Undead, FactionType.Bandits, FactionType.Monsters };
            var goodFactions = new[] { FactionType.Player, FactionType.Elves, FactionType.Humans, FactionType.Dwarves };

            bool faction1Evil = System.Array.Exists(evilFactions, f => f == faction1);
            bool faction2Evil = System.Array.Exists(evilFactions, f => f == faction2);
            bool faction1Good = System.Array.Exists(goodFactions, f => f == faction1);
            bool faction2Good = System.Array.Exists(goodFactions, f => f == faction2);

            if (faction1Evil && faction2Good) return FactionRelationship.Hostile;
            if (faction1Good && faction2Evil) return FactionRelationship.Hostile;
            if (faction1Good && faction2Good) return FactionRelationship.Friendly;

            // Default to neutral
            return FactionRelationship.Neutral;
        }

        // ==================== DEBUG & TESTING ====================

#if UNITY_EDITOR
        [ContextMenu("Debug: Print All Relationships")]
        private void DebugPrintAllRelationships()
        {
            if (factionDatabase == null)
            {
                Debug.LogWarning("[FactionManager] No database assigned!");
                return;
            }
            
            Debug.Log("=== FACTION RELATIONSHIP MATRIX ===");
            
            FactionType[] allFactions = (FactionType[])System.Enum.GetValues(typeof(FactionType));
            
            foreach (FactionType faction1 in allFactions)
            {
                if (faction1 == FactionType.None) continue;
                
                var factionDef = factionDatabase.GetFaction(faction1);
                if (factionDef == null) continue;
                
                Debug.Log($"\n--- {factionDef.displayName} ({faction1}) ---");
                
                foreach (FactionType faction2 in allFactions)
                {
                    if (faction2 == FactionType.None || faction1 == faction2) continue;
                    
                    FactionRelationship rel = GetRelationship(faction1, faction2);
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
        
        [ContextMenu("Debug: Test Player Relationships")]
        private void DebugTestPlayerRelationships()
        {
            Debug.Log("=== PLAYER FACTION RELATIONSHIPS ===");
            
            FactionType[] allFactions = (FactionType[])System.Enum.GetValues(typeof(FactionType));
            
            foreach (FactionType faction in allFactions)
            {
                if (faction == FactionType.None || faction == FactionType.Player) continue;
                
                FactionRelationship rel = GetRelationship(FactionType.Player, faction);
                string colorTag = rel switch
                {
                    FactionRelationship.Friendly => "<color=green>",
                    FactionRelationship.Neutral => "<color=yellow>",
                    FactionRelationship.Hostile => "<color=red>",
                    _ => ""
                };
                
                Debug.Log($"{colorTag}Player → {faction}: {rel}</color>");
            }
        }
        
        [ContextMenu("Debug: Print Faction Summary")]
        private void DebugPrintSummary()
        {
            if (factionDatabase == null)
            {
                Debug.LogWarning("[FactionManager] No database assigned!");
                return;
            }
            
            Debug.Log("=== FACTION DATABASE SUMMARY ===");
            Debug.Log($"Total Factions: {factionDatabase.factions.Count}");
            Debug.Log($"Joinable Factions: {factionDatabase.GetJoinableFactions().Count}");
            
            foreach (var faction in factionDatabase.factions)
            {
                if (faction == null) continue;
                Debug.Log($"  - {faction.displayName} ({faction.factionType})");
            }
        }
        
        [ContextMenu("Validate: Check Database Assignment")]
        private void ValidateDatabaseAssignment()
        {
            if (factionDatabase == null)
            {
                Debug.LogError("[FactionManager] ❌ No FactionDatabase assigned! Please assign one in the inspector.");
            }
            else
            {
                Debug.Log($"[FactionManager] ✅ Database assigned: {factionDatabase.name}");
                factionDatabase.Initialize();
            }
        }
#endif
    }
}