using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines the semantic facts available for a specific entity type.
/// Designer creates one schema per entity archetype (Ninja, Wolf, Mage, etc.)
/// 
/// Architecture:
/// - ScriptableObject asset (data-driven, no code changes)
/// - Each entity type has unique semantic facts
/// - Shared facts use same key names across schemas
/// 
/// Example Schemas:
/// 
/// NinjaBlackboardSchema.asset:
/// - IsWounded (Bool) - "Health below 25%"
/// - CanDodge (Bool) - "Stamina above 20"
/// - LowStamina (Bool) - "Stamina below 30%"
/// 
/// WolfBlackboardSchema.asset:
/// - IsWounded (Bool) - "Health below 25%"
/// - IsHungry (Bool) - "Not recently fed"
/// - ScentFound (Bool) - "Prey detected via smell"
/// 
/// Usage:
/// 1. Create schema asset in Project window
/// 2. Define keys with names, types, defaults
/// 3. Assign to entity's BlackboardSystem
/// 4. Generate code constants via editor tool
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "BlackboardSchema", menuName = "NinjaGame/Blackboard/Schema")]
public class BlackboardSchema : ScriptableObject
{
    [System.Serializable]
    public class KeyDefinition
    {
        [Tooltip("Unique key name (e.g., 'IsWounded', 'CanDodge')")]
        public string keyName;

        [Tooltip("Data type for this fact")]
        public BlackboardValueType type;

        [Tooltip("Description for designers (what does this fact mean?)")]
        [TextArea(2, 4)]
        public string description;

        // Default values (only one used based on type)
        public bool defaultBool;
        public float defaultFloat;
        public int defaultInt;

        /// <summary>
        /// Get typed default value based on fact type
        /// </summary>
        public object GetDefaultValue()
        {
            switch (type)
            {
                case BlackboardValueType.Bool: return defaultBool;
                case BlackboardValueType.Float: return defaultFloat;
                case BlackboardValueType.Int: return defaultInt;
                default: return false;
            }
        }
    }

    [Header("Schema Identity")]
    [Tooltip("Unique schema ID (e.g., 'Ninja', 'Wolf', 'Mage')")]
    public string schemaId;

    [Header("Semantic Facts")]
    [Tooltip("List of facts this entity type can have")]
    public List<KeyDefinition> keys = new List<KeyDefinition>();

    /// <summary>
    /// Get key definition by name (O(n) - use sparingly)
    /// </summary>
    public KeyDefinition GetKey(string keyName)
    {
        if (string.IsNullOrEmpty(keyName)) return null;

        foreach (var key in keys)
        {
            if (key.keyName == keyName)
                return key;
        }

        return null;
    }

    /// <summary>
    /// Validate schema for common issues
    /// </summary>
    public bool Validate(out string error)
    {
        if (string.IsNullOrEmpty(schemaId))
        {
            error = "Schema ID cannot be empty";
            return false;
        }

        if (keys == null || keys.Count == 0)
        {
            error = "Schema must have at least one key";
            return false;
        }

        // Check for duplicate key names
        var seen = new HashSet<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(key.keyName))
            {
                error = "Key name cannot be empty";
                return false;
            }

            if (!seen.Add(key.keyName))
            {
                error = $"Duplicate key name: {key.keyName}";
                return false;
            }
        }

        error = null;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Validate(out string error))
        {
            Debug.LogWarning($"[BlackboardSchema] Validation failed: {error}", this);
        }
    }
#endif
}

/// <summary>
/// Supported blackboard value types
/// </summary>
public enum BlackboardValueType
{
    Bool,   // True/false semantic facts (IsWounded, CanDodge)
    Float,  // Numeric values (ThreatLevel, DangerRating)
    Int     // Counters (ComboCount, StackCount)
}
