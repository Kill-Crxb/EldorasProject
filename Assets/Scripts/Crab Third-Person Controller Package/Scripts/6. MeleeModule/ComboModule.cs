// Combo Module - Handles combo counting, timing, and progression
using UnityEngine;

public class ComboModule : MonoBehaviour, IMeleeSubModule
{
    [Header("Combo Settings")]
    [SerializeField] private int maxComboCount = 3;
    [SerializeField] private float comboWindow = 1.5f;
    [SerializeField] private float comboDecayRate = 1f; // How fast combo decays when not attacking

    [Header("Combo Bonuses")]
    [SerializeField] private bool enableComboBonuses = true;
    [SerializeField] private float comboDamageBonus = 0.15f; // 15% more damage per combo level
    [SerializeField] private float comboSpeedBonus = 0.1f; // 10% faster attacks per combo level
    [SerializeField] private float comboStaminaReduction = 0.05f; // 5% less stamina per combo level

    [Header("Visual Feedback")]
    [SerializeField] private bool showComboCounter = true;
    //[SerializeField] private float comboDisplayDuration = 2f;

    [Header("Animation Parameters")]
    [SerializeField] private string comboCountParam = "ComboCount";
    [SerializeField] private string comboMultiplierParam = "ComboMultiplier";

    [Header("Debug")]
    [SerializeField] private bool debugCombos = false;

    // Parent reference
    private MeleeModule parentMelee;
    private ThirdPersonController controller;
    private WeaponModule weaponModule;

    // Combo state
    private int currentComboCount = 0;
    private float lastAttackTime = 0f;
    private float comboStartTime = 0f;
    private bool isInCombo = false;

    // Combo bonuses (cached for performance)
    private float currentDamageMultiplier = 1f;
    private float currentSpeedMultiplier = 1f;
    private float currentStaminaMultiplier = 1f;
    private bool bonusesNeedUpdate = true;

    // Events
    public System.Action<int> OnComboHit; // Passes current combo count
    public System.Action<int> OnComboIncreased; // When combo count goes up
    public System.Action OnComboReset; // When combo is reset
    public System.Action<int> OnComboFinisher; // When max combo is reached
    public System.Action<float> OnComboDecay; // Passes decay progress (0-1)

    // Properties
    public bool IsEnabled { get; set; } = true;
    public int CurrentComboCount => currentComboCount;
    public int MaxComboCount => GetMaxComboCount();
    public bool IsInCombo => isInCombo;
    public float ComboProgress => currentComboCount / (float)GetMaxComboCount();
    public float TimeSinceLastAttack => Time.time - lastAttackTime;
    public float ComboTimeRemaining => Mathf.Max(0f, comboWindow - TimeSinceLastAttack);

    // Bonus getters (cached)
    public float DamageMultiplier
    {
        get
        {
            if (bonusesNeedUpdate) UpdateBonuses();
            return currentDamageMultiplier;
        }
    }

    public float SpeedMultiplier
    {
        get
        {
            if (bonusesNeedUpdate) UpdateBonuses();
            return currentSpeedMultiplier;
        }
    }

    public float StaminaMultiplier
    {
        get
        {
            if (bonusesNeedUpdate) UpdateBonuses();
            return currentStaminaMultiplier;
        }
    }

    public void Initialize(MeleeModule parentMelee)
    {
        this.parentMelee = parentMelee;
        controller = parentMelee.Controller;
        weaponModule = parentMelee.WeaponModule;

        // Subscribe to weapon changes for max combo updates
        if (weaponModule != null)
        {
            weaponModule.OnWeaponChanged += OnWeaponChanged;
        }

        UpdateBonuses();

        if (debugCombos)
            Debug.Log("✅ ComboModule initialized");
    }

    public void UpdateSubModule()
    {
        UpdateComboTiming();
        UpdateComboDecay();
    }

    public void Cleanup()
    {
        if (weaponModule != null)
        {
            weaponModule.OnWeaponChanged -= OnWeaponChanged;
        }
    }

    void UpdateComboTiming()
    {
        // Check if combo window has expired
        if (isInCombo && TimeSinceLastAttack > comboWindow)
        {
            if (debugCombos)
                Debug.Log($"Combo: Window expired ({TimeSinceLastAttack:F2}s > {comboWindow}s)");

            ResetCombo();
        }
    }

    void UpdateComboDecay()
    {
        if (!isInCombo || comboDecayRate <= 0f) return;

        // Optional: Gradually reduce combo over time instead of hard reset
        float decayProgress = TimeSinceLastAttack / comboWindow;
        OnComboDecay?.Invoke(decayProgress);
    }

    #region Combo Management

    public void IncrementCombo()
    {
        int previousCombo = currentComboCount;
        currentComboCount = Mathf.Clamp(currentComboCount + 1, 1, GetMaxComboCount());
        lastAttackTime = Time.time;

        if (!isInCombo)
        {
            isInCombo = true;
            comboStartTime = Time.time;
        }

        bonusesNeedUpdate = true;
        UpdateAnimationParameters();

        // Fire events
        OnComboHit?.Invoke(currentComboCount);

        if (currentComboCount > previousCombo)
        {
            OnComboIncreased?.Invoke(currentComboCount);
        }

        // Check for combo finisher
        if (currentComboCount >= GetMaxComboCount())
        {
            OnComboFinisher?.Invoke(currentComboCount);

            if (debugCombos)
                Debug.Log($"Combo: Finisher reached! {currentComboCount}/{GetMaxComboCount()}");
        }

        if (debugCombos)
            Debug.Log($"Combo: Hit {currentComboCount}/{GetMaxComboCount()}");
    }

    public void ResetCombo()
    {
        if (currentComboCount == 0) return; // Already reset

        int previousCombo = currentComboCount;
        currentComboCount = 0;
        isInCombo = false;
        bonusesNeedUpdate = true;

        UpdateAnimationParameters();
        OnComboReset?.Invoke();

        if (debugCombos)
            Debug.Log($"Combo: Reset from {previousCombo}");
    }

    public void ExtendComboWindow(float additionalTime)
    {
        lastAttackTime += additionalTime;

        if (debugCombos)
            Debug.Log($"Combo: Extended window by {additionalTime}s");
    }

    #endregion

    #region Bonus Calculation

    void UpdateBonuses()
    {
        if (!enableComboBonuses || currentComboCount <= 1)
        {
            currentDamageMultiplier = 1f;
            currentSpeedMultiplier = 1f;
            currentStaminaMultiplier = 1f;
        }
        else
        {
            float comboBonus = currentComboCount - 1; // Start bonuses from combo 2

            currentDamageMultiplier = 1f + (comboBonus * comboDamageBonus);
            currentSpeedMultiplier = 1f + (comboBonus * comboSpeedBonus);
            currentStaminaMultiplier = 1f - (comboBonus * comboStaminaReduction);
            currentStaminaMultiplier = Mathf.Max(0.1f, currentStaminaMultiplier); // Don't go below 10%
        }

        bonusesNeedUpdate = false;

        if (debugCombos && currentComboCount > 1)
        {
            Debug.Log($"Combo: Bonuses updated - Damage: {currentDamageMultiplier:F2}x, Speed: {currentSpeedMultiplier:F2}x, Stamina: {currentStaminaMultiplier:F2}x");
        }
    }

    #endregion

    #region Weapon Integration

    void OnWeaponChanged(WeaponData newWeapon)
    {
        // Reset combo when weapon changes
        if (isInCombo)
        {
            ResetCombo();
        }

        // Update max combo based on weapon
        if (newWeapon != null)
        {
            maxComboCount = newWeapon.maxComboCount;

            if (debugCombos)
                Debug.Log($"Combo: Max combo updated to {maxComboCount} for weapon {newWeapon.weaponName}");
        }
    }

    int GetMaxComboCount()
    {
        // Use weapon's max combo if available, otherwise use default
        if (weaponModule?.CurrentWeapon != null)
        {
            return weaponModule.CurrentWeapon.maxComboCount;
        }
        return maxComboCount;
    }

    #endregion

    #region Animation Integration

    void UpdateAnimationParameters()
    {
        if (controller?.Animator == null) return;

        controller.SetAnimationInt(comboCountParam, currentComboCount);
        controller.SetAnimationFloat(comboMultiplierParam, DamageMultiplier);
    }

    #endregion

    #region Special Combo Effects

    public bool IsComboFinisher()
    {
        return currentComboCount >= GetMaxComboCount();
    }

    public bool CanExtendCombo()
    {
        return isInCombo && currentComboCount < GetMaxComboCount();
    }

    public float GetComboEfficiency()
    {
        if (!isInCombo) return 0f;

        float comboDuration = Time.time - comboStartTime;
        return currentComboCount / comboDuration; // Combos per second
    }

    // For special moves that require specific combo counts
    public bool HasComboCount(int requiredCount)
    {
        return currentComboCount >= requiredCount;
    }

    // For systems that want to reward consistent comboing
    public bool IsComboActive(float minDuration = 1f)
    {
        return isInCombo && (Time.time - comboStartTime) >= minDuration;
    }

    #endregion

    #region Public API

    public void ForceSetCombo(int comboCount)
    {
        currentComboCount = Mathf.Clamp(comboCount, 0, GetMaxComboCount());
        lastAttackTime = Time.time;
        isInCombo = currentComboCount > 0;
        bonusesNeedUpdate = true;
        UpdateAnimationParameters();

        if (debugCombos)
            Debug.Log($"Combo: Force set to {currentComboCount}");
    }

    public void AddComboBonusTime(float bonusTime)
    {
        ExtendComboWindow(bonusTime);
    }

    // For power-ups or special effects that modify combo behavior
    public void SetComboMultipliers(float damageMultiplier, float speedMultiplier, float staminaMultiplier)
    {
        comboDamageBonus = damageMultiplier;
        comboSpeedBonus = speedMultiplier;
        comboStaminaReduction = 1f - staminaMultiplier; // Convert to reduction
        bonusesNeedUpdate = true;
    }

    #endregion

    #region Debug

    public void DrawMeleeGizmos()
    {
        if (!debugCombos || !isInCombo) return;

        Vector3 playerPos = transform.position;

        // Show combo count with stacked cubes
        Gizmos.color = Color.yellow;
        for (int i = 0; i < currentComboCount; i++)
        {
            Vector3 cubePos = playerPos + Vector3.up * (2f + i * 0.3f) + Vector3.right * 0.5f;
            Gizmos.DrawWireCube(cubePos, Vector3.one * 0.1f);
        }

        // Show combo window remaining time as a shrinking circle
        float timeRatio = ComboTimeRemaining / comboWindow;
        if (timeRatio > 0f)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, timeRatio);
            Gizmos.DrawWireSphere(playerPos + Vector3.up * 1.5f + Vector3.left * 0.5f, 0.3f * timeRatio);
        }
    }

    void OnGUI()
    {
        if (!debugCombos || !showComboCounter) return;

        if (isInCombo)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 200, 50, 180, 100));
            GUILayout.Label($"COMBO: {currentComboCount}/{GetMaxComboCount()}");
            GUILayout.Label($"Time Left: {ComboTimeRemaining:F1}s");
            GUILayout.Label($"Damage: {DamageMultiplier:F2}x");
            GUILayout.Label($"Speed: {SpeedMultiplier:F2}x");
            GUILayout.Label($"Stamina: {StaminaMultiplier:F2}x");
            GUILayout.EndArea();
        }
    }

    void OnDestroy()
    {
        Cleanup();
    }

    #endregion
}