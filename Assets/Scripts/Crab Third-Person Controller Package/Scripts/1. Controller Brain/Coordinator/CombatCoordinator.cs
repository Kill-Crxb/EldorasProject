using UnityEngine;

public class CombatCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private MeleeModule melee;
    private WeaponModule weapon;
    private DamageCoordinator damage;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        melee = brain.GetModule<MeleeModule>();
        weapon = brain.GetModule<WeaponModule>();
        damage = brain.GetModule<DamageCoordinator>();
    }

    public void UpdateModule()
    {
    }

    public MeleeModule GetMeleeModule() => melee;
    public WeaponModule GetWeaponModule() => weapon;
    public DamageCoordinator GetDamageCoordinator() => damage;

    public bool CanAttack() => melee?.CanAttack() ?? false;
    public bool CanBlock() => melee?.CanBlock() ?? false;

    public bool IsAttacking() => melee?.IsAttacking ?? false;
    public bool IsBlocking() => melee?.IsBlocking ?? false;

    public bool IsBusyWithCombat() => melee?.IsBusyWithMelee ?? false;

    public bool IsAlive() => damage?.IsAlive() ?? true;
    public bool IsDefending() => damage?.IsDefending() ?? false;

    public int GetComboCount() => melee?.CurrentComboCount ?? 0;

    public float GetAttackPower() => damage?.GetAttackPower() ?? 0f;
    public float GetCurrentHealth() => damage?.GetCurrentHealth() ?? 0f;
    public float GetHealthPercentage() => damage?.GetHealthPercentage() ?? 0f;

    public AttackModule GetAttackModule() => melee?.Attack;
    public ComboModule GetComboModule() => melee?.Combo;
    public ActiveDefenseModule GetDefenseModule() => melee?.ActiveDefense;
}