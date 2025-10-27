using UnityEngine;
using System.Collections.Generic;

namespace RPG.Factions
{
    /// <summary>
    /// Defines all the data for a single faction.
    /// This ScriptableObject holds rich data while FactionType enum provides compile-time safety.
    /// </summary>
    [CreateAssetMenu(fileName = "Faction_", menuName = "RPG/Factions/Faction Definition")]
    public class FactionDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The enum type this definition represents")]
        public FactionType factionType = FactionType.None;

        [Tooltip("Display name shown in UI")]
        public string displayName = "Unknown Faction";

        [Tooltip("Lore description of the faction")]
        [TextArea(3, 5)]
        public string description = "";

        [Header("Visual Representation")]
        [Tooltip("Color used for nameplates when this faction interacts with player")]
        public Color nameplateColor = Color.white;

        [Tooltip("Faction icon (for UI, quest logs, etc.)")]
        public Sprite icon;

        [Tooltip("Faction banner (for faction halls, territory markers)")]
        public Sprite banner;

        [Header("Player Reputation")]
        [Tooltip("Starting reputation with player (-100 to +100)")]
        [Range(-100, 100)]
        public int defaultPlayerReputation = 0;

        [Tooltip("Below this reputation, faction is hostile to player")]
        [Range(-100, 0)]
        public int hostileThreshold = -25;

        [Tooltip("Above this reputation, faction is friendly to player")]
        [Range(0, 100)]
        public int friendlyThreshold = 25;

        [Header("Reputation Gain/Loss")]
        [Tooltip("Reputation gained when player helps this faction")]
        public int reputationGainOnQuestComplete = 10;

        [Tooltip("Reputation lost when player kills member of this faction")]
        public int reputationLossOnKill = -5;

        [Tooltip("Reputation gained when player kills enemy of this faction")]
        public int reputationGainOnEnemyKill = 2;

        [Header("Faction Membership")]
        [Tooltip("Can the player join this faction?")]
        public bool canJoinFaction = false;

        [Tooltip("Quest ID required to join faction (if applicable)")]
        public string joinQuestId = "";

        [Header("Faction Relationships")]
        [Tooltip("How this faction views other factions")]
        public List<FactionRelationshipData> relationships = new List<FactionRelationshipData>();

        [Header("Rank Rewards")]
        [Tooltip("Rewards unlocked at different reputation ranks")]
        public List<FactionRankReward> rankRewards = new List<FactionRankReward>();

        [Header("Vendor & Services")]
        [Tooltip("Does this faction have a vendor?")]
        public bool hasVendor = false;

        [Tooltip("Vendor NPC ID (links to specific NPC)")]
        public string vendorNpcId = "";

        [Tooltip("Services available from this faction")]
        public List<string> serviceIds = new List<string>();

        [Header("Audio")]
        [Tooltip("Music that plays in faction territory")]
        public AudioClip factionMusic;

        [Tooltip("Ambient sounds in faction territory")]
        public AudioClip ambientSound;

        // ==================== RUNTIME METHODS ====================

        /// <summary>
        /// Get relationship with another faction.
        /// </summary>
        public FactionRelationship GetRelationshipWith(FactionType otherFaction)
        {
            // Same faction is always friendly
            if (otherFaction == factionType)
                return FactionRelationship.Friendly;

            foreach (var rel in relationships)
            {
                if (rel.targetFaction == otherFaction)
                    return rel.relationshipType;
            }

            // Default to neutral if no relationship defined
            return FactionRelationship.Neutral;
        }

        /// <summary>
        /// Determine relationship with player based on reputation.
        /// </summary>
        public FactionRelationship GetPlayerRelationship(int playerReputation)
        {
            if (playerReputation <= hostileThreshold)
                return FactionRelationship.Hostile;

            if (playerReputation >= friendlyThreshold)
                return FactionRelationship.Friendly;

            return FactionRelationship.Neutral;
        }

        /// <summary>
        /// Get nameplate color for player relationship.
        /// </summary>
        public Color GetPlayerNameplateColor(int playerReputation)
        {
            var relationship = GetPlayerRelationship(playerReputation);

            return relationship switch
            {
                FactionRelationship.Hostile => Color.red,
                FactionRelationship.Friendly => Color.green,
                FactionRelationship.Neutral => Color.yellow,
                _ => Color.white
            };
        }

        /// <summary>
        /// Get faction rank based on reputation points.
        /// </summary>
        public FactionRank GetRankForReputation(int reputation)
        {
            if (reputation <= -75) return FactionRank.Hated;
            if (reputation <= -25) return FactionRank.Hostile;
            if (reputation < 0) return FactionRank.Unfriendly;
            if (reputation < 25) return FactionRank.Neutral;
            if (reputation < 50) return FactionRank.Friendly;
            if (reputation < 75) return FactionRank.Honored;
            if (reputation < 90) return FactionRank.Revered;
            return FactionRank.Exalted;
        }

        /// <summary>
        /// Get rewards available at specific reputation level.
        /// </summary>
        public List<FactionRankReward> GetAvailableRewards(int reputation)
        {
            var rank = GetRankForReputation(reputation);
            var available = new List<FactionRankReward>();

            foreach (var reward in rankRewards)
            {
                if (reward.requiredRank <= rank)
                {
                    available.Add(reward);
                }
            }

            return available;
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Relationships")]
        private void ValidateRelationships()
        {
            // Check for duplicate relationships
            var seenFactions = new System.Collections.Generic.HashSet<FactionType>();
            foreach (var rel in relationships)
            {
                if (seenFactions.Contains(rel.targetFaction))
                {
                    Debug.LogWarning($"[{displayName}] Duplicate relationship found for {rel.targetFaction}!");
                }
                seenFactions.Add(rel.targetFaction);
            }
            
            // Check for self-reference
            foreach (var rel in relationships)
            {
                if (rel.targetFaction == factionType)
                {
                    Debug.LogWarning($"[{displayName}] Faction has relationship with itself!");
                }
            }
            
            Debug.Log($"[{displayName}] Validated {relationships.Count} relationships");
        }
        
        [ContextMenu("Debug: Print Faction Info")]
        private void DebugPrintInfo()
        {
            Debug.Log($"=== {displayName} ({factionType}) ===");
            Debug.Log($"Default Reputation: {defaultPlayerReputation}");
            Debug.Log($"Hostile Threshold: {hostileThreshold}");
            Debug.Log($"Friendly Threshold: {friendlyThreshold}");
            Debug.Log($"Relationships: {relationships.Count}");
            foreach (var rel in relationships)
            {
                Debug.Log($"  → {rel.targetFaction}: {rel.relationshipType}");
            }
            Debug.Log($"Rank Rewards: {rankRewards.Count}");
        }
#endif
    }

    /// <summary>
    /// Defines how one faction views another.
    /// </summary>
    [System.Serializable]
    public class FactionRelationshipData
    {
        [Tooltip("The faction this relationship is with")]
        public FactionType targetFaction;

        [Tooltip("How we view this faction")]
        public FactionRelationship relationshipType;

        [Tooltip("Optional notes on why this relationship exists")]
        [TextArea(2, 3)]
        public string notes;

        [Header("Reputation Spillover")]
        [Tooltip("If player gains rep with target faction, this % spills to us (-1.0 to 1.0)")]
        [Range(-1f, 1f)]
        public float reputationSpillover = 0f;
    }

    /// <summary>
    /// Rewards unlocked at different reputation ranks.
    /// </summary>
    [System.Serializable]
    public class FactionRankReward
    {
        [Tooltip("Rank required to unlock this reward")]
        public FactionRank requiredRank = FactionRank.Friendly;

        [Tooltip("Display name of the reward")]
        public string rewardName = "New Reward";

        [Tooltip("Description shown to player")]
        [TextArea(2, 3)]
        public string rewardDescription = "";

        [Tooltip("Type of reward")]
        public FactionRewardType rewardType = FactionRewardType.QuestAccess;

        [Header("Reward Data")]
        [Tooltip("Quest ID unlocked (if rewardType = QuestAccess)")]
        public string questIdUnlocked = "";

        [Tooltip("Item ID given (if rewardType = Item)")]
        public string itemIdGiven = "";

        [Tooltip("Discount percentage (if rewardType = VendorDiscount)")]
        [Range(0f, 100f)]
        public float discountPercentage = 0f;

        [Tooltip("Title earned (if rewardType = Title)")]
        public string titleEarned = "";
    }

    /// <summary>
    /// Types of rewards factions can give.
    /// </summary>
    public enum FactionRewardType
    {
        QuestAccess,        // Unlock new quest
        Item,               // Give specific item
        VendorDiscount,     // Discount at faction vendor
        Title,              // Earn a title
        ServiceAccess,      // Unlock faction service
        AbilityUnlock       // Unlock special ability
    }
}