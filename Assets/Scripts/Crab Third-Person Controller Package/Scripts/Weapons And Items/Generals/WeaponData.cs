using UnityEngine;

/// <summary>
/// Weapon Data - Enhanced for Natural Weapons (Animals)
/// Supports both manufactured weapons (swords, axes) and natural weapons (claws, teeth)
/// </summary>
[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Properties")]
    public string weaponName = "My Sword";
    public WeaponType weaponType = WeaponType.Sword;
    public WeaponCategory category = WeaponCategory.Manufactured;
    public float damage = 25f;
    public float attackSpeed = 1.2f;
    public float reach = 2f;

    [Header("Weapon Classification")]
    [Tooltip("Is this a natural weapon (claws, teeth) or manufactured (sword, axe)?")]
    public bool isNaturalWeapon = false;

    [Tooltip("For natural weapons: specific socket name (e.g., 'LeftClaw', 'RightClaw', 'Jaw')")]
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
    public GameObject weaponModel; // Your weapon model prefab
    public Transform weaponSocket; // Where to attach it (legacy - use socketConfigs instead)

    [Header("Effects")]
    public ParticleSystem attackEffect;
    public AudioClip[] attackSounds;
}

/// <summary>
/// Weapon Type Enum - Expanded for Natural Weapons
/// </summary>


/// <summary>
/// Weapon Category - Determines behavior patterns
/// </summary>
public enum WeaponCategory
{
    Manufactured,  // Traditional weapons - switched out of combat
    Natural        // Body parts - can switch during combat
}