// AttackModule - Enhanced Version with Full NPC Animation Support
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using CrabThirdPerson.Character;

/// <summary>
/// Handles light and heavy attacks with combo support
/// Works for both Players (with ThirdPersonController) and NPCs (standalone)
/// 
/// IMPROVEMENTS:
/// - Better NPC animator handling with fallback search
/// - Cached animator reference for performance
/// - Improved animation parameter management for NPCs
/// - Clear separation of player/NPC logic paths
/// </summary>
public class AttackModule : MonoBehaviour, IMeleeSubModule
{
    [Header("Attack Settings")]
    [SerializeField] private float attackDuration = 0.8f;
    [SerializeField] private float heavyAttackDuration = 1.5f;
    [SerializeField] private float heavyAttackChargeTime = 0.3f;

    [Header("Stamina Costs")]
    [SerializeField] private float attackStaminaCost = 15f;
    [SerializeField] private float heavyAttackStaminaCost = 30f;

    [Header("Damage Settings")]
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private float heavyAttackDamageMultiplier = 1.5f;
    [SerializeField] private float criticalHitMultiplier = 2f;
    [SerializeField] private float comboDamageScaling = 0.15f;

    [Header("Animation Parameters")]
    [SerializeField] private string attackParam = "Attack";
    [SerializeField] private string heavyAttackParam = "HeavyAttack";
    [SerializeField] private string comboCountParam = "ComboCount";

    [Header("Input Buffer Settings")]
    [SerializeField] private float inputBufferDuration = 0.5f;
    [SerializeField] private int maxBufferedInputs = 2;

    [Header("Debug")]
    [SerializeField] private bool debugAttacks = false;

    // Parent reference
    private MeleeModule parentMelee;
    private ThirdPersonController controller; // NULL for NPCs
    private ComboModule comboModule;
    private WeaponModule weaponModule;
    private Animator animator; // Cached animator reference (for NPCs)

    // Attack state
    private bool isAttacking;
    private bool isChargingHeavyAttack;
    private bool canCombo; // Set by animation events
    private float heavyAttackChargeTimer;
    private Coroutine currentAttackCoroutine;

    // Simple input buffer
    private System.Collections.Generic.Queue<float> inputBuffer = new System.Collections.Generic.Queue<float>();
    private bool hasQueuedAttack;

    // Weapon stat overrides
    private float weaponDamageMultiplier = 1f;
    private float weaponSpeedMultiplier = 1f;
    private float weaponReach = 1.5f;

    // Events
    public System.Action OnAttackBegin;
    public System.Action OnAttackComplete;
    public System.Action OnCancelWindowOpened;
    public System.Action OnCancelWindowClosed;
    public System.Action OnHeavyAttackChargeStart;
    public System.Action OnHeavyAttackChargeEnd;
    public System.Action<float> OnDamageDealt;
    public System.Action<bool, int> OnAttackPerformed;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public bool IsAttacking => isAttacking || isChargingHeavyAttack;
    public bool CanCombo => canCombo;
    public bool IsChargingHeavyAttack => isChargingHeavyAttack;
    public float HeavyAttackChargeProgress => Mathf.Clamp01(heavyAttackChargeTimer / heavyAttackChargeTime);
    public bool HasQueuedAttack => hasQueuedAttack;

    public void Initialize(MeleeModule parentMelee)
    {
        this.parentMelee = parentMelee;
        controller = parentMelee.Controller; // Can be null for NPCs
        comboModule = parentMelee.Combo;
        weaponModule = parentMelee.WeaponModule;

        // Cache animator reference for NPCs
        if (controller == null)
        {
            CacheAnimatorReference();
        }

        if (weaponModule != null)
        {
            weaponModule.OnWeaponChanged += OnWeaponChanged;
            if (weaponModule.CurrentWeapon != null)
            {
                UpdateWeaponStats(weaponModule.CurrentWeapon);
            }
        }
    }

    /// <summary>
    /// Cache animator reference for NPCs (performance optimization)
    /// Priority: Brain→ModelModule (correct) -> ComponentsInParent (fallback)
    /// </summary>
    void CacheAnimatorReference()
    {
        // PRIORITY 1: Use Brain → ModelModule (most reliable for modular architecture)
        if (parentMelee != null && parentMelee.Brain != null)
        {
            var modelModule = parentMelee.Brain.GetModule<ModelModule>();
            if (modelModule != null)
            {
                animator = modelModule.ModelAnimator;

                if (animator != null)
                {
                    if (debugAttacks)
                        Debug.Log($"[AttackModule] Found Animator via ModelModule: {animator.gameObject.name}");
                    return;
                }
            }
        }

        // FALLBACK 1: Try self (unlikely but check anyway)
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            if (debugAttacks)
                Debug.Log($"[AttackModule] Found Animator on self: {animator.gameObject.name}");
            return;
        }

        // FALLBACK 2: Try immediate parent
        if (transform.parent != null)
        {
            animator = transform.parent.GetComponent<Animator>();
            if (animator != null)
            {
                if (debugAttacks)
                    Debug.Log($"[AttackModule] Found Animator on parent: {animator.gameObject.name}");
                return;
            }
        }

        // FALLBACK 3: Search up hierarchy (last resort)
        animator = GetComponentInParent<Animator>();
        if (animator != null)
        {
            if (debugAttacks)
                Debug.Log($"[AttackModule] Found Animator in parent hierarchy: {animator.gameObject.name}");
            return;
        }

        // Failed to find animator
        Debug.LogWarning($"[AttackModule] No Animator found for NPC on {gameObject.name}. Animation triggers will not work!");
    }

    public void UpdateSubModule()
    {
        UpdateChargeTiming();
        ProcessInputBuffer();
    }

    void UpdateChargeTiming()
    {
        if (isChargingHeavyAttack)
        {
            heavyAttackChargeTimer += Time.deltaTime;
        }
    }

    void ProcessInputBuffer()
    {
        // Clean expired inputs
        while (inputBuffer.Count > 0 && Time.time - inputBuffer.Peek() > inputBufferDuration)
        {
            inputBuffer.Dequeue();
        }

        // Process queued attacks when combo window is open
        if (canCombo && hasQueuedAttack && inputBuffer.Count > 0)
        {
            inputBuffer.Dequeue();
            hasQueuedAttack = false;

            if (CanAttack())
            {
                StartLightAttack();
            }
        }
    }

    #region Input Handlers

    public void OnAttackStarted(InputAction.CallbackContext context)
    {
        // Handle heavy attack charging
        if (!isAttacking && CanStartAttackCharge())
        {
            isChargingHeavyAttack = true;
            heavyAttackChargeTimer = 0f;
            OnHeavyAttackChargeStart?.Invoke();
            return;
        }

        // Immediate attack if not attacking
        if (!isAttacking && CanAttack())
        {
            StartLightAttack();
            return;
        }

        // Buffer input for combo continuation
        if (inputBuffer.Count < maxBufferedInputs)
        {
            inputBuffer.Enqueue(Time.time);
            hasQueuedAttack = true;
        }
    }

    public void OnAttackCanceled(InputAction.CallbackContext context)
    {
        if (isChargingHeavyAttack)
        {
            isChargingHeavyAttack = false;
            OnHeavyAttackChargeEnd?.Invoke();

            if (heavyAttackChargeTimer >= heavyAttackChargeTime && CanHeavyAttack())
            {
                StartHeavyAttack();
            }
            else if (CanAttack())
            {
                StartLightAttack();
            }
        }
    }

    #endregion

    #region Attack Logic

    public bool CanStartAttackCharge()
    {
        // NPCs don't charge attacks (AI controls this)
        if (controller == null)
            return false;

        return controller.CanAct() && !isAttacking;
    }

    public bool CanAttack()
    {
        // NPCs don't have stamina/controller - AI manages timing
        if (controller == null)
            return !isAttacking; // Simple check for NPCs

        // Player validation with stamina
        return controller.CurrentStamina >= GetAttackStaminaCost() &&
               (controller.CanAct() || canCombo);
    }

    public bool CanHeavyAttack()
    {
        // NPCs don't have stamina/controller - AI manages timing  
        if (controller == null)
            return !isAttacking; // Simple check for NPCs

        // Player validation with stamina
        return controller.CurrentStamina >= GetHeavyAttackStaminaCost() &&
               (controller.CanAct() || canCombo);
    }

    public void StartLightAttack()
    {
        // Only consume stamina if controller exists (player)
        if (controller != null)
        {
            if (!controller.ConsumeStamina(GetAttackStaminaCost()))
                return;
        }

        StartAttackSequence(false);
    }

    public void StartHeavyAttack()
    {
        // Only consume stamina if controller exists (player)
        if (controller != null)
        {
            if (!controller.ConsumeStamina(GetHeavyAttackStaminaCost()))
                return;
        }

        StartAttackSequence(true);
    }

    void StartAttackSequence(bool isHeavyAttack)
    {
        isAttacking = true;
        canCombo = false; // Will be enabled by animation event
        hasQueuedAttack = false;

        string animParam = isHeavyAttack ? heavyAttackParam : attackParam;

        // PLAYER PATH: Use ThirdPersonController
        if (controller != null)
        {
            controller.TriggerAnimation(animParam);
        }
        // NPC PATH: Use cached animator
        else if (animator != null)
        {
            animator.SetTrigger(animParam);

            if (debugAttacks)
                Debug.Log($"[AttackModule] NPC triggered animation: {animParam}");
        }
        else
        {
            Debug.LogWarning($"[AttackModule] Cannot trigger animation - no animator available!");
        }

        OnAttackBegin?.Invoke();
        OnAttackPerformed?.Invoke(isHeavyAttack, comboModule?.CurrentComboCount ?? 1);

        // Set duration as fallback (animation events should end attack)
        if (currentAttackCoroutine != null)
            StopCoroutine(currentAttackCoroutine);

        float duration = isHeavyAttack ? GetHeavyAttackDuration() : GetAttackDuration();
        currentAttackCoroutine = StartCoroutine(EndAttackAfterTime(duration));
    }

    IEnumerator EndAttackAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        EndAttack();
    }

    void EndAttack()
    {
        isAttacking = false;
        canCombo = false;
        currentAttackCoroutine = null;
        OnAttackComplete?.Invoke();
    }

    #endregion

    #region Animation Event Handlers

    /// <summary>
    /// Called by Animation Event to enable weapon collision
    /// </summary>
    public void Attack_On()
    {
        weaponModule?.EnableWeaponCollider();
        OnCancelWindowOpened?.Invoke();
    }

    /// <summary>
    /// Called by Animation Event to disable weapon collision
    /// </summary>
    public void Attack_Off()
    {
        weaponModule?.DisableWeaponCollider();
        OnCancelWindowClosed?.Invoke();
    }

    /// <summary>
    /// Called by Animation Event to increment combo and enable combo window
    /// </summary>
    public void Combo_Up()
    {
        // Increment combo counter
        comboModule?.IncrementCombo();

        // Enable combo window for next attack
        canCombo = true;

        // Update animator with new combo count
        int comboCount = comboModule?.CurrentComboCount ?? 1;

        // PLAYER PATH
        if (controller != null)
        {
            controller.SetAnimationInt(comboCountParam, comboCount);
        }
        // NPC PATH
        else if (animator != null)
        {
            animator.SetInteger(comboCountParam, comboCount);

            if (debugAttacks)
                Debug.Log($"[AttackModule] NPC combo count updated: {comboCount}");
        }
    }

    /// <summary>
    /// Optional: Called by animation event to close combo window
    /// </summary>
    public void Combo_Window_Close()
    {
        canCombo = false;
    }

    /// <summary>
    /// Optional: Called by animation event to end attack completely
    /// </summary>
    public void Attack_Complete()
    {
        EndAttack();
    }

    #endregion

    #region Weapon Integration

    void OnWeaponChanged(WeaponData newWeapon)
    {
        UpdateWeaponStats(newWeapon);
    }

    void UpdateWeaponStats(WeaponData weapon)
    {
        if (weapon == null) return;

        attackStaminaCost = weapon.lightAttackStamina;
        heavyAttackStaminaCost = weapon.heavyAttackStamina;
        weaponDamageMultiplier = weapon.damage / baseDamage;
        weaponSpeedMultiplier = weapon.attackSpeed;
        weaponReach = weapon.reach;
    }

    #endregion

    #region Damage Calculation

    public float CalculateDamage(bool isHeavyAttack = false, bool isCritical = false)
    {
        float damage = baseDamage * weaponDamageMultiplier;

        if (isHeavyAttack)
            damage *= heavyAttackDamageMultiplier;

        if (isCritical)
            damage *= criticalHitMultiplier;

        int comboCount = comboModule?.CurrentComboCount ?? 1;
        if (comboCount > 1)
            damage *= 1f + (comboCount - 1) * comboDamageScaling;

        return damage;
    }

    public void TriggerDamageDealt(float damage)
    {
        OnDamageDealt?.Invoke(damage);
    }

    #endregion

    #region Public API

    public bool CanAllowMovementAction()
    {
        return !isAttacking || canCombo;
    }

    public void ForceEndCurrentAction()
    {
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        EndAttack();

        if (isChargingHeavyAttack)
        {
            isChargingHeavyAttack = false;
            OnHeavyAttackChargeEnd?.Invoke();
        }

        inputBuffer.Clear();
        hasQueuedAttack = false;
    }

    public float GetWeaponReach() => weaponReach;
    public void ClearInputBuffer() => inputBuffer.Clear();
    public int GetBufferedInputCount() => inputBuffer.Count;

    #endregion

    #region Stat Getters

    float GetAttackStaminaCost() => attackStaminaCost;
    float GetHeavyAttackStaminaCost() => heavyAttackStaminaCost;
    float GetAttackDuration() => attackDuration / weaponSpeedMultiplier;
    float GetHeavyAttackDuration() => heavyAttackDuration / weaponSpeedMultiplier;

    #endregion

    #region Debug

    public void DrawMeleeGizmos()
    {
        if (!debugAttacks) return;

        Vector3 playerPos = transform.position;

        // Weapon reach
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerPos + Vector3.up, weaponReach);

        // Heavy attack charge
        if (isChargingHeavyAttack)
        {
            float chargeProgress = HeavyAttackChargeProgress;
            Gizmos.color = Color.Lerp(Color.white, Color.red, chargeProgress);
            Gizmos.DrawWireSphere(playerPos + Vector3.up * 3f, 0.5f + (0.5f * chargeProgress));
        }

        // Combo window active
        if (canCombo)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerPos + Vector3.up * 2.5f, 0.3f);
        }

        // Buffered input
        if (hasQueuedAttack)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(playerPos + Vector3.up * 2f, Vector3.one * 0.3f);
        }
    }

    void OnGUI()
    {
        if (!debugAttacks) return;

        GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 200, 150));
        GUILayout.Label("=== ATTACK DEBUG ===");
        GUILayout.Label($"Is Attacking: {isAttacking}");
        GUILayout.Label($"Can Combo: {canCombo}");
        GUILayout.Label($"Is Charging: {isChargingHeavyAttack}");
        GUILayout.Label($"Combo Count: {comboModule?.CurrentComboCount ?? 0}");
        GUILayout.Label($"Buffer Count: {inputBuffer.Count}");
        GUILayout.Label($"Has Queued: {hasQueuedAttack}");
        GUILayout.Label($"Animator: {(animator != null ? animator.gameObject.name : "NULL")}");
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (weaponModule != null)
        {
            weaponModule.OnWeaponChanged -= OnWeaponChanged;
        }
    }

    #endregion
}