using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Ability Loadout Module - Manages ability quickslots and combo progression
/// 
/// UPDATED: Now uses IAbilityControlSource pattern (matches MovementSystem)
/// 
/// Control Source Integration:
/// - Polls activeControlSource.GetAbilitySlotToTrigger() each frame
/// - Supports runtime control source switching (possession, admin, testing)
/// - Works with PlayerAbilityControlSource, AIAbilityControlSource, or InputModule
/// 
/// Responsibilities:
/// - Quickslot assignments (Q/Z/X/C/V)
/// - Basic attack chain
/// - Defense ability slot
/// - Combo progression tracking
/// - Combo timeout handling
/// - Ability execution triggering
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
    [SerializeField] private AbilityDefinition defenseSlot;

    [Header("Default Unarmed Abilities")]
    [Tooltip("Default basic attack when unarmed (reverts here when weapon unequipped)")]
    [SerializeField] private AbilitySlotData defaultUnarmedAttack;
    [Tooltip("Default defense when unarmed (reverts here when weapon unequipped)")]
    [SerializeField] private AbilityDefinition defaultUnarmedDefense;

    [Header("Combo Settings")]
    [Tooltip("Time window to continue combo before resetting")]
    [SerializeField] private float comboResetTime = 2f;

    [Header("Control Sources")]
    [Tooltip("Available ability control sources (auto-discovered if empty)")]
    [SerializeField] private List<MonoBehaviour> controlSourceComponents;

    [Header("Debug")]
    [SerializeField] private bool debugLoadout = false;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;

    // Track current combo index for each slot
    private Dictionary<string, int> comboIndices = new Dictionary<string, int>();

    // Track last use time for combo reset
    private Dictionary<string, float> lastUseTime = new Dictionary<string, float>();

    // Control source management
    private IAbilityControlSource activeControlSource;
    private List<IAbilityControlSource> availableControlSources;

    public bool IsEnabled { get; set; } = true;

    // Events
    public event Action<string, int> OnComboAdvanced; // slotKey, newIndex
    public event Action<string> OnComboReset; // slotKey
    public event Action<string, AbilitySlotData> OnSlotChanged; // slotKey, newSlotData

    // ========================================
    // Properties
    // ========================================

    public IAbilityControlSource ActiveControlSource => activeControlSource;
    public ControllerBrain Brain => brain;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Get AbilityModule reference
        abilitySystem = brain.Abilities;
        if (abilitySystem == null)
        {
            Debug.LogError("[AbilityLoadoutModule] AbilitySystem not found!");
        }

        // Initialize combo tracking
        InitializeSlot("BasicAttack");
        InitializeSlot("Q");
        InitializeSlot("Z");
        InitializeSlot("X");
        InitializeSlot("C");
        InitializeSlot("V");

        // PHASE 2: Initialize with default unarmed abilities
        if (defaultUnarmedAttack != null)
            SetBasicAttackSlot(defaultUnarmedAttack);

        if (defaultUnarmedDefense != null)
            AssignDefense(defaultUnarmedDefense);

        // Setup control sources
        SetupControlSources();

        // Activate default control source
        ActivateDefaultControlSource();

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Initialized on {brain.name}");
            Debug.Log($"  Active Control: {activeControlSource?.SourceName ?? "NONE"}");
            LogLoadout();
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        // Update active control source
        activeControlSource?.UpdateSource();

        // Poll for ability input
        string slot = activeControlSource?.GetAbilitySlotToTrigger();
        if (!string.IsNullOrEmpty(slot))
        {
            TriggerAbilitySlot(slot);
        }

        // Check for combo timeouts
        CheckComboTimeouts();
    }

    // ========================================
    // Control Source Management
    // ========================================

    private void SetupControlSources()
    {
        availableControlSources = new List<IAbilityControlSource>();

        // Find all control sources in children
        var sources = GetComponentsInChildren<IAbilityControlSource>();
        availableControlSources.AddRange(sources);

        // Also check serialized list (for manual assignment)
        if (controlSourceComponents != null)
        {
            foreach (var component in controlSourceComponents)
            {
                if (component is IAbilityControlSource source && !availableControlSources.Contains(source))
                {
                    availableControlSources.Add(source);
                }
            }
        }

        // CRITICAL: Check if InputSystem implements IAbilityControlSource
        var inputSystem = brain.GetModule<InputSystem>();
        if (inputSystem is IAbilityControlSource inputAsControlSource && !availableControlSources.Contains(inputAsControlSource))
        {
            availableControlSources.Add(inputAsControlSource);

            if (debugLoadout)
                Debug.Log("[AbilityLoadoutModule] Found InputSystem as control source");
        }

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Found {availableControlSources.Count} control sources:");
            foreach (var source in availableControlSources)
            {
                Debug.Log($"  - {source.SourceName}");
            }
        }
    }

    private void ActivateDefaultControlSource()
    {
        // Find first enabled control source
        foreach (var source in availableControlSources)
        {
            var monoBehaviour = source as MonoBehaviour;
            if (monoBehaviour != null && monoBehaviour.enabled)
            {
                SetControlSource(source);
                return;
            }
        }

        Debug.LogWarning($"[AbilityLoadoutModule] No enabled control source found on {brain.name}");
    }

    /// <summary>
    /// Switch to a different control source at runtime
    /// This enables possession, pets, cutscenes, etc.
    /// </summary>
    public void SetControlSource(IAbilityControlSource newSource)
    {
        if (newSource == activeControlSource) return;

        // Deactivate current source
        if (activeControlSource != null)
        {
            activeControlSource.OnDeactivated();

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Deactivated: {activeControlSource.SourceName}");
        }

        // Activate new source
        activeControlSource = newSource;

        if (activeControlSource != null)
        {
            activeControlSource.OnActivated();

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Activated: {activeControlSource.SourceName}");
        }
    }

    /// <summary>
    /// Get control source by type
    /// Useful for possession: SetControlSource(GetControlSource<AdminAbilityControlSource>())
    /// </summary>
    public T GetControlSource<T>() where T : class, IAbilityControlSource
    {
        return availableControlSources.OfType<T>().FirstOrDefault();
    }

    // ========================================
    // Ability Triggering
    // ========================================

    /// <summary>
    /// Trigger an ability from a slot
    /// Called when control source returns a slot key
    /// </summary>
    public void TriggerAbilitySlot(string slotKey)
    {
        var ability = GetCurrentAbilityForSlot(slotKey);
        if (ability == null)
        {
            if (debugLoadout)
                Debug.LogWarning($"[AbilityLoadoutModule] No ability in slot {slotKey}");
            return;
        }

        // Check if ability can be used
        if (abilitySystem != null && abilitySystem.CanUseAbility(ability.abilityId))
        {
            abilitySystem.UseAbility(ability.abilityId);
            MarkSlotUsed(slotKey);

            // Advance combo after successful use
            AdvanceCombo(slotKey);

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Triggered {ability.abilityName} from slot {slotKey}");
        }
        else
        {
            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Cannot use {ability.abilityName} (cooldown/resources)");
        }
    }

    // ========================================
    // Slot Management
    // ========================================

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
        Debug.Log($"Defense: {defenseSlot?.abilityName ?? "None"}");
        Debug.Log($"Q: {quickslotQ?.slotName ?? "Empty"}");
        Debug.Log($"Z: {quickslotZ?.slotName ?? "Empty"}");
        Debug.Log($"X: {quickslotX?.slotName ?? "Empty"}");
        Debug.Log($"C: {quickslotC?.slotName ?? "Empty"}");
        Debug.Log($"V: {quickslotV?.slotName ?? "Empty"}");
    }

    /// <summary>
    /// Get the current defense ability.
    /// </summary>
    public AbilityDefinition GetDefenseAbility()
    {
        return defenseSlot;
    }

    /// <summary>
    /// Assign a new defense ability to the defense slot.
    /// </summary>
    public bool AssignDefense(AbilityDefinition newDefense)
    {
        defenseSlot = newDefense;

        if (debugLoadout)
        {
            Debug.Log($"[AbilityLoadoutModule] Assigned defense: {newDefense?.abilityName ?? "None"}");
        }

        return true;
    }

    // ========================================
    // Debug Info
    // ========================================

    private void OnGUI()
    {
        if (!debugLoadout || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 500, 300, 200));
        GUILayout.Label("=== ABILITY LOADOUT ===");
        GUILayout.Label($"Control: {activeControlSource?.SourceName ?? "NONE"}");
        GUILayout.Label($"BasicAttack: {basicAttackChain?.slotName ?? "Empty"}");

        var qAbility = GetCurrentAbilityForSlot("Q");
        GUILayout.Label($"Q: {qAbility?.abilityName ?? "Empty"} [{GetComboProgressText("Q")}]");

        GUILayout.EndArea();
    }

    // ========================================
    // PHASE 2: Weapon Ability Management
    // ========================================

    /// <summary>
    /// Set weapon abilities (called by equipment system)
    /// Replaces current slots with weapon's abilities
    /// </summary>
    public void SetWeaponAbilities(AbilitySlotData weaponAttack, AbilityDefinition weaponDefense)
    {
        if (weaponAttack != null)
        {
            SetBasicAttackSlot(weaponAttack);

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Set weapon attack: {weaponAttack.slotName}");
        }

        if (weaponDefense != null)
        {
            AssignDefense(weaponDefense);

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Set weapon defense: {weaponDefense.abilityName}");
        }
    }

    /// <summary>
    /// Revert to default unarmed abilities
    /// Called when weapon unequipped
    /// </summary>
    public void RevertToDefaultAbilities()
    {
        if (defaultUnarmedAttack != null)
        {
            SetBasicAttackSlot(defaultUnarmedAttack);

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Reverted to unarmed attack: {defaultUnarmedAttack.slotName}");
        }

        if (defaultUnarmedDefense != null)
        {
            AssignDefense(defaultUnarmedDefense);

            if (debugLoadout)
                Debug.Log($"[AbilityLoadoutModule] Reverted to unarmed defense: {defaultUnarmedDefense.abilityName}");
        }
    }

    /// <summary>
    /// Get current default unarmed attack
    /// </summary>
    public AbilitySlotData GetDefaultUnarmedAttack() => defaultUnarmedAttack;

    /// <summary>
    /// Get current default unarmed defense
    /// </summary>
    public AbilityDefinition GetDefaultUnarmedDefense() => defaultUnarmedDefense;

}