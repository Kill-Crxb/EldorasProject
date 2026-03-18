using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Tactical
{
    /// <summary>
    /// Represents a single tactical positioning point around a target entity.
    /// Tracks occupancy, role assignments, and provides reservation system.
    /// </summary>
    public class TacticalPoint
    {
        // Identity
        public int PointIndex { get; private set; }
        public PointDirection Direction { get; private set; }
        public PointRing Ring { get; private set; }

        // Position (updated dynamically)
        public Vector3 WorldPosition { get; private set; }
        public Vector3 LocalOffset { get; private set; }

        // Occupancy tracking
        private List<PointOccupant> occupants;
        private int maxOccupants;

        // Timing
        public float LastUpdateTime { get; private set; }
        public float LastOccupancyChangeTime { get; private set; }

        // Configuration
        private float defaultReservationDuration = 3f;

        // Properties
        public IReadOnlyList<PointOccupant> Occupants => occupants.AsReadOnly();
        public int CurrentOccupancy => occupants.Count;
        public bool HasSpace => occupants.Count < maxOccupants;
        public bool IsEmpty => occupants.Count == 0;
        public int MaxOccupants => maxOccupants;

        /// <summary>
        /// Constructor
        /// </summary>
        public TacticalPoint(int index, PointDirection direction, PointRing ring, Vector3 localOffset, int maxOccupants = 2)
        {
            PointIndex = index;
            Direction = direction;
            Ring = ring;
            LocalOffset = localOffset;
            this.maxOccupants = maxOccupants;

            occupants = new List<PointOccupant>();
            LastUpdateTime = Time.time;
            LastOccupancyChangeTime = Time.time;
        }

        /// <summary>
        /// Update the world position based on target transform
        /// </summary>
        public void UpdatePosition(Transform targetTransform)
        {
            // Calculate world position from local offset
            WorldPosition = targetTransform.position + targetTransform.rotation * LocalOffset;
            LastUpdateTime = Time.time;
        }

        /// <summary>
        /// Check if an entity can reserve this point
        /// </summary>
        public bool CanReserve(GameObject entity, TacticalRole role)
        {
            // Remove expired occupants
            CleanupExpiredOccupants();

            // If point is empty, always allow
            if (IsEmpty)
                return true;

            // If entity already occupies this point, allow (renewal)
            if (IsOccupiedBy(entity))
                return true;

            // If point has space, allow
            if (HasSpace)
                return true;

            // Point is full - could implement priority bumping here later
            return false;
        }

        /// <summary>
        /// Reserve this point for an entity
        /// </summary>
        public bool TryReserve(GameObject entity, TacticalRole role, int priority = 0, float? duration = null)
        {
            if (!CanReserve(entity, role))
                return false;

            // Remove expired occupants
            CleanupExpiredOccupants();

            // Check if entity already has reservation (renewal)
            var existing = occupants.FirstOrDefault(o => o.Entity == entity);
            if (existing != null)
            {
                // Renew reservation
                existing.Role = role;
                existing.ReservedUntil = Time.time + (duration ?? defaultReservationDuration);
                existing.Priority = priority;
                return true;
            }

            // Add new occupant
            var occupant = new PointOccupant(
                entity,
                role,
                duration ?? defaultReservationDuration,
                priority
            );

            occupants.Add(occupant);
            LastOccupancyChangeTime = Time.time;

            return true;
        }

        /// <summary>
        /// Release this point for an entity
        /// </summary>
        public void Release(GameObject entity)
        {
            int removed = occupants.RemoveAll(o => o.Entity == entity);

            if (removed > 0)
            {
                LastOccupancyChangeTime = Time.time;
            }
        }

        /// <summary>
        /// Check if entity currently occupies this point
        /// </summary>
        public bool IsOccupiedBy(GameObject entity)
        {
            CleanupExpiredOccupants();
            return occupants.Any(o => o.Entity == entity && !o.IsExpired);
        }

        /// <summary>
        /// Get the primary occupant (highest priority)
        /// </summary>
        public PointOccupant GetPrimaryOccupant()
        {
            CleanupExpiredOccupants();
            return occupants.OrderByDescending(o => o.Priority).FirstOrDefault();
        }

        /// <summary>
        /// Calculate priority score for this point for a given requester
        /// </summary>
        public float GetPriorityScore(Vector3 requesterPosition, int requesterPriority, TacticalRole role)
        {
            CleanupExpiredOccupants();

            float distance = Vector3.Distance(requesterPosition, WorldPosition);
            float timeSinceChange = Time.time - LastOccupancyChangeTime;

            return TacticalUtility.CalculatePriority(
                HasSpace,
                distance,
                requesterPriority,
                role,
                timeSinceChange
            );
        }

        /// <summary>
        /// Remove expired or invalid occupants
        /// </summary>
        private void CleanupExpiredOccupants()
        {
            int removed = occupants.RemoveAll(o => !o.IsValid || o.IsExpired);

            if (removed > 0)
            {
                LastOccupancyChangeTime = Time.time;
            }
        }

        /// <summary>
        /// Force clear all occupants (for emergency resets)
        /// </summary>
        public void ClearAllOccupants()
        {
            occupants.Clear();
            LastOccupancyChangeTime = Time.time;
        }

        /// <summary>
        /// Get debug info string
        /// </summary>
        public string GetDebugInfo()
        {
            CleanupExpiredOccupants();

            string info = $"Point {PointIndex} ({Direction}/{Ring})\n";
            info += $"Occupancy: {CurrentOccupancy}/{MaxOccupants}\n";

            if (!IsEmpty)
            {
                foreach (var occupant in occupants)
                {
                    info += $"  - {occupant.Entity.name} ({occupant.Role})\n";
                }
            }

            return info;
        }
    }
}