// FactionType.cs
// Dedicated enum for faction types - easy to customize per project
// Simply add/remove entries here for different game projects

using UnityEngine;

namespace RPG.Factions
{
    /// <summary>
    /// Core faction types in the game world.
    /// Add or remove entries based on your project's needs.
    /// </summary>
    public enum FactionType
    {
        None,           // No faction (unaffiliated)

        // Player faction
        Player,         // The player's personal faction

        // Civilized factions (generally friendly)
        Elves,          // Forest elves
        Humans,         // Human kingdoms
        Dwarves,        // Mountain dwarves

        // Evil factions (generally hostile)
        Warlocks,       // Dark magic users
        Undead,         // Undead creatures
        Bandits,        // Outlaws and thieves

        // Creature factions
        Wildlife,       // Natural animals (bears, wolves)
        Monsters,       // Hostile monsters

        // Special factions
        Neutral,        // Neutral NPCs (merchants, quest givers)
        Friendly,       // Always friendly to player
        Hostile         // Always hostile to player
    }

    /// <summary>
    /// Defines the relationship between two factions.
    /// </summary>
    public enum FactionRelationship
    {
        Friendly,       // Will not attack, may assist
        Neutral,        // Ignores each other
        Hostile         // Will attack on sight
    }

    /// <summary>
    /// Color coding for faction nameplates and UI elements.
    /// </summary>
    public static class FactionColors
    {
        // Relationship-based colors (used for nameplates)
        public static readonly Color FriendlyColor = new Color(0.2f, 1f, 0.2f);      // Green
        public static readonly Color NeutralColor = new Color(1f, 0.92f, 0.016f);    // Yellow
        public static readonly Color HostileColor = new Color(1f, 0.2f, 0.2f);       // Red

        // Faction-specific colors (used for faction UI)
        public static readonly Color ElvesColor = new Color(0.4f, 0.8f, 0.4f);       // Forest Green
        public static readonly Color HumansColor = new Color(0.6f, 0.6f, 0.9f);      // Royal Blue
        public static readonly Color DwarvesColor = new Color(0.7f, 0.5f, 0.3f);     // Bronze
        public static readonly Color WarlocksColor = new Color(0.6f, 0.2f, 0.8f);    // Dark Purple
        public static readonly Color UndeadColor = new Color(0.3f, 0.3f, 0.3f);      // Gray

        /// <summary>
        /// Get the color for a specific faction relationship.
        /// </summary>
        public static Color GetRelationshipColor(FactionRelationship relationship)
        {
            return relationship switch
            {
                FactionRelationship.Friendly => FriendlyColor,
                FactionRelationship.Neutral => NeutralColor,
                FactionRelationship.Hostile => HostileColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// Get the color for a specific faction (for UI elements).
        /// </summary>
        public static Color GetFactionColor(FactionType faction)
        {
            return faction switch
            {
                FactionType.Elves => ElvesColor,
                FactionType.Humans => HumansColor,
                FactionType.Dwarves => DwarvesColor,
                FactionType.Warlocks => WarlocksColor,
                FactionType.Undead => UndeadColor,
                _ => Color.white
            };
        }
    }

    /// <summary>
    /// Reputation ranks for player-faction relationships.
    /// Used by FactionReputationHandler.
    /// </summary>
    public enum FactionRank
    {
        Hated,          // -100 to -75: Attacked on sight
        Hostile,        // -75 to -25: Unfriendly, will attack if provoked
        Unfriendly,     // -25 to 0: Cold, limited interaction
        Neutral,        // 0 to 25: Standard interaction
        Friendly,       // 25 to 50: Helpful, better prices
        Honored,        // 50 to 75: Respected, access to special services
        Revered,        // 75 to 90: Highly trusted, premium rewards
        Exalted         // 90 to 100: Maximum reputation, all benefits
    }
}