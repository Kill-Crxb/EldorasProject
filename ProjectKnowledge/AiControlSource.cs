using UnityEngine;

/// <summary>
/// AI Control Source - Bridges PathfindingModule to MovementSystem
/// 
/// This is THE KEY integration point for GOAP and NPC movement.
/// 
/// Architecture:
/// - Strategic Layer: PathfindingModule calculates NavMesh path (where to go)
/// - Tactical Layer: AIControlSource reads waypoints, converts to input (how to follow)
/// - Execution Layer: LocomotionHandler moves entity smoothly (CharacterController)
/// 
/// This source makes NPCs follow NavMesh paths with smooth CharacterController movement.
/// No more NavMeshAgent teleportation!
/// </summary>
public class AIControlSource : MonoBehaviour, IMovementControlSource
{
    [Header("AI Settings")]
    [Tooltip("How close to waypoint before considering it reached")]
    [SerializeField] private float waypointReachThreshold = 0.5f;

    [Tooltip("Auto-sprint when chasing (GOAP ApproachGoal active)")]
    [SerializeField] private bool autoSprintWhenChasing = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // References
    private ControllerBrain brain;
    private PathfindingModule pathfinding;
    private GOAPModule goapModule;
    private Transform rootTransform;

    // State
    private bool isActive;
    private Vector2 currentMoveInput;
    private Vector2 currentLookInput;
    private bool currentSprint;

    // ========================================
    // IMovementControlSource Implementation
    // ========================================

    public MovementInput GetMovementInput()
    {
        // Calculate fresh input every call
        CalculateMovementInput();

        return new MovementInput
        {
            MoveDirection = currentMoveInput,
            LookDirection = currentLookInput,
            Sprint = currentSprint,
            Jump = false,
            Dash = false
        };
    }

    public void OnActivated()
    {
        isActive = true;

        // Get references
        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError("[AIControlSource] ControllerBrain not found!");
            return;
        }

        pathfinding = brain.GetModule<PathfindingModule>();
        goapModule = brain.GetModule<GOAPModule>();
        rootTransform = brain.transform;

        if (pathfinding == null)
        {
            Debug.LogWarning("[AIControlSource] PathfindingModule not found - AI movement won't work!");
        }

        if (showDebugInfo)
            Debug.Log($"[AIControlSource] Activated on {brain.name}");
    }

    public void OnDeactivated()
    {
        isActive = false;
        currentMoveInput = Vector2.zero;
        currentLookInput = Vector2.zero;
        currentSprint = false;

        if (showDebugInfo)
            Debug.Log($"[AIControlSource] Deactivated on {brain.name}");
    }

    public void UpdateSource()
    {
        // Nothing needed here - input calculated on-demand in GetMovementInput
    }

    public bool IsActive => isActive;
    public string SourceName => "AI Control";

    // ========================================
    // Input Calculation
    // ========================================

    private void CalculateMovementInput()
    {
        // Reset to zero
        currentMoveInput = Vector2.zero;
        currentLookInput = Vector2.zero;
        currentSprint = false;

        // Check if we have pathfinding and a path
        if (pathfinding == null || !pathfinding.HasPath || rootTransform == null)
            return;

        // Get next waypoint from pathfinding
        Vector3 nextWaypoint = pathfinding.GetNextPathPosition();

        // Calculate direction to waypoint
        Vector3 directionToWaypoint = nextWaypoint - rootTransform.position;
        directionToWaypoint.y = 0f; // Flatten to XZ plane

        float distanceToWaypoint = directionToWaypoint.magnitude;

        // Check if reached waypoint
        if (distanceToWaypoint < waypointReachThreshold)
        {
            // At waypoint - stop moving
            return;
        }

        // Normalize direction
        directionToWaypoint.Normalize();

        // Convert to 2D input (world space)
        currentMoveInput = new Vector2(directionToWaypoint.x, directionToWaypoint.z);

        // Look in movement direction
        currentLookInput = currentMoveInput;

        // Determine sprint based on context
        currentSprint = ShouldSprint();

        if (showDebugInfo && currentMoveInput.magnitude > 0.1f)
        {
            Debug.Log($"[AIControlSource] Move: {currentMoveInput}, Sprint: {currentSprint}, Distance: {distanceToWaypoint:F2}");
        }
    }

    private bool ShouldSprint()
    {
        if (!autoSprintWhenChasing)
            return false;

        // Sprint if chasing (GOAP has ApproachGoal or similar combat goal active)
        if (goapModule != null)
        {
            var activeGoal = goapModule.CurrentGoal;
            if (activeGoal != null)
            {
                string goalName = activeGoal.GetType().Name;
                // Sprint during combat/chase goals
                return goalName == "ApproachGoal" || goalName == "FlankGoal" || goalName == "RetreatGoal";
            }
        }

        // Default: run when moving (not sprint, not walk)
        return false;
    }

    // ========================================
    // Debug Visualization
    // ========================================

    private void OnDrawGizmos()
    {
        if (!showDebugInfo || !isActive || pathfinding == null || !pathfinding.HasPath)
            return;

        // Draw direction arrow
        Vector3 direction3D = new Vector3(currentMoveInput.x, 0f, currentMoveInput.y);

        Gizmos.color = currentSprint ? Color.red : Color.cyan;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, direction3D * 2f);

        // Draw next waypoint
        Vector3 nextWaypoint = pathfinding.GetNextPathPosition();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(nextWaypoint, waypointReachThreshold);
    }
}