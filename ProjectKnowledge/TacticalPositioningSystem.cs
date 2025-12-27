using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace RPG.Tactical
{
    /// <summary>
    /// Manages tactical positioning points around a target entity.
    /// Provides spatial framework for AI positioning, player abilities, and tactical combat.
    /// 
    /// Features:
    /// - Inner ring (close combat) and outer ring (positioning/circling)
    /// - 8-point cardinal/intercardinal system
    /// - Soft occupancy limits with priority system
    /// - Stable point updates with intelligent triggers
    /// - Role-based point assignment
    /// </summary>
    public class TacticalPositioningSystem : MonoBehaviour
    {
        [Header("Ring Configuration")]
        [SerializeField] private float innerRingRadius = 3f;
        [SerializeField] private float outerRingRadius = 8f;
        [SerializeField] private int pointsPerRing = 8;

        [Header("Occupancy Limits")]
        [SerializeField] private int innerRingMaxOccupants = 2;
        [SerializeField] private int outerRingMaxOccupants = 3;

        [Header("Update Stability")]
        [SerializeField] private float innerRingUpdateInterval = 0.2f;
        [SerializeField] private float outerRingUpdateInterval = 0.5f;
        [SerializeField] private float positionThreshold = 3f;
        [SerializeField] private float rotationThreshold = 45f;
        [SerializeField] private float quickRotationThreshold = 60f;
        [SerializeField] private float quickRotationTime = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;
        [SerializeField] private bool showOccupancy = true;
        [SerializeField] private bool logPointRequests = false;

        // Point collections
        private TacticalPoint[] innerRingPoints;
        private TacticalPoint[] outerRingPoints;
        private Dictionary<GameObject, TacticalPoint> entityAssignments;

        // Target tracking
        private Transform targetTransform;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private float lastInnerUpdateTime;
        private float lastOuterUpdateTime;

        // Movement tracking
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private float rotationChangeTime;

        // Properties
        public Transform TargetTransform => targetTransform;
        public float InnerRadius => innerRingRadius;
        public float OuterRadius => outerRingRadius;
        public int TotalPoints => pointsPerRing * 2;

        #region Initialization

        private void Awake()
        {
            targetTransform = transform;
            entityAssignments = new Dictionary<GameObject, TacticalPoint>();

            InitializePoints();
            UpdateAllPointPositions();

            lastPosition = targetTransform.position;
            lastRotation = targetTransform.rotation;
            previousPosition = lastPosition;
            previousRotation = lastRotation;
        }

        private void InitializePoints()
        {
            innerRingPoints = new TacticalPoint[pointsPerRing];
            outerRingPoints = new TacticalPoint[pointsPerRing];

            for (int i = 0; i < pointsPerRing; i++)
            {
                PointDirection direction = (PointDirection)i;
                float angle = TacticalUtility.DirectionToAngle(direction);

                // Calculate local offsets (relative to forward direction)
                Vector3 innerOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * innerRingRadius;
                Vector3 outerOffset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * outerRingRadius;

                // Create points
                innerRingPoints[i] = new TacticalPoint(i, direction, PointRing.Inner, innerOffset, innerRingMaxOccupants);
                outerRingPoints[i] = new TacticalPoint(i, direction, PointRing.Outer, outerOffset, outerRingMaxOccupants);
            }

            if (logPointRequests)
            {
                Debug.Log($"[TacticalPositioningSystem] Initialized {pointsPerRing * 2} points on {gameObject.name}");
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            TrackMovement();
            UpdatePointsIfNeeded();
        }

        private void TrackMovement()
        {
            // Track position changes
            Vector3 currentPosition = targetTransform.position;
            Quaternion currentRotation = targetTransform.rotation;

            // Check for quick rotation
            float rotationDelta = Quaternion.Angle(previousRotation, currentRotation);
            if (rotationDelta > quickRotationThreshold)
            {
                rotationChangeTime = Time.time;
            }

            previousPosition = currentPosition;
            previousRotation = currentRotation;
        }

        private void UpdatePointsIfNeeded()
        {
            // Check inner ring update
            if (Time.time - lastInnerUpdateTime >= innerRingUpdateInterval)
            {
                if (ShouldUpdatePoints(PointRing.Inner))
                {
                    UpdateRingPoints(innerRingPoints);
                    lastInnerUpdateTime = Time.time;
                }
            }

            // Check outer ring update
            if (Time.time - lastOuterUpdateTime >= outerRingUpdateInterval)
            {
                if (ShouldUpdatePoints(PointRing.Outer))
                {
                    UpdateRingPoints(outerRingPoints);
                    lastOuterUpdateTime = Time.time;
                }
            }
        }

        private bool ShouldUpdatePoints(PointRing ring)
        {
            Vector3 currentPosition = targetTransform.position;
            Quaternion currentRotation = targetTransform.rotation;

            // Check for large movement (always update)
            float positionDelta = Vector3.Distance(currentPosition, lastPosition);
            if (positionDelta >= positionThreshold)
            {
                lastPosition = currentPosition;
                lastRotation = currentRotation;
                return true;
            }

            // Check for quick rotation (update after delay)
            float rotationDelta = Quaternion.Angle(lastRotation, currentRotation);
            if (rotationDelta >= rotationThreshold)
            {
                // If rotation was quick, wait for delay
                if (Time.time - rotationChangeTime > quickRotationTime)
                {
                    lastPosition = currentPosition;
                    lastRotation = currentRotation;
                    return true;
                }
            }

            // Small changes - update periodically based on interval
            return false;
        }

        private void UpdateRingPoints(TacticalPoint[] ring)
        {
            foreach (var point in ring)
            {
                point.UpdatePosition(targetTransform);
            }
        }

        private void UpdateAllPointPositions()
        {
            UpdateRingPoints(innerRingPoints);
            UpdateRingPoints(outerRingPoints);

            lastPosition = targetTransform.position;
            lastRotation = targetTransform.rotation;
        }

        #endregion

        #region Point Request API

        /// <summary>
        /// Request a tactical point for an entity
        /// </summary>
        public TacticalPoint RequestPoint(
            GameObject requester,
            TacticalRole role,
            PointPreference preference = null,
            int priority = 0)
        {
            if (requester == null)
            {
                Debug.LogWarning("[TacticalPositioningSystem] Null requester in RequestPoint");
                return null;
            }

            preference = preference ?? PointPreference.Any;

            // Determine which ring to search
            TacticalPoint[] targetRing = GetRingForRole(role);
            TacticalPoint[] alternateRing = targetRing == innerRingPoints ? outerRingPoints : innerRingPoints;

            // Try preferred ring first
            TacticalPoint bestPoint = FindBestPoint(requester, role, preference, priority, targetRing);

            // If no suitable point found and can use alternate ring, try it
            if (bestPoint == null && preference.AllowAnyRing)
            {
                bestPoint = FindBestPoint(requester, role, preference, priority, alternateRing);
            }

            // If found a point, attempt reservation
            if (bestPoint != null)
            {
                if (bestPoint.TryReserve(requester, role, priority))
                {
                    // Track assignment
                    if (entityAssignments.ContainsKey(requester))
                    {
                        // Release old point if different
                        var oldPoint = entityAssignments[requester];
                        if (oldPoint != bestPoint)
                        {
                            oldPoint.Release(requester);
                        }
                    }

                    entityAssignments[requester] = bestPoint;

                    if (logPointRequests)
                    {
                        Debug.Log($"[TacticalPositioningSystem] Assigned {requester.name} to {bestPoint.Direction}/{bestPoint.Ring} ({role})");
                    }

                    return bestPoint;
                }
            }

            if (logPointRequests)
            {
                Debug.LogWarning($"[TacticalPositioningSystem] No suitable point found for {requester.name} ({role})");
            }

            return null;
        }

        /// <summary>
        /// Release a point assignment for an entity
        /// </summary>
        public void ReleasePoint(GameObject entity)
        {
            if (entity == null) return;

            if (entityAssignments.TryGetValue(entity, out TacticalPoint point))
            {
                point.Release(entity);
                entityAssignments.Remove(entity);

                if (logPointRequests)
                {
                    Debug.Log($"[TacticalPositioningSystem] Released point for {entity.name}");
                }
            }
        }

        /// <summary>
        /// Get specific point by direction and ring
        /// </summary>
        public TacticalPoint GetPoint(PointDirection direction, PointRing ring)
        {
            int index = (int)direction;

            if (index < 0 || index >= pointsPerRing)
                return null;

            return ring == PointRing.Inner ? innerRingPoints[index] : outerRingPoints[index];
        }

        /// <summary>
        /// Get currently assigned point for an entity (if any)
        /// </summary>
        public TacticalPoint GetAssignedPoint(GameObject entity)
        {
            return entityAssignments.TryGetValue(entity, out TacticalPoint point) ? point : null;
        }

        #endregion

        #region Helper Methods

        private TacticalPoint[] GetRingForRole(TacticalRole role)
        {
            // Determine preferred ring based on role
            return role switch
            {
                TacticalRole.ActiveFighter => innerRingPoints,
                TacticalRole.Pressuring => innerRingPoints,
                TacticalRole.Circling => outerRingPoints,
                TacticalRole.Flanking => outerRingPoints,
                TacticalRole.Waiting => outerRingPoints,
                TacticalRole.Retreating => outerRingPoints,
                _ => outerRingPoints
            };
        }

        private TacticalPoint FindBestPoint(
            GameObject requester,
            TacticalRole role,
            PointPreference preference,
            int priority,
            TacticalPoint[] ring)
        {
            Vector3 requesterPosition = requester.transform.position;
            TacticalPoint bestPoint = null;
            float bestScore = float.MinValue;

            foreach (var point in ring)
            {
                // Check direction preference
                if (!preference.AllowAnyDirection && preference.PreferredDirection.HasValue)
                {
                    if (point.Direction != preference.PreferredDirection.Value)
                        continue;
                }

                // Prefer empty points if requested
                if (preference.PreferEmpty && !point.HasSpace)
                    continue;

                // Check if can reserve
                if (!point.CanReserve(requester, role))
                    continue;

                // Calculate priority score
                float score = point.GetPriorityScore(requesterPosition, priority, role);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = point;
                }
            }

            return bestPoint;
        }

        #endregion

        #region Debug & Gizmos

        private void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;
            if (innerRingPoints == null || outerRingPoints == null) return;

            // Draw rings
            DrawRing(innerRingRadius, Color.yellow);
            DrawRing(outerRingRadius, Color.cyan);

            // Draw points
            foreach (var point in innerRingPoints)
            {
                DrawPoint(point, Color.yellow);
            }

            foreach (var point in outerRingPoints)
            {
                DrawPoint(point, Color.cyan);
            }
        }

        private void DrawRing(float radius, Color color)
        {
            Gizmos.color = new Color(color.r, color.g, color.b, 0.2f);

            int segments = 32;
            Vector3 prevPoint = transform.position + transform.forward * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = (i / (float)segments) * 360f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                Vector3 newPoint = transform.position + transform.rotation * offset;

                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        private void DrawPoint(TacticalPoint point, Color baseColor)
        {
            if (point == null) return;

            // Color based on occupancy
            Color color = point.IsEmpty ? baseColor : Color.red;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.6f);

            // Draw sphere at point
            Gizmos.DrawSphere(point.WorldPosition, 0.3f);

            // Draw direction indicator
            Vector3 toTarget = transform.position - point.WorldPosition;
            Gizmos.color = new Color(color.r, color.g, color.b, 0.4f);
            Gizmos.DrawLine(point.WorldPosition, point.WorldPosition + toTarget.normalized * 0.5f);

            // Show occupancy count
            if (showOccupancy && !point.IsEmpty)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    point.WorldPosition + Vector3.up * 0.5f,
                    $"{point.CurrentOccupancy}/{point.MaxOccupants}",
                    new GUIStyle { normal = new GUIStyleState { textColor = Color.white } }
                );
#endif
            }
        }

        [ContextMenu("Debug: Print All Points")]
        private void DebugPrintAllPoints()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Must be in Play mode");
                return;
            }

            Debug.Log("=== INNER RING ===");
            foreach (var point in innerRingPoints)
            {
                Debug.Log(point.GetDebugInfo());
            }

            Debug.Log("=== OUTER RING ===");
            foreach (var point in outerRingPoints)
            {
                Debug.Log(point.GetDebugInfo());
            }
        }

        [ContextMenu("Debug: Clear All Occupants")]
        private void DebugClearAllOccupants()
        {
            foreach (var point in innerRingPoints)
                point.ClearAllOccupants();

            foreach (var point in outerRingPoints)
                point.ClearAllOccupants();

            entityAssignments.Clear();
            Debug.Log("[TacticalPositioningSystem] Cleared all occupants");
        }

        #endregion
    }
}