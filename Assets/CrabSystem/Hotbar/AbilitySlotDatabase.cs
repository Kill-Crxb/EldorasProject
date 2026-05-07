using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Inspector-registered lookup for AbilitySlotData ScriptableObjects.
/// Follows the IGameManager pattern (ResourceManager, ItemManager) — registered
/// with ManagerBrain, never uses Resources.Load.
///
/// Setup: drag all AbilitySlotData SOs into the `slots` list in the Inspector.
/// HotbarSystem resolves abilitySlotId strings back to SOs via AbilitySlotDatabase.Get(name).
/// </summary>
public class AbilitySlotDatabase : MonoBehaviour, IGameManager
{
    private static AbilitySlotDatabase instance;
    public static AbilitySlotDatabase Instance => instance;

    [Header("Slot Registry")]
    [Tooltip("All AbilitySlotData SOs available to the hotbar. " +
             "Keys are the asset name (SO.name). Keep names unique.")]
    [SerializeField] private List<AbilitySlotData> slots = new List<AbilitySlotData>();

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private Dictionary<string, AbilitySlotData> lookup;

    // ── IGameManager ─────────────────────────────────────────────────────

    public string ManagerName           => "Ability Slot Database";
    public int    InitializationPriority => 7;
    public bool   IsEnabled             => enabled;
    public bool   IsInitialized         { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;
        instance = this;
        BuildLookup();
        IsInitialized = true;
    }

    public void LateInitialize() { }

    public void Shutdown()
    {
        lookup = null;
        IsInitialized = false;
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (slots == null || slots.Count == 0)
        {
            result.Info.Add("No AbilitySlotData entries registered — hotbar save/load will not resolve SOs.");
            return result;
        }

        foreach (var slot in slots)
        {
            if (slot == null)
            {
                result.Warnings.Add("Null entry in AbilitySlotDatabase slot list.");
                continue;
            }
            if (string.IsNullOrEmpty(slot.name))
                result.Warnings.Add($"AbilitySlotData '{slot}' has an empty asset name.");
        }

        return result;
    }

    // ── Static Query ─────────────────────────────────────────────────────

    /// <summary>Returns the AbilitySlotData whose asset name matches, or null.</summary>
    public static AbilitySlotData Get(string assetName)
    {
        if (instance == null || instance.lookup == null)
        {
            Debug.LogWarning("[AbilitySlotDatabase] Not initialized. Add it to the ManagerBrain prefab.");
            return null;
        }
        if (string.IsNullOrEmpty(assetName)) return null;

        if (instance.lookup.TryGetValue(assetName, out var slot)) return slot;

        if (instance.debugLogging)
            Debug.LogWarning($"[AbilitySlotDatabase] '{assetName}' not found. Register it in the Inspector.");
        return null;
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void BuildLookup()
    {
        lookup = new Dictionary<string, AbilitySlotData>(slots.Count);

        foreach (var slot in slots)
        {
            if (slot == null) continue;
            if (string.IsNullOrEmpty(slot.name)) continue;

            if (lookup.ContainsKey(slot.name))
            {
                Debug.LogWarning($"[AbilitySlotDatabase] Duplicate asset name '{slot.name}' — second entry ignored.");
                continue;
            }

            lookup[slot.name] = slot;
        }

        if (debugLogging)
            Debug.Log($"[AbilitySlotDatabase] Built lookup with {lookup.Count} entries.");
    }
}
