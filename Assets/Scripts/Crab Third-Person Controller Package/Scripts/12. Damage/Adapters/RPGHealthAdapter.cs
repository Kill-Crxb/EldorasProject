using UnityEngine;

/// <summary>
/// Adapter that bridges RPGResources to the universal damage system.
/// Implements IHealthProvider to provide health management for damage calculations.
/// 
/// Design:
/// - Translates RPGResources health system to universal health interface
/// - Auto-discovers RPGResources from Brain
/// - Handles damage application and healing
/// - Works with universal DamageIn module
/// 
/// Usage:
/// 1. Add to entity (as child of Component_Damage or Component_Brain)
/// 2. Automatically discovers RPGResources
/// 3. DamageIn finds this adapter automatically
/// </summary>
public class RPGHealthAdapter : MonoBehaviour, IHealthProvider
{
    [Header("Adapter Settings")]
    [SerializeField] private bool debugAdapter = false;

    [Header("Death Settings")]
    [SerializeField] private bool canDie = true;
    [SerializeField] private float minHealth = 0f;

    [Header("Manual References (Optional)")]
    [SerializeField] private RPGResources rpgResources;

    private ControllerBrain brain;
    private bool isInitialized = false;
    private bool isDead = false;

    // === Events ===

    /// <summary>Fired when health changes</summary>
    public event System.Action<float, float> OnHealthChanged; // (current, max)

    /// <summary>Fired when entity dies</summary>
    public event System.Action OnDied;

    // === Lifecycle ===

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Find Brain
        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError($"[RPGHealthAdapter] No ControllerBrain found in parent! " +
                          "Adapter must be child of Brain.");
            return;
        }

        // Auto-discover RPGResources if not manually assigned
        if (rpgResources == null)
        {
            rpgResources = brain.RPGResources;
        }

        if (rpgResources == null)
        {
            Debug.LogError($"[RPGHealthAdapter] No RPGResources found on {brain.name}! " +
                          "Cannot manage health.");
            return;
        }

        isInitialized = true;

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] Initialized on {brain.name} " +
                     $"(Health: {rpgResources.CurrentHealth}/{rpgResources.MaxHealth})");
        }
    }

    // === IHealthProvider Implementation ===

    public void ApplyDamage(float amount)
    {
        if (!ValidateResources()) return;

        if (isDead && canDie)
        {
            if (debugAdapter)
                Debug.Log("[RPGHealthAdapter] Already dead, ignoring damage.");
            return;
        }

        float healthBefore = rpgResources.CurrentHealth;

        // Apply damage through RPGResources
        rpgResources.ModifyHealth(-amount);

        // Clamp to minimum health if needed
        if (rpgResources.CurrentHealth < minHealth)
        {
            // Set health to minimum by modifying the difference
            float diff = minHealth - rpgResources.CurrentHealth;
            rpgResources.ModifyHealth(diff);
        }

        float healthAfter = rpgResources.CurrentHealth;
        float actualDamage = healthBefore - healthAfter;

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] Damage applied: {actualDamage:F1} " +
                     $"(Health: {healthAfter:F1}/{rpgResources.MaxHealth:F1})");
        }

        // Fire health changed event
        OnHealthChanged?.Invoke(healthAfter, rpgResources.MaxHealth);

        // Check for death
        if (canDie && healthAfter <= minHealth && !isDead)
        {
            HandleDeath();
        }
    }

    public void ApplyHealing(float amount)
    {
        if (!ValidateResources()) return;

        if (isDead && canDie)
        {
            if (debugAdapter)
                Debug.Log("[RPGHealthAdapter] Cannot heal while dead.");
            return;
        }

        float healthBefore = rpgResources.CurrentHealth;

        // Apply healing through RPGResources
        rpgResources.ModifyHealth(amount);

        float healthAfter = rpgResources.CurrentHealth;
        float actualHealing = healthAfter - healthBefore;

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] Healing applied: {actualHealing:F1} " +
                     $"(Health: {healthAfter:F1}/{rpgResources.MaxHealth:F1})");
        }

        // Fire health changed event
        OnHealthChanged?.Invoke(healthAfter, rpgResources.MaxHealth);
    }

    public float GetCurrentHealth()
    {
        if (!ValidateResources()) return 0f;
        return rpgResources.CurrentHealth;
    }

    public float GetMaxHealth()
    {
        if (!ValidateResources()) return 100f;
        return rpgResources.MaxHealth;
    }

    public float GetHealthPercentage()
    {
        if (!ValidateResources()) return 0f;
        return rpgResources.HealthPercentage; // Changed from GetHealthPercentage()
    }

    public bool IsAlive()
    {
        if (!canDie) return true;
        if (!ValidateResources()) return false;

        return !isDead && rpgResources.CurrentHealth > minHealth;
    }

    // === Helper Methods ===

    private bool ValidateResources()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (rpgResources == null)
        {
            Debug.LogWarning("[RPGHealthAdapter] RPGResources not available!");
            return false;
        }

        return true;
    }

    private void HandleDeath()
    {
        isDead = true;

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] {brain.name} has died!");
        }

        OnDied?.Invoke();
    }

    /// <summary>
    /// Revive this entity (set health above minimum and clear death flag)
    /// </summary>
    public void Revive(float healthAmount)
    {
        if (!ValidateResources()) return;

        isDead = false;
        // Changed: Use ModifyHealth to set to specific value
        rpgResources.ModifyHealth(healthAmount - rpgResources.CurrentHealth);

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] {brain.name} revived with {healthAmount:F1} health!");
        }

        OnHealthChanged?.Invoke(rpgResources.CurrentHealth, rpgResources.MaxHealth);
    }

    /// <summary>
    /// Set health to full
    /// </summary>
    public void FullHeal()
    {
        if (!ValidateResources()) return;

        rpgResources.SetHealthToMax(); // This method DOES exist in RPGResources

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] {brain.name} fully healed!");
        }

        OnHealthChanged?.Invoke(rpgResources.CurrentHealth, rpgResources.MaxHealth);
    }

    /// <summary>
    /// Set invulnerable (cannot die)
    /// </summary>
    public void SetInvulnerable(bool invulnerable)
    {
        canDie = !invulnerable;

        if (debugAdapter)
        {
            Debug.Log($"[RPGHealthAdapter] {brain.name} invulnerability: {invulnerable}");
        }
    }

    // === Inspector Helpers ===

    [ContextMenu("Debug: Show Health Info")]
    private void DebugShowHealthInfo()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[Adapter] Only works in Play Mode!");
            return;
        }

        if (!ValidateResources())
        {
            Debug.LogError("[Adapter] Cannot show health - validation failed!");
            return;
        }

        Debug.Log($"=== RPG Health Adapter ===\n" +
                  $"Entity: {brain.name}\n" +
                  $"Current Health: {GetCurrentHealth():F1}\n" +
                  $"Max Health: {GetMaxHealth():F1}\n" +
                  $"Health %: {GetHealthPercentage():P0}\n" +
                  $"Alive: {IsAlive()}\n" +
                  $"Can Die: {canDie}\n" +
                  $"Is Dead: {isDead}");
    }

    [ContextMenu("Test: Take 25 Damage")]
    private void TestTakeDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Adapter] Test only works in Play Mode!");
            return;
        }

        ApplyDamage(25f);
    }

    [ContextMenu("Test: Heal 50 HP")]
    private void TestHeal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Adapter] Test only works in Play Mode!");
            return;
        }

        ApplyHealing(50f);
    }

    [ContextMenu("Test: Full Heal")]
    private void TestFullHeal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Adapter] Test only works in Play Mode!");
            return;
        }

        FullHeal();
    }

    [ContextMenu("Test: Kill Entity")]
    private void TestKill()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Adapter] Test only works in Play Mode!");
            return;
        }

        ApplyDamage(9999f);
    }

    [ContextMenu("Test: Revive with 50 HP")]
    private void TestRevive()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Adapter] Test only works in Play Mode!");
            return;
        }

        Revive(50f);
    }
}
