using UnityEngine;

/// <summary>
/// ItemBaseType - Defines default moveset and properties for item types
/// 
/// Purpose: Share common combat behaviors across items of same type
/// 
/// Examples:
/// - "Katana Base" → All katanas inherit: [QuickSlash, Thrust, Overhead] combo
/// - "Longsword Base" → All longswords inherit: [Slash, Stab, Pommel Strike] combo
/// - "Bow Base" → All bows inherit: [QuickShot] + charge mechanics
/// 
/// Usage:
/// - Create assets: Right-click → Items/Base Type
/// - Configure default combo/abilities
/// - Reference in ItemDefinition (items inherit unless overridden)
/// 
/// Benefits:
/// - Consistent movesets across item families
/// - Easy to create variants (Steel Katana, Legendary Katana share base)
/// - Individual items can override for unique weapons
/// - Animation set reuse
/// </summary>
[CreateAssetMenu(fileName = "New Item Base Type", menuName = "Items/Base Type")]
public class ItemBaseType : ScriptableObject
{
    [Header("Identification")]
    [Tooltip("Unique identifier (e.g., 'katana_base', 'longsword_base')")]
    public string baseTypeId;

    [Tooltip("Display name for designer reference")]
    public string displayName;

    [Header("Default Moveset")]
    [Tooltip("Default attack combo (usually 3 hits). Items inherit unless overridden.")]
    public AbilityDefinition[] defaultCombo;

    [Tooltip("Default defense ability (block/parry). Items inherit unless overridden.")]
    public AbilityDefinition defaultDefense;

    [Tooltip("Special abilities available to all items of this type (optional)")]
    public AbilityDefinition[] defaultSpecials;

    [Header("Animation")]
    [Tooltip("Animator override controller for this weapon type")]
    public AnimatorOverrideController animatorOverride;

    [Header("Base Properties")]
    [Tooltip("Base attack speed multiplier")]
    public float baseAttackSpeed = 1.0f;

    [Tooltip("Base reach/range in meters")]
    public float baseReach = 2.0f;

    [Tooltip("Does this weapon type support combo chaining?")]
    public bool canCombo = true;

    [Tooltip("Maximum combo count if canCombo is true")]
    public int maxComboCount = 3;

    [Header("Behavior Flags")]
    [Tooltip("Can this weapon type block attacks?")]
    public bool canBlock = true;

    [Tooltip("Can this weapon type parry attacks?")]
    public bool canParry = true;

    [Tooltip("Is this a two-handed weapon?")]
    public bool isTwoHanded = false;

    [Header("Visual/Audio")]
    [Tooltip("Default weapon trail effect")]
    public ParticleSystem weaponTrailEffect;

    [Tooltip("Default impact effects")]
    public ParticleSystem[] impactEffects;

    [Tooltip("Default swing sounds")]
    public AudioClip[] swingSounds;

    [Tooltip("Default hit sounds")]
    public AudioClip[] hitSounds;

    [Header("Description")]
    [TextArea(3, 5)]
    public string description;

    /// <summary>
    /// Get the complete ability set (combo + defense + specials)
    /// </summary>
    public AbilityDefinition[] GetAllDefaultAbilities()
    {
        // Count total abilities
        int comboCount = defaultCombo != null ? defaultCombo.Length : 0;
        int defenseCount = defaultDefense != null ? 1 : 0;
        int specialCount = defaultSpecials != null ? defaultSpecials.Length : 0;
        int totalCount = comboCount + defenseCount + specialCount;

        // Guard clause - no abilities
        if (totalCount == 0) return new AbilityDefinition[0];

        // Build array
        var allAbilities = new AbilityDefinition[totalCount];
        int index = 0;

        // Add combo
        if (defaultCombo != null)
        {
            foreach (var ability in defaultCombo)
            {
                allAbilities[index++] = ability;
            }
        }

        // Add defense
        if (defaultDefense != null)
        {
            allAbilities[index++] = defaultDefense;
        }

        // Add specials
        if (defaultSpecials != null)
        {
            foreach (var ability in defaultSpecials)
            {
                allAbilities[index++] = ability;
            }
        }

        return allAbilities;
    }

    /// <summary>
    /// Validation helper
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(baseTypeId) && !string.IsNullOrEmpty(displayName);
    }
}