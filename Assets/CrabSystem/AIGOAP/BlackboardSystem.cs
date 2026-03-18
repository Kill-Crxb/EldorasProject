using UnityEngine;

/// <summary>
/// Blackboard system module - per-entity semantic fact storage.
/// Integrates Blackboard runtime with ControllerBrain architecture.
/// 
/// Responsibilities:
/// - Initialize blackboard with schema
/// - Expose blackboard to other modules
/// - Handle debug visualization
/// 
/// NOT Responsible For:
/// - Evaluating conditions (SemanticBridge does this)
/// - Calculating fact values (SemanticBridge does this)
/// - Interpreting thresholds (SemanticBridge does this)
/// 
/// Architecture Pattern:
/// - IBrainModule (standard module interface)
/// - Provides blackboard access via brain.Blackboard
/// - Each entity type uses different schema
/// 
/// Usage:
/// 1. Add BlackboardSystem to entity prefab
/// 2. Assign appropriate schema (NinjaSchema, WolfSchema, etc.)
/// 3. Access via: brain.Blackboard.GetBool(key)
/// 
/// Integration:
/// Other systems query facts:
///   if (brain.Blackboard.GetBool(BlackboardKey.IsWounded)) { ... }
/// 
/// SemanticBridge writes facts:
///   brain.Blackboard.SetBool(BlackboardKey.IsWounded, true);
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
public class BlackboardSystem : MonoBehaviour, IBrainModule
{
    [Header("Configuration")]
    [Tooltip("Schema defining semantic facts for this entity type")]
    [SerializeField] private BlackboardSchema schema;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    // Core components
    private ControllerBrain brain;
    private Blackboard blackboard;

    // IBrainModule
    public bool IsEnabled { get; set; } = true;

    // Public access
    public Blackboard Blackboard => blackboard;
    public BlackboardSchema Schema => schema;

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        if (controllerBrain == null)
        {
            Debug.LogError("[BlackboardSystem] Cannot initialize with null brain", this);
            return;
        }

        if (schema == null)
        {
            Debug.LogError($"[BlackboardSystem] No schema assigned on {controllerBrain.name}!", this);
            return;
        }

        brain = controllerBrain;

        // Validate schema
        if (!schema.Validate(out string error))
        {
            Debug.LogError($"[BlackboardSystem] Schema validation failed: {error}", this);
            return;
        }

        // Create runtime blackboard
        blackboard = new Blackboard();
        blackboard.Initialize(schema, debugLogging, brain.name);

        if (debugLogging)
        {
            Debug.Log($"[BlackboardSystem] Initialized on {brain.name} with schema: {schema.schemaId}");
            Debug.Log($"[BlackboardSystem] Memory footprint: {blackboard.GetMemoryFootprint()} bytes");
        }
    }

    public void UpdateModule()
    {
        // Blackboard is passive - SemanticBridge updates it
        // No per-frame logic needed here
    }

    #endregion

    #region Debug Helpers

    /// <summary>
    /// Log all current facts (debug only)
    /// </summary>
    [ContextMenu("Print All Facts")]
    public void PrintAllFacts()
    {
        if (blackboard == null)
        {
            Debug.Log($"[BlackboardSystem] Blackboard not initialized on {name}");
            return;
        }

        if (schema == null)
        {
            Debug.Log($"[BlackboardSystem] No schema assigned on {name}");
            return;
        }

        Debug.Log($"=== Blackboard Facts ({name}) ===");

        foreach (var keyDef in schema.keys)
        {
            int hash = keyDef.keyName.GetHashCode();

            switch (keyDef.type)
            {
                case BlackboardValueType.Bool:
                    bool boolVal = blackboard.GetBool(hash);
                    Debug.Log($"  {keyDef.keyName} (Bool): {boolVal}");
                    break;

                case BlackboardValueType.Float:
                    float floatVal = blackboard.GetFloat(hash);
                    Debug.Log($"  {keyDef.keyName} (Float): {floatVal:F2}");
                    break;

                case BlackboardValueType.Int:
                    int intVal = blackboard.GetInt(hash);
                    Debug.Log($"  {keyDef.keyName} (Int): {intVal}");
                    break;
            }
        }
    }

    /// <summary>
    /// Clear all facts to defaults (debug only)
    /// </summary>
    [ContextMenu("Clear All Facts")]
    public void ClearAllFacts()
    {
        if (blackboard == null) return;

        blackboard.Clear();
        
        // Re-initialize with defaults from schema
        if (schema != null)
        {
            blackboard.Initialize(schema, debugLogging, brain?.name ?? name);
        }

        if (debugLogging)
            Debug.Log($"[BlackboardSystem] Cleared all facts on {name}");
    }

    #endregion

    #region Validation

    private void OnValidate()
    {
        if (schema == null) return;

        if (!schema.Validate(out string error))
        {
            Debug.LogWarning($"[BlackboardSystem] Schema validation failed: {error}", this);
        }
    }

    #endregion
}
