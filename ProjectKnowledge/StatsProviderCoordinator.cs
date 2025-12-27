using UnityEngine;

public class StatsProviderCoordinator : ProviderCoordinator
{
    [Header("Required Providers")]
    [Tooltip("Provides: Attack Power, Crit Chance, Armor, etc.\nAssign: RPGSecondaryStats")]
    [SerializeField] private MonoBehaviour combatStatsProvider; // ICombatStatsProvider

    [Tooltip("Provides: Health, Mana, Stamina (all resources)\nAssign: RPGResources")]
    [SerializeField] private MonoBehaviour resourcesProvider; // IHealthProvider + IResourceProvider

    // Cached interfaces
    private ICombatStatsProvider cachedCombatStats;
    private IHealthProvider cachedHealth;
    private IResourceProvider cachedResources;

    // Public accessors
    public ICombatStatsProvider CombatStats => cachedCombatStats;
    public IHealthProvider Health => cachedHealth;
    public IResourceProvider Resources => cachedResources;

    protected override bool ValidateSlots()
    {
        bool valid = true;

        // Validate combat stats
        if (combatStatsProvider == null || !(combatStatsProvider is ICombatStatsProvider))
        {
            Debug.LogError($"[StatsProviderCoordinator] Combat Stats Provider slot empty or invalid! Assign RPGSecondaryStats.", this);
            valid = false;
        }

        // Validate resources (which provides BOTH health and resources)
        if (resourcesProvider == null)
        {
            Debug.LogError($"[StatsProviderCoordinator] Resources Provider slot empty! Assign RPGResources (handles health + mana + stamina).", this);
            valid = false;
        }
        else
        {
            if (!(resourcesProvider is IHealthProvider))
            {
                Debug.LogError($"[StatsProviderCoordinator] Resources Provider must implement IHealthProvider!", this);
                valid = false;
            }
            if (!(resourcesProvider is IResourceProvider))
            {
                Debug.LogWarning($"[StatsProviderCoordinator] Resources Provider doesn't implement IResourceProvider - mana/stamina won't work.", this);
            }
        }

        return valid;
    }

    protected override void CacheProviders()
    {
        cachedCombatStats = combatStatsProvider as ICombatStatsProvider;

        // Cache both health AND resources from the same provider
        cachedHealth = resourcesProvider as IHealthProvider;
        cachedResources = resourcesProvider as IResourceProvider;
    }
}