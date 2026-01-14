using UnityEngine;

namespace RPG.Factions
{
    /// <summary>
    /// Faction Manager - Updated with ManagerBrain coordination
    /// 
    /// Implements IGameManager for ManagerBrain integration
    /// Provides faction-based modifiers for damage and resource regen
    /// 
    /// Priority: 15 (Independent - doesn't depend on other managers)
    /// </summary>
    public class FactionManager : MonoBehaviour, IGameManager
    {
        #region Singleton (Simplified)

        private static FactionManager _instance;
        public static FactionManager Instance => _instance;

        #endregion

        #region IGameManager Implementation

        public string ManagerName => "Faction Manager";
        public int InitializationPriority => 15; // Independent
        public bool IsEnabled => enabled;
        public bool IsInitialized { get; private set; }

        // Config versioning for hot-reload sync
        public int ConfigVersion { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            _instance = this;

            InitializeRelationships();

            IsInitialized = true;

            if (enableDebugLogs)
            {
                Debug.Log($"[{ManagerName}] Initialized");
            }
        }

        public void LateInitialize()
        {
            // Validate faction relationships
            if (relationshipConfig != null && enableDebugLogs)
            {
                Debug.Log($"[{ManagerName}] Faction relationships validated");
            }
        }

        public void Shutdown()
        {
            if (enableDebugLogs)
                Debug.Log($"[{ManagerName}] Shutdown complete");
        }

        public ValidationResult Validate()
        {
            var result = ValidationResult.Success();

            if (relationshipConfig == null)
            {
                result.IsFatal = true;
                result.Errors.Add("No FactionRelationshipConfig assigned");
                return result;
            }

            result.Info.Add("Faction relationships loaded");
            return result;
        }

        #endregion

        #region Inspector Fields

        [Header("Configuration")]
        [Tooltip("ScriptableObject containing all faction relationships")]
        [SerializeField] private FactionRelationshipConfig relationshipConfig;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        #endregion

        #region Initialization

        private void InitializeRelationships()
        {
            if (relationshipConfig == null)
            {
                Debug.LogError("[FactionManager] No FactionRelationshipConfig assigned! Please assign one in the Inspector.");
                Debug.LogWarning("[FactionManager] Faction queries will return Neutral by default.");
                return;
            }

            relationshipConfig.Initialize();

            if (enableDebugLogs)
            {
                Debug.Log($"[FactionManager] Initialized with FactionRelationshipConfig: {relationshipConfig.name}");
            }
        }

        #endregion

        #region Faction Queries

        /// <summary>
        /// Get relationship between two factions
        /// </summary>
        public static FactionRelationship GetRelationship(FactionType sourceFaction, FactionType targetFaction)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[FactionManager] No instance available. Defaulting to Neutral.");
                return FactionRelationship.Neutral;
            }

            if (Instance.relationshipConfig == null)
            {
                Debug.LogWarning("[FactionManager] No relationship config assigned. Defaulting to Neutral.");
                return FactionRelationship.Neutral;
            }

            return Instance.relationshipConfig.GetRelationship(sourceFaction, targetFaction);
        }

        public static bool IsHostile(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Hostile;
        }

        public static bool IsFriendly(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Friendly;
        }

        public static bool IsNeutral(FactionType sourceFaction, FactionType targetFaction)
        {
            return GetRelationship(sourceFaction, targetFaction) == FactionRelationship.Neutral;
        }

        public static string GetFactionName(FactionType faction)
        {
            return faction.ToString();
        }

        #endregion

        #region Coordination: Damage Modifiers

        /// <summary>
        /// COORDINATION: Get faction-based damage modifier for PvP combat
        /// Called by DamageManager when calculating damage between factions
        /// 
        /// Currently set to 1.0 (normal damage) for all relationships
        /// Uncomment code below to enable faction-based damage bonuses
        /// </summary>
        public float GetFactionDamageModifier(FactionType attackerFaction, FactionType defenderFaction)
        {
            var relationship = GetRelationship(attackerFaction, defenderFaction);

            // TODO: Currently set to normal rates (1.0) - adjust for game balance later
            return 1.0f;

            // FUTURE: When ready to enable faction damage bonuses, uncomment this:
            /*
            return relationship switch
            {
                FactionRelationship.Friendly => 0f,      // No friendly fire
                FactionRelationship.Neutral => 1f,       // Normal damage
                FactionRelationship.Hostile => 1.5f,     // Bonus vs enemies
                _ => 1f
            };
            */
        }

        #endregion

        #region Coordination: Resource Regen Bonuses

        /// <summary>
        /// COORDINATION: Get faction-based resource regeneration bonus
        /// Called by ResourceSystem when regenerating resources
        /// 
        /// Currently set to 1.0 (normal regen) for all factions
        /// Uncomment code below to enable faction-specific regen
        /// </summary>
        public float GetFactionResourceRegenBonus(FactionType faction, ResourceDefinition resource)
        {
            // TODO: Currently set to normal rates (1.0) - adjust for game balance later
            return 1.0f;

            // FUTURE: When ready to enable faction-specific regen, uncomment this:
            /*
            // Example: Undead don't regen health naturally
            if (faction == FactionType.Undead)
            {
                return resource switch
                {
                    ResourceType.Health => 0f,    // No health regen
                    ResourceType.Mana => 1.5f,    // Bonus mana regen
                    _ => 1f
                };
            }

            // Example: Elves get bonus mana regen
            if (faction == FactionType.Elves)
            {
                return resource switch
                {
                    ResourceType.Mana => 2f,      // 2x mana regen
                    _ => 1f
                };
            }

            return 1f; // Default
            */
        }

        #endregion

        #region Context Menu Helpers

        [ContextMenu("Print Faction Relationships")]
        private void PrintFactionRelationships()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FactionManager] Can only print in Play Mode");
                return;
            }

            if (relationshipConfig == null)
            {
                Debug.LogError("[FactionManager] No relationship config assigned");
                return;
            }

            Debug.Log("=== Faction Relationships ===");
            var factions = System.Enum.GetValues(typeof(FactionType));

            foreach (FactionType faction1 in factions)
            {
                if (faction1 == FactionType.None) continue;

                foreach (FactionType faction2 in factions)
                {
                    if (faction2 == FactionType.None || faction1 == faction2) continue;

                    var relationship = GetRelationship(faction1, faction2);
                    Debug.Log($"{faction1} → {faction2}: {relationship}");
                }
            }
        }

        #endregion
    }
}