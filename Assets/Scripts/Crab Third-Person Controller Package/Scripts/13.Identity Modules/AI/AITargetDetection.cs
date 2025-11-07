using UnityEngine;
using System;
using RPG.Factions;

/// <summary>
/// AI Target Detection - Companion script for AIModule
/// Handles all target acquisition, faction validation, and line of sight checking.
/// 
/// FIXED VERSION: Corrected PlayerInfoModule handler access pattern
/// </summary>
public class AITargetDetection : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private LayerMask detectionLayers = -1;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Faction Settings")]
    [SerializeField] private bool useFactionBasedDetection = true;
    [Tooltip("If true, only detect hostile targets. If false, detect all targets (old behavior)")]
    [SerializeField] private bool onlyDetectHostileTargets = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private AIModule aiModule;
    private ControllerBrain brain;
    private FactionAffiliationHandler factionHandler;
    private NPCModule npcModule;

    // Cache for performance
    private Collider[] detectionBuffer = new Collider[20];

    /// <summary>
    /// Initialize the target detection system
    /// </summary>
    public void Initialize(AIModule module, ControllerBrain controllerBrain)
    {
        aiModule = module;
        brain = controllerBrain;

        // Try to get faction handler (lazy initialization as it may not be ready yet)
        TryInitializeFactionHandler();

        if (debugMode)
        {
            Debug.Log($"[AITargetDetection] Initialized on {gameObject.name}");
        }
    }

    /// <summary>
    /// Attempt to initialize faction handler (may be called multiple times)
    /// </summary>
    private void TryInitializeFactionHandler()
    {
        if (factionHandler != null) return;

        if (brain != null)
        {
            factionHandler = brain.GetComponentInChildren<FactionAffiliationHandler>();
            npcModule = brain.GetModule<NPCModule>();

            if (factionHandler != null && debugMode)
            {
                Debug.Log($"[AITargetDetection] Faction handler initialized: {factionHandler.AffiliatedFaction}");
            }
        }
    }

    #region Public API

    /// <summary>
    /// Main detection method - finds valid targets in range
    /// Returns the closest valid target, or null if none found
    /// </summary>
    public Transform DetectTarget()
    {
        // Lazy init faction handler if needed
        if (factionHandler == null)
        {
            TryInitializeFactionHandler();
        }

        // Early exit if no detection range
        if (detectionRange <= 0f) return null;

        // Find all entities in detection range
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRange,
            detectionBuffer,
            detectionLayers
        );

        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = detectionBuffer[i];

            // Skip self
            if (hit.transform == transform) continue;

            // Get ControllerBrain (might be on child object)
            ControllerBrain targetBrain = hit.GetComponentInChildren<ControllerBrain>();
            if (targetBrain == null) continue;
            if (targetBrain == brain) continue; // Skip own brain

            // Validate target based on faction settings
            if (!IsValidTarget(targetBrain, hit.transform))
                continue;

            // Check line of sight if required
            if (requireLineOfSight && !HasLineOfSight(hit.transform))
                continue;

            // Track closest target
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = hit.transform;
            }
        }

        if (closestTarget != null && debugMode)
        {
            Debug.Log($"[AITargetDetection] Target detected: {closestTarget.name} at {closestDistance:F2} units");
        }

        return closestTarget;
    }

    /// <summary>
    /// Check if there's a clear line of sight to target
    /// </summary>
    public bool HasLineOfSight(Transform target)
    {
        if (target == null) return false;

        Vector3 directionToTarget = target.position - transform.position;
        float distanceToTarget = directionToTarget.magnitude;

        // Raycast from eye level (1 unit up from position)
        if (Physics.Raycast(
            transform.position + Vector3.up,
            directionToTarget.normalized,
            out RaycastHit rayHit,
            distanceToTarget,
            detectionLayers))
        {
            // Check if we hit the target or something in front of it
            return rayHit.transform == target || rayHit.transform.IsChildOf(target);
        }

        return false;
    }

    /// <summary>
    /// Get this entity's faction type
    /// </summary>
    public FactionType GetFaction()
    {
        if (factionHandler == null)
            TryInitializeFactionHandler();

        return factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;
    }

    /// <summary>
    /// Get relationship to a specific target entity
    /// </summary>
    public FactionRelationship GetRelationshipToTarget(Transform target)
    {
        if (factionHandler == null || target == null)
            return FactionRelationship.Neutral;

        var targetBrain = target.GetComponentInChildren<ControllerBrain>();
        if (targetBrain == null)
            return FactionRelationship.Neutral;

        FactionType? targetFaction = GetEntityFaction(targetBrain);
        if (!targetFaction.HasValue)
            return FactionRelationship.Neutral;

        return FactionManager.GetRelationship(factionHandler.AffiliatedFaction, targetFaction.Value);
    }

    /// <summary>
    /// Update detection range at runtime
    /// </summary>
    public void SetDetectionRange(float range)
    {
        detectionRange = Mathf.Max(0f, range);
    }

    /// <summary>
    /// Get current detection range
    /// </summary>
    public float GetDetectionRange() => detectionRange;

    /// <summary>
    /// Toggle faction-based detection
    /// </summary>
    public void SetUseFactionDetection(bool enabled)
    {
        useFactionBasedDetection = enabled;
    }

    /// <summary>
    /// Toggle hostile-only detection
    /// </summary>
    public void SetOnlyDetectHostile(bool enabled)
    {
        onlyDetectHostileTargets = enabled;
    }

    #endregion

    #region Private Validation Methods

    /// <summary>
    /// Validate if a target is valid based on faction settings
    /// </summary>
    private bool IsValidTarget(ControllerBrain targetBrain, Transform targetTransform)
    {
        // If not using faction detection, all targets are valid
        if (!useFactionBasedDetection)
            return true;

        // Need faction handler for faction-based detection
        if (factionHandler == null)
        {
            if (debugMode)
                Debug.LogWarning("[AITargetDetection] Faction-based detection enabled but no FactionAffiliationHandler found");
            return true; // Fallback to accepting all targets
        }

        // Get target's faction
        FactionType? targetFaction = GetEntityFaction(targetBrain);
        if (!targetFaction.HasValue)
        {
            // Target has no faction - treat as neutral
            return !onlyDetectHostileTargets;
        }

        // Check relationship
        FactionRelationship relationship = FactionManager.GetRelationship(
            factionHandler.AffiliatedFaction,
            targetFaction.Value
        );

        // If only detecting hostile targets
        if (onlyDetectHostileTargets)
        {
            // Must be hostile AND this NPC must be aggressive to hostile factions
            bool isValidHostileTarget = relationship == FactionRelationship.Hostile &&
                                       factionHandler.AggressiveToHostile;

            if (debugMode && !isValidHostileTarget)
            {
                Debug.Log($"[AITargetDetection] Rejecting {targetTransform.name}: " +
                         $"Relationship={relationship}, AggressiveToHostile={factionHandler.AggressiveToHostile}");
            }

            return isValidHostileTarget;
        }

        // Detecting all targets regardless of relationship
        return true;
    }

    /// <summary>
    /// Get the faction of any entity (player or NPC)
    /// FIXED: Corrected handler access pattern for PlayerInfoModule
    /// </summary>
    private FactionType? GetEntityFaction(ControllerBrain targetBrain)
    {
        if (targetBrain == null) return null;

        // Try PlayerInfoModule first (for players)
        var playerInfo = targetBrain.GetModule<PlayerInfoModule>();
        if (playerInfo != null)
        {
            // FIXED: Use property access instead of GetHandler<T>()
            var playerFactionHandler = playerInfo.FactionHandler;
            if (playerFactionHandler != null)
            {
                return playerFactionHandler.PlayerFaction;
            }
        }

        // Try NPCModule (for NPCs)
        var npcModule = targetBrain.GetModule<NPCModule>();
        if (npcModule != null)
        {
            // NPCModule uses GetHandler<T>() pattern
            var npcFactionHandler = npcModule.GetHandler<FactionAffiliationHandler>();
            if (npcFactionHandler != null)
            {
                return npcFactionHandler.AffiliatedFaction;
            }
        }

        // Entity has no faction info
        return null;
    }

    #endregion

    #region Debug & Gizmos

    private void OnDrawGizmosSelected()
    {
        // Draw detection range sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }

    [ContextMenu("Debug: Test Detection")]
    private void DebugTestDetection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AITargetDetection] Must be in Play mode to test detection");
            return;
        }

        TryInitializeFactionHandler();

        Debug.Log($"=== Testing Target Detection ===");
        Debug.Log($"My Faction: {GetFaction()}");
        Debug.Log($"Detection Range: {detectionRange}");
        Debug.Log($"Faction Detection: {useFactionBasedDetection}");
        Debug.Log($"Only Hostile: {onlyDetectHostileTargets}");

        Transform target = DetectTarget();
        if (target != null)
        {
            Debug.Log($"<color=green>Target Found: {target.name}</color>");
            Debug.Log($"Relationship: {GetRelationshipToTarget(target)}");
        }
        else
        {
            Debug.Log("<color=yellow>No valid targets detected</color>");
        }
    }

    [ContextMenu("Debug: List All Entities In Range")]
    private void DebugListEntities()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AITargetDetection] Must be in Play mode");
            return;
        }

        TryInitializeFactionHandler();

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectionRange,
            detectionBuffer,
            detectionLayers
        );

        Debug.Log($"=== Entities in Range ({hitCount}) ===");

        for (int i = 0; i < hitCount; i++)
        {
            var targetBrain = detectionBuffer[i].GetComponentInChildren<ControllerBrain>();
            if (targetBrain == null || targetBrain == brain) continue;

            FactionType? targetFaction = GetEntityFaction(targetBrain);
            string factionStr = targetFaction.HasValue ? targetFaction.Value.ToString() : "No Faction";

            bool isValid = IsValidTarget(targetBrain, detectionBuffer[i].transform);
            string validStr = isValid ? "<color=green>VALID</color>" : "<color=red>INVALID</color>";

            Debug.Log($"  {detectionBuffer[i].name}: {factionStr} - {validStr}");
        }
    }

    #endregion
}