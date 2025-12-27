using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Pathfinding Module - NavMesh wrapper for AI navigation
/// 
/// Provides clean interface for GOAP goals to request pathfinding.
/// Wraps Unity's NavMeshAgent with better error handling and status queries.
/// 
/// Features:
/// - SetDestination with automatic path calculation
/// - Path status queries (calculating, valid, blocked)
/// - Distance to destination
/// - Stop/Resume navigation
/// - Dynamic obstacle avoidance
/// 
/// Integration:
/// - Used by GOAPContext for movement queries
/// - Used by movement goals (ApproachGoal, FlankGoal, etc)
/// - Works with NPCMovementModule for actual locomotion
/// </summary>
public class PathfindingModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("NavMesh Settings")]
    [Tooltip("Auto-create NavMeshAgent if missing")]
    [SerializeField] private bool autoCreateAgent = true;

    [Tooltip("Stopping distance from destination")]
    [SerializeField] private float stoppingDistance = 0.5f;

    [Tooltip("Agent acceleration")]
    [SerializeField] private float acceleration = 8f;

    [Tooltip("Agent angular speed (rotation)")]
    [SerializeField] private float angularSpeed = 360f;

    [Header("Path Validation")]
    [Tooltip("Maximum valid path distance (prevents impossible paths)")]
    [SerializeField] private float maxPathDistance = 100f;

    [Tooltip("Replan path if destination moves this far")]
    [SerializeField] private float replanThreshold = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool drawPath = true;

    // References
    private ControllerBrain brain;
    private NavMeshAgent agent;
    private MovementSystem movementSystem;

    // State tracking
    private Vector3 currentDestination;
    private bool hasDestination;
    private float lastReplanTime;
    private float replanCooldown = 0.5f; // Don't replan too frequently

    #region Properties

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    /// <summary>
    /// Is the agent currently navigating to a destination?
    /// </summary>
    public bool HasPath => agent != null && agent.hasPath;

    /// <summary>
    /// Is the agent calculating a path?
    /// </summary>
    public bool IsPathCalculating => agent != null && agent.pathPending;

    /// <summary>
    /// Has the agent reached its destination?
    /// </summary>
    public bool HasReachedDestination
    {
        get
        {
            if (!hasDestination || agent == null) return false;

            if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Distance remaining to destination
    /// </summary>
    public float RemainingDistance => agent != null ? agent.remainingDistance : float.MaxValue;

    /// <summary>
    /// Is the current path valid?
    /// </summary>
    public bool IsPathValid
    {
        get
        {
            if (agent == null || !agent.hasPath) return false;

            // Check if path is complete and reachable
            return agent.pathStatus == NavMeshPathStatus.PathComplete;
        }
    }

    /// <summary>
    /// Current destination position
    /// </summary>
    public Vector3 Destination => currentDestination;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        movementSystem = brain.GetModule<MovementSystem>();

        // Get or create NavMeshAgent
        agent = GetComponent<NavMeshAgent>();

        if (agent == null && autoCreateAgent)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();

            if (debugMode)
                Debug.Log($"[PathfindingModule] Created NavMeshAgent on {brain.name}");
        }

        if (agent == null)
        {
            Debug.LogError($"[PathfindingModule] No NavMeshAgent found on {brain.name}!");
            isEnabled = false;
            return;
        }

        // Configure agent
        agent.stoppingDistance = stoppingDistance;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.autoBraking = true;
        agent.autoRepath = true;

        // IMPORTANT: Disable NavMeshAgent's built-in movement
        // We'll use NPCMovementModule for actual locomotion
        agent.updatePosition = false;
        agent.updateRotation = false;


    }

    public void UpdateModule()
    {
        if (!isEnabled || agent == null) return;

        // Update agent position to match actual transform
        // (Since we disabled updatePosition, we need to sync manually)
        if (movementSystem != null)  // ← ERROR: movementSystem doesn't exist
        {
            agent.nextPosition = transform.position;
        }

        // Check if we need to replan due to destination movement
        if (hasDestination && Time.time - lastReplanTime > replanCooldown)
        {
            CheckReplan();
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Set a destination for the agent to navigate to
    /// </summary>
    public bool SetDestination(Vector3 destination)
    {
        if (!isEnabled || agent == null) return false;

        // Sample NavMesh to ensure destination is valid
        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 2f, NavMesh.AllAreas))
        {
            destination = hit.position;
        }
        else
        {
            if (debugMode)
                Debug.LogWarning($"[PathfindingModule] Destination not on NavMesh: {destination}");
            return false;
        }

        // Set destination
        bool success = agent.SetDestination(destination);

        if (success)
        {
            currentDestination = destination;
            hasDestination = true;
            lastReplanTime = Time.time;

            if (debugMode)
                Debug.Log($"[PathfindingModule] Set destination: {destination}, Distance: {agent.remainingDistance:F2}");
        }
        else
        {
            if (debugMode)
                Debug.LogWarning($"[PathfindingModule] Failed to set destination: {destination}");
        }

        return success;
    }

    /// <summary>
    /// Stop current navigation
    /// </summary>
    public void Stop()
    {
        if (agent != null && agent.hasPath)
        {
            agent.ResetPath();
            hasDestination = false;

            if (debugMode)
                Debug.Log("[PathfindingModule] Stopped navigation");
        }
    }

    /// <summary>
    /// Resume navigation to current destination
    /// </summary>
    public void Resume()
    {
        if (hasDestination)
        {
            SetDestination(currentDestination);
        }
    }

    /// <summary>
    /// Get the next position along the path
    /// Used by NPCMovementModule to move toward path
    /// </summary>
    public Vector3 GetNextPathPosition()
    {
        if (agent == null || !agent.hasPath) return transform.position;

        return agent.steeringTarget;
    }

    /// <summary>
    /// Check if a destination is reachable
    /// </summary>
    public bool IsDestinationReachable(Vector3 destination)
    {
        if (agent == null) return false;

        NavMeshPath path = new NavMeshPath();
        agent.CalculatePath(destination, path);

        return path.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// Get distance to a position along the NavMesh
    /// </summary>
    public float GetPathDistance(Vector3 destination)
    {
        if (agent == null) return float.MaxValue;

        NavMeshPath path = new NavMeshPath();
        agent.CalculatePath(destination, path);

        if (path.status == NavMeshPathStatus.PathComplete)
        {
            float distance = 0f;
            for (int i = 0; i < path.corners.Length - 1; i++)
            {
                distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
            }
            return distance;
        }

        return float.MaxValue;
    }

    /// <summary>
    /// Set agent speed (affects NavMeshAgent's speed)
    /// </summary>
    public void SetSpeed(float speed)
    {
        if (agent != null)
        {
            agent.speed = speed;
        }
    }

    #endregion

    #region Internal Helpers

    /// <summary>
    /// Check if we need to replan due to destination movement
    /// </summary>
    private void CheckReplan()
    {
        if (!hasDestination) return;

        // Check if destination has moved significantly
        float distanceFromOriginal = Vector3.Distance(agent.destination, currentDestination);

        if (distanceFromOriginal > replanThreshold)
        {
            if (debugMode)
                Debug.Log($"[PathfindingModule] Destination moved {distanceFromOriginal:F2}m, replanning");

            SetDestination(currentDestination);
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!drawPath || agent == null || !agent.hasPath) return;

        // Draw path
        Gizmos.color = Color.yellow;
        Vector3[] corners = agent.path.corners;

        for (int i = 0; i < corners.Length - 1; i++)
        {
            Gizmos.DrawLine(corners[i], corners[i + 1]);
            Gizmos.DrawWireSphere(corners[i], 0.2f);
        }

        // Draw destination
        if (hasDestination)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentDestination, stoppingDistance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugMode || agent == null) return;

        // Draw agent info
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        // Draw remaining distance text
#if UNITY_EDITOR
        if (hasDestination)
        {
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"Distance: {RemainingDistance:F2}m\nStatus: {(agent.hasPath ? agent.pathStatus.ToString() : "No Path")}"
            );
        }
#endif
    }

    #endregion
}