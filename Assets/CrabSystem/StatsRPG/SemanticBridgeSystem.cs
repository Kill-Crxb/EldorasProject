using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Semantic Bridge System - Single source of truth for interpreting numbers â†’ meaning.
/// Evaluates conditions and writes semantic facts to blackboard.
/// 
/// Core Philosophy:
/// "Gameplay systems must not interpret raw numbers. All meaning is derived once, centrally."
/// 
/// Responsibilities:
/// - Evaluate BlackboardConditions against entity state
/// - Write semantic facts to Blackboard
/// - Update on pulse (not every frame)
/// 
/// NOT Responsible For:
/// - Storing facts (Blackboard does this)
/// - Reading facts (systems query Blackboard)
/// - Acting on facts (GOAP, UI, etc. consume facts)
/// 
/// Architecture Pattern:
/// 
/// BAD (Duplicated Logic):
///   GOAP: if (health% < 25%) flee();
///   UI: if (health% < 25%) showWarning();
///   Ability: if (target.health% < 25%) enableFinisher();
/// 
/// GOOD (Single Source):
///   SemanticBridge: if (health% < 25%) blackboard.Set(IsWounded, true);
///   GOAP: if (blackboard.Get(IsWounded)) flee();
///   UI: if (blackboard.Get(IsWounded)) showWarning();
///   Ability: if (target.blackboard.Get(IsWounded)) enableFinisher();
/// 
/// Performance:
/// - Pulse-based updates (default 10Hz, not 60Hz)
/// - Hashed integer keys (no string comparisons)
/// - Only evaluates enabled conditions
/// - Zero allocations in hot path
/// 
/// Example Setup:
/// 1. Create condition assets (IsWounded, CanDodge, LowMana)
/// 2. Assign conditions to SemanticBridge.conditions list
/// 3. Bridge evaluates every updateInterval seconds
/// 4. Results written to blackboard automatically
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
public class SemanticBridgeSystem : MonoBehaviour, IBrainModule
{
    [Header("Configuration")]
    [Tooltip("Universal condition library (recommended - evaluates all conditions, entities ignore inapplicable ones)")]
    [SerializeField] private BlackboardConditionLibrary conditionLibrary;

    [Tooltip("Optional: Entity-specific conditions (only used if library not assigned)")]
    [SerializeField] private List<BlackboardCondition> conditions = new List<BlackboardCondition>();

    [Header("Performance")]
    [Tooltip("Update frequency in seconds (0.1 = 10Hz, 0.0 = every frame)")]
    [SerializeField] private float updateInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // Core components
    private ControllerBrain brain;
    private Blackboard blackboard;

    // Update timing
    private float nextUpdateTime;

    // IBrainModule
    public bool IsEnabled { get; set; } = true;

    // Properties
    public bool UsesLibrary => conditionLibrary != null;
    public int ConditionCount => GetActiveConditions()?.Count ?? 0;

    /// <summary>
    /// Get the active condition list (library or entity-specific)
    /// </summary>
    private List<BlackboardCondition> GetActiveConditions()
    {
        return conditionLibrary != null ? conditionLibrary.conditions : conditions;
    }

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        // Guard clauses
        if (controllerBrain == null)
        {
            Debug.LogError("[SemanticBridge] Cannot initialize with null brain", this);
            return;
        }

        brain = controllerBrain;

        // Get blackboard system
        var blackboardSystem = brain.GetModule<BlackboardSystem>();
        if (blackboardSystem == null)
        {
            Debug.LogError($"[SemanticBridge] No BlackboardSystem found on {brain.name}!", this);
            return;
        }

        blackboard = blackboardSystem.Blackboard;
        if (blackboard == null)
        {
            Debug.LogError($"[SemanticBridge] Blackboard not initialized on {brain.name}!", this);
            return;
        }

        // Validate conditions
        ValidateConditions();

        // Initialize update timing
        nextUpdateTime = Time.time + updateInterval;

        if (debugLogging)
        {
            string source = UsesLibrary ? $"library '{conditionLibrary.name}'" : "entity-specific list";
            Debug.Log($"[SemanticBridge] Initialized on {brain.name}");
            Debug.Log($"  Source: {source}");
            Debug.Log($"  Conditions: {ConditionCount}");
            Debug.Log($"  Update interval: {updateInterval}s ({1f / updateInterval}Hz)");
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        if (blackboard == null) return;

        var activeConditions = GetActiveConditions();
        if (activeConditions == null || activeConditions.Count == 0) return;

        // Pulse-based updates (not every frame)
        if (Time.time < nextUpdateTime) return;
        nextUpdateTime = Time.time + updateInterval;

        // Evaluate all conditions
        EvaluateAllConditions();
    }

    #endregion

    #region Condition Evaluation

    /// <summary>
    /// Evaluate all enabled conditions and write results to blackboard
    /// </summary>
    private void EvaluateAllConditions()
    {
        var activeConditions = GetActiveConditions();
        if (activeConditions == null) return;

        // Flat loop with early continue (no nesting!)
        foreach (var condition in activeConditions)
        {
            if (condition == null) continue;
            if (!condition.IsEnabled) continue;
            if (condition.ValueSource == null) continue;

            EvaluateCondition(condition);
        }
    }

    /// <summary>
    /// Evaluate single condition and write to blackboard
    /// </summary>
    private void EvaluateCondition(BlackboardCondition condition)
    {
        // Guard clauses
        if (condition == null) return;
        if (string.IsNullOrEmpty(condition.OutputFactKey)) return;

        // Evaluate condition
        bool result = condition.Evaluate(brain);

        // Write to blackboard
        int factHash = HashFactKey(condition.OutputFactKey);
        blackboard.SetBool(factHash, result);

        if (debugLogging)
        {
            Debug.Log($"[SemanticBridge:{brain.name}] {condition.name} = {result}");
        }
    }

    /// <summary>
    /// Hash fact key for blackboard lookup
    /// </summary>
    private int HashFactKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return 0;
        return key.GetHashCode();
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate all conditions on initialization
    /// </summary>
    private void ValidateConditions()
    {
        // Check which source we're using
        if (conditionLibrary != null)
        {
            // Validate library
            if (!conditionLibrary.Validate(out string error))
            {
                Debug.LogError($"[SemanticBridge] Library '{conditionLibrary.name}' validation failed: {error}", this);
                return;
            }

            if (debugLogging)
            {
                Debug.Log($"[SemanticBridge] Using library '{conditionLibrary.name}' with {conditionLibrary.ConditionCount} conditions");
            }
            return;
        }

        // Using entity-specific list - validate it
        if (conditions == null || conditions.Count == 0)
        {
            Debug.LogWarning($"[SemanticBridge] No condition library or conditions assigned on {brain?.name ?? name}", this);
            return;
        }

        int validCount = 0;
        int invalidCount = 0;

        foreach (var condition in conditions)
        {
            if (condition == null)
            {
                invalidCount++;
                continue;
            }

            if (!condition.Validate(out string error))
            {
                Debug.LogWarning($"[SemanticBridge] Invalid condition '{condition.name}': {error}", condition);
                invalidCount++;
                continue;
            }

            validCount++;
        }

        if (debugLogging || invalidCount > 0)
        {
            Debug.Log($"[SemanticBridge] Entity-specific validation: {validCount} valid, {invalidCount} invalid conditions");
        }
    }

    private void OnValidate()
    {
        // Don't remove nulls here - it prevents adding items in inspector!
        // Null checking happens at runtime in Initialize() and EvaluateAllConditions()
    }

    #endregion

    #region Debug Helpers

    /// <summary>
    /// Force immediate evaluation of all conditions (debug only)
    /// </summary>
    [ContextMenu("Force Evaluate All")]
    public void ForceEvaluateAll()
    {
        if (blackboard == null)
        {
            Debug.LogWarning("[SemanticBridge] Blackboard not initialized");
            return;
        }

        string source = UsesLibrary ? $"library '{conditionLibrary.name}'" : "entity-specific list";
        Debug.Log($"[SemanticBridge] Force evaluating {ConditionCount} conditions from {source}...");
        EvaluateAllConditions();
    }

    /// <summary>
    /// Print readable condition descriptions (debug only)
    /// </summary>
    [ContextMenu("Print All Conditions")]
    public void PrintAllConditions()
    {
        var activeConditions = GetActiveConditions();
        if (activeConditions == null || activeConditions.Count == 0)
        {
            Debug.Log("[SemanticBridge] No conditions assigned");
            return;
        }

        string source = UsesLibrary ? $"Library: {conditionLibrary.name}" : "Entity-Specific";
        Debug.Log($"=== Semantic Bridge Conditions ({brain?.name ?? name}) ===");
        Debug.Log($"Source: {source} ({activeConditions.Count} conditions)");

        foreach (var condition in activeConditions)
        {
            if (condition == null) continue;

            string status = condition.IsEnabled ? "✓" : "✗";
            string desc = condition.GetReadableDescription();
            Debug.Log($"  {status} {condition.name}: {desc}");
        }
    }

    #endregion
}

