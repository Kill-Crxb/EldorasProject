using UnityEngine;

// Blackboard system module - per-entity semantic fact storage.
// Integrates Blackboard runtime with ControllerBrain architecture.
//
// Responsibilities:
// - Initialize blackboard with schema
// - Expose blackboard to other modules
// - Handle debug visualization
//
// NOT Responsible For:
// - Evaluating conditions (SemanticBridge does this)
// - Calculating fact values (SemanticBridge does this)
// - Interpreting thresholds (SemanticBridge does this)
//
// Architecture Pattern:
// - IBrainModule (standard module interface)
// - Provides blackboard access via brain.Blackboard
// - Each entity type uses different schema
//
// Usage:
// 1. Add BlackboardSystem to entity prefab
// 2. Assign appropriate schema (NinjaSchema, WolfSchema, etc.)
// 3. Access via: brain.Blackboard.GetBool(key)
//
// Integration:
// Other systems query facts:
//   if (brain.Blackboard.GetBool(BlackboardKey.IsWounded)) { ... }
//
// SemanticBridge writes facts:
//   brain.Blackboard.SetBool(BlackboardKey.IsWounded, true);
//
// Phase 1.3: Semantic Bridge System

namespace CrabSystem
{
    public class BlackboardSystem : MonoBehaviour, IBrainModule
    {
        public int InitOrder => 110;

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

        public void Initialize(ControllerBrain controllerBrain)
        {
            if (controllerBrain == null)
            {
                Debug.LogError("[BlackboardSystem] Cannot initialize with null brain", this);
                return;
            }

            if (schema == null)
            {
                Debug.LogError($"[BlackboardSystem] No schema assigned on {controllerBrain.EntityName}!", this);
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
        }

        public void UpdateModule()
        {
            // Blackboard is passive - SemanticBridge updates it
            // No per-frame logic needed here
        }

        // Log all current facts (debug only)
        [ContextMenu("Print All Facts")]
        public void PrintAllFacts()
        {
            if (blackboard == null)
            {
                return;
            }

            if (schema == null)
            {
                return;
            }

            foreach (var keyDef in schema.keys)
            {
                int hash = BlackboardKey.Hash(keyDef.keyName);

                switch (keyDef.type)
                {
                    case BlackboardValueType.Bool:
                        bool boolVal = blackboard.GetBool(hash);
                        break;

                    case BlackboardValueType.Float:
                        float floatVal = blackboard.GetFloat(hash);
                        break;

                    case BlackboardValueType.Int:
                        int intVal = blackboard.GetInt(hash);
                        break;
                }
            }
        }

        // Clear all facts to defaults (debug only)
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
        }

        private void OnValidate()
        {
            if (schema == null) return;

            if (!schema.Validate(out string error))
            {
                Debug.LogWarning($"[BlackboardSystem] Schema validation failed: {error}", this);
            }
        }
    }
}
