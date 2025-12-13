using UnityEngine;

/// <summary>
/// Configuration for NPC ability loadout.
/// Used by humanoid NPCs to configure AbilityLoadoutModule with slots and combos.
/// 
/// This allows NPCs to use the same ability system as players:
/// - BasicAttack slot with combo chains
/// - QZXCV slots for special abilities
/// 
/// Bears don't need this - they just use raw abilities from the archetype.
/// Humanoids NEED this - they use loadout system like players.
/// </summary>
[System.Serializable]
public class AbilityLoadoutConfiguration
{
    [Header("Basic Attack")]
    [Tooltip("Combo chain for basic attacks (left-click equivalent)")]
    public AbilitySlotData basicAttackSlot;

    [Header("Special Ability Slots")]
    [Tooltip("Q key ability")]
    public AbilitySlotData qSlot;

    [Tooltip("Z key ability")]
    public AbilitySlotData zSlot;

    [Tooltip("X key ability")]
    public AbilitySlotData xSlot;

    [Tooltip("C key ability")]
    public AbilitySlotData cSlot;

    [Tooltip("V key ability")]
    public AbilitySlotData vSlot;

    /// <summary>
    /// Check if this configuration has any slots configured
    /// </summary>
    public bool HasAnySlots()
    {
        return basicAttackSlot != null ||
               qSlot != null ||
               zSlot != null ||
               xSlot != null ||
               cSlot != null ||
               vSlot != null;
    }

    /// <summary>
    /// Get all configured slots with their keys
    /// </summary>
    public (string key, AbilitySlotData slot)[] GetConfiguredSlots()
    {
        var slots = new System.Collections.Generic.List<(string, AbilitySlotData)>();

        if (basicAttackSlot != null) slots.Add(("BasicAttack", basicAttackSlot));
        if (qSlot != null) slots.Add(("Q", qSlot));
        if (zSlot != null) slots.Add(("Z", zSlot));
        if (xSlot != null) slots.Add(("X", xSlot));
        if (cSlot != null) slots.Add(("C", cSlot));
        if (vSlot != null) slots.Add(("V", vSlot));

        return slots.ToArray();
    }
}