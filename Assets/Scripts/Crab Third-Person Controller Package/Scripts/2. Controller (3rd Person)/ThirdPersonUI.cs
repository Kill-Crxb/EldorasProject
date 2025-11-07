// Core Third Person UI System - Fully integrated with RPG Resources
using UnityEngine;
using UnityEngine.UI;

public class ThirdPersonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThirdPersonController playerController;
    [SerializeField] private SimpleThirdPersonCamera cameraController;

    [Header("Health UI")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Image healthFill;
    [SerializeField] private Color healthColor = new Color(0.8f, 0.1f, 0.1f); // Dark red
    [SerializeField] private Color lowHealthColor = new Color(1f, 0f, 0f); // Bright red
    [SerializeField] private float lowHealthThreshold = 25f;
    [SerializeField] private bool healthAlwaysVisible = true;

    [Header("Stamina UI")]
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Image staminaFill;
    [SerializeField] private Color staminaColor = Color.yellow;
    [SerializeField] private Color lowStaminaColor = Color.red;
    [SerializeField] private float lowStaminaThreshold = 25f;
    [SerializeField] private bool hideStaminaWhenFull = true;
    [SerializeField] private float staminaHideDelay = 2f;

    [Header("Mana UI")]
    [SerializeField] private Slider manaBar;
    [SerializeField] private Image manaFill;
    [SerializeField] private Color manaColor = new Color(0.2f, 0.4f, 1f); // Blue
    [SerializeField] private Color lowManaColor = new Color(0.5f, 0f, 1f); // Purple
    [SerializeField] private float lowManaThreshold = 25f;
    [SerializeField] private bool hideManaWhenFull = true;
    [SerializeField] private float manaHideDelay = 2f;

    [Header("Target Lock Indicator")]
    [SerializeField] private GameObject lockOnReticle;
    [SerializeField] private RectTransform lockOnTransform;
    [SerializeField] private Image lockOnImage;
    [SerializeField] private Color lockOnColor = Color.white;
    [SerializeField] private float lockOnPulseSpeed = 2f;

    [Header("Status Effects (For Future Packages)")]
    [SerializeField] private Transform statusEffectsParent;
    [SerializeField] private GameObject statusEffectPrefab;

    [Header("Module Detection")]
    [SerializeField] private bool autoDetectModules = true;
    [SerializeField] private bool debugResourceUpdates = false;

    // Private variables
    private float lastStaminaChangeTime;
    private float staminaHideTimer;
    private float lastManaChangeTime;
    private float manaHideTimer;
    private Camera playerCamera;

    // Canvas groups for fade effects
    private CanvasGroup healthCanvasGroup;
    private CanvasGroup staminaCanvasGroup;
    private CanvasGroup manaCanvasGroup;
    private CanvasGroup lockOnCanvasGroup;

    // Module references (found automatically)
    private ControllerBrain brain;
    private RPGResources rpgResources;
    private TargetLockModule targetLockModule;
    private AnimationStateModule animationStateModule;

    // Module availability flags
    private bool hasTargetLock;
    private bool hasAnimationState;
    private bool hasRPGResources;
    private bool hasHotbar;

    void Start()
    {
        Initialize();
    }

    void Initialize()
    {
        // Auto-find controller if not assigned
        if (playerController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var brainTransform = player.transform.Find("Component_Brain");
                if (brainTransform != null)
                {
                    brain = brainTransform.GetComponent<ControllerBrain>();
                    if (brain != null)
                    {
                        playerController = brain.Controller;
                    }
                }

                if (playerController == null)
                {
                    playerController = player.GetComponentInChildren<ThirdPersonController>();
                }
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<ThirdPersonController>();
            }
        }

        // Find SimpleThirdPersonCamera
        if (cameraController == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var cameraComponent = player.transform.Find("Component_Camera");
                if (cameraComponent != null)
                {
                    cameraController = cameraComponent.GetComponentInChildren<SimpleThirdPersonCamera>();
                }
            }

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

        // Subscribe to RPG Resources events
        SubscribeToEvents();

        // Initialize UI elements
        if (hasRPGResources)
        {
            UpdateHealthBar();
            UpdateManaBar();
        }
        UpdateStaminaBar();
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
            rpgResources = brain.GetModule<RPGResources>();
        }
        else
        {
            // Fallback: search in scene
            targetLockModule = FindFirstObjectByType<TargetLockModule>();
            animationStateModule = FindFirstObjectByType<AnimationStateModule>();
            rpgResources = FindFirstObjectByType<RPGResources>();
        }

        // Set availability flags
        hasTargetLock = targetLockModule != null;
        hasAnimationState = animationStateModule != null;
        hasRPGResources = rpgResources != null;

        if (debugResourceUpdates)
        {
            Debug.Log($"[ThirdPersonUI] Module Detection: RPGResources={hasRPGResources}, TargetLock={hasTargetLock}");
        }
    }

    void SubscribeToEvents()
    {
        if (rpgResources != null)
        {
            rpgResources.OnHealthChanged += HandleHealthChanged;
            rpgResources.OnManaChanged += HandleManaChanged;
            rpgResources.OnStaminaChanged += HandleStaminaChanged;
            rpgResources.OnMaxValuesChanged += HandleMaxValuesChanged;

            if (debugResourceUpdates)
            {
                Debug.Log("[ThirdPersonUI] Successfully subscribed to RPGResources events");
            }
        }
    }

    void OnDestroy()
    {
        // Unsubscribe from events
        if (rpgResources != null)
        {
            rpgResources.OnHealthChanged -= HandleHealthChanged;
            rpgResources.OnManaChanged -= HandleManaChanged;
            rpgResources.OnStaminaChanged -= HandleStaminaChanged;
            rpgResources.OnMaxValuesChanged -= HandleMaxValuesChanged;
        }
    }

    void SetupUIElements()
    {
        // Setup health UI
        if (healthBar != null)
        {
            healthCanvasGroup = healthBar.GetComponent<CanvasGroup>();
            if (healthCanvasGroup == null)
            {
                healthCanvasGroup = healthBar.gameObject.AddComponent<CanvasGroup>();
            }
            healthBar.gameObject.SetActive(hasRPGResources);

            if (healthFill != null)
            {
                healthFill.color = healthColor;
            }
        }

        // Setup stamina UI (works with both RPGResources and ThirdPersonController)
        if (staminaBar != null)
        {
            staminaCanvasGroup = staminaBar.GetComponent<CanvasGroup>();
            if (staminaCanvasGroup == null)
            {
                staminaCanvasGroup = staminaBar.gameObject.AddComponent<CanvasGroup>();
            }
            staminaBar.gameObject.SetActive(true);

            if (staminaFill != null)
            {
                staminaFill.color = staminaColor;
            }
        }

        // Setup mana UI
        if (manaBar != null)
        {
            manaCanvasGroup = manaBar.GetComponent<CanvasGroup>();
            if (manaCanvasGroup == null)
            {
                manaCanvasGroup = manaBar.gameObject.AddComponent<CanvasGroup>();
            }
            manaBar.gameObject.SetActive(hasRPGResources);

            if (manaFill != null)
            {
                manaFill.color = manaColor;
            }
        }

        // Setup target lock UI
        if (lockOnReticle != null)
        {
            lockOnCanvasGroup = lockOnReticle.GetComponent<CanvasGroup>();
            if (lockOnCanvasGroup == null)
            {
                lockOnCanvasGroup = lockOnReticle.AddComponent<CanvasGroup>();
            }
            lockOnReticle.SetActive(hasTargetLock);
        }

        // Hide status effects if no modules support them
        if (statusEffectsParent != null)
        {
            statusEffectsParent.gameObject.SetActive(hasRPGResources || hasHotbar);
        }
    }

    void Update()
    {
        // Update stamina (works with or without RPGResources)
        UpdateStaminaBar();
        HandleStaminaVisibility();

        // Update RPG Resources UI if available
        if (hasRPGResources)
        {
            UpdateHealthBar();
            UpdateManaBar();
            HandleManaVisibility();
        }

        // Update target lock
        if (hasTargetLock)
        {
            UpdateLockOnIndicator();
        }
    }

    #region Event Handlers

    void HandleHealthChanged(float current, float max)
    {
        if (debugResourceUpdates)
        {
            Debug.Log($"[ThirdPersonUI] Health changed: {current}/{max}");
        }
        UpdateHealthBar();
    }

    void HandleManaChanged(float current, float max)
    {
        if (debugResourceUpdates)
        {
            Debug.Log($"[ThirdPersonUI] Mana changed: {current}/{max}");
        }
        lastManaChangeTime = Time.time;
        manaHideTimer = 0f;
        UpdateManaBar();
    }

    void HandleStaminaChanged(float current, float max)
    {
        if (debugResourceUpdates)
        {
            Debug.Log($"[ThirdPersonUI] Stamina changed: {current}/{max}");
        }
        lastStaminaChangeTime = Time.time;
        staminaHideTimer = 0f;
        UpdateStaminaBar();
    }

    void HandleMaxValuesChanged(float maxHealth, float maxMana, float maxStamina)
    {
        if (debugResourceUpdates)
        {
            Debug.Log($"[ThirdPersonUI] Max values changed: HP={maxHealth}, MP={maxMana}, SP={maxStamina}");
        }
        UpdateHealthBar();
        UpdateManaBar();
        UpdateStaminaBar();
    }

    #endregion

    #region Health Bar

    void UpdateHealthBar()
    {
        if (healthBar == null || !hasRPGResources || rpgResources == null) return;

        float current = rpgResources.CurrentHealth;
        float max = rpgResources.MaxHealth;
        float healthPercent = max > 0 ? current / max : 0f;

        // Update slider value
        healthBar.value = healthPercent;

        // Update color based on health level
        if (healthFill != null)
        {
            Color targetColor = healthPercent <= (lowHealthThreshold / 100f) ? lowHealthColor : healthColor;
            healthFill.color = targetColor;
        }

        // Handle visibility
        if (healthCanvasGroup != null && !healthAlwaysVisible)
        {
            healthCanvasGroup.alpha = healthPercent < 1f ? 1f : 0.5f;
        }
    }

    #endregion

    #region Stamina Bar

    void UpdateStaminaBar()
    {
        if (staminaBar == null) return;

        float current, max, staminaPercent;

        // Use RPGResources if available, otherwise fall back to ThirdPersonController
        if (hasRPGResources && rpgResources != null)
        {
            current = rpgResources.CurrentStamina;
            max = rpgResources.MaxStamina;
        }
        else if (playerController != null)
        {
            current = playerController.CurrentStamina;
            max = playerController.MaxStamina;
        }
        else
        {
            return;
        }

        staminaPercent = max > 0 ? current / max : 0f;

        // Update slider value
        staminaBar.value = staminaPercent;

        // Update color based on stamina level
        if (staminaFill != null)
        {
            Color targetColor = staminaPercent <= (lowStaminaThreshold / 100f) ? lowStaminaColor : staminaColor;
            staminaFill.color = targetColor;
        }
    }

    void HandleStaminaVisibility()
    {
        if (!hideStaminaWhenFull || staminaCanvasGroup == null) return;

        float current, max;
        bool isConsumingStamina = false;

        if (hasRPGResources && rpgResources != null)
        {
            current = rpgResources.CurrentStamina;
            max = rpgResources.MaxStamina;
        }
        else if (playerController != null)
        {
            current = playerController.CurrentStamina;
            max = playerController.MaxStamina;
            isConsumingStamina = playerController.IsSprinting;
        }
        else
        {
            return;
        }

        bool isFullStamina = Mathf.Approximately(current, max);

        // Show if stamina is not full or if actively using stamina
        if (!isFullStamina || isConsumingStamina)
        {
            staminaHideTimer = 0f;
            staminaCanvasGroup.alpha = Mathf.Lerp(staminaCanvasGroup.alpha, 1f, 5f * Time.deltaTime);
        }
        else
        {
            staminaHideTimer += Time.deltaTime;

            if (staminaHideTimer >= staminaHideDelay)
            {
                staminaCanvasGroup.alpha = Mathf.Lerp(staminaCanvasGroup.alpha, 0f, 2f * Time.deltaTime);
            }
        }
    }

    #endregion

    #region Mana Bar

    void UpdateManaBar()
    {
        if (manaBar == null || !hasRPGResources || rpgResources == null) return;

        float current = rpgResources.CurrentMana;
        float max = rpgResources.MaxMana;
        float manaPercent = max > 0 ? current / max : 0f;

        // Update slider value
        manaBar.value = manaPercent;

        // Update color based on mana level
        if (manaFill != null)
        {
            Color targetColor = manaPercent <= (lowManaThreshold / 100f) ? lowManaColor : manaColor;
            manaFill.color = targetColor;
        }
    }

    void HandleManaVisibility()
    {
        if (!hideManaWhenFull || manaCanvasGroup == null || !hasRPGResources || rpgResources == null) return;

        float current = rpgResources.CurrentMana;
        float max = rpgResources.MaxMana;
        bool isFullMana = Mathf.Approximately(current, max);

        // Show if mana is not full
        if (!isFullMana)
        {
            manaHideTimer = 0f;
            manaCanvasGroup.alpha = Mathf.Lerp(manaCanvasGroup.alpha, 1f, 5f * Time.deltaTime);
        }
        else
        {
            manaHideTimer += Time.deltaTime;

            if (manaHideTimer >= manaHideDelay)
            {
                manaCanvasGroup.alpha = Mathf.Lerp(manaCanvasGroup.alpha, 0f, 2f * Time.deltaTime);
            }
        }
    }

    #endregion

    #region Target Lock Indicator

    void UpdateLockOnIndicator()
    {
        if (lockOnReticle == null || targetLockModule == null || playerCamera == null) return;

        bool isLockedOn = targetLockModule.IsLockedOn;
        Transform target = targetLockModule.LockedTarget;

        if (isLockedOn && target != null)
        {
            if (!lockOnReticle.activeInHierarchy)
            {
                lockOnReticle.SetActive(true);
            }

            Vector3 targetPosition = target.position + Vector3.up * 1f;
            Vector3 screenPos = playerCamera.WorldToScreenPoint(targetPosition);

            if (screenPos.z > 0)
            {
                if (lockOnTransform != null)
                {
                    lockOnTransform.position = screenPos;
                }

                if (lockOnImage != null)
                {
                    float pulse = Mathf.Sin(Time.time * lockOnPulseSpeed) * 0.1f + 0.9f;
                    lockOnImage.color = new Color(lockOnColor.r, lockOnColor.g, lockOnColor.b, pulse);

                    float scale = 1f + Mathf.Sin(Time.time * lockOnPulseSpeed * 0.5f) * 0.1f;
                    lockOnTransform.localScale = Vector3.one * scale;
                }

                if (lockOnCanvasGroup != null)
                {
                    lockOnCanvasGroup.alpha = Mathf.Lerp(lockOnCanvasGroup.alpha, 1f, 5f * Time.deltaTime);
                }
            }
            else
            {
                if (lockOnCanvasGroup != null)
                {
                    lockOnCanvasGroup.alpha = Mathf.Lerp(lockOnCanvasGroup.alpha, 0f, 5f * Time.deltaTime);
                }
            }
        }
        else
        {
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

    #endregion

    #region Public API

    public void ForceShowStamina(float duration = 3f)
    {
        lastStaminaChangeTime = Time.time;
        staminaHideTimer = -duration;
    }

    public void ForceShowMana(float duration = 3f)
    {
        lastManaChangeTime = Time.time;
        manaHideTimer = -duration;
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

    public void RefreshModules()
    {
        DetectModules();
        SetupUIElements();
        SubscribeToEvents();
    }

    #endregion

    #region Properties

    public bool HasTargetLock => hasTargetLock;
    public bool HasRPGResources => hasRPGResources;
    public bool HasHotbar => hasHotbar;

    #endregion
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