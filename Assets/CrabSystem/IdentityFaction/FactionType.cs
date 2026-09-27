// FactionType.cs
// Faction identity is data-driven: FactionDefinition ScriptableObject assets
// are the factions. The old FactionType enum was removed — create a
// FactionDefinition asset instead (Create → RPG/Factions/Faction Definition).
// This file keeps the shared relationship enums and relationship colors.

using UnityEngine;

namespace RPG.Factions
{
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
    /// Relationship-based color coding for nameplates and UI elements.
    /// Per-faction colors live on FactionDefinition.FactionColor.
    /// </summary>
    public static class FactionColors
    {
        public static readonly Color FriendlyColor = new Color(0.2f, 1f, 0.2f);      // Green
        public static readonly Color NeutralColor = new Color(1f, 0.92f, 0.016f);    // Yellow
        public static readonly Color HostileColor = new Color(1f, 0.2f, 0.2f);       // Red

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
    }

    /// <summary>
    /// Reputation ranks for player-faction relationships.
    /// Reserved for a future reputation modifier layer.
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
