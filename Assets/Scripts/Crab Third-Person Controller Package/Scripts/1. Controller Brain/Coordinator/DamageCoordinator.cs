using UnityEngine;

/// <summary>
/// Coordinates damage processing systems (DamageOut, DamageIn, and adapters).
/// Provides convenience API for damage operations.
/// All child modules remain independently accessible via Brain.
/// </summary>
public class DamageCoordinator : MonoBehaviour, IBrainModule, ISystemCoordinator
{
    [Header("Module Settings")]
    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private DamageOut damageOut;
    private DamageIn damageIn;
    private ICombatStatsProvider statsAdapter;
    private IHealthProvider healthAdapter;
    private IDefenseProvider defenseAdapter;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        damageOut = GetComponentInChildren<DamageOut>();
        damageIn = GetComponentInChildren<DamageIn>();

        var statsAdapterComp = GetComponentInChildren<ICombatStatsProvider>() as MonoBehaviour;
        if (statsAdapterComp != null) statsAdapter = statsAdapterComp as ICombatStatsProvider;

        var healthAdapterComp = GetComponentInChildren<IHealthProvider>() as MonoBehaviour;
        if (healthAdapterComp != null) healthAdapter = healthAdapterComp as IHealthProvider;

        var defenseAdapterComp = GetComponentInChildren<IDefenseProvider>() as MonoBehaviour;
        if (defenseAdapterComp != null) defenseAdapter = defenseAdapterComp as IDefenseProvider;
    }

    public void UpdateModule()
    {
    }

    public DamageOut GetDamageOut() => damageOut;
    public DamageIn GetDamageIn() => damageIn;
    public ICombatStatsProvider GetStatsAdapter() => statsAdapter;
    public IHealthProvider GetHealthAdapter() => healthAdapter;
    public IDefenseProvider GetDefenseAdapter() => defenseAdapter;

    public bool IsAlive() => healthAdapter?.IsAlive() ?? true;
    public float GetCurrentHealth() => healthAdapter?.GetCurrentHealth() ?? 0f;
    public float GetMaxHealth() => healthAdapter?.GetMaxHealth() ?? 100f;
    public float GetHealthPercentage() => healthAdapter?.GetHealthPercentage() ?? 0f;

    public bool CanDefend() => defenseAdapter?.CanDefend() ?? false;
    public bool IsDefending() => (defenseAdapter?.IsBlocking() ?? false) || (defenseAdapter?.IsParrying() ?? false);

    public float GetAttackPower() => statsAdapter?.GetAttackPower() ?? 0f;
    public float GetArmor() => statsAdapter?.GetArmor() ?? 0f;
}
