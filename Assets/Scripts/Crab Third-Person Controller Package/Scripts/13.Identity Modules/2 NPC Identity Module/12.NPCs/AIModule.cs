using UnityEngine;
using System;
using RPG.Factions;

public enum AIState
{
    Idle,
    Chase,
    Combat,
    Patrol,
    Flee,
    Investigate
}

/// <summary>
/// AI Module - Manages NPC behavior states and coordination.
/// Delegates combat execution to IAICombatBehavior implementations.
/// 
/// PHASE 2 COMPLETE: ✅ Entity-Agnostic Faction Detection
/// - Detects ANY entity with ControllerBrain (player or NPC)
/// - Checks faction relationships dynamically via FactionManager
/// - No hardcoded tag checking or entity-type assumptions
/// - Supports player-vs-NPC, NPC-vs-NPC, and any future entity types
/// 
/// Detection Flow:
/// 1. Find entities with ControllerBrain in detection range
/// 2. Get their faction (via PlayerInfoModule or NPCModule)
/// 3. Check relationship using FactionManager.GetRelationship()
/// 4. Attack if Hostile, ignore if Neutral/Friendly
/// </summary>
public class AIModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    public bool isEnabled = true;

    [Header("AI State")]
    [SerializeField] private AIState currentState = AIState.Idle;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRange = 12f;
    [SerializeField] private LayerMask detectionLayers = -1;
    [SerializeField] private bool requireLineOfSight = false;

    [Header("Faction Settings (Phase 2)")]
    [SerializeField] private bool useFactionBasedDetection = true;
    [Tooltip("If true, only detect hostile targets. If false, detect all targets (old behavior)")]
    [SerializeField] private bool onlyDetectHostileTargets = true;

    [Header("Behavior Settings")]
    [SerializeField] private float combatTimeout = 5f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float combatExitBuffer = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool debugMode = false;
    [SerializeField] private Transform currentTarget;

    private ControllerBrain brain;
    private NPCMovementModule npcMovement;
    private IAICombatBehavior combatBehavior;
    private NPCModule npcModule;
    private FactionAffiliationHandler factionHandler;

    private float lastSeenTargetTime;
    private float lastAttackTime;

    private float cachedAttackRange;
    private float cachedExitCombatRange;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public AIState CurrentState => currentState;
    public Transform CurrentTarget => currentTarget;

    // Events
    public event Action<AIState, AIState> OnStateChanged;
    public event Action<Transform> OnTargetAcquired;
    public event Action OnTargetLost;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Try multiple ways to find NPCMovementModule
        npcMovement = brain.GetModule<NPCMovementModule>();

        if (npcMovement == null)
        {
            // Fallback: Search in children
            npcMovement = brain.GetComponentInChildren<NPCMovementModule>();

            if (npcMovement == null)
            {
                // Final fallback: Search from root
                npcMovement = GetComponentInParent<NPCMovementModule>();
            }
        }

        if (npcMovement == null)
        {
            Debug.LogError($"[AIModule] NPCMovementModule NOT FOUND on {gameObject.name}! AI will not work.");
            isEnabled = false;
            return;
        }

        if (debugMode)
        {
            Debug.Log($"[AIModule] Found NPCMovementModule on {npcMovement.gameObject.name}");
        }

        // Get combat behavior from Brain (it's now an IBrainModule)
        combatBehavior = brain.GetModule<AICombatBehaviorModule>() as IAICombatBehavior;

        // Fallback: If not found via Brain, try direct child search
        if (combatBehavior == null)
        {
            combatBehavior = GetComponentInChildren<IAICombatBehavior>();
        }

        if (combatBehavior != null)
        {
            // Initialize the combat behavior with AIModule reference
            combatBehavior.Initialize(this, brain);

            if (debugMode)
            {
                Debug.Log($"[AIModule] Found combat behavior: {combatBehavior.GetType().Name}");
            }
        }
        else
        {
            Debug.LogWarning($"[AIModule] No IAICombatBehavior found on {gameObject.name}");
        }

        // ========================================
        // PHASE 2: Initialize Faction System
        // ========================================
        // NOTE: We try to get faction handler here, but it might not be ready yet
        // because handlers initialize AFTER modules. We'll use lazy initialization.
        TryInitializeFactionHandler();

        lastAttackTime = -attackCooldown;
        ChangeState(AIState.Idle);
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdleState();
                break;
            case AIState.Chase:
                UpdateChaseState();
                break;
            case AIState.Combat:
                UpdateCombatState();
                break;
        }
    }

    #region State Updates

    void UpdateIdleState()
    {
        Transform detectedTarget = DetectTarget();

        if (detectedTarget != null)
        {
            currentTarget = detectedTarget;
            lastSeenTargetTime = Time.time;
            ChangeState(AIState.Chase);
            OnTargetAcquired?.Invoke(currentTarget);
        }
    }

    void UpdateChaseState()
    {
        if (currentTarget == null)
        {
            ChangeState(AIState.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        float attackRange = combatBehavior.AttackRange;

        // Debug logging removed - too spammy
        // if (debugMode)
        // {
        //     Debug.Log($"[AIModule] Chase - Distance: {distance:F2}, Attack Range: {attackRange}");
        // }

        // Transition to combat when in attack range
        if (distance <= attackRange)
        {
            if (combatBehavior.CanEnterCombat())
            {
                ChangeState(AIState.Combat);
                return;
            }
        }

        // Check if target escaped
        if (distance > detectionRange)
        {
            if (Time.time - lastSeenTargetTime > combatTimeout)
            {
                currentTarget = null;
                ChangeState(AIState.Idle);
                OnTargetLost?.Invoke();
                return;
            }
        }
        else
        {
            lastSeenTargetTime = Time.time;
        }

        // Always rotate toward target while chasing
        if (npcMovement != null)
        {
            npcMovement.RotateTowards(currentTarget.position);
        }

        // Move towards target
        npcMovement.MoveTowards(currentTarget.position);
    }

    void UpdateCombatState()
    {
        if (currentTarget == null)
        {
            ChangeState(AIState.Idle);
            return;
        }

        float distance = Vector3.Distance(transform.position, currentTarget.position);
        float exitRange = combatBehavior.ExitCombatRange;

        // Exit combat if target too far
        if (distance > exitRange + combatExitBuffer)
        {
            ChangeState(AIState.Chase);
            return;
        }

        // Always face target in combat
        if (npcMovement != null)
        {
            npcMovement.RotateTowards(currentTarget.position);
        }

        // Let combat behavior handle attacking
        if (combatBehavior != null && Time.time >= lastAttackTime + attackCooldown)
        {
            combatBehavior.ExecuteAttack();
            lastAttackTime = Time.time;
        }
    }

    #endregion

    #region Target Detection

    Transform DetectTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, detectionLayers);

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;

            // Check if target has a ControllerBrain (any entity - player or NPC)
            // Use GetComponentInChildren since ControllerBrain might be on a child GameObject
            var targetBrain = hit.GetComponentInChildren<ControllerBrain>();
            if (targetBrain == null) continue;

            // Skip self
            if (targetBrain == brain) continue;

            // Check if target is alive
            var targetResources = targetBrain.GetModule<RPGResources>();
            if (targetResources != null && targetResources.CurrentHealth <= 0)
            {
                continue; // Skip dead targets
            }

            // PHASE 2: Faction-based detection
            if (useFactionBasedDetection && onlyDetectHostileTargets)
            {
                if (!IsHostileTarget(targetBrain))
                {
                    if (debugMode)
                    {
                        Debug.Log($"[AIModule] Detected {targetBrain.name} but not hostile. Ignoring.");
                    }
                    continue; // Skip non-hostile targets
                }
            }

            // Line of sight check (if enabled)
            if (requireLineOfSight)
            {
                if (!HasLineOfSight(hit.transform))
                {
                    continue;
                }
            }

            if (debugMode)
            {
                Debug.Log($"[AIModule] HOSTILE target detected: {targetBrain.name}");
            }

            return hit.transform;
        }

        return null;
    }

    // ========================================
    // PHASE 2: Faction System (Lazy Initialization)
    // ========================================

    /// <summary>
    /// Try to initialize faction handler (lazy initialization)
    /// Called during Initialize() and whenever faction handler is needed
    /// </summary>
    private void TryInitializeFactionHandler()
    {
        // Already have it
        if (factionHandler != null) return;

        // Try to get NPCModule
        if (npcModule == null)
        {
            npcModule = brain.GetModule<NPCModule>();
            if (npcModule == null)
            {
                npcModule = brain.GetComponentInChildren<NPCModule>();
            }
        }

        // Try to get FactionAffiliationHandler from NPCModule
        if (npcModule != null)
        {
            factionHandler = npcModule.GetHandler<FactionAffiliationHandler>();

            if (factionHandler != null)
            {
                // SUCCESS! Re-enable faction detection if it was disabled
                if (!useFactionBasedDetection)
                {
                    useFactionBasedDetection = true;
                    Debug.Log($"<color=cyan>[AIModule] Faction system initialized (lazy). Faction: {factionHandler.AffiliatedFaction}. Re-enabled faction detection.</color>");
                }
                else
                {
                    Debug.Log($"<color=cyan>[AIModule] Faction system initialized. Faction: {factionHandler.AffiliatedFaction}</color>");
                }
            }
        }

        // Don't permanently disable - just skip faction checks until it's found
        // The flag useFactionBasedDetection stays true so we keep trying
    }

    /// <summary>
    /// PHASE 2 REFACTORED: Entity-agnostic faction checking
    /// Checks if ANY entity (player or NPC) is hostile based on faction relationships
    /// </summary>
    private bool IsHostileTarget(ControllerBrain targetBrain)
    {
        // Try to initialize faction handler if we don't have it yet (lazy initialization)
        if (factionHandler == null)
        {
            TryInitializeFactionHandler();
        }

        // If still no faction system, default to hostile (old behavior)
        if (factionHandler == null)
        {
            Debug.LogWarning($"<color=red>[AIModule] factionHandler is NULL! Defaulting to hostile. Faction system failed to initialize.</color>");
            return true;
        }

        // Try to get target's faction
        FactionType? targetFaction = GetEntityFaction(targetBrain);

        if (!targetFaction.HasValue)
        {
            // Target has no faction - default to non-hostile
            Debug.Log($"[AIModule] Target {targetBrain.name} has no faction");
            return false;
        }

        // Check faction relationship
        FactionRelationship relationship = FactionManager.GetRelationship(
            factionHandler.AffiliatedFaction,
            targetFaction.Value
        );

        bool isHostile = relationship == FactionRelationship.Hostile && factionHandler.AggressiveToHostile;

        Debug.Log($"<color=cyan>[AIModule] Faction Check: {factionHandler.AffiliatedFaction} vs {targetFaction.Value} = {relationship} → Hostile: {isHostile}</color>");

        return isHostile;
    }

    /// <summary>
    /// Get faction of any entity (player or NPC)
    /// </summary>
    private FactionType? GetEntityFaction(ControllerBrain targetBrain)
    {
        if (targetBrain == null) return null;

        // Check if it's a player (has PlayerInfoModule)
        var playerInfo = targetBrain.GetModule<PlayerInfoModule>();
        if (playerInfo != null && playerInfo.FactionHandler != null)
        {
            return playerInfo.GetPlayerFaction();
        }

        // Check if it's an NPC (has NPCModule)
        var npcModule = targetBrain.GetModule<NPCModule>();
        if (npcModule != null)
        {
            var npcFaction = npcModule.GetHandler<FactionAffiliationHandler>();
            if (npcFaction != null)
            {
                return npcFaction.AffiliatedFaction;
            }
        }

        // Entity has no faction info
        return null;
    }

    /// <summary>
    /// Check if there's a clear line of sight to target
    /// </summary>
    private bool HasLineOfSight(Transform target)
    {
        Vector3 directionToTarget = target.position - transform.position;
        if (Physics.Raycast(transform.position + Vector3.up, directionToTarget.normalized,
            out RaycastHit rayHit, detectionRange))
        {
            return rayHit.transform == target;
        }
        return false;
    }

    #endregion

    #region State Management

    public void ChangeState(AIState newState)
    {
        if (currentState == newState) return;

        AIState oldState = currentState;
        currentState = newState;

        // Notify combat behavior of state changes
        if (combatBehavior != null)
        {
            if (newState == AIState.Combat)
                combatBehavior.OnCombatEnter(currentTarget);
            else if (oldState == AIState.Combat)
                combatBehavior.OnCombatExit();
        }

        Debug.Log($"[AIModule] {gameObject.name} state: {oldState} → {newState}");
        OnStateChanged?.Invoke(oldState, newState);
    }

    public void ForceTargetLost()
    {
        currentTarget = null;
        if (currentState != AIState.Idle)
        {
            ChangeState(AIState.Idle);
        }
        OnTargetLost?.Invoke();
    }

    #endregion

    #region Public API

    public bool HasTarget() => currentTarget != null;
    public bool IsInCombat() => currentState == AIState.Combat;

    public float GetDistanceToTarget()
    {
        if (currentTarget == null) return float.MaxValue;
        return Vector3.Distance(transform.position, currentTarget.position);
    }

    public void SetDetectionRange(float range)
    {
        detectionRange = Mathf.Max(0f, range);
    }

    public IAICombatBehavior GetCombatBehavior() => combatBehavior;

    /// <summary>
    /// PHASE 2: Get this NPC's faction
    /// </summary>
    public FactionType GetFaction()
    {
        return factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;
    }

    /// <summary>
    /// PHASE 2 DEPRECATED: Use GetRelationshipToTarget() instead
    /// This method assumes player is FactionType.Player
    /// </summary>
    [System.Obsolete("Use GetRelationshipToTarget() instead to support dynamic player factions")]
    public FactionRelationship GetRelationshipToPlayer()
    {
        return factionHandler != null ? factionHandler.GetRelationshipToPlayer() : FactionRelationship.Neutral;
    }

    /// <summary>
    /// PHASE 2 REFACTORED: Get relationship to any entity (player or NPC)
    /// </summary>
    public FactionRelationship GetRelationshipToTarget(Transform target)
    {
        if (factionHandler == null || target == null)
            return FactionRelationship.Neutral;

        // Get the target's ControllerBrain (search children since it might not be on root)
        var targetBrain = target.GetComponentInChildren<ControllerBrain>();
        if (targetBrain == null)
            return FactionRelationship.Neutral;

        // Get target's faction using helper method
        FactionType? targetFaction = GetEntityFaction(targetBrain);
        if (!targetFaction.HasValue)
            return FactionRelationship.Neutral;

        return FactionManager.GetRelationship(factionHandler.AffiliatedFaction, targetFaction.Value);
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Detection range (color-coded by faction relationship)
        if (factionHandler != null)
        {
            // Note: This uses the deprecated method for gizmo visualization
            // In a real scenario, you'd want to check the actual player in the scene
            FactionRelationship relationship = factionHandler.GetRelationshipToPlayer();
            Gizmos.color = relationship switch
            {
                FactionRelationship.Hostile => new Color(1f, 0.2f, 0.2f, 0.3f), // Red
                FactionRelationship.Neutral => new Color(1f, 0.92f, 0.016f, 0.3f), // Yellow
                FactionRelationship.Friendly => new Color(0.2f, 1f, 0.2f, 0.3f), // Green
                _ => Color.yellow
            };
        }
        else
        {
            Gizmos.color = Color.yellow;
        }
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Attack range (red) - use combat behavior's range
        if (combatBehavior != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, combatBehavior.AttackRange);

            // Exit range (orange)
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, combatBehavior.ExitCombatRange);
        }

        // Line to target
        if (currentTarget != null)
        {
            Gizmos.color = currentState == AIState.Combat ? Color.red : Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up, currentTarget.position + Vector3.up);
        }
    }

    [ContextMenu("Debug: Print Faction Info")]
    private void DebugPrintFactionInfo()
    {
        if (factionHandler != null)
        {
            Debug.Log($"=== AI Faction Info ===");
            Debug.Log($"NPC: {npcModule?.NPCName ?? "Unknown"}");
            Debug.Log($"Faction: {factionHandler.AffiliatedFaction}");
            Debug.Log($"Detection Enabled: {useFactionBasedDetection}");
            Debug.Log($"Only Detect Hostile: {onlyDetectHostileTargets}");
            Debug.Log($"Aggressive to Hostile: {factionHandler.AggressiveToHostile}");
        }
        else
        {
            Debug.LogWarning("[AIModule] No faction handler found");
        }
    }

    [ContextMenu("Debug: Test Target Detection")]
    private void DebugTestTargetDetection()
    {
        // Try to initialize faction handler if not already initialized
        TryInitializeFactionHandler();

        Debug.Log($"=== Testing Target Detection ===");
        Debug.Log($"My Faction: {factionHandler?.AffiliatedFaction ?? FactionType.None}");
        Debug.Log($"Detection Range: {detectionRange}");

        // Find all entities in range
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, detectionLayers);
        int potentialTargets = 0;
        int hostileTargets = 0;

        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;

            // Use GetComponentInChildren since ControllerBrain might be on a child
            var targetBrain = hit.GetComponentInChildren<ControllerBrain>();
            if (targetBrain == null) continue;
            if (targetBrain == brain) continue;

            potentialTargets++;

            FactionType? targetFaction = GetEntityFaction(targetBrain);
            if (!targetFaction.HasValue)
            {
                Debug.Log($"  - {targetBrain.name}: <color=gray>No faction</color>");
                continue;
            }

            FactionRelationship relationship = FactionManager.GetRelationship(
                factionHandler.AffiliatedFaction,
                targetFaction.Value
            );

            bool isHostile = relationship == FactionRelationship.Hostile && factionHandler.AggressiveToHostile;
            if (isHostile) hostileTargets++;

            string colorTag = relationship switch
            {
                FactionRelationship.Hostile => "<color=red>",
                FactionRelationship.Friendly => "<color=green>",
                _ => "<color=yellow>"
            };

            Debug.Log($"  - {targetBrain.name}: {colorTag}{targetFaction.Value} → {relationship}</color> (Would Attack: {isHostile})");
        }

        Debug.Log($"Summary: {potentialTargets} entities detected, {hostileTargets} hostile");
    }

    [ContextMenu("Debug: Toggle Faction Detection")]
    private void DebugToggleFactionDetection()
    {
        useFactionBasedDetection = !useFactionBasedDetection;
        Debug.Log($"[AIModule] Faction-based detection: {(useFactionBasedDetection ? "ENABLED" : "DISABLED")}");
    }

    #endregion
}