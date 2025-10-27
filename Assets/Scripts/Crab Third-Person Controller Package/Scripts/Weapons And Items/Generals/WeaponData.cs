using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "Combat/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Basic Properties")]
    public string weaponName = "My Sword";
    public WeaponType weaponType = WeaponType.Sword;
    public float damage = 25f;
    public float attackSpeed = 1.2f;
    public float reach = 2f;

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
    public Transform weaponSocket; // Where to attach it

    [Header("Effects")]
    public ParticleSystem attackEffect;
    public AudioClip[] attackSounds;
}

