using UnityEngine;

/// <summary>
/// GOAP Context - World state representation for goal evaluation
/// 
/// Contains all information needed for GOAP goals to:
/// - Evaluate if they can execute (permissions)
/// - Calculate their weight/desirability
/// - Execute their behavior
/// 
/// Features:
/// - Spatial data (positions, distances, angles)
/// - State data (health, stamina, resources)
/// - Module references (cached for performance)
/// - Busy state tracking with watchdog protection
/// </summary>
public class GOAPContext
{
    #region References
    public ControllerBrain brain;
    public Transform self;
    public Transform target;
    #endregion

    #region Spatial Data
    public Vector3 selfPosition;
    public Vector3 targetPosition;
    public Vector3 toTarget;
    public float distanceToTarget;
    public float angleToTarget;
    #endregion

    #region State Data
    public float healthPercent;
    public float currentHealth;
    public float maxHealth;
    public float staminaPercent;
    public float currentStamina;
    public float maxStamina;
    public bool hasAlliesNearby;
    public int allyCount;
    #endregion

    #region Execution State (Busy System)
    public bool isBusy;
    public bool isAnimationLocked;
    public bool isStaggered;

    // Busy watchdog (PRODUCTION FIX: Prevent deadlock)
    private float busyTimeout = 5f;
    private float busySinceTime;
    private bool wasBusyLastFrame;
    #endregion

    #region Module References (Cached)
    public IAbilityProvider abilityModule;
    public MovementSystem movementSystem; // UPDATED: Was IMovementProvider
    public IHealthProvider healthModule;
    public IResourceProvider resourceModule;
    // REMOVED: AnimationStateModule - no longer exists
    public PathfindingModule pathfinding;
    public PerceptionModule perception;
    #endregion

    #region Goal State
    public bool goalComplete;
    public bool goalInterrupted;
    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        self = brain.transform;

        // Cache module references
        abilityModule = brain.Abilities;
        movementSystem = brain.Movement; // UPDATED
        healthModule = brain.GetModuleImplementing<IHealthProvider>();
        resourceModule = brain.GetModuleImplementing<IResourceProvider>();
        pathfinding = brain.GetModule<PathfindingModule>();
        perception = brain.GetModule<PerceptionModule>();

        if (abilityModule == null)
            Debug.LogWarning("[GOAPContext] No AbilitySystem found - abilities won't work");
        if (movementSystem == null)
            Debug.LogWarning("[GOAPContext] No MovementSystem found - movement won't work");
        if (healthModule == null)
            Debug.LogWarning("[GOAPContext] No IHealthProvider found - health checks won't work");
    }

    #endregion

    #region Context Update

    /// <summary>
    /// Update all context data - called every frame by GOAPModule
    /// </summary>
    public void UpdateContext()
    {
        // Get current target from perception system
        if (perception != null)
        {
            target = perception.CurrentTarget;
        }

        // Update spatial data
        if (target != null)
        {
            selfPosition = self.position;
            targetPosition = target.position;
            toTarget = targetPosition - selfPosition;
            distanceToTarget = toTarget.magnitude;

            Vector3 selfForward = self.forward;
            angleToTarget = Vector3.Angle(selfForward, toTarget);
        }
        else
        {
            // No target - clear spatial data
            targetPosition = Vector3.zero;
            toTarget = Vector3.zero;
            distanceToTarget = float.MaxValue;
            angleToTarget = 0f;
        }

        // Update health/resources
        if (healthModule != null)
        {
            currentHealth = healthModule.GetCurrentHealth();
            maxHealth = healthModule.GetMaxHealth();
            healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;
        }

        if (resourceModule != null)
        {
            currentStamina = resourceModule.GetResource(ResourceType.Stamina);
            maxStamina = resourceModule.GetMaxResource(ResourceType.Stamina);
            staminaPercent = maxStamina > 0 ? currentStamina / maxStamina : 0f;
        }

        // Check for allies (simple sphere check on Enemy layer)
        Collider[] nearbyAllies = Physics.OverlapSphere(
            selfPosition, 10f, LayerMask.GetMask("Enemy")
        );
        allyCount = nearbyAllies.Length - 1; // Exclude self
        hasAlliesNearby = allyCount > 0;

        // Update busy state with watchdog (PRODUCTION FIX)
        UpdateBusyStateWithWatchdog();

        // Reset flags
        goalComplete = false;
        goalInterrupted = false;
    }

    #endregion

    #region Busy Watchdog System

    /// <summary>
    /// Update busy state and check for deadlocks
    /// PRODUCTION FIX: Prevents entities from getting stuck in "busy" state forever
    /// </summary>
    private void UpdateBusyStateWithWatchdog()
    {
        // Check all busy sources
        bool newBusy = false;

        // Check if ability is executing
        if (abilityModule != null)
        {
            var abilityMod = abilityModule as AbilitySystem;
            if (abilityMod != null)
                newBusy |= abilityMod.IsExecuting;
        }

        // REMOVED: Animation lock check - AnimationStateModule no longer exists
        // Animation locking is now handled internally by abilities/combat
        isAnimationLocked = false;

        // Check if staggered (placeholder - implement based on your stagger system)
        isStaggered = false;
        newBusy |= isStaggered;

        // Track busy duration
        if (newBusy)
        {
            // First frame of being busy
            if (!wasBusyLastFrame)
            {
                busySinceTime = Time.time;
            }

            isBusy = true;

            // WATCHDOG: Check for timeout
            float busyDuration = Time.time - busySinceTime;
            if (busyDuration > busyTimeout)
            {
                Debug.LogError($"[GOAP] DEADLOCK DETECTED on {self.name}: Busy for {busyDuration:F1}s, forcing reset!");
                ForceClearBusy();
            }
        }
        else
        {
            isBusy = false;
        }

        wasBusyLastFrame = isBusy;
    }

    /// <summary>
    /// Force-clear busy state when watchdog triggers
    /// PRODUCTION FIX: Recovery mechanism for deadlocked entities
    /// </summary>
    private void ForceClearBusy()
    {
        isBusy = false;

        // Force-clear ability execution
        var abilityMod = abilityModule as AbilitySystem;
        if (abilityMod != null)
        {
            abilityMod.CancelCurrentAbility();
        }

        // REMOVED: Animation unlock - AnimationStateModule no longer exists

        Debug.Log($"[GOAP] Busy state force-cleared on {self.name}, entity recovered");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check if we can execute an ability by slot key
    /// </summary>
    public bool CanUseAbility(string slotKey)
    {
        if (abilityModule == null) return false;

        var abilityMod = abilityModule as AbilitySystem;
        if (abilityMod == null) return false;

        return abilityMod.CanUseAbility(slotKey);
    }

    /// <summary>
    /// Check if we're facing the target (within angle threshold)
    /// </summary>
    public bool IsFacingTarget(float angleThreshold = 45f)
    {
        return angleToTarget <= angleThreshold;
    }

    /// <summary>
    /// Check if target is in range
    /// </summary>
    public bool IsTargetInRange(float range)
    {
        return distanceToTarget <= range;
    }

    /// <summary>
    /// Get normalized direction to target
    /// </summary>
    public Vector3 GetDirectionToTarget()
    {
        return toTarget.magnitude > 0.01f ? toTarget.normalized : Vector3.zero;
    }

    #endregion
}