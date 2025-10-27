// Core Third Person UI System - Adapts to available modules
using UnityEngine;
using UnityEngine.UI;

public class ThirdPersonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThirdPersonController playerController;
    [SerializeField] private SimpleThirdPersonCamera cameraController;

    [Header("Stamina UI")]
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Color staminaColor = Color.yellow;
    [SerializeField] private Color lowStaminaColor = Color.red;
    [SerializeField] private float lowStaminaThreshold = 25f;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float hideDelay = 2f;

    [Header("Target Lock Indicator")]
    [SerializeField] private GameObject lockOnReticle;
    [SerializeField] private RectTransform lockOnTransform;
    [SerializeField] private Image lockOnImage;
    [SerializeField] private Color lockOnColor = Color.white;
    [SerializeField] private float lockOnPulseSpeed = 2f;

    [Header("Health UI (For Combat Package)")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Image healthFill;
    [SerializeField] private Color healthColor = Color.red;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private float maxHealth = 100f;

    [Header("Status Effects (For Future Packages)")]
    [SerializeField] private Transform statusEffectsParent;
    [SerializeField] private GameObject statusEffectPrefab;

    [Header("Module Detection")]
    [SerializeField] private bool autoDetectModules = true;
    //[SerializeField] private bool debugModuleDetection = false;

    // Private variables
    private float lastStaminaChangeTime;
    private float staminaHideTimer;
    private Camera playerCamera;
    private CanvasGroup staminaCanvasGroup;
    private CanvasGroup lockOnCanvasGroup;
    private CanvasGroup healthCanvasGroup;

    // Module references (found automatically)
    private ControllerBrain brain;
    private TargetLockModule targetLockModule;
    private AnimationStateModule animationStateModule;


    // Module availability flags
    private bool hasTargetLock;
    private bool hasAnimationState;
    private bool hasHotbar;
    private bool hasHealthSystem; // For future combat/damage packages

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Auto-find controller if not assigned - updated for new hierarchy
        if (playerController == null)
        {
            // First try to find by tag
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // Look for Component_Brain -> Component_Controller -> ThirdPersonController
                var brainTransform = player.transform.Find("Component_Brain");
                if (brainTransform != null)
                {
                    brain = brainTransform.GetComponent<ControllerBrain>();
                    if (brain != null)
                    {
                        playerController = brain.Controller;
                    }
                }

                // Fallback: search in all children
                if (playerController == null)
                {
                    playerController = player.GetComponentInChildren<ThirdPersonController>();
                }
            }

            // Ultimate fallback: search entire scene
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<ThirdPersonController>();
            }
        }

        // Find SimpleThirdPersonCamera
        if (cameraController == null)
        {
            // Look for Component_Camera -> SimpleThirdPersonCamera
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var cameraComponent = player.transform.Find("Component_Camera");
                if (cameraComponent != null)
                {
                    cameraController = cameraComponent.GetComponentInChildren<SimpleThirdPersonCamera>();
                }
            }

            // Fallback: search entire scene
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<SimpleThirdPersonCamera>();
            }
        }

        playerCamera = Camera.main;

        // Detect available modules
        if (autoDetectModules)
        {
            DetectModules();
        }

        // Setup UI based on available modules
        SetupUIElements();

        // Initialize UI elements
        UpdateStaminaBar();
        if (hasHealthSystem) UpdateHealthBar();
        if (hasTargetLock) UpdateLockOnIndicator();
    }

    void DetectModules()
    {
        if (playerController == null) return;

        // Get Brain reference
        if (brain == null)
        {
            brain = playerController.GetBrain();
        }

        if (brain != null)
        {
            // Get modules from Brain
            targetLockModule = brain.TargetLock;
            animationStateModule = brain.AnimationState;
        }
        else
        {
            // Fallback: search in scene
            targetLockModule = FindFirstObjectByType<TargetLockModule>();
            animationStateModule = FindFirstObjectByType<AnimationStateModule>();
        }

        // Set availability flags
        hasTargetLock = targetLockModule != null;
        hasAnimationState = animationStateModule != null;

        // Check for health system (combat/damage modules)
        // This will be expanded when we add those packages
        hasHealthSystem = false; // Default to false for core package
    }

    void SetupUIElements()
    {
        // Setup stamina UI (always available in core)
        if (staminaBar != null)
        {
            staminaCanvasGroup = staminaBar.GetComponent<CanvasGroup>();
            if (staminaCanvasGroup == null)
            {
                staminaCanvasGroup = staminaBar.gameObject.AddComponent<CanvasGroup>();
            }
            staminaBar.gameObject.SetActive(true);
        }
        else
        {
           
        }

        // Setup target lock UI (only if target lock module exists)
        if (lockOnReticle != null)
        {
            lockOnCanvasGroup = lockOnReticle.GetComponent<CanvasGroup>();
            if (lockOnCanvasGroup == null)
            {
                lockOnCanvasGroup = lockOnReticle.AddComponent<CanvasGroup>();
            }
            lockOnReticle.SetActive(hasTargetLock);
        }
        else if (hasTargetLock)
        {
          
        }

        // Setup health UI (only if health system exists)
        if (healthBar != null)
        {
            healthCanvasGroup = healthBar.GetComponent<CanvasGroup>();
            if (healthCanvasGroup == null)
            {
                healthCanvasGroup = healthBar.gameObject.AddComponent<CanvasGroup>();
            }
            healthBar.gameObject.SetActive(hasHealthSystem);
        }

        // Hide status effects if no modules support them
        if (statusEffectsParent != null)
        {
            statusEffectsParent.gameObject.SetActive(hasHealthSystem || hasHotbar);
        }

        // Check canvas setup
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
          
        }
    }

    void Update()
    {
        // Always update stamina (core feature)
        UpdateStaminaBar();
        HandleStaminaVisibility();

        // Only update module-specific UI if modules exist
        if (hasTargetLock)
        {
            UpdateLockOnIndicator();
        }

        if (hasHealthSystem)
        {
            UpdateHealthBar();
        }
    }

    void UpdateStaminaBar()
    {
        if (staminaBar == null || playerController == null) return;

        float currentStamina = playerController.CurrentStamina;
        float maxStamina = playerController.MaxStamina;
        float staminaPercent = currentStamina / maxStamina;

        // Update slider value
        staminaBar.value = staminaPercent;

        // Update color based on stamina level
        if (staminaFill != null)
        {
            Color targetColor = staminaPercent <= (lowStaminaThreshold / 100f) ? lowStaminaColor : staminaColor;
            staminaFill.color = targetColor;
        }

        // Track stamina changes for visibility
        if (Mathf.Abs(staminaBar.value - staminaPercent) > 0.01f)
        {
            lastStaminaChangeTime = Time.time;
        }
    }

    void UpdateHealthBar()
    {
        if (healthBar == null) return;

        float healthPercent = currentHealth / maxHealth;
        healthBar.value = healthPercent;

        if (healthFill != null)
        {
            healthFill.color = healthColor;
        }
    }

    void UpdateLockOnIndicator()
    {
        if (lockOnReticle == null || targetLockModule == null || playerCamera == null) return;

        bool isLockedOn = targetLockModule.IsLockedOn;
        Transform target = targetLockModule.LockedTarget;

        if (isLockedOn && target != null)
        {
            // Show and position the lock-on reticle
            if (!lockOnReticle.activeInHierarchy)
            {
                lockOnReticle.SetActive(true);
            }

            // Convert world position to screen position
            Vector3 targetPosition = target.position + Vector3.up * 1f; // Offset up a bit
            Vector3 screenPos = playerCamera.WorldToScreenPoint(targetPosition);

            // Check if target is in front of camera
            if (screenPos.z > 0)
            {
                // Update reticle position
                if (lockOnTransform != null)
                {
                    lockOnTransform.position = screenPos;
                }

                // Pulse effect
                if (lockOnImage != null)
                {
                    float pulse = Mathf.Sin(Time.time * lockOnPulseSpeed) * 0.1f + 0.9f;
                    lockOnImage.color = new Color(lockOnColor.r, lockOnColor.g, lockOnColor.b, pulse);

                    // Scale pulse
                    float scale = 1f + Mathf.Sin(Time.time * lockOnPulseSpeed * 0.5f) * 0.1f;
                    lockOnTransform.localScale = Vector3.one * scale;
                }

                // Fade in
                if (lockOnCanvasGroup != null)
                {
                    lockOnCanvasGroup.alpha = Mathf.Lerp(lockOnCanvasGroup.alpha, 1f, 5f * Time.deltaTime);
                }
            }
            else
            {
                // Target behind camera, fade out
                if (lockOnCanvasGroup != null)
                {
                    lockOnCanvasGroup.alpha = Mathf.Lerp(lockOnCanvasGroup.alpha, 0f, 5f * Time.deltaTime);
                }
            }
        }
        else
        {
            // Hide lock-on reticle
            if (lockOnReticle.activeInHierarchy)
            {
                if (lockOnCanvasGroup != null)
                {
                    lockOnCanvasGroup.alpha = Mathf.Lerp(lockOnCanvasGroup.alpha, 0f, 5f * Time.deltaTime);

                    if (lockOnCanvasGroup.alpha < 0.01f)
                    {
                        lockOnReticle.SetActive(false);
                    }
                }
                else
                {
                    lockOnReticle.SetActive(false);
                }
            }
        }
    }

    void HandleStaminaVisibility()
    {
        if (!hideWhenFull || staminaCanvasGroup == null || playerController == null) return;

        float currentStamina = playerController.CurrentStamina;
        float maxStamina = playerController.MaxStamina;
        bool isFullStamina = Mathf.Approximately(currentStamina, maxStamina);

        // Check for stamina consumption (core features only)
        bool isConsumingStamina = playerController.IsSprinting;

        // Show if stamina is not full or if actively using stamina
        if (!isFullStamina || isConsumingStamina)
        {
            staminaHideTimer = 0f;
            staminaCanvasGroup.alpha = Mathf.Lerp(staminaCanvasGroup.alpha, 1f, 5f * Time.deltaTime);
        }
        else
        {
            // Start hide timer when stamina is full and not being used
            staminaHideTimer += Time.deltaTime;

            if (staminaHideTimer >= hideDelay)
            {
                staminaCanvasGroup.alpha = Mathf.Lerp(staminaCanvasGroup.alpha, 0f, 2f * Time.deltaTime);
            }
        }
    }

    #region Public API (Health Management - For Future Combat Package)

    public void SetHealth(float health)
    {
        currentHealth = Mathf.Clamp(health, 0, maxHealth);
    }

    public void SetMaxHealth(float maxHp)
    {
        maxHealth = maxHp;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void DamageHealth(float damage)
    {
        SetHealth(currentHealth - damage);
    }

    public void HealHealth(float heal)
    {
        SetHealth(currentHealth + heal);
    }

    public void ShowStatusEffect(string effectName, Sprite icon, float duration)
    {
        if (statusEffectsParent == null || statusEffectPrefab == null) return;

        GameObject statusEffect = Instantiate(statusEffectPrefab, statusEffectsParent);
        StatusEffectUI statusUI = statusEffect.GetComponent<StatusEffectUI>();

        if (statusUI != null)
        {
            statusUI.Initialize(effectName, icon, duration);
        }
    }

    public void ForceShowStamina(float duration = 3f)
    {
        lastStaminaChangeTime = Time.time;
        staminaHideTimer = -duration;
    }

    #endregion

    #region Module Integration Methods (For Future Packages)

    // Method for Combat Package to enable health UI
    public void EnableHealthSystem(bool enable = true)
    {
        hasHealthSystem = enable;
        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(enable);
        }
    }

    // Method to refresh module detection (for runtime changes)
    public void RefreshModules()
    {
        DetectModules();
        SetupUIElements();
    }

    #endregion

    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercent => currentHealth / maxHealth;
    public bool HasTargetLock => hasTargetLock;
    public bool HasHealthSystem => hasHealthSystem;
    public bool HasHotbar => hasHotbar;
}

// Status Effect UI Component (For Future Packages)
[System.Serializable]
public class StatusEffectUI : MonoBehaviour
{
    [Header("UI References")]
    public Image iconImage;
    public Image durationFill;
    public Text nameText;

    private float duration;
    private float remainingTime;
    private string effectName;

    public void Initialize(string name, Sprite icon, float dur)
    {
        effectName = name;
        duration = dur;
        remainingTime = duration;

        if (iconImage != null) iconImage.sprite = icon;
        if (nameText != null) nameText.text = name;
        if (durationFill != null) durationFill.fillAmount = 1f;
    }

    void Update()
    {
        remainingTime -= Time.deltaTime;

        if (durationFill != null)
        {
            durationFill.fillAmount = remainingTime / duration;
        }

        if (remainingTime <= 0)
        {
            Destroy(gameObject);
        }
    }
}