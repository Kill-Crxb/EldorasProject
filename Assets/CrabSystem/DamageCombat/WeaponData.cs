using UnityEngine;

/// <summary>
/// Weapon Data — stats and dice damage expression for a weapon.
/// Supports both manufactured weapons (swords, axes) and natural weapons (claws, teeth).
///
/// Damage resolution order (evaluated in DamageEffect.GetWeaponDamage):
///   damageDice.Roll() + flatBonus → passed as baseDamage into the full pipeline.
/// The flat damage field is retained for reference/display but no longer drives runtime damage.
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Properties")]
    public string weaponName = "My Sword";
    public WeaponType weaponType = WeaponType.Sword;
    public WeaponCategory category = WeaponCategory.Manufactured;
    public float attackSpeed = 1.2f;
    public float reach = 2f;

    [Header("Dice Damage")]
    [Tooltip("Dice expression rolled on each hit — e.g. 1d8 for a longsword, 1d4 for a dagger")]
    public DiceRoll damageDice = DiceRoll.D6();

    [Tooltip("Flat value added to every roll (can be negative for off-hand penalty)")]
    public float flatBonus = 0f;

    [Header("Weapon Classification")]
    [Tooltip("Is this a natural weapon (claws, teeth) or manufactured (sword, axe)?")]
    public bool isNaturalWeapon = false;

    [Tooltip("For natural weapons: specific socket name (e.g. 'LeftClaw', 'RightClaw', 'Jaw')")]
    public string preferredSocketName = "";

    [Header("Stamina Costs")]
    public float lightAttackStamina = 15f;
    public float heavyAttackStamina = 30f;
    public float blockStamina = 5f;

    [Header("Combat Properties")]
    public bool canBlock = true;
    public bool canParry = true;
    public bool hasCombos = true;
    public int maxComboCount = 3;

    [Header("Visual")]
    public GameObject weaponModel;
    public Transform weaponSocket;

    [Header("Effects")]
    public ParticleSystem attackEffect;
    public AudioClip[] attackSounds;

    /// <summary>
    /// Roll the weapon's damage expression and add the flat bonus.
    /// Call once per hit — do not cache.
    /// </summary>
    public float RollDamage() => damageDice.Roll() + flatBonus;

    /// <summary>Minimum possible damage roll (before stats/multipliers).</summary>
    public float DamageMin() => damageDice.Min() + flatBonus;

    /// <summary>Maximum possible damage roll (before stats/multipliers).</summary>
    public float DamageMax() => damageDice.Max() + flatBonus;

    /// <summary>Human-readable label for tooltips — e.g. "1d8 + 2".</summary>
    public string DamageLabel() => flatBonus == 0f
        ? damageDice.Label()
        : $"{damageDice.Label()} {(flatBonus > 0f ? "+" : "")}{flatBonus}";
}

/// <summary>
/// Weapon Category — determines behaviour patterns.
/// </summary>
public enum WeaponCategory
{
    Manufactured,
    Natural
}