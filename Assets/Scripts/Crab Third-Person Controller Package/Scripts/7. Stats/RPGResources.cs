// RPG Resources - Manages current and max resource values (receives max from RPGSecondaryStats)
using UnityEngine;

public class RPGResources : MonoBehaviour, IPlayerModule
{
    [Header("Current Values")]
    [SerializeField] private float currentHealth;
    [SerializeField] private float currentMana;
    [SerializeField] private float currentStamina;

    [Header("Max Values (Read-Only - Calculated by SecondaryStats)")]
    [SerializeField, ReadOnly] private float maxHealth;
    [SerializeField, ReadOnly] private float maxMana;
    [SerializeField, ReadOnly] private float maxStamina;

    [Header("Recovery Values (Read-Only - Calculated by SecondaryStats)")]
    [SerializeField, ReadOnly] private float regeneration;
    [SerializeField, ReadOnly] private float recollection;
    [SerializeField, ReadOnly] private float recovery;

    [Header("Regeneration Settings")]
    [SerializeField] private bool enableAutoRegeneration = true;
    [SerializeField] private float healthRegenRate = 1f; // Ticks per second
    [SerializeField] private float manaRegenRate = 1f; // Ticks per second
    [SerializeField] private float staminaRegenRate = 1f; // Ticks per second

    [Header("Debug")]
    [SerializeField] private bool debugStats = false;
    //[SerializeField] private bool logResourceChanges = false;

    private ControllerBrain brain;
    private RPGSecondaryStats secondaryStats;

    // Regeneration timing
    private float lastHealthRegen;
    private float lastManaRegen;
    private float lastStaminaRegen;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public float CurrentHealth => currentHealth;
    public float CurrentMana => currentMana;
    public float CurrentStamina => currentStamina;

    // Max values (received from secondary stats)
    public float MaxHealth => maxHealth;
    public float MaxMana => maxMana;
    public float MaxStamina => maxStamina;

    // Recovery values (received from secondary stats)
    public float Regeneration => regeneration;
    public float Recollection => recollection;
    public float Recovery => recovery;

    // Percentage properties
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public float ManaPercentage => maxMana > 0 ? currentMana / maxMana : 0f;
    public float StaminaPercentage => maxStamina > 0 ? currentStamina / maxStamina : 0f;

    // Events
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action<float, float> OnManaChanged;
    public System.Action<float, float> OnStaminaChanged;
    public System.Action<float, float, float> OnMaxValuesChanged; // maxHealth, maxMana, maxStamina

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        secondaryStats = brain.GetModule<RPGSecondaryStats>();

        if (secondaryStats != null)
        {
            secondaryStats.OnSecondaryStatChanged += OnSecondaryStatChanged;
        }

        UpdateMaxValuesFromSecondaryStats();
        InitializeCurrentValues();
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        if (enableAutoRegeneration)
        {
            HandleAutoRegeneration();
        }
    }

    void OnSecondaryStatChanged(string statName, float oldValue, float newValue)
    {
        // When secondary stats change, update our max values
        if (statName == "MaxHealth" || statName == "MaxMana" || statName == "MaxStamina" ||
            statName == "Regeneration" || statName == "Recollection" || statName == "Recovery")
        {
            UpdateMaxValuesFromSecondaryStats();
        }
    }

    void UpdateMaxValuesFromSecondaryStats()
    {
        if (secondaryStats == null) return;

        float oldMaxHealth = maxHealth;
        float oldMaxMana = maxMana;
        float oldMaxStamina = maxStamina;

        // Get calculated max values from secondary stats
        maxHealth = secondaryStats.MaxHealthFinal;
        maxMana = secondaryStats.MaxManaFinal;
        maxStamina = secondaryStats.MaxStaminaFinal;

        // Get recovery values
        regeneration = secondaryStats.RegenerationFinal;
        recollection = secondaryStats.RecollectionFinal;
        recovery = secondaryStats.RecoveryFinal;

        // Check if max values changed significantly
        if (Mathf.Abs(oldMaxHealth - maxHealth) > 0.1f ||
            Mathf.Abs(oldMaxMana - maxMana) > 0.1f ||
            Mathf.Abs(oldMaxStamina - maxStamina) > 0.1f)
        {
            ClampCurrentValues();
            OnMaxValuesChanged?.Invoke(maxHealth, maxMana, maxStamina);
        }
    }

    void InitializeCurrentValues()
    {
        // Initialize current values to max if they're zero
        if (currentHealth <= 0) currentHealth = maxHealth;
        if (currentMana <= 0) currentMana = maxMana;
        if (currentStamina <= 0) currentStamina = maxStamina;

        ClampCurrentValues();
    }

    void ClampCurrentValues()
    {
        float oldHealth = currentHealth;
        float oldMana = currentMana;
        float oldStamina = currentStamina;

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        currentMana = Mathf.Clamp(currentMana, 0f, maxMana);
        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);

        if (oldHealth != currentHealth) OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (oldMana != currentMana) OnManaChanged?.Invoke(currentMana, maxMana);
        if (oldStamina != currentStamina) OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    void HandleAutoRegeneration()
    {
        float time = Time.time;

        // Health regeneration (slower)
        if (currentHealth < maxHealth && time - lastHealthRegen >= 1f / healthRegenRate)
        {
            float regenAmount = regeneration * 0.01f; // 1% of regeneration pool per tick
            ModifyHealth(regenAmount);
            lastHealthRegen = time;
        }

        // Mana regeneration (medium)
        if (currentMana < maxMana && time - lastManaRegen >= 1f / manaRegenRate)
        {
            float regenAmount = recollection * 0.015f; // 1.5% of recollection pool per tick
            ModifyMana(regenAmount);
            lastManaRegen = time;
        }

        // Stamina regeneration (faster)
        if (currentStamina < maxStamina && time - lastStaminaRegen >= 1f / staminaRegenRate)
        {
            float regenAmount = recovery * 0.02f; // 2% of recovery pool per tick
            ModifyStamina(regenAmount);
            lastStaminaRegen = time;
        }
    }

    #region Resource Management

    public void ModifyHealth(float amount)
    {
        float oldHealth = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + amount, 0f, maxHealth);

        if (Mathf.Abs(oldHealth - currentHealth) > 0.001f)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    public void ModifyMana(float amount)
    {
        float oldMana = currentMana;
        currentMana = Mathf.Clamp(currentMana + amount, 0f, maxMana);

        if (Mathf.Abs(oldMana - currentMana) > 0.001f)
        {
            OnManaChanged?.Invoke(currentMana, maxMana);
        }
    }

    public void ModifyStamina(float amount)
    {
        float oldStamina = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);

        if (Mathf.Abs(oldStamina - currentStamina) > 0.001f)
        {
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    public void SetHealthToMax() => ModifyHealth(maxHealth - currentHealth);
    public void SetManaToMax() => ModifyMana(maxMana - currentMana);
    public void SetStaminaToMax() => ModifyStamina(maxStamina - currentStamina);

    public void SetHealthToPercentage(float percentage)
    {
        float targetHealth = maxHealth * Mathf.Clamp01(percentage);
        ModifyHealth(targetHealth - currentHealth);
    }

    public void SetManaToPercentage(float percentage)
    {
        float targetMana = maxMana * Mathf.Clamp01(percentage);
        ModifyMana(targetMana - currentMana);
    }

    public void SetStaminaToPercentage(float percentage)
    {
        float targetStamina = maxStamina * Mathf.Clamp01(percentage);
        ModifyStamina(targetStamina - currentStamina);
    }

    // Resource checks
    public bool HasHealth(float amount) => currentHealth >= amount;
    public bool HasMana(float amount) => currentMana >= amount;
    public bool HasStamina(float amount) => currentStamina >= amount;

    public bool IsHealthFull() => currentHealth >= maxHealth;
    public bool IsManFull() => currentMana >= maxMana;
    public bool IsStaminaFull() => currentStamina >= maxStamina;

    public bool IsDead() => currentHealth <= 0f;

    #endregion

    #region Public API

    public string GetResourcesSummary()
    {
        var summary = new System.Text.StringBuilder();
        summary.AppendLine("=== CURRENT RESOURCES ===");
        summary.AppendLine($"Health: {currentHealth:F0}/{MaxHealth:F0} ({HealthPercentage:P0})");
        summary.AppendLine($"Mana: {currentMana:F0}/{MaxMana:F0} ({ManaPercentage:P0})");
        summary.AppendLine($"Stamina: {currentStamina:F0}/{MaxStamina:F0} ({StaminaPercentage:P0})");
        summary.AppendLine();
        summary.AppendLine("=== RECOVERY RATES ===");
        summary.AppendLine($"Regeneration: {Regeneration:F0} ({Regeneration * 0.01f:F1}/tick)");
        summary.AppendLine($"Recollection: {Recollection:F0} ({Recollection * 0.015f:F1}/tick)");
        summary.AppendLine($"Recovery: {Recovery:F0} ({Recovery * 0.02f:F1}/tick)");

        return summary.ToString();
    }

    #endregion

    #region Debug

    void OnGUI()
    {
        if (!debugStats) return;

        GUILayout.BeginArea(new Rect(730, 10, 350, Screen.height - 20));
        GUILayout.Label("=== RPG RESOURCES ===");
        GUILayout.Label($"Health: {currentHealth:F0}/{MaxHealth:F0} ({HealthPercentage:P0})");
        GUILayout.Label($"Mana: {currentMana:F0}/{MaxMana:F0} ({ManaPercentage:P0})");
        GUILayout.Label($"Stamina: {currentStamina:F0}/{MaxStamina:F0} ({StaminaPercentage:P0})");

        GUILayout.Space(10);
        GUILayout.Label("Recovery Rates:");
        GUILayout.Label($"Regeneration: {Regeneration:F0}");
        GUILayout.Label($"Recollection: {Recollection:F0}");
        GUILayout.Label($"Recovery: {Recovery:F0}");

        GUILayout.EndArea();
    }

    #endregion
}