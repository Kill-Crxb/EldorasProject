using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ModelSocketProvider — lives on the model root prefab.
/// Holds explicit serialised references to all socket transforms for this model.
/// ModelModule reads from this component instead of searching by name.
///
/// Usage:
/// - Add to the root GameObject of each model prefab.
/// - Drag the correct transforms from the rig into each socket field.
/// - Bone names in Blender/FBX do not need to match the socket key names.
/// </summary>
public class ModelSocketProvider : MonoBehaviour
{
    [Header("Standard Sockets")]
    [Tooltip("Right hand weapon attachment point")]
    public Transform weapon;

    [Tooltip("Left hand shield / off-hand attachment point")]
    public Transform shield;

    [Tooltip("Head equipment attachment point")]
    public Transform helmet;

    [Tooltip("Chest equipment attachment point")]
    public Transform chest;

    [Tooltip("Foot equipment attachment point")]
    public Transform boots;

    [Tooltip("Floor-level foot effects (dust, footsteps)")]
    public Transform feeteffects;

    [Tooltip("Upper spine rear — back equipment, capes")]
    public Transform backeffects;

    [Header("Additional Sockets")]
    [Tooltip("Optional extra sockets for this model. Key must be lowercase with no spaces.")]
    public List<NamedSocket> extraSockets = new List<NamedSocket>();

    /// <summary>
    /// Returns all sockets as a flat dictionary keyed by lowercase name.
    /// ModelModule calls this once after instantiating the model.
    /// </summary>
    public Dictionary<string, Transform> GetAllSockets()
    {
        var result = new Dictionary<string, Transform>();

        TryAdd(result, "weapon", weapon);
        TryAdd(result, "shield", shield);
        TryAdd(result, "helmet", helmet);
        TryAdd(result, "chest", chest);
        TryAdd(result, "boots", boots);
        TryAdd(result, "feeteffects", feeteffects);
        TryAdd(result, "backeffects", backeffects);

        foreach (var entry in extraSockets)
        {
            if (!string.IsNullOrWhiteSpace(entry.key) && entry.socket != null)
                TryAdd(result, entry.key.ToLower(), entry.socket);
        }

        return result;
    }

    private void TryAdd(Dictionary<string, Transform> dict, string key, Transform value)
    {
        if (value != null)
            dict[key] = value;
    }

    [System.Serializable]
    public class NamedSocket
    {
        [Tooltip("Lowercase key — must match the socketName on EquipmentSlotDefinition")]
        public string key;
        public Transform socket;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        foreach (var kvp in GetAllSockets())
        {
            Gizmos.DrawWireSphere(kvp.Value.position, 0.04f);
            UnityEditor.Handles.Label(kvp.Value.position + Vector3.up * 0.06f, kvp.Key);
        }
    }
#endif
}