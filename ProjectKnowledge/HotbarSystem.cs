using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the three action bars and bridges slot assignments to AbilitySystem.
/// Single source of truth for what is assigned to each hotbar slot.
///
/// SO resolution uses AbilitySlotDatabase (inspector-registered) — never Resources.Load.
/// Combo chains belong to AbilityLoadoutModule (ZXCV); TriggerSlot calls UseAbility directly.
/// </summary>
public class HotbarSystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private ControllerBrain brain;
    private AbilitySystem abilitySystem;
    private SlotTransformationSystem transformSystem;

    private ActionBarConfig centreBar;
    private ActionBarConfig bottomLeftBar;
    private ActionBarConfig bottomRightBar;

    public event Action<string, int> OnSlotChanged;

    public bool IsEnabled { get; set; } = true;

    // ── IBrainModule ─────────────────────────────────────────────────────

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        abilitySystem = brain.GetModule<AbilitySystem>();

        centreBar      = new ActionBarConfig("centre",      4, 1);
        bottomLeftBar  = new ActionBarConfig("bottomLeft",  4, 1);
        bottomRightBar = new ActionBarConfig("bottomRight", 4, 1);
    }

    public void LateInitialize()
    {
        if (abilitySystem == null) abilitySystem = brain.GetModule<AbilitySystem>();
        transformSystem = brain.GetModule<SlotTransformationSystem>();
    }

    public void UpdateModule() { }

    // ── Assignment API ────────────────────────────────────────────────────

    public void AssignSlot(string barId, int index, AbilitySlotData slotData)
    {
        var config = ResolveBar(barId);
        if (config == null || index < 0 || index >= config.slots.Count) return;

        config.slots[index].abilitySlotId = slotData != null ? slotData.name : "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);

        if (debugLogging)
            Debug.Log($"[HotbarSystem] {barId}[{index}] = '{slotData?.name ?? "empty"}'");
    }

    public void AssignSlot(string barId, int index, AbilityDefinition ability)
    {
        var config = ResolveBar(barId);
        if (config == null || index < 0 || index >= config.slots.Count) return;

        config.slots[index].abilitySlotId = ability != null ? $"ability:{ability.abilityId}" : "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);
    }

    /// <summary>
    /// Stub — assigns an item to a slot. Full implementation deferred until ItemSystem.UseItem exists.
    /// </summary>
    public void AssignItemSlot(string barId, int index, ItemInstance item)
    {
        var config = ResolveBar(barId);
        if (config == null || index < 0 || index >= config.slots.Count) return;

        config.slots[index].abilitySlotId  = "";
        config.slots[index].itemInstanceId = item?.instanceId ?? "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);

        if (debugLogging)
            Debug.Log($"[HotbarSystem] Item stub assigned to {barId}[{index}]: {item?.instanceId ?? "null"}");
    }

    public void ClearSlot(string barId, int index)
    {
        var config = ResolveBar(barId);
        if (config == null || index < 0 || index >= config.slots.Count) return;

        config.slots[index].abilitySlotId  = "";
        config.slots[index].itemInstanceId = "";
        OnSlotChanged?.Invoke(barId, index);
        GameEvents.HotbarSlotChanged(barId, index);
    }

    // ── Query API ─────────────────────────────────────────────────────────

    public ActionBarSlotData GetSlot(string barId, int index)
        => ResolveBar(barId)?.GetSlot(index);

    public ActionBarConfig GetConfig(string barId)
        => ResolveBar(barId);

    /// <summary>
    /// Resolves the base AbilityDefinition for a slot.
    /// Returns null if unassigned or the SO cannot be found.
    /// </summary>
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

    /// <summary>
    /// Finds which slot in any bar is currently assigned a given abilityId.
    /// Returns (null, -1) if not found.
    /// </summary>
    public (string barId, int index) FindSlotForAbility(string abilityId)
    {
        foreach (var (id, config) in AllBars())
        {
            for (int i = 0; i < config.slots.Count; i++)
            {
                var slot = config.slots[i];
                if (!slot.IsAssigned) continue;

                if (slot.abilitySlotId == $"ability:{abilityId}") return (id, i);

                var slotData = AbilitySlotDatabase.Get(slot.abilitySlotId);
                if (slotData?.abilityChain == null) continue;
                foreach (var ability in slotData.abilityChain)
                    if (ability != null && ability.abilityId == abilityId) return (id, i);
            }
        }
        return (null, -1);
    }

    public void ResizeBar(string barId, int slotCount, int rowCount)
    {
        ResolveBar(barId)?.Resize(slotCount, rowCount);
    }

    // ── Execution ─────────────────────────────────────────────────────────

    /// <summary>
    /// Called by InputSystem on Hotbar1–9 press.
    /// Priority: active override → base ability. Base is fully suppressed while override exists.
    /// Charge input is handled by InputSystem before this is called.
    /// </summary>
    public void TriggerSlot(string barId, int index)
    {
        if (abilitySystem == null) return;

        var slot = GetSlot(barId, index);
        if (slot == null || !slot.IsAssigned) return;

        // Item slot — stub until ItemSystem.UseItem is implemented
        if (slot.HasItem)
        {
            Debug.LogWarning($"[HotbarSystem] Item use not yet implemented: {slot.itemInstanceId}");
            return;
        }

        // Override path — base ability is suppressed while this is active
        if (transformSystem != null && transformSystem.HasOverride(barId, index))
        {
            var overrideAbility = transformSystem.GetEffectiveAbility(barId, index);
            if (overrideAbility != null && abilitySystem.CanUseAbility(overrideAbility.abilityId))
            {
                abilitySystem.UseAbility(overrideAbility.abilityId);
                transformSystem.ClearOverride(barId, index);
            }
            return; // Base ability NOT executed regardless of success
        }

        // Base path
        var ability = ResolveSlotAbility(slot);
        if (ability == null) return;
        abilitySystem.UseAbility(ability.abilityId);
    }

    // ── ISaveable ─────────────────────────────────────────────────────────

    public string GetSaveId()      => "hotbar";
    public int    GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var data = new HotbarSaveData
        {
            version        = GetSaveVersion(),
            centreBar      = centreBar,
            bottomLeftBar  = bottomLeftBar,
            bottomRightBar = bottomRightBar,
        };
        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<HotbarSaveData>(json);
        if (data == null) return;

        if (data.centreBar      != null) centreBar      = data.centreBar;
        if (data.bottomLeftBar  != null) bottomLeftBar  = data.bottomLeftBar;
        if (data.bottomRightBar != null) bottomRightBar = data.bottomRightBar;

        NotifyAllBarsChanged();

        if (debugLogging) Debug.Log("[HotbarSystem] Loaded save data.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private ActionBarConfig ResolveBar(string barId)
    {
        switch (barId)
        {
            case "centre":      return centreBar;
            case "bottomLeft":  return bottomLeftBar;
            case "bottomRight": return bottomRightBar;
            default:
                Debug.LogWarning($"[HotbarSystem] Unknown barId '{barId}'");
                return null;
        }
    }

    private IEnumerable<(string id, ActionBarConfig config)> AllBars()
    {
        yield return ("centre",      centreBar);
        yield return ("bottomLeft",  bottomLeftBar);
        yield return ("bottomRight", bottomRightBar);
    }

    private void NotifyAllBarsChanged()
    {
        foreach (var (id, config) in AllBars())
            for (int i = 0; i < config.slots.Count; i++)
                OnSlotChanged?.Invoke(id, i);
    }
}
