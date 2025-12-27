using UnityEngine;

/// <summary>
/// Defines a defensive ability that can be assigned to the defense slot.
/// Uses Sekiro-style unified blocking: hold to block, parry window at start.
/// Different weapons have different parry windows and blocking efficiency.
/// 
/// Examples:
/// - Katana: Short parry window (0.15s), high block efficiency (80% reduction)
/// - Kunai: Long parry window (0.35s), low block efficiency (50% reduction)
/// - Greatsword: Very short parry window (0.1s), very high block efficiency (90% reduction)
/// </summary>
[CreateAssetMenu(fileName = "New Defense Ability", menuName = "RPG/Defense Ability")]
public class DefenseAbilityData : ScriptableObject
{
    [Header("Basic Info")]
    public string defenseId;
    public string defenseName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Block Effectiveness")]
    [Tooltip("How much damage is blocked (0-1). 0.8 = 80% reduction")]
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.7f;

    [Tooltip("Angular coverage in degrees (e.g., 120 = blocks attacks from front 120 degrees)")]
    [Range(0f, 360f)]
    public float blockAngle = 120f;

    [Header("Parry Window (Sekiro-Style)")]
    [Tooltip("Duration of parry window at start of block (0 = no parry)")]
    public float parryWindowDuration = 0.2f;

    [Tooltip("Damage reduction during parry window (usually 1.0 = 100%)")]
    [Range(0f, 1f)]
    public float parryDamageReduction = 1.0f;

    [Tooltip("Opens counter-attack window on successful parry")]
    public bool parryEnablesCounter = true;

    [Tooltip("How long counter window stays open after successful parry")]
    public float counterWindowDuration = 0.8f;

    [Header("Stamina Costs")]
    [Tooltip("Stamina cost per second while blocking")]
    public float blockStaminaDrain = 8f;

    [Tooltip("Stamina cost when parrying an attack (instant)")]
    public float parryStaminaCost = 5f;

    [Tooltip("Stamina cost when blocking an attack (instant, per hit)")]
    public float blockStaminaCost = 10f;

    [Tooltip("If true, successful parry refunds parry stamina cost")]
    public bool parryRefundsStamina = true;

    [Header("Timing")]
    [Tooltip("Time before block becomes active after pressing button")]
    public float blockStartupTime = 0.05f;

    [Header("Animation Parameters")]
    [Tooltip("Animation parameter for blocking (bool)")]
    public string blockAnimParam = "IsBlocking";

    [Tooltip("Animation trigger for successful parry")]
    public string parryAnimTrigger = "Parry";

    [Tooltip("Animation trigger when attack is blocked (not parried)")]
    public string blockHitAnimTrigger = "BlockHit";

    [Header("Effects & Feedback")]
    public GameObject blockActivationVFX;
    public GameObject parrySuccessVFX;
    public GameObject blockImpactVFX;

    [Header("Weapon Requirements (Optional)")]
    [Tooltip("If set, this defense requires specific weapon types")]
    public WeaponType[] requiredWeaponTypes;

    [Tooltip("Can this defense be used without a weapon equipped?")]
    public bool allowsUnarmed = false;

    /// <summary>
    /// Calculate damage reduction based on whether attack was parried
    /// </summary>
    public float GetDamageReduction(bool isInParryWindow)
    {
        return isInParryWindow ? parryDamageReduction : blockDamageReduction;
    }

    /// <summary>
    /// Get stamina cost for defending this attack
    /// </summary>
    public float GetStaminaCost(bool isInParryWindow)
    {
        return isInParryWindow ? parryStaminaCost : blockStaminaCost;
    }

    /// <summary>
    /// Check if this defense can be used with current weapon
    /// </summary>
    public bool IsCompatibleWithWeapon(WeaponType weaponType)
    {
        if (requiredWeaponTypes == null || requiredWeaponTypes.Length == 0)
            return true;

        foreach (var type in requiredWeaponTypes)
        {
            if (type == weaponType)
                return true;
        }

        return false;
    }
}
