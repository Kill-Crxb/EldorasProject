using System;
using System.Collections.Generic;
using UnityEngine;

public class HotbarSystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Bar Defaults (fresh character only — saves override these)")]
    [SerializeField] private BarSizeConfig centreBarDefault = new BarSizeConfig(4, 1);
    [SerializeField] private BarSizeConfig bottomLeftBarDefault = new BarSizeConfig(4, 1);
    [SerializeField] private BarSizeConfig bottomRightBarDefault = new BarSizeConfig(4, 1);

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;
    private SlotTransformationSystem transformSystem;

    private ActionBarConfig centreBar;
    private ActionBarConfig bottomLeftBar;
    private ActionBarConfig bottomRightBar;

    public event Action<string, int> OnSlotChanged;
    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        abilitySystem = brain.GetModule<AbilitySystem>();

        centreBar = new ActionBarConfig("centre", centreBarDefault.slots, centreBarDefault.rows);
        bottomLeftBar = new ActionBarConfig("bottomLeft", bottomLeftBarDefault.slots, bottomLeftBarDefault.rows);
        bottomRightBar = new ActionBarConfig("bottomRight", bottomRightBarDefault.slots, bottomRightBarDefault.rows);
    }

    public void LateInitialize()
    {
        if (abilitySystem == null) abilitySystem = brain.GetModule<AbilitySystem>();
        transformSystem = brain.GetModule<SlotTransformationSystem>();
    }

    public void UpdateModule() { }

    public void AssignSlot(string barId, int index, string abilitySlotId)
    {
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = abilitySlotId ?? "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);
    }

    public void AssignSlot(string barId, int index, AbilitySlotData slotData)
    {
        AssignSlot(barId, index, slotData?.name ?? "");
    }

    public void AssignSlot(string barId, int index, AbilityDefinition ability)
    {
        AssignSlot(barId, index, ability != null ? $"ability:{ability.abilityId}" : "");
    }

    public void AssignItemSlot(string barId, int index, string itemInstanceId)
    {
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = "";
        config.slots[index].itemInstanceId = itemInstanceId ?? "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);
    }

    public void AssignItemSlot(string barId, int index, ItemInstance item)
    {
        AssignItemSlot(barId, index, item?.instanceId ?? "");
    }

    public void ClearSlot(string barId, int index)
    {
        var config = ResolveBar(barId);
        if (config == null || !IsValidIndex(index, config)) return;

        config.slots[index].abilitySlotId = "";
        config.slots[index].itemInstanceId = "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);
    }

    public ActionBarSlotData GetSlot(string barId, int index)
        => ResolveBar(barId)?.GetSlot(index);

    public ActionBarConfig GetConfig(string barId)
        => ResolveBar(barId);

    public AbilityDefinition ResolveSlotAbility(ActionBarSlotData slot)
    {
        if (slot == null || !slot.IsAssigned) return null;

        if (slot.abilitySlotId.StartsWith("ability:"))
        {
            string abilityId = slot.abilitySlotId.Substring("ability:".Length);
            return abilitySystem?.GetAbility(abilityId);
        }

        var slotData = AbilitySlotDatabase.Get(slot.abilitySlotId);
        return slotData?.abilityChain != null && slotData.abilityChain.Length > 0
            ? slotData.abilityChain[0]
            : null;
    }

    public (string barId, int index) FindSlotForAbility(string abilityId)
    {
        foreach (var (id, config) in AllBars())
        {
            for (int i = 0; i < config.slots.Count; i++)
            {
                if (DoesSlotContainAbility(config.slots[i], abilityId))
                    return (id, i);
            }
        }
        return (null, -1);
    }

    public void ResizeBar(string barId, int slotCount, int rowCount)
    {
        ResolveBar(barId)?.Resize(slotCount, rowCount);
    }

    public void TriggerSlot(string barId, int index)
    {
        if (abilitySystem == null) return;

        var slot = GetSlot(barId, index);
        if (slot == null || !slot.IsAssigned) return;

        if (slot.HasItem)
        {
            Debug.LogWarning($"[HotbarSystem] Item use not yet implemented: {slot.itemInstanceId}");
            return;
        }

        if (transformSystem != null && transformSystem.HasOverride(barId, index))
        {
            TriggerOverride(barId, index);
            return;
        }

        var ability = ResolveSlotAbility(slot);
        if (ability != null)
            abilitySystem.UseAbility(ability.abilityId);
    }

    public string GetSaveId() => "hotbar";
    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var data = new HotbarSaveData
        {
            version = GetSaveVersion(),
            centreBar = centreBar,
            bottomLeftBar = bottomLeftBar,
            bottomRightBar = bottomRightBar,
        };
        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<HotbarSaveData>(json);
        if (data == null) return;

        if (data.centreBar != null) centreBar = data.centreBar;
        if (data.bottomLeftBar != null) bottomLeftBar = data.bottomLeftBar;
        if (data.bottomRightBar != null) bottomRightBar = data.bottomRightBar;

        NotifyAllBarsChanged();
    }

    private void TriggerOverride(string barId, int index)
    {
        var overrideAbility = transformSystem.GetEffectiveAbility(barId, index);
        if (overrideAbility != null && abilitySystem.CanUseAbility(overrideAbility.abilityId))
        {
            abilitySystem.UseAbility(overrideAbility.abilityId);
            transformSystem.ClearOverride(barId, index);
        }
    }

    private bool DoesSlotContainAbility(ActionBarSlotData slot, string abilityId)
    {
        if (!slot.IsAssigned) return false;

        if (slot.abilitySlotId == $"ability:{abilityId}") return true;

        var slotData = AbilitySlotDatabase.Get(slot.abilitySlotId);
        if (slotData?.abilityChain == null) return false;

        foreach (var ability in slotData.abilityChain)
            if (ability != null && ability.abilityId == abilityId) return true;

        return false;
    }

    private bool IsValidIndex(int index, ActionBarConfig config)
        => index >= 0 && index < config.slots.Count;

    private ActionBarConfig ResolveBar(string barId)
    {
        switch (barId)
        {
            case "centre": return centreBar;
            case "bottomLeft": return bottomLeftBar;
            case "bottomRight": return bottomRightBar;
            default: return null;
        }
    }

    private IEnumerable<(string id, ActionBarConfig config)> AllBars()
    {
        yield return ("centre", centreBar);
        yield return ("bottomLeft", bottomLeftBar);
        yield return ("bottomRight", bottomRightBar);
    }

    private void NotifyAllBarsChanged()
    {
        foreach (var (id, config) in AllBars())
            for (int i = 0; i < config.slots.Count; i++)
                OnSlotChanged?.Invoke(id, i);
    }
}

[System.Serializable]
public class BarSizeConfig
{
    [Range(1, 12)] public int slots = 4;
    [Range(1, 3)] public int rows = 1;

    public BarSizeConfig() { }
    public BarSizeConfig(int slots, int rows) { this.slots = slots; this.rows = rows; }
}