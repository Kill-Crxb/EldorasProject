using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ModelSocketProvider — mapping of equipment slot assets to bone transforms.
/// 
/// Architecture:
/// - Takes EquipmentSlotDefinition assets as input
/// - Maps each slot to a bone transform on the model rig
/// - Provides O(1) lookup for sockets by slot ID
/// 
/// Setup:
/// 1. Add this component to model prefab root (next to Animator)
/// 2. For each slot that needs visual rendering, add an entry to slotSockets
/// 3. Drag the EquipmentSlotDefinition SO into the "slot" field
/// 4. Drag the bone transform into the "socket" field
/// 
/// Example:
/// - Slot: Slot_MainWeapon
/// - Socket: Armature|Spine|Chest|Shoulder.R|Arm.R|Hand.R (the right hand bone)
/// 
/// If a slot isn't in this list, it won't render (e.g., rings, storage, amulets without visuals).
/// </summary>
public class ModelSocketProvider : MonoBehaviour
{
    [System.Serializable]
    public class SlotSocketMapping
    {
        [Tooltip("The EquipmentSlotDefinition asset (e.g., Slot_MainWeapon)")]
        public EquipmentSlotDefinition slot;

        [Tooltip("The bone/transform on this model's rig where this item should attach")]
        public Transform socket;
    }

    [System.Serializable]
    public class NamedSocketMapping
    {
        [Tooltip("Free-form socket id, e.g. 'mainwep_sheathed'. Not tied to an equipment slot.")]
        public string socketId;

        [Tooltip("The bone/transform on this model's rig")]
        public Transform socket;
    }

    [Header("Slot-to-Socket Mappings")]
    [Tooltip("For each slot that needs visual rendering on this model, add an entry here.\n" +
             "Drag the EquipmentSlotDefinition asset and the bone transform.")]
    public List<SlotSocketMapping> slotSockets = new List<SlotSocketMapping>();

    [Header("Named Sockets")]
    [Tooltip("Sockets that are not equipment slots — sheath points, VFX anchors, prop mounts.\n" +
             "Add 'mainwep_sheathed' here for the armed/unarmed stance toggle.")]
    public List<NamedSocketMapping> namedSockets = new List<NamedSocketMapping>();

    private Dictionary<string, Transform> socketCache = new Dictionary<string, Transform>();
    private Dictionary<string, Transform> namedCache = new Dictionary<string, Transform>();
    private bool isCached = false;
    private bool isNamedCached = false;

    /// <summary>
    /// Returns all sockets as a dictionary keyed by slot ID.
    /// Caches the result on first call.
    /// </summary>
    public Dictionary<string, Transform> GetAllSockets()
    {
        if (isCached)
            return socketCache;

        socketCache.Clear();

        foreach (var mapping in slotSockets)
        {
            if (mapping.slot == null || mapping.socket == null)
                continue;

            socketCache[mapping.slot.slotId] = mapping.socket;
        }

        isCached = true;
        return socketCache;
    }

    /// <summary>
    /// Returns a non-slot socket by its free-form id. Caches on first call.
    /// </summary>
    public Transform GetNamedSocket(string socketId)
    {
        if (string.IsNullOrEmpty(socketId))
            return null;

        if (!isNamedCached)
            BuildNamedCache();

        if (namedCache.TryGetValue(socketId.ToLower(), out var socket))
            return socket;

        return null;
    }

    private void BuildNamedCache()
    {
        namedCache.Clear();

        foreach (var mapping in namedSockets)
        {
            if (mapping == null || string.IsNullOrEmpty(mapping.socketId) || mapping.socket == null)
                continue;

            namedCache[mapping.socketId.ToLower()] = mapping.socket;
        }

        isNamedCached = true;
    }

    /// <summary>
    /// Gets a socket by slot ID.
    /// Returns null if slot not found.
    /// </summary>
    public Transform GetSocket(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return null;

        var sockets = GetAllSockets();
        if (sockets.TryGetValue(slotId.ToLower(), out var socket))
            return socket;

        return null;
    }

    /// <summary>
    /// Gets a socket by EquipmentSlotDefinition asset.
    /// Returns null if slot not in mappings.
    /// </summary>
    public Transform GetSocket(EquipmentSlotDefinition slot)
    {
        if (slot == null)
            return null;

        return GetSocket(slot.slotId);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (slotSockets == null || slotSockets.Count == 0)
            return;

        Gizmos.color = Color.cyan;
        foreach (var mapping in slotSockets)
        {
            if (mapping.socket == null)
                continue;

            Gizmos.DrawWireSphere(mapping.socket.position, 0.04f);

            string label = mapping.slot != null ? mapping.slot.slotId : "(null slot)";
            UnityEditor.Handles.Label(mapping.socket.position + Vector3.up * 0.06f, label);
        }
    }
#endif
}