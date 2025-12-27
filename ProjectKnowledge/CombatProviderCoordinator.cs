using UnityEngine;

public class CombatProviderCoordinator : ProviderCoordinator
{
    [Header("Core Modules")]
    [SerializeField] private DamageModule damageModule;
    [SerializeField] private EffectManagerModule effectManager;

    [Header("Optional Providers")]
    // REMOVED: attackProvider - AttackModule deprecated, use abilityProvider
    [SerializeField] private MonoBehaviour abilityProvider;
    [SerializeField] private MonoBehaviour defenseProvider;

    // REMOVED: cachedAttack - AttackModule deprecated, use Ability
    private IAbilityProvider cachedAbility;
    private IDefenseProvider cachedDefense;

    public DamageModule Damage => damageModule;
    public EffectManagerModule Effects => effectManager;
    // REMOVED: Attack property - Use Ability instead
    public IAbilityProvider Ability => cachedAbility;
    public IDefenseProvider Defense => cachedDefense;

    public bool HasDamageModule => damageModule != null;
    public bool HasEffectManager => effectManager != null;
    // REMOVED: HasAttackProvider - Use HasAbilityProvider instead
    public bool HasAbilityProvider => cachedAbility != null;
    public bool HasDefenseProvider => cachedDefense != null;

    protected override bool ValidateSlots()
    {
        bool valid = true;

        // REMOVED: IAttackProvider validation - deprecated
        valid &= ValidateOptionalProvider<IAbilityProvider>(abilityProvider, "Ability Provider");
        valid &= ValidateOptionalProvider<IDefenseProvider>(defenseProvider, "Defense Provider");

        return valid;
    }

    protected override void CacheProviders()
    {
        // REMOVED: cachedAttack - deprecated
        cachedAbility = abilityProvider as IAbilityProvider;
        cachedDefense = defenseProvider as IDefenseProvider;

        // Auto-discover EffectManager if not assigned
        if (effectManager == null)
            effectManager = GetComponentInChildren<EffectManagerModule>();
    }

    protected override void OnInitialized()
    {
        // Initialize provider modules
        // REMOVED: attackProvider initialization - deprecated
        if (abilityProvider is IBrainModule m2) m2.Initialize(brain);
        if (defenseProvider is IBrainModule m3) m3.Initialize(brain);
        if (effectManager != null) effectManager.Initialize(brain);
    }
}