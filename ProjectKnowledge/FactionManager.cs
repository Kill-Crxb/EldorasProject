using UnityEngine;

namespace RPG.Factions
{
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
            InitializeRelationships();
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

            result.Info.Add("Faction relationships loaded");
            return result;
        }

        #endregion

        #region Inspector Fields

        [Header("Configuration")]
        [SerializeField] private FactionRelationshipConfig relationshipConfig;
        [SerializeField] private FactionDefinitionDatabase factionDatabase;

        #endregion

        #region Initialization

        private void InitializeRelationships()
        {
            if (relationshipConfig == null)
                return;

            relationshipConfig.Initialize();

            if (factionDatabase != null)
                factionDatabase.Initialize();
        }

        #endregion

        #region Faction Queries

        public static FactionRelationship GetRelationship(FactionType sourceFaction, FactionType targetFaction)
        {
            if (Instance == null)
                return FactionRelationship.Neutral;

            if (Instance.relationshipConfig == null)
                return FactionRelationship.Neutral;

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

        #region Faction Metadata Queries

        public static string GetFactionDisplayName(string factionId)
        {
            if (Instance?.factionDatabase == null)
                return factionId;

            return Instance.factionDatabase.GetFactionName(factionId);
        }

        public static Color GetFactionDisplayColor(string factionId)
        {
            if (Instance?.factionDatabase == null)
                return Color.white;

            return Instance.factionDatabase.GetFactionColor(factionId);
        }

        #endregion

        #region Coordination: Damage Modifiers

        public float GetFactionDamageModifier(FactionType attackerFaction, FactionType defenderFaction)
        {
            return 1.0f;
        }

        #endregion

        #region Coordination: Resource Regen Bonuses

        public float GetFactionResourceRegenBonus(FactionType faction, ResourceDefinition resource)
        {
            return 1.0f;
        }

        #endregion
    }
}