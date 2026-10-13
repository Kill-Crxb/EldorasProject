using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime storage for semantic facts (boolean/float/int values).
// Per-entity cache of interpreted game state.
//
// Responsibilities:
// - Store semantic facts with fast O(1) lookup
// - Emit change events for reactive systems
// - Preallocate memory based on schema
//
// NOT Responsible For:
// - Calculating fact values (SemanticBridge does this)
// - Interpreting numbers to meaning (SemanticBridge does this)
// - Knowing where facts come from
//
// Architecture Pattern:
// - Separate dictionary per type (better cache locality)
// - Integer keys (hashed from strings)
// - Event-driven updates (not polling)
//
// Performance:
// - ~350 bytes per entity (typical 20-30 facts)
// - O(1) lookup via int key
// - Zero allocations in hot paths
//
// Usage:
// blackboard.SetBool(BlackboardKey.IsWounded, true);
// if (blackboard.GetBool(BlackboardKey.CanDodge)) { ... }
//
// Phase 1.3: Semantic Bridge System

namespace CrabSystem
{
    public class Blackboard
    {
        // Separate dictionaries by type for better performance
        private Dictionary<int, bool> bools;
        private Dictionary<int, float> floats;
        private Dictionary<int, int> ints;

        // A held flag (a capability grant, a status's denial) is raised while any source claims it. Keyed by source,
        // so re-claiming under one key is a no-op and a release can only drop its own claim.
        private readonly Dictionary<int, HashSet<string>> claims = new();

        // Change events for reactive systems
        public event Action<int, bool> OnBoolChanged;
        public event Action<int, float> OnFloatChanged;
        public event Action<int, int> OnIntChanged;

        // Debug
        private bool debugLogging = false;
        private string ownerName = "Unknown";

        // Initialize blackboard with preallocated capacity from schema
        public void Initialize(BlackboardSchema schema, bool enableDebug = false, string owner = "Unknown")
        {
            if (schema == null)
            {
                Debug.LogError("[Blackboard] Cannot initialize with null schema");
                return;
            }

            debugLogging = enableDebug;
            ownerName = owner;

            // Count facts by type
            int boolCount = 0;
            int floatCount = 0;
            int intCount = 0;

            foreach (var key in schema.keys)
            {
                switch (key.type)
                {
                    case BlackboardValueType.Bool: boolCount++; break;
                    case BlackboardValueType.Float: floatCount++; break;
                    case BlackboardValueType.Int: intCount++; break;
                }
            }

            // Preallocate dictionaries (zero allocations during gameplay)
            bools = new Dictionary<int, bool>(boolCount);
            floats = new Dictionary<int, float>(floatCount);
            ints = new Dictionary<int, int>(intCount);

            // Set default values
            foreach (var key in schema.keys)
            {
                int hash = BlackboardKey.Hash(key.keyName);

                switch (key.type)
                {
                    case BlackboardValueType.Bool:
                        bools[hash] = key.defaultBool;
                        break;
                    case BlackboardValueType.Float:
                        floats[hash] = key.defaultFloat;
                        break;
                    case BlackboardValueType.Int:
                        ints[hash] = key.defaultInt;
                        break;
                }
            }

            if (debugLogging)
                Debug.Log($"[Blackboard] Initialized for {ownerName}: {boolCount} bools, {floatCount} floats, {intCount} ints");
        }

        // Get boolean fact value (returns false if not found)
        public bool GetBool(int key)
        {
            if (bools == null) return false;
            return bools.TryGetValue(key, out var value) && value;
        }

        // Set boolean fact value (emits event on change)
        public void SetBool(int key, bool value)
        {
            if (bools == null) return;

            // Check if value actually changed
            bool changed = !bools.TryGetValue(key, out var oldValue) || oldValue != value;
        
            if (!changed) return;

            bools[key] = value;
            OnBoolChanged?.Invoke(key, value);

            if (debugLogging)
                Debug.Log($"[Blackboard:{ownerName}] Bool changed - Key:{key} Value:{value}");
        }

        // Get float fact value (returns 0 if not found)
        public float GetFloat(int key)
        {
            if (floats == null) return 0f;
            return floats.TryGetValue(key, out var value) ? value : 0f;
        }

        // Set float fact value (emits event on change)
        public void SetFloat(int key, float value)
        {
            if (floats == null) return;

            // Check if value actually changed (with epsilon for floats)
            bool changed = !floats.TryGetValue(key, out var oldValue) || !Mathf.Approximately(oldValue, value);
        
            if (!changed) return;

            floats[key] = value;
            OnFloatChanged?.Invoke(key, value);

            if (debugLogging)
                Debug.Log($"[Blackboard:{ownerName}] Float changed - Key:{key} Value:{value:F2}");
        }

        // Get int fact value (returns 0 if not found)
        public int GetInt(int key)
        {
            if (ints == null) return 0;
            return ints.TryGetValue(key, out var value) ? value : 0;
        }

        // Set int fact value (emits event on change)
        public void SetInt(int key, int value)
        {
            if (ints == null) return;

            // Check if value actually changed
            bool changed = !ints.TryGetValue(key, out var oldValue) || oldValue != value;
        
            if (!changed) return;

            ints[key] = value;
            OnIntChanged?.Invoke(key, value);

            if (debugLogging)
                Debug.Log($"[Blackboard:{ownerName}] Int changed - Key:{key} Value:{value}");
        }

        // Clear all facts to defaults (useful for entity respawn/reset)
        public void Clear()
        {
            if (bools != null) bools.Clear();
            if (floats != null) floats.Clear();
            if (ints != null) ints.Clear();
            claims.Clear();

            if (debugLogging)
                Debug.Log($"[Blackboard:{ownerName}] Cleared all facts");
        }

        // Check if a key exists in the blackboard
        public bool HasKey(int key)
        {
            if (bools != null && bools.ContainsKey(key)) return true;
            if (floats != null && floats.ContainsKey(key)) return true;
            if (ints != null && ints.ContainsKey(key)) return true;
            return false;
        }

        // Get memory footprint for profiling
        public int GetMemoryFootprint()
        {
            int bytes = 0;
        
            if (bools != null) bytes += bools.Count * 8; // int key + bool value
            if (floats != null) bytes += floats.Count * 12; // int key + float value
            if (ints != null) bytes += ints.Count * 12; // int key + int value
        
            return bytes + 300; // + dictionary overhead
        }

        // One ledger for every held flag: statuses claim under status:{id}, rewards under their source key.
        public void Claim(int key, string sourceKey)
        {
            if (!claims.TryGetValue(key, out HashSet<string> sources))
            {
                sources = new HashSet<string>();
                claims[key] = sources;
            }

            sources.Add(sourceKey);
            SetBool(key, true);
        }

        public void Release(int key, string sourceKey)
        {
            if (!claims.TryGetValue(key, out HashSet<string> sources)) return;
            if (!sources.Remove(sourceKey)) return;
            if (sources.Count > 0) return;

            claims.Remove(key);
            SetBool(key, false);
        }

        public bool IsClaimed(int key) => claims.ContainsKey(key);
    }
}
