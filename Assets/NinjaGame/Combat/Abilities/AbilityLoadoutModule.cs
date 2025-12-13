using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages which abilities/combos are assigned to quickslots.
/// Tracks combo progression for each slot.
/// Handles ability swapping and loadout management.
/// </summary>
public class AbilityLoadoutModule : MonoBehaviour, IBrainModule
{
    [Header("Quickslot Assignments")]
    [Tooltip("Ability or combo assigned to Q key")]
    [SerializeField] private AbilitySlotData quickslotQ;

    [Tooltip("Ability or combo assigned to Z key")]
    [SerializeField] private AbilitySlotData quickslotZ;

    [Tooltip("Ability or combo assigned to X key")]
    [SerializeField] private AbilitySlotData quickslotX;

    [Tooltip("Ability or combo assigned to C key")]
    [SerializeField] private AbilitySlotData quickslotC;

    [Tooltip("Ability or combo assigned to V key")]
    [SerializeField] private AbilitySlotData quickslotV;

    [Header("Basic Attack (Fixed - Not Editable)")]
    [Tooltip("Basic attack combo chain (triggered by left click)")]
    [SerializeField] private AbilitySlotData basicAttackChain;

    [Header("Defense Slot")]
    [Tooltip("Defense ability (triggered by right click/block button)")]
    [SerializeField] private DefenseAbilityData defenseSlot;

    [Header("Combo Settings")]
    [Tooltip("Time window to continue combo before resetting")]
    [SerializeField] private float comboResetTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugLoadout = false;

    private ControllerBrain brain;

    // Track current combo index for each slot
    private Dictionary<string, int> comboIndices = new Dictionary<string, int>();

    // Track last use time for combo reset
    private Dictionary<string, float> lastUseTime = new Dictionary<string, float>();

    public bool IsEnabled { get; set; } = true;

    // Events
    public event Action<string, int> OnComboAdvanced; // slotKey, newIndex
    public event Action<string> OnComboReset; // slotKey
    public event Action<string, AbilitySlotData> OnSlotChanged; // slotKey, newSlotData

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Initialize combo tracking
        InitializeSlot("BasicAttack");
        InitializeSlot("Q");
        InitializeSlot("Z");
        InitializeSlot("X");
        InitializeSlot("C");
        InitializeSlot("V");

        if (debugLoadout)
        {
            Debug.Log("[AbilityLoadoutModule] Initialized");
            LogLoadout();
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        // Check for combo timeouts
        CheckComboTimeouts();
    }

    private void InitializeSlot(string slotKey)
    {
        comboIndices[slotKey] = 0;
        lastUseTime[slotKey] = -999f;
    }

    /// <summary>
    /// Get the current ability for a given slot key.
    /// Handles combo progression automatically.
    /// </summary>
    public AbilityDefinition GetCurrentAbilityForSlot(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null) return null;

        // Check if combo timed out
        if (HasComboTimedOut(slotKey))
        {
            ResetCombo(slotKey);
        }

        int index = GetComboIndex(slotKey);
        return slotData.GetAbilityAtIndex(index);
    }

    /// <summary>
    /// Get the slot data for a given key.
    /// </summary>
    public AbilitySlotData GetSlotData(string slotKey)
    {
        switch (slotKey.ToUpper())
        {
            case "BASICATTACK": return basicAttackChain;
            case "Q": return quickslotQ;
            case "Z": return quickslotZ;
            case "X": return quickslotX;
            case "C": return quickslotC;
            case "V": return quickslotV;
            default:
                Debug.LogWarning($"[AbilityLoadoutModule] Unknown slot key: {slotKey}");
                return null;
        }
    }

    /// <summary>
    /// Advance combo to next ability in chain.
    /// Called when current ability completes.
    /// </summary>
    public void AdvanceCombo(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null) return;

        int currentIndex = GetComboIndex(slotKey);
        int nextIndex = (currentIndex + 1) % slotData.ChainLength;

        comboIndices[slotKey] = nextIndex;
        lastUseTime[slotKey] = Time.time;

        OnComboAdvanced?.Invoke(slotKey, nextIndex);

        if (debugLoadout)
        {
            var nextAbility = slotData.GetAbilityAtIndex(nextIndex);
            Debug.Log($"[AbilityLoadoutModule] {slotKey} combo advanced to {nextIndex}: {nextAbility?.abilityName}");
        }
    }

    /// <summary>
    /// Reset combo back to first ability.
    /// </summary>
    public void ResetCombo(string slotKey)
    {
        if (comboIndices.ContainsKey(slotKey) && comboIndices[slotKey] != 0)
        {
            comboIndices[slotKey] = 0;
            OnComboReset?.Invoke(slotKey);

            if (debugLoadout)
            {
                Debug.Log($"[AbilityLoadoutModule] {slotKey} combo reset to start");
            }
        }
    }

    /// <summary>
    /// Mark that a slot was just used (for combo timing).
    /// </summary>
    public void MarkSlotUsed(string slotKey)
    {
        lastUseTime[slotKey] = Time.time;
    }

    /// <summary>
    /// Get current combo index for a slot.
    /// </summary>
    public int GetComboIndex(string slotKey)
    {
        return comboIndices.ContainsKey(slotKey) ? comboIndices[slotKey] : 0;
    }

    /// <summary>
    /// Check if combo has timed out for a slot.
    /// </summary>
    private bool HasComboTimedOut(string slotKey)
    {
        if (!lastUseTime.ContainsKey(slotKey))
            return false;

        float timeSinceUse = Time.time - lastUseTime[slotKey];
        return timeSinceUse > comboResetTime;
    }

    /// <summary>
    /// Check all slots for combo timeouts.
    /// </summary>
    private void CheckComboTimeouts()
    {
        // Create a list copy to avoid modifying collection during enumeration
        var slotsToCheck = new System.Collections.Generic.List<string>(comboIndices.Keys);

        foreach (var slotKey in slotsToCheck)
        {
            if (HasComboTimedOut(slotKey))
            {
                ResetCombo(slotKey);
            }
        }
    }

    /// <summary>
    /// Assign a new ability/combo to a quickslot.
    /// Cannot change BasicAttack slot.
    /// </summary>
    public bool AssignSlot(string slotKey, AbilitySlotData slotData)
    {
        if (slotKey.ToUpper() == "BASICATTACK")
        {
            Debug.LogWarning("[AbilityLoadoutModule] Cannot reassign BasicAttack slot");
            return false;
        }

        if (slotData != null && !slotData.IsUnlocked())
        {
            Debug.LogWarning($"[AbilityLoadoutModule] Cannot assign locked slot: {slotData.slotName}");
            return false;
        }

        switch (slotKey.ToUpper())
        {
            case "Q": quickslotQ = slotData; break;
            case "Z": quickslotZ = slotData; break;
            case "X": quickslotX = slotData; break;
            case "C": quickslotC = slotData; break;
            case "V": quickslotV = slotData; break;
            default:
                Debug.LogWarning($"[AbilityLoadoutModule] Unknown slot key: {slotKey}");
                return false;
        }

        // Reset combo for this slot
        ResetCombo(slotKey);

        OnSlotChanged?.Invoke(slotKey, slotData);

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Assigned {slotData?.slotName ?? "Empty"} to slot {slotKey}");
        }

        return true;
    }

    /// <summary>
    /// Set the BasicAttack slot (for NPC configuration only).
    /// Players cannot change BasicAttack at runtime.
    /// </summary>
    public void SetBasicAttackSlot(AbilitySlotData slotData)
    {
        basicAttackChain = slotData;
        ResetCombo("BasicAttack");

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Set BasicAttack to: {slotData?.slotName ?? "empty"}");
        }
    }

    /// <summary>
    /// Get the display icon for a slot (for UI).
    /// </summary>
    public Sprite GetSlotIcon(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        return slotData?.GetDisplayIcon();
    }

    /// <summary>
    /// Get the current ability icon for a slot (changes as combo advances).
    /// </summary>
    public Sprite GetCurrentAbilityIcon(string slotKey)
    {
        var ability = GetCurrentAbilityForSlot(slotKey);
        return ability?.icon;
    }

    /// <summary>
    /// Check if a slot has a combo assigned.
    /// </summary>
    public bool IsSlotCombo(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        return slotData != null && slotData.IsCombo;
    }

    /// <summary>
    /// Get combo progress info for UI (e.g., "2/3").
    /// </summary>
    public string GetComboProgressText(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null || !slotData.IsCombo)
            return "";

        int current = GetComboIndex(slotKey) + 1;
        int total = slotData.ChainLength;
        return $"{current}/{total}";
    }

    private void LogLoadout()
    {
        Debug.Log("=== Ability Loadout ===");
        Debug.Log($"BasicAttack: {basicAttackChain?.slotName ?? "None"}");
        Debug.Log($"Defense: {defenseSlot?.defenseName ?? "None"}");
        Debug.Log($"Q: {quickslotQ?.slotName ?? "Empty"}");
        Debug.Log($"Z: {quickslotZ?.slotName ?? "Empty"}");
        Debug.Log($"X: {quickslotX?.slotName ?? "Empty"}");
        Debug.Log($"C: {quickslotC?.slotName ?? "Empty"}");
        Debug.Log($"V: {quickslotV?.slotName ?? "Empty"}");
    }

    /// <summary>
    /// Get the current defense ability.
    /// </summary>
    public DefenseAbilityData GetDefenseAbility()
    {
        return defenseSlot;
    }

    /// <summary>
    /// Assign a new defense ability to the defense slot.
    /// </summary>
    public bool AssignDefense(DefenseAbilityData newDefense)
    {
        defenseSlot = newDefense;

        // Notify ActiveDefenseModule of the change
        var defenseModule = brain?.GetModule<ActiveDefenseModule>();
        if (defenseModule != null)
        {
            defenseModule.SetDefense(newDefense);
        }

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Assigned defense: {newDefense?.defenseName ?? "None"}");
        }

        return true;
    }
}