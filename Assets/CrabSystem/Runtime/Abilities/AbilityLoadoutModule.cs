using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class AbilityLoadoutModule : MonoBehaviour, IBrainModule
{
    [SerializeField] private AbilitySlotData quickslotQ;
    [SerializeField] private AbilitySlotData quickslotZ;
    [SerializeField] private AbilitySlotData quickslotX;
    [SerializeField] private AbilitySlotData quickslotC;
    [SerializeField] private AbilitySlotData quickslotV;
    [SerializeField] private AbilitySlotData basicAttackChain;
    [SerializeField] private AbilityDefinition defenseSlot;
    [SerializeField] private AbilitySlotData defaultUnarmedAttack;
    [SerializeField] private AbilityDefinition defaultUnarmedDefense;
    [SerializeField] private float comboResetTime = 2f;
    [SerializeField] private List<MonoBehaviour> controlSourceComponents;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;

    private Dictionary<string, int> comboIndices = new Dictionary<string, int>();
    private Dictionary<string, float> lastUseTime = new Dictionary<string, float>();

    private IAbilityControlSource activeControlSource;
    private List<IAbilityControlSource> availableControlSources;

    public bool IsEnabled { get; set; } = true;

    public event Action<string, int> OnComboAdvanced;
    public event Action<string> OnComboReset;
    public event Action<string, AbilitySlotData> OnSlotChanged;

    public IAbilityControlSource ActiveControlSource => activeControlSource;
    public ControllerBrain Brain => brain;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        abilitySystem = brain.Abilities;
        if (abilitySystem == null)
            Debug.LogError("[AbilityLoadoutModule] AbilitySystem not found!");

        InitializeSlot("BasicAttack");
        InitializeSlot("Q");
        InitializeSlot("Z");
        InitializeSlot("X");
        InitializeSlot("C");
        InitializeSlot("V");

        if (defaultUnarmedAttack != null)
            SetBasicAttackSlot(defaultUnarmedAttack);

        if (defaultUnarmedDefense != null)
            AssignDefense(defaultUnarmedDefense);

        SetupControlSources();
        ActivateDefaultControlSource();
    }

    public void LateInitialize()
    {
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        activeControlSource?.UpdateSource();

        string slot = activeControlSource?.GetAbilitySlotToTrigger();
        if (!string.IsNullOrEmpty(slot))
            TriggerAbilitySlot(slot);

        CheckComboTimeouts();
    }

    private void SetupControlSources()
    {
        availableControlSources = new List<IAbilityControlSource>();

        var sources = GetComponentsInChildren<IAbilityControlSource>();
        availableControlSources.AddRange(sources);

        if (controlSourceComponents != null)
        {
            foreach (var component in controlSourceComponents)
            {
                if (component is IAbilityControlSource source && !availableControlSources.Contains(source))
                    availableControlSources.Add(source);
            }
        }

        var inputSystem = brain.GetModule<InputSystem>();
        if (inputSystem is IAbilityControlSource inputAsControlSource && !availableControlSources.Contains(inputAsControlSource))
            availableControlSources.Add(inputAsControlSource);
    }

    private void ActivateDefaultControlSource()
    {
        foreach (var source in availableControlSources)
        {
            var monoBehaviour = source as MonoBehaviour;
            if (monoBehaviour != null && monoBehaviour.enabled)
            {
                SetControlSource(source);
                return;
            }
        }

        Debug.LogWarning($"[AbilityLoadoutModule] No enabled control source found on {brain.EntityName}");
    }

    public void SetControlSource(IAbilityControlSource newSource)
    {
        if (newSource == activeControlSource) return;

        if (activeControlSource != null)
            activeControlSource.OnDeactivated();

        activeControlSource = newSource;

        if (activeControlSource != null)
            activeControlSource.OnActivated();
    }

    public T GetControlSource<T>() where T : class, IAbilityControlSource
    {
        return availableControlSources.OfType<T>().FirstOrDefault();
    }

    public void TriggerAbilitySlot(string slotKey)
    {
        var ability = GetCurrentAbilityForSlot(slotKey);
        if (ability == null)
            return;

        if (abilitySystem != null && abilitySystem.CanUseAbility(ability.abilityId))
        {
            abilitySystem.UseAbility(ability.abilityId);
            MarkSlotUsed(slotKey);
            AdvanceCombo(slotKey);
        }
    }

    private void InitializeSlot(string slotKey)
    {
        comboIndices[slotKey] = 0;
        lastUseTime[slotKey] = -999f;
    }

    public AbilityDefinition GetCurrentAbilityForSlot(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null) return null;

        if (HasComboTimedOut(slotKey))
            ResetCombo(slotKey);

        int index = GetComboIndex(slotKey);
        return slotData.GetAbilityAtIndex(index);
    }

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

    public void AdvanceCombo(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null) return;

        int currentIndex = GetComboIndex(slotKey);
        int nextIndex = (currentIndex + 1) % slotData.ChainLength;

        comboIndices[slotKey] = nextIndex;
        lastUseTime[slotKey] = Time.time;

        OnComboAdvanced?.Invoke(slotKey, nextIndex);
    }

    public void ResetCombo(string slotKey)
    {
        if (comboIndices.ContainsKey(slotKey) && comboIndices[slotKey] != 0)
        {
            comboIndices[slotKey] = 0;
            OnComboReset?.Invoke(slotKey);
        }
    }

    public void MarkSlotUsed(string slotKey)
    {
        lastUseTime[slotKey] = Time.time;
    }

    public int GetComboIndex(string slotKey)
    {
        return comboIndices.ContainsKey(slotKey) ? comboIndices[slotKey] : 0;
    }

    private bool HasComboTimedOut(string slotKey)
    {
        if (!lastUseTime.ContainsKey(slotKey))
            return false;

        float timeSinceUse = Time.time - lastUseTime[slotKey];
        return timeSinceUse > comboResetTime;
    }

    private void CheckComboTimeouts()
    {
        var slotsToCheck = new List<string>(comboIndices.Keys);

        foreach (var slotKey in slotsToCheck)
        {
            if (HasComboTimedOut(slotKey))
                ResetCombo(slotKey);
        }
    }

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

        ResetCombo(slotKey);
        OnSlotChanged?.Invoke(slotKey, slotData);

        return true;
    }

    public void SetBasicAttackSlot(AbilitySlotData slotData)
    {
        basicAttackChain = slotData;
        ResetCombo("BasicAttack");
    }

    public Sprite GetSlotIcon(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        return slotData?.GetDisplayIcon();
    }

    public Sprite GetCurrentAbilityIcon(string slotKey)
    {
        var ability = GetCurrentAbilityForSlot(slotKey);
        return ability?.icon;
    }

    public bool IsSlotCombo(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        return slotData != null && slotData.IsCombo;
    }

    public string GetComboProgressText(string slotKey)
    {
        var slotData = GetSlotData(slotKey);
        if (slotData == null || !slotData.IsCombo)
            return "";

        int current = GetComboIndex(slotKey) + 1;
        int total = slotData.ChainLength;
        return $"{current}/{total}";
    }

    public AbilityDefinition GetDefenseAbility()
    {
        return defenseSlot;
    }

    public bool AssignDefense(AbilityDefinition newDefense)
    {
        defenseSlot = newDefense;
        return true;
    }

    public void SetWeaponAbilities(AbilitySlotData weaponAttack, AbilityDefinition weaponDefense)
    {
        if (weaponAttack != null)
            SetBasicAttackSlot(weaponAttack);

        if (weaponDefense != null)
            AssignDefense(weaponDefense);
    }

    public void RevertToDefaultAbilities()
    {
        if (defaultUnarmedAttack != null)
            SetBasicAttackSlot(defaultUnarmedAttack);

        if (defaultUnarmedDefense != null)
            AssignDefense(defaultUnarmedDefense);
    }

    public AbilitySlotData GetDefaultUnarmedAttack() => defaultUnarmedAttack;

    public AbilityDefinition GetDefaultUnarmedDefense() => defaultUnarmedDefense;
}