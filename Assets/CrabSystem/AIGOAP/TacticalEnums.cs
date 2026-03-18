using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Core enums and data structures for the tactical positioning system.
/// Used by both AI behaviors and player abilities.
/// </summary>

namespace RPG.Tactical
{
    /// <summary>
    /// Ring distance from target entity
    /// </summary>
    public enum PointRing
    {
        Inner,  // Close combat range (~3m)
        Outer   // Positioning/circling range (~8m)
    }

    /// <summary>
    /// Cardinal and intercardinal directions around target
    /// </summary>
    public enum PointDirection
    {
        Front,      // 0° (North)
        FrontRight, // 45° (Northeast)
        Right,      // 90° (East)
        BackRight,  // 135° (Southeast)
        Back,       // 180° (South)
        BackLeft,   // 225° (Southwest)
        Left,       // 270° (West)
        FrontLeft   // 315° (Northwest)
    }

    /// <summary>
    /// Tactical role an entity is performing at a point
    /// </summary>
    public enum TacticalRole
    {
        ActiveFighter,  // Inner ring - currently attacking target
        Pressuring,     // Inner ring - threatening but not fully committing
        Circling,       // Outer ring - observing, looking for openings
        Flanking,       // Outer ring - repositioning for better angle
        Waiting,        // Outer ring - ready to swap in when needed
        Retreating      // Moving away from target entirely
    }

    /// <summary>
    /// Tactical state for AI decision making
    /// </summary>
    public enum TacticalState
    {
        Observing,   // Watching target, circling, learning patterns
        Engaging,    // Active attacking, standard combat
        Defensive,   // Low health, cautious approach
        Aggressive,  // Target is weak, press advantage
        Retreating,  // Very low health, attempting to flee
        Flanking,    // Repositioning for better attack angle
        Recovering   // Backing away to regenerate stamina/health
    }

    /// <summary>
    /// Point selection preferences for requesting tactical points
    /// </summary>
    public class PointPreference
    {
        public PointDirection? PreferredDirection;
        public PointRing? PreferredRing;
        public bool AllowAnyDirection = true;
        public bool AllowAnyRing = false;
        public bool PreferEmpty = true;

        public static PointPreference Any => new PointPreference
        {
            AllowAnyDirection = true,
            AllowAnyRing = true,
            PreferEmpty = true
        };

        public static PointPreference Closest => new PointPreference
        {
            AllowAnyDirection = true,
            AllowAnyRing = true,
            PreferEmpty = false
        };

        public static PointPreference Direction(PointDirection dir, PointRing ring) => new PointPreference
        {
            PreferredDirection = dir,
            PreferredRing = ring,
            AllowAnyDirection = false,
            AllowAnyRing = false
        };
    }

    /// <summary>
    /// Entity occupying a tactical point
    /// </summary>
    public class PointOccupant
    {
        public GameObject Entity;
        public TacticalRole Role;
        public float ReservedUntil;
        public int Priority;
        public float ArrivalTime;

        public bool IsExpired => Time.time >= ReservedUntil;
        public bool IsValid => Entity != null;

        public PointOccupant(GameObject entity, TacticalRole role, float duration, int priority)
        {
            Entity = entity;
            Role = role;
            ReservedUntil = Time.time + duration;
            Priority = priority;
            ArrivalTime = Time.time;
        }
    }

    /// <summary>
    /// Interface for entities that can assess threat levels
    /// </summary>
    public interface ITacticalConfiguration
    {
        TacticalState CurrentTacticalState { get; }
        float ThreatLevel { get; }
        void UpdateTacticalState();
    }

    /// <summary>
    /// Utility methods for tactical positioning
    /// </summary>
    public static class TacticalUtility
    {
        /// <summary>
        /// Convert PointDirection to angle in degrees (0° = North/Forward)
        /// </summary>
        public static float DirectionToAngle(PointDirection direction)
        {
            return direction switch
            {
                PointDirection.Front => 0f,
                PointDirection.FrontRight => 45f,
                PointDirection.Right => 90f,
                PointDirection.BackRight => 135f,
                PointDirection.Back => 180f,
                PointDirection.BackLeft => 225f,
                PointDirection.Left => 270f,
                PointDirection.FrontLeft => 315f,
                _ => 0f
            };
        }

        /// <summary>
        /// Get the next direction in clockwise order
        /// </summary>
        public static PointDirection GetNextDirection(PointDirection current, bool clockwise = true)
        {
            int index = (int)current;
            int total = System.Enum.GetValues(typeof(PointDirection)).Length;

            if (clockwise)
                index = (index + 1) % total;
            else
                index = (index - 1 + total) % total;

            return (PointDirection)index;
        }

        /// <summary>
        /// Get opposite direction
        /// </summary>
        public static PointDirection GetOppositeDirection(PointDirection direction)
        {
            return direction switch
            {
                PointDirection.Front => PointDirection.Back,
                PointDirection.FrontRight => PointDirection.BackLeft,
                PointDirection.Right => PointDirection.Left,
                PointDirection.BackRight => PointDirection.FrontLeft,
                PointDirection.Back => PointDirection.Front,
                PointDirection.BackLeft => PointDirection.FrontRight,
                PointDirection.Left => PointDirection.Right,
                PointDirection.FrontLeft => PointDirection.BackRight,
                _ => PointDirection.Front
            };
        }

        /// <summary>
        /// Calculate priority score for point assignment
        /// Higher score = higher priority
        /// </summary>
        public static float CalculatePriority(
            bool hasSpace,
            float distanceToPoint,
            int entityPriority,
            TacticalRole role,
            float timeSinceLastChange)
        {
            float score = 0f;

            // Space availability (most important)
            if (hasSpace)
                score += 100f;
            else
                score -= 50f;

            // Distance (closer is better)
            score += Mathf.Max(0f, 20f - distanceToPoint);

            // Entity priority (elites, bosses)
            score += entityPriority * 10f;

            // Role importance (Active Fighter > Pressuring > Others)
            score += role switch
            {
                TacticalRole.ActiveFighter => 15f,
                TacticalRole.Pressuring => 10f,
                TacticalRole.Flanking => 5f,
                _ => 0f
            };

            // Stability bonus (point hasn't changed recently)
            if (timeSinceLastChange > 2f)
                score += 5f;

            return score;
        }
    }
}