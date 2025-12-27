using UnityEngine;

/// <summary>
/// Represents a slottable ability or combo chain.
/// Can be assigned to quickslots (Z, X, C, V).
/// Single abilities are just combos with a chain of 1.
/// </summary>
[CreateAssetMenu(fileName = "New Ability Slot", menuName = "RPG/Ability Slot")]
public class AbilitySlotData : ScriptableObject
{
    [Header("Display Info")]
    [Tooltip("Name shown in UI (e.g., 'Fire Slam' or 'My Combo 1')")]
    public string slotName = "New Ability Slot";

    [Tooltip("Icon shown on quickslot button - uses first ability's icon if empty")]
    public Sprite displayIcon;

    [Header("Ability Chain")]
    [Tooltip("Chain of abilities. Single ability = array of 1, Combo = array of 2+")]
    public AbilityDefinition[] abilityChain;

    [Header("Unlock Requirements (Optional)")]
    [Tooltip("Does this slot require unlocking through progression?")]
    public bool requiresUnlock = false;

    [Tooltip("Minimum level required to use this slot")]
    public int requiredLevel = 1;

    [Tooltip("Quest/Achievement ID required to unlock (leave empty if none)")]
    public string unlockCondition = "";

    // Properties
    public bool IsCombo => abilityChain != null && abilityChain.Length > 1;
    public int ChainLength => abilityChain?.Length ?? 0;

    /// <summary>
    /// Get the icon to display. Uses displayIcon if set, otherwise first ability's icon.
    /// </summary>
    public Sprite GetDisplayIcon()
    {
        if (displayIcon != null)
            return displayIcon;

        if (abilityChain != null && abilityChain.Length > 0 && abilityChain[0] != null)
            return abilityChain[0].icon;

        return null;
    }

    /// <summary>
    /// Get ability at specific index in chain.
    /// </summary>
    public AbilityDefinition GetAbilityAtIndex(int index)
    {
        if (abilityChain == null || index < 0 || index >= abilityChain.Length)
            return null;

        return abilityChain[index];
    }

    /// <summary>
    /// Check if this slot is unlocked for the player.
    /// TODO: Hook into your progression system when ready.
    /// </summary>
    public bool IsUnlocked()
    {
        if (!requiresUnlock)
            return true;

        // TODO: Implement unlock checking when progression system is ready
        // Example: return PlayerProgression.Level >= requiredLevel && PlayerProgression.HasUnlocked(unlockCondition);

        // For now, everything is unlocked
        return true;
    }

    /// <summary>
    /// Validate the ability chain on creation.
    /// </summary>
    private void OnValidate()
    {
        if (abilityChain != null)
        {
            for (int i = 0; i < abilityChain.Length; i++)
            {
                if (abilityChain[i] == null)
                {
                    Debug.LogWarning($"[AbilitySlotData] '{name}' has null ability at index {i}");
                }
            }
        }
    }
}