using UnityEngine;
using System.Collections.Generic;
using RPG.Factions;

/// <summary>
/// Perception Module - Advanced target detection for AI
/// 
/// Features:
/// - Vision cone (field of view)
/// - Line of sight checking
/// - Faction-based filtering
/// - Memory (remember last known position)
/// - Performance optimized with detection throttling
/// </summary>
public class PerceptionModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Vision Settings")]
    [Tooltip("How far the entity can see")]
    [SerializeField] private float visionRange = 15f;

    [Tooltip("Field of view angle (degrees)")]
    [SerializeField] private float visionAngle = 90f;

    [Tooltip("Require line of sight to detect targets")]
    [SerializeField] private bool requireLineOfSight = true;

    [Tooltip("Eye height offset from transform position")]
    [SerializeField] private float eyeHeight = 1.5f;

    [Header("Sound Detection")]
    [Tooltip("Enable sound-based detection")]
    [SerializeField] private bool enableSoundDetection = false;

    [Tooltip("Range for detecting sounds")]
    [SerializeField] private float soundRange = 8f;

    [Header("Detection Layers")]
    [Tooltip("Layers to detect (typically Player, Enemy)")]
    [SerializeField] private LayerMask detectionLayers = -1;

    [Tooltip("Layers that block line of sight (typically Default, Environment)")]
    [SerializeField] private LayerMask obstacleLayers = -1;

    [Header("Faction Settings")]
    [Tooltip("Use faction-based detection (only detect hostiles)")]
    [SerializeField] private bool useFactionDetection = true;

    [Tooltip("Only detect hostile targets")]
    [SerializeField] private bool onlyDetectHostiles = true;

    [Header("Memory")]
    [Tooltip("Remember last known target position")]
    [SerializeField] private bool enableMemory = true;

    [Tooltip("How long to remember last known position (seconds)")]
    [SerializeField] private float memoryDuration = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool drawGizmos = true;

    // References
    private ControllerBrain brain;
    private UniversalFactionHandler factionHandler;

    // Detection state
    private Transform currentTarget;
    private Vector3 lastKnownPosition;
    private float lastSeenTime;
    private bool hasMemory;

    // Performance optimization
    private Collider[] detectionBuffer = new Collider[20];
    private float nextDetectionTime;
    private float detectionInterval = 0.2f; // Check every 0.2s instead of every frame

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    /// <summary>
    /// Current detected target
    /// </summary>
    public Transform CurrentTarget => currentTarget;

    /// <summary>
    /// Has a target in sight right now
    /// </summary>
    public bool HasTarget => currentTarget != null;

    /// <summary>
    /// Last known position of target (even if lost sight)
    /// </summary>
    public Vector3 LastKnownPosition => hasMemory ? lastKnownPosition : (currentTarget != null ? currentTarget.position : Vector3.zero);

    /// <summary>
    /// Has a memory of where target was
    /// </summary>
    public bool HasMemory => hasMemory && Time.time - lastSeenTime < memoryDuration;

    /// <summary>
    /// Vision range
    /// </summary>
    public float VisionRange => visionRange;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get faction handler through ControllerBrain's Identity system
        if (brain.Identity != null)
        {
            factionHandler = brain.Identity.Faction;
        }

        if (useFactionDetection && factionHandler == null)
        {
            Debug.LogWarning($"[PerceptionModule] Faction detection enabled but no FactionAffiliationHandler found on {brain.name}");
        }

        if (debugMode)
        {
            Debug.Log($"[PerceptionModule] Initialized on {brain.name}");
            Debug.Log($"  Detection Layers: {detectionLayers.value}");
            Debug.Log($"  Obstacle Layers: {obstacleLayers.value}");
            Debug.Log($"  Vision Range: {visionRange}m, FOV: {visionAngle}°");
            Debug.Log($"  Faction Detection: {useFactionDetection}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        // Throttle detection checks for performance
        if (Time.time < nextDetectionTime) return;
        nextDetectionTime = Time.time + detectionInterval;

        if (debugMode)
            Debug.Log($"[PerceptionModule] Running detection scan (interval: {detectionInterval}s)");

        // Detect targets
        DetectTargets();
    }

    public void PhysicsUpdateModule()
    {
        // Not needed for perception
    }

    public void LateUpdateModule()
    {
        // Not needed for perception
    }

    public void OnDestroyModule()
    {
        // Cleanup if needed
    }

    #endregion

    #region Detection Logic

    private void DetectTargets()
    {
        // Use OverlapSphereNonAlloc for performance
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            visionRange,
            detectionBuffer,
            detectionLayers,
            QueryTriggerInteraction.Collide  // Detect trigger colliders
        );

        if (debugMode)
            Debug.Log($"[PerceptionModule] OverlapSphere found {hitCount} colliders");

        Transform bestTarget = null;
        float closestDistance = float.MaxValue;

        // Check each detected collider
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = detectionBuffer[i];

            if (debugMode)
                Debug.Log($"[PerceptionModule] [{i}] Checking collider: {hit.name}");

            // Only detect CharacterControllers (skip trigger colliders like hitboxes/feet detection)
            CharacterController charController = hit.GetComponent<CharacterController>();
            if (charController == null)
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Not a CharacterController");
                continue;
            }

            // Skip if this is self
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Is self or child");
                continue;
            }

            // Get brain (CharacterController is usually on root, brain on child)
            ControllerBrain targetBrain = hit.GetComponentInChildren<ControllerBrain>();

            if (debugMode)
                Debug.Log($"[PerceptionModule] [{i}] Brain search result: {(targetBrain != null ? targetBrain.name : "NULL")}");

            if (targetBrain == null)
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - No ControllerBrain found");
                continue;
            }

            if (targetBrain == brain)
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Is own brain");
                continue;
            }

            // Validate faction
            if (useFactionDetection)
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] Checking faction...");

                if (!IsValidFaction(targetBrain))
                {
                    if (debugMode)
                        Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Invalid faction (not hostile)");
                    continue;
                }

                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] PASSED - Valid hostile faction");
            }

            // Check vision cone
            if (!IsInVisionCone(hit.transform.position))
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Outside vision cone");
                continue;
            }

            if (debugMode)
                Debug.Log($"[PerceptionModule] [{i}] PASSED - Inside vision cone");

            // Check line of sight
            if (requireLineOfSight && !HasLineOfSight(hit.transform.position))
            {
                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] SKIPPED - Line of sight blocked");
                continue;
            }

            if (debugMode && requireLineOfSight)
                Debug.Log($"[PerceptionModule] [{i}] PASSED - Line of sight clear");

            // Track closest valid target
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = targetBrain.transform;

                if (debugMode)
                    Debug.Log($"[PerceptionModule] [{i}] NEW BEST TARGET - Distance: {distance:F2}m");
            }
        }

        // Update current target
        if (bestTarget != currentTarget)
        {
            if (debugMode)
            {
                if (bestTarget != null)
                    Debug.Log($"[PerceptionModule] TARGET ACQUIRED: {bestTarget.name}");
                else if (currentTarget != null)
                    Debug.Log($"[PerceptionModule] TARGET LOST: {currentTarget.name}");
            }

            currentTarget = bestTarget;
        }

        // Update memory
        if (currentTarget != null)
        {
            lastKnownPosition = currentTarget.position;
            lastSeenTime = Time.time;
            hasMemory = enableMemory;
        }
    }

    private bool IsValidFaction(ControllerBrain targetBrain)
    {
        // If no faction detection, all targets are valid
        if (!useFactionDetection) return true;

        // If no faction handler, can't filter by faction
        if (factionHandler == null)
        {
            if (debugMode)
                Debug.LogWarning($"[PerceptionModule] No faction handler - cannot filter by faction");
            return true;
        }

        // Get target's faction handler
        if (targetBrain.Identity == null || targetBrain.Identity.Faction == null)
        {
            if (debugMode)
                Debug.Log($"[PerceptionModule] Target {targetBrain.name} has no faction - skipping");
            return false;
        }

        UniversalFactionHandler targetFaction = targetBrain.Identity.Faction;
        FactionType myFaction = factionHandler.AffiliatedFaction;
        FactionType theirFaction = targetFaction.AffiliatedFaction;

        if (debugMode)
            Debug.Log($"[PerceptionModule] Faction check: My={myFaction}, Their={theirFaction}");

        // Check relationship
        FactionRelationship relationship = FactionManager.GetRelationship(myFaction, theirFaction);

        if (debugMode)
            Debug.Log($"[PerceptionModule] Relationship: {relationship}");

        // Only detect hostiles if configured
        if (onlyDetectHostiles)
        {
            return relationship == FactionRelationship.Hostile;
        }

        // Otherwise detect any non-friendly
        return relationship != FactionRelationship.Friendly;
    }

    private bool IsInVisionCone(Vector3 targetPosition)
    {
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
        return angleToTarget < visionAngle / 2f;
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 directionToTarget = targetPosition - eyePosition;
        float distanceToTarget = directionToTarget.magnitude;

        // Raycast to check for obstacles
        if (Physics.Raycast(eyePosition, directionToTarget.normalized, out RaycastHit hit, distanceToTarget, obstacleLayers, QueryTriggerInteraction.Ignore))
        {
            // Hit an obstacle before reaching target
            if (debugMode)
                Debug.Log($"[PerceptionModule] LOS blocked by {hit.collider.name} at {hit.distance:F2}m");
            return false;
        }

        return true;
    }

    #endregion

    #region Public Methods

    public void ClearTarget()
    {
        currentTarget = null;
        hasMemory = false;
    }

    public void ForgetMemory()
    {
        hasMemory = false;
    }

    public void SetDetectionInterval(float interval)
    {
        detectionInterval = Mathf.Max(0.1f, interval);
    }

    public void SetVisionRange(float range)
    {
        visionRange = Mathf.Max(0f, range);
    }

    public void SetVisionAngle(float angle)
    {
        visionAngle = Mathf.Clamp(angle, 0f, 360f);
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!drawGizmos || !isEnabled) return;

        // Draw vision range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // Draw vision cone
        Vector3 forward = transform.forward * visionRange;
        Vector3 right = Quaternion.Euler(0, visionAngle / 2f, 0) * forward;
        Vector3 left = Quaternion.Euler(0, -visionAngle / 2f, 0) * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + right);
        Gizmos.DrawLine(transform.position, transform.position + left);

        // Draw arc
        int segments = 20;
        float angleStep = visionAngle / segments;
        Vector3 prevPoint = transform.position + right;
        for (int i = 1; i <= segments; i++)
        {
            float angle = -visionAngle / 2f + angleStep * i;
            Vector3 point = transform.position + Quaternion.Euler(0, angle, 0) * forward;
            Gizmos.DrawLine(prevPoint, point);
            prevPoint = point;
        }

        // Draw current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
            Gizmos.DrawWireSphere(currentTarget.position, 0.5f);
        }

        // Draw last known position
        if (hasMemory && currentTarget == null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.3f);
            Gizmos.DrawLine(transform.position, lastKnownPosition);
        }

        // Draw line of sight check
        if (currentTarget != null && requireLineOfSight)
        {
            Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(eyePosition, currentTarget.position);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!isEnabled) return;

        // Draw detailed info when selected
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * eyeHeight, 0.2f);
    }

    #endregion
}