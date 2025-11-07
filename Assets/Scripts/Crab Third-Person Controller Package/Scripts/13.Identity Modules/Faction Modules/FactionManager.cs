using UnityEngine;

namespace RPG.Factions
{
    public class FactionManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("ScriptableObject containing all faction relationships")]
        [SerializeField] private FactionRelationshipConfig relationshipConfig;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private static FactionManager _instance;
        public static FactionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FactionManager>();
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
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(transform.root.gameObject);

            InitializeRelationships();
        }

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

        public static Color GetRelationshipColor(FactionRelationship relationship)
        {
            return FactionColors.GetRelationshipColor(relationship);
        }

        public FactionRelationshipConfig GetConfig()
        {
            return relationshipConfig;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Print All Relationships")]
        private void DebugPrintAllRelationships()
        {
            if (relationshipConfig == null)
            {
                Debug.LogWarning("[FactionManager] No relationship config assigned!");
                return;
            }

            Debug.Log("=== FACTION RELATIONSHIP MATRIX ===");

            FactionType[] allFactions = (FactionType[])System.Enum.GetValues(typeof(FactionType));

            foreach (FactionType faction1 in allFactions)
            {
                if (faction1 == FactionType.None) continue;

                Debug.Log($"\n--- {faction1} ---");

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

        [ContextMenu("Validate: Check Config Assignment")]
        private void ValidateConfigAssignment()
        {
            if (relationshipConfig == null)
            {
                Debug.LogError("[FactionManager] ❌ No FactionRelationshipConfig assigned! Please assign one in the inspector.");
                Debug.Log("To create one: Right-click in Project → Create → RPG/Factions/Faction Relationships");
            }
            else
            {
                Debug.Log($"[FactionManager] ✅ Config assigned: {relationshipConfig.name}");
                relationshipConfig.Initialize();
            }
        }

        [ContextMenu("Quick Setup: Create Default Config")]
        private void CreateDefaultConfig()
        {
            Debug.Log("To create a default config:");
            Debug.Log("1. Right-click in Project → Create → RPG/Factions/Faction Relationships");
            Debug.Log("2. Select the created asset");
            Debug.Log("3. Right-click in Inspector → Quick Setup: Create Default Relationships");
            Debug.Log("4. Assign the config to this FactionManager");
        }
#endif
    }
}