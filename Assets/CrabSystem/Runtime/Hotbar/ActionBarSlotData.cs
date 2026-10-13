using System;

/// <summary>
/// Serialisable state for one slot in an action bar.
/// abilitySlotId holds the asset name of the AbilitySlotData SO assigned to this slot,
/// or empty when the slot is unassigned.
/// keybindKey is reserved for a future per-player remapping pass and is always "" for now.
/// </summary>
[Serializable]
public class ActionBarSlotData
{
    public string barId       = "";
    public int    slotIndex   = 0;
    public string abilitySlotId  = "";  // AbilitySlotData.name, or "ability:<id>", or "" = unassigned
    public string itemInstanceId = "";  // ItemInstance.instanceId stub — only one of the two should be set
    public string keybindKey     = "";  // reserved — not used yet

    public bool IsAssigned  => !string.IsNullOrEmpty(abilitySlotId) || !string.IsNullOrEmpty(itemInstanceId);
    public bool HasAbility  => !string.IsNullOrEmpty(abilitySlotId);
    public bool HasItem     => !string.IsNullOrEmpty(itemInstanceId);

    public ActionBarSlotData() { }

    public ActionBarSlotData(string barId, int slotIndex)
    {
        this.barId      = barId;
        this.slotIndex  = slotIndex;
    }
}
