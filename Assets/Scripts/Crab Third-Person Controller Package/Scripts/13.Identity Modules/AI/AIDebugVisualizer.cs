using UnityEngine;
using RPG.Factions;

/// <summary>
/// AI Debug Visualizer - Companion script for AIModule
/// Handles all debug visualization, gizmos, and testing tools.
/// 
/// Responsibilities:
/// - Gizmo drawing (detection range, attack range, relationships)
/// - Debug context menus
/// - Faction information printing
/// - Target detection testing
/// - Visual feedback for AI state
/// 
/// This companion script isolates debug code from the main AIModule,
/// making it easy to remove in production builds or disable for performance.
/// </summary>
public class AIDebugVisualizer : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private bool showStateLabels = false;
    [SerializeField] private bool logStateChanges = true;

    [Header("Gizmo Colors")]
    [SerializeField] private Color detectionRangeColor = new Color(1f, 1f, 0f, 0.3f);
    [SerializeField] private Color attackRangeColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color exitRangeColor = new Color(1f, 0.5f, 0f, 0.3f);
    [SerializeField] private Color targetLineColorIdle = Color.green;
    [SerializeField] private Color targetLineColorCombat = Color.red;

    // References (set by AIModule)
    private AIModule aiModule;
    private AITargetDetection targetDetection;
    private IAICombatBehavior combatBehavior;
    private FactionAffiliationHandler factionHandler;
    private NPCModule npcModule;

    /// <summary>
    /// Initialize the debug visualizer with required references
    /// </summary>
    public void Initialize(AIModule module, AITargetDetection detection, IAICombatBehavior combat)
    {
        aiModule = module;
        targetDetection = detection;
        combatBehavior = combat;

        // Try to get faction handler
        if (module != null)
        {
            factionHandler = module.GetComponentInChildren<FactionAffiliationHandler>();
            npcModule = module.GetComponent<NPCModule>();
        }
    }

    #region Gizmo Visualization

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;

        // Detection range (color-coded by faction relationship if available)
        DrawDetectionRange();

        // Attack range (red)
        DrawAttackRange();

        // Exit combat range (orange)
        DrawExitCombatRange();

        // Line to current target
        DrawTargetLine();
    }

    private void DrawDetectionRange()
    {
        if (targetDetection == null) return;

        Color rangeColor = detectionRangeColor;

        // Color-code by faction relationship to player if possible
        if (factionHandler != null && Application.isPlaying)
        {
#pragma warning disable CS0618 // Using deprecated method for gizmo visualization
            FactionRelationship relationship = factionHandler.GetRelationshipToPlayer();
#pragma warning restore CS0618

            rangeColor = relationship switch
            {
                FactionRelationship.Hostile => new Color(1f, 0.2f, 0.2f, 0.3f),  // Red
                FactionRelationship.Neutral => new Color(1f, 0.92f, 0.016f, 0.3f), // Yellow
                FactionRelationship.Friendly => new Color(0.2f, 1f, 0.2f, 0.3f),  // Green
                _ => detectionRangeColor
            };
        }

        Gizmos.color = rangeColor;
        Gizmos.DrawWireSphere(transform.position, targetDetection.GetDetectionRange());
    }

    private void DrawAttackRange()
    {
        if (combatBehavior == null) return;

        Gizmos.color = attackRangeColor;
        Gizmos.DrawWireSphere(transform.position, combatBehavior.AttackRange);
    }

    private void DrawExitCombatRange()
    {
        if (combatBehavior == null) return;

        Gizmos.color = exitRangeColor;
        Gizmos.DrawWireSphere(transform.position, combatBehavior.ExitCombatRange);
    }

    private void DrawTargetLine()
    {
        if (aiModule == null || aiModule.CurrentTarget == null) return;

        // Color based on state
        Gizmos.color = aiModule.CurrentState == AIState.Combat ? targetLineColorCombat : targetLineColorIdle;

        Vector3 start = transform.position + Vector3.up;
        Vector3 end = aiModule.CurrentTarget.position + Vector3.up;

        Gizmos.DrawLine(start, end);

        // Draw distance indicator
        float distance = Vector3.Distance(transform.position, aiModule.CurrentTarget.position);
        Vector3 midPoint = (start + end) / 2f;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(midPoint, $"{distance:F1}m");
#endif
    }

    private void OnDrawGizmos()
    {
        // Draw state label above NPC
        if (showStateLabels && aiModule != null && Application.isPlaying)
        {
#if UNITY_EDITOR
            Vector3 labelPos = transform.position + Vector3.up * 2.5f;
            string stateText = $"{aiModule.CurrentState}";
            
            if (aiModule.CurrentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, aiModule.CurrentTarget.position);
                stateText += $"\n{distance:F1}m";
            }
            
            UnityEditor.Handles.Label(labelPos, stateText);
#endif
        }
    }

    #endregion

    #region Debug Context Menus

    [ContextMenu("Debug: Print AI Info")]
    private void DebugPrintAIInfo()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        Debug.Log($"=== AI Debug Info ===");
        Debug.Log($"GameObject: {gameObject.name}");

        if (aiModule != null)
        {
            Debug.Log($"Current State: {aiModule.CurrentState}");
            Debug.Log($"Has Target: {aiModule.HasTarget()}");
            Debug.Log($"Target: {(aiModule.CurrentTarget != null ? aiModule.CurrentTarget.name : "None")}");

            if (aiModule.CurrentTarget != null)
            {
                Debug.Log($"Distance to Target: {aiModule.GetDistanceToTarget():F2}");
            }
        }

        if (targetDetection != null)
        {
            Debug.Log($"Detection Range: {targetDetection.GetDetectionRange()}");
            Debug.Log($"Faction: {targetDetection.GetFaction()}");
        }

        if (combatBehavior != null)
        {
            Debug.Log($"Attack Range: {combatBehavior.AttackRange}");
            Debug.Log($"Exit Range: {combatBehavior.ExitCombatRange}");
            Debug.Log($"Can Attack: {combatBehavior.CanAttack()}");
        }
    }

    [ContextMenu("Debug: Print Faction Info")]
    private void DebugPrintFactionInfo()
    {
        Debug.Log($"=== AI Faction Info ===");
        Debug.Log($"NPC: {(npcModule != null ? npcModule.NPCName : "Unknown")}");

        if (factionHandler != null)
        {
            Debug.Log($"Faction: {factionHandler.AffiliatedFaction}");
            Debug.Log($"Aggressive to Hostile: {factionHandler.AggressiveToHostile}");
        }
        else
        {
            Debug.LogWarning("No faction handler found");
        }

        if (targetDetection != null)
        {
            Debug.Log($"Faction Detection Enabled: {targetDetection.GetDetectionRange() > 0}");
        }
    }

    [ContextMenu("Debug: Test Target Detection")]
    private void DebugTestTargetDetection()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (targetDetection == null)
        {
            Debug.LogError("[AIDebugVisualizer] No AITargetDetection component found");
            return;
        }

        Debug.Log($"=== Testing Target Detection ===");
        Debug.Log($"My Faction: {targetDetection.GetFaction()}");
        Debug.Log($"Detection Range: {targetDetection.GetDetectionRange()}");

        Transform detectedTarget = targetDetection.DetectTarget();

        if (detectedTarget != null)
        {
            Debug.Log($"<color=green>✓ Target Detected: {detectedTarget.name}</color>");
            Debug.Log($"Relationship: {targetDetection.GetRelationshipToTarget(detectedTarget)}");
            Debug.Log($"Distance: {Vector3.Distance(transform.position, detectedTarget.position):F2}");
        }
        else
        {
            Debug.Log("<color=yellow>✗ No valid targets detected</color>");
        }
    }

    [ContextMenu("Debug: Force State -> Idle")]
    private void DebugForceIdle()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (aiModule != null)
        {
            aiModule.ChangeState(AIState.Idle);
            Debug.Log("[AIDebugVisualizer] Forced state to Idle");
        }
    }

    [ContextMenu("Debug: Force Target Lost")]
    private void DebugForceTargetLost()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (aiModule != null)
        {
            aiModule.ForceTargetLost();
            Debug.Log("[AIDebugVisualizer] Forced target lost");
        }
    }

    [ContextMenu("Debug: Change to Friendly Faction (Elves)")]
    private void DebugChangeFactionToFriendly()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (factionHandler != null)
        {
            factionHandler.SetFaction(FactionType.Elves);
            Debug.Log("[AIDebugVisualizer] Changed faction to Elves (Friendly)");
        }
        else
        {
            Debug.LogWarning("[AIDebugVisualizer] No FactionAffiliationHandler found");
        }
    }

    [ContextMenu("Debug: Change to Hostile Faction (Warlocks)")]
    private void DebugChangeFactionToHostile()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (factionHandler != null)
        {
            factionHandler.SetFaction(FactionType.Warlocks);
            Debug.Log("[AIDebugVisualizer] Changed faction to Warlocks (Hostile)");
        }
        else
        {
            Debug.LogWarning("[AIDebugVisualizer] No FactionAffiliationHandler found");
        }
    }

    [ContextMenu("Debug: Change to Neutral Faction")]
    private void DebugChangeFactionToNeutral()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AIDebugVisualizer] Must be in Play mode");
            return;
        }

        if (factionHandler != null)
        {
            factionHandler.SetFaction(FactionType.Neutral);
            Debug.Log("[AIDebugVisualizer] Changed faction to Neutral");
        }
        else
        {
            Debug.LogWarning("[AIDebugVisualizer] No FactionAffiliationHandler found");
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Subscribe to AI events for debug logging
    /// </summary>
    public void SubscribeToEvents()
    {
        if (aiModule == null) return;

        aiModule.OnStateChanged += HandleStateChanged;
        aiModule.OnTargetAcquired += HandleTargetAcquired;
        aiModule.OnTargetLost += HandleTargetLost;
    }

    /// <summary>
    /// Unsubscribe from AI events
    /// </summary>
    public void UnsubscribeFromEvents()
    {
        if (aiModule == null) return;

        aiModule.OnStateChanged -= HandleStateChanged;
        aiModule.OnTargetAcquired -= HandleTargetAcquired;
        aiModule.OnTargetLost -= HandleTargetLost;
    }

    private void HandleStateChanged(AIState oldState, AIState newState)
    {
        if (logStateChanges)
        {
            Debug.Log($"[AI] {gameObject.name}: {oldState} → {newState}");
        }
    }

    private void HandleTargetAcquired(Transform target)
    {
        if (logStateChanges)
        {
            Debug.Log($"[AI] {gameObject.name}: Target acquired → {target.name}");
        }
    }

    private void HandleTargetLost()
    {
        if (logStateChanges)
        {
            Debug.Log($"[AI] {gameObject.name}: Target lost");
        }
    }

    #endregion

    #region Lifecycle

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    #endregion

    #region Public Configuration

    /// <summary>
    /// Toggle gizmo visibility
    /// </summary>
    public void SetShowGizmos(bool show)
    {
        showDebugGizmos = show;
    }

    /// <summary>
    /// Toggle state label visibility
    /// </summary>
    public void SetShowStateLabels(bool show)
    {
        showStateLabels = show;
    }

    /// <summary>
    /// Toggle state change logging
    /// </summary>
    public void SetLogStateChanges(bool log)
    {
        logStateChanges = log;
    }

    #endregion
}