// FactionType.cs
// Faction identity is data-driven: FactionDefinition ScriptableObject assets
// are the factions. The old FactionType enum was removed — create a
// FactionDefinition asset instead (Create → RPG/Factions/Faction Definition).
// This file keeps the shared relationship enums and relationship colors.

using UnityEngine;

namespace CrabSystem.Factions
{
    // Defines the relationship between two factions.
    public enum FactionRelationship
    {
        Friendly,       // Will not attack, may assist
        Neutral,        // Ignores each other
        Hostile         // Will attack on sight
    }

    // Relationship-based color coding for nameplates and UI elements.
    // Per-faction colors live on FactionDefinition.FactionColor.
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
}
