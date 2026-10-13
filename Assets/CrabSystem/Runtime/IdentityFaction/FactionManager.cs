using UnityEngine;

namespace RPG.Factions
{
    /// <summary>
    /// Global faction service on Manager_Brain.
    ///
    /// Two jobs:
    /// 1. Stance queries — GetStance(a, b) against the FactionRelationships matrix.
    /// 2. Save-id resolution — Resolve("faction_x") → FactionDefinition asset,
    ///    via the FactionDefinitionDatabase. Strings exist ONLY at persistence
    ///    boundaries (save files, CharacterConfigData); runtime code passes
    ///    FactionDefinition references.
    ///
    /// Display name/color queries were removed — read DisplayName / FactionColor
    /// directly off the FactionDefinition you hold.
    /// </summary>
    public class FactionManager : MonoBehaviour, IGameManager
    {
        #region Singleton

        private static FactionManager _instance;
        public static FactionManager Instance => _instance;

        #endregion

        #region IGameManager Implementation

        public string ManagerName => "Faction Manager";
        public int InitializationPriority => 15;
        public bool IsEnabled => enabled;
        public bool IsInitialized { get; private set; }

        public int ConfigVersion { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;

            _instance = this;

            if (relationshipConfig != null)
                relationshipConfig.Initialize();

            if (factionDatabase != null)
                factionDatabase.Initialize();

            IsInitialized = true;
        }

        public void LateInitialize()
        {
        }

        public void Shutdown()
        {
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

            if (factionDatabase == null)
                result.Errors.Add("No FactionDefinitionDatabase assigned — save-file faction ids cannot be resolved");

            result.Info.Add("Faction relationships loaded");
            return result;
        }

        #endregion

        #region Inspector Fields

        [Header("Configuration")]
        [SerializeField] private FactionRelationshipConfig relationshipConfig;
        [SerializeField] private FactionDefinitionDatabase factionDatabase;

        #endregion

        #region Stance Queries

        public static FactionRelationship GetStance(FactionDefinition a, FactionDefinition b)
        {
            if (Instance == null || Instance.relationshipConfig == null)
                return FactionRelationship.Neutral;

            return Instance.relationshipConfig.GetRelationship(a, b);
        }

        public static bool IsHostile(FactionDefinition a, FactionDefinition b)
            => GetStance(a, b) == FactionRelationship.Hostile;

        public static bool IsFriendly(FactionDefinition a, FactionDefinition b)
            => GetStance(a, b) == FactionRelationship.Friendly;

        public static bool IsNeutral(FactionDefinition a, FactionDefinition b)
            => GetStance(a, b) == FactionRelationship.Neutral;

        /// <summary>Damage multiplier for attacker → defender, from the relationship entry. 1.0 by default.</summary>
        public static float GetDamageModifier(FactionDefinition attacker, FactionDefinition defender)
        {
            if (Instance == null || Instance.relationshipConfig == null)
                return 1f;

            return Instance.relationshipConfig.GetDamageMultiplier(attacker, defender);
        }

        #endregion

        #region Persistence Resolution

        /// <summary>Resolve a saved faction id to its asset. Null if unknown or database missing.</summary>
        public static FactionDefinition Resolve(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return null;

            if (Instance?.factionDatabase == null)
            {
                Debug.LogWarning($"[FactionManager] Cannot resolve '{factionId}' — no FactionDefinitionDatabase assigned on Manager_Brain.");
                return null;
            }

            var faction = Instance.factionDatabase.GetFaction(factionId);
            if (faction == null)
                Debug.LogWarning($"[FactionManager] Unknown faction id '{factionId}' — add its FactionDefinition to the database.");

            return faction;
        }

        #endregion
    }
}
