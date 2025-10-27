// Weapon Module - Updated for Melee Refactoring
using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WeaponSocketConfig
{
    public WeaponType weaponType;
    public Transform socketTransform;
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = new Vector3(-90f, 90f, 90f);
    public Vector3 localScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Visual Feedback")]
    public bool showGizmos = false;
}

public class WeaponModule : MonoBehaviour, IPlayerModule, IInputHandler
{
    [Header("Weapon System")]

    [SerializeField] private WeaponData[] availableWeapons;
    [SerializeField] private int currentWeaponIndex = 0;

    [Header("Default Weapon Transform")]
    [SerializeField] private Vector3 defaultWeaponPosition = Vector3.zero;
    [SerializeField] private Vector3 defaultWeaponRotation = new Vector3(-90f, 90f, 90f);
    [SerializeField] private Vector3 defaultWeaponScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Socket Configuration")]
    [SerializeField] private WeaponSocketConfig[] socketConfigs;
    [SerializeField] private Transform defaultWeaponSocket;
    [SerializeField] private bool autoFindHandSocket = true;

    [Header("Hitbox Configuration")]
    [SerializeField] private bool enableWeaponHitboxes = true;

    [Header("Weapon Switching")]
    [SerializeField] private bool allowWeaponSwitching = true;
    [SerializeField] private float weaponSwitchTime = 0.5f;
    [SerializeField] private string weaponSwitchParam = "WeaponSwitch";

    [Header("Animation Overrides")]
    [SerializeField] private AnimatorOverrideController[] weaponAnimators;

    [Header("Debug")]
    [SerializeField] private bool debugWeapons = false;

    // Component references - UPDATED FOR MELEE
    private ControllerBrain brain;
    private ThirdPersonController controller;
    private MeleeModule meleeModule; // CHANGED from CombatModule
    private AnimationStateModule animationState;
    private PlayerInputControls inputControls;
    private bool isSubscribedToInput;

    // Current weapon state
    private WeaponData currentWeapon;
    private GameObject currentWeaponModel;
    private bool isSwitchingWeapon;
    private Transform currentWeaponSocket;

    // Events
    public System.Action<WeaponData> OnWeaponChanged;
    public System.Action<WeaponData> OnWeaponEquipped;
    public System.Action<WeaponData> OnWeaponUnequipped;
    public System.Action<Collider, float> OnWeaponHitTarget;
    public System.Action<Vector3> OnWeaponHitWorld;

    // Properties
    public bool IsEnabled { get; set; } = true;
    public WeaponData CurrentWeapon => currentWeapon;
    public bool IsSwitchingWeapon => isSwitchingWeapon;
    public bool HasWeapons => availableWeapons != null && availableWeapons.Length > 0;
    public Transform CurrentWeaponSocket => currentWeaponSocket;
    public SimpleWeaponHit CurrentWeaponHitbox { get; private set; }

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.Controller;
        meleeModule = brain.GetModule<MeleeModule>(); // CHANGED from CombatModule
        animationState = brain.AnimationState;

        if (autoFindHandSocket && defaultWeaponSocket == null)
        {
            defaultWeaponSocket = FindHandSocket();
        }

        if (HasWeapons)
        {
            EquipWeapon(currentWeaponIndex);
        }

   
    }

    public void UpdateModule()
    {
        if (isSwitchingWeapon)
        {
            // Weapon switching logic
        }
    }

    void Start()
    {
        if (brain != null)
        {
            inputControls = brain.GetInputControls();
            SubscribeToInputs(inputControls);
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromInputs(inputControls);

        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.OnHitTarget -= OnWeaponHitTarget;
            CurrentWeaponHitbox.OnHitWorld -= OnWeaponHitWorld;
        }
    }

    #region Socket Management

    Transform FindHandSocket()
    {
        string[] handBoneNames = {
            "RightHand", "Right Hand", "Hand_R", "mixamorig:RightHand",
            "Bip01 R Hand", "R_Hand", "hand_r", "RightArm_Hand"
        };

        Transform[] allTransforms = GetComponentsInParent<Transform>();

        foreach (string handName in handBoneNames)
        {
            foreach (Transform t in allTransforms)
            {
                if (t.name.ToLower().Contains(handName.ToLower()))
                {
                    if (debugWeapons)
                        Debug.Log($"Auto-found hand socket: {t.name}");
                    return t;
                }
            }
        }

        if (debugWeapons)
            Debug.LogWarning("Could not auto-find hand socket. Please assign defaultWeaponSocket manually.");

        return null;
    }

    Transform GetSocketForWeapon(WeaponType weaponType)
    {
        foreach (var config in socketConfigs)
        {
            if (config.weaponType == weaponType && config.socketTransform != null)
            {
                return config.socketTransform;
            }
        }
        return defaultWeaponSocket;
    }

    WeaponSocketConfig GetSocketConfigForWeapon(WeaponType weaponType)
    {
        foreach (var config in socketConfigs)
        {
            if (config.weaponType == weaponType)
            {
                return config;
            }
        }
        return null;
    }

    #endregion

    #region Hitbox Management



  

    void SetupWeaponHitbox(WeaponType weaponType)
    {
        if (!enableWeaponHitboxes || currentWeaponModel == null) return;



        // Remove any existing SimpleWeaponHit components
        var existingSimpleHit = currentWeaponModel.GetComponent<SimpleWeaponHit>();
        if (existingSimpleHit != null)
        {
            Destroy(existingSimpleHit);
        }

        // Add SimpleWeaponHit component instead of WeaponHitbox
        var simpleHit = currentWeaponModel.AddComponent<SimpleWeaponHit>();

        // Subscribe to simple hit events (now the types match)
        simpleHit.OnHitTarget += OnWeaponHitTarget;
        simpleHit.OnHitWorld += OnWeaponHitWorld;

        // Store reference
        CurrentWeaponHitbox = simpleHit;

        // Ensure weapon has a trigger collider
        var weaponCollider = currentWeaponModel.GetComponent<Collider>();
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
        }

        if (debugWeapons)
            Debug.Log($"Setup SimpleWeaponHit for weapon type: {weaponType}");
    }

    #endregion
    #region Weapon Collision Control (Add this section to WeaponModule.cs)

    /// <summary>
    /// Enable weapon collision detection - called by AttackModule animation events
    /// </summary>
    public void EnableWeaponCollider()
    {
        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.ForceEnableHitting();

            if (debugWeapons)
                Debug.Log("WeaponModule: Enabled weapon collision via SimpleWeaponHit");
        }
        else if (currentWeaponModel != null)
        {
            // Fallback: enable trigger colliders directly
            var colliders = currentWeaponModel.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    col.enabled = true;
                }
            }

            if (debugWeapons)
                Debug.Log("WeaponModule: Enabled weapon collision via direct collider access");
        }
        else
        {
            if (debugWeapons)
                Debug.LogWarning("WeaponModule: No weapon or hitbox to enable");
        }
    }

    /// <summary>
    /// Disable weapon collision detection - called by AttackModule animation events
    /// </summary>
    public void DisableWeaponCollider()
    {
        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.ForceDisableHitting();

            if (debugWeapons)
                Debug.Log("WeaponModule: Disabled weapon collision via SimpleWeaponHit");
        }
        else if (currentWeaponModel != null)
        {
            // Fallback: disable trigger colliders directly
            var colliders = currentWeaponModel.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    col.enabled = false;
                }
            }

            if (debugWeapons)
                Debug.Log("WeaponModule: Disabled weapon collision via direct collider access");
        }
        else
        {
            if (debugWeapons)
                Debug.LogWarning("WeaponModule: No weapon or hitbox to disable");
        }
    }

    /// <summary>
    /// Check if weapon collision is currently enabled
    /// </summary>
    public bool IsWeaponColliderEnabled()
    {
        if (CurrentWeaponHitbox != null)
        {
            return CurrentWeaponHitbox.CanCurrentlyHit;
        }

        if (currentWeaponModel != null)
        {
            var collider = currentWeaponModel.GetComponent<Collider>();
            return collider != null && collider.enabled && collider.isTrigger;
        }

        return false;
    }

    /// <summary>
    /// Clear hit tracking for current weapon (useful for combo resets)
    /// </summary>
    public void ClearWeaponHitTracking()
    {
        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.ClearHitTracking();

            if (debugWeapons)
                Debug.Log("WeaponModule: Cleared weapon hit tracking");
        }
    }

    #endregion
    #region Input System

    public void SubscribeToInputs(PlayerInputControls playerInputControls)
    {
        if (playerInputControls == null || isSubscribedToInput) return;
        if (!allowWeaponSwitching) return;

        try
        {
            var playerActions = playerInputControls.Player;

            for (int i = 1; i <= 9; i++)
            {
                string actionName = $"Weapon{i}";
                TrySubscribeToWeaponSlot(playerActions, actionName, i - 1);
            }

            TrySubscribeToAction(playerActions, "NextWeapon", action => {
                action.performed += OnNextWeapon;
            });

            TrySubscribeToAction(playerActions, "PreviousWeapon", action => {
                action.performed += OnPreviousWeapon;
            });

            isSubscribedToInput = true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"WeaponModule: Some inputs not available: {e.Message}");
        }
    }

    public void UnsubscribeFromInputs(PlayerInputControls playerInputControls)
    {
        if (!isSubscribedToInput || playerInputControls == null) return;

        try
        {
            var playerActions = playerInputControls.Player;

            TryUnsubscribeFromAction(playerActions, "NextWeapon", action => {
                action.performed -= OnNextWeapon;
            });

            TryUnsubscribeFromAction(playerActions, "PreviousWeapon", action => {
                action.performed -= OnPreviousWeapon;
            });

            isSubscribedToInput = false;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"WeaponModule: Error unsubscribing: {e.Message}");
        }
    }

    void TrySubscribeToWeaponSlot(object playerActions, string actionName, int weaponIndex)
    {
        var actionProperty = playerActions.GetType().GetProperty(actionName);
        if (actionProperty?.GetValue(playerActions) is UnityEngine.InputSystem.InputAction action)
        {
            action.performed += (context) => OnWeaponSlot(weaponIndex);
        }
    }

    void TrySubscribeToAction(object playerActions, string actionName, System.Action<UnityEngine.InputSystem.InputAction> callback)
    {
        var actionProperty = playerActions.GetType().GetProperty(actionName);
        if (actionProperty?.GetValue(playerActions) is UnityEngine.InputSystem.InputAction action)
        {
            callback(action);
        }
    }

    void TryUnsubscribeFromAction(object playerActions, string actionName, System.Action<UnityEngine.InputSystem.InputAction> callback)
    {
        var actionProperty = playerActions.GetType().GetProperty(actionName);
        if (actionProperty?.GetValue(playerActions) is UnityEngine.InputSystem.InputAction action)
        {
            callback(action);
        }
    }

    #endregion

    #region Input Handlers

    void OnWeaponSlot(int weaponIndex)
    {
        if (CanSwitchWeapon() && weaponIndex < availableWeapons.Length)
        {
            SwitchToWeapon(weaponIndex);
        }
    }

    void OnNextWeapon(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (CanSwitchWeapon())
        {
            int nextIndex = (currentWeaponIndex + 1) % availableWeapons.Length;
            SwitchToWeapon(nextIndex);
        }
    }

    void OnPreviousWeapon(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (CanSwitchWeapon())
        {
            int prevIndex = currentWeaponIndex - 1;
            if (prevIndex < 0) prevIndex = availableWeapons.Length - 1;
            SwitchToWeapon(prevIndex);
        }
    }

    #endregion

    #region Weapon Management

    public bool CanSwitchWeapon()
    {
        return allowWeaponSwitching &&
               !isSwitchingWeapon &&
               HasWeapons &&
               (meleeModule == null || !meleeModule.IsBusyWithMelee) && // CHANGED from IsBusyWithCombat
               (animationState == null || animationState.CanAct());
    }

    public void SwitchToWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Length) return;
        if (weaponIndex == currentWeaponIndex) return;

        StartCoroutine(WeaponSwitchCoroutine(weaponIndex));
    }

    System.Collections.IEnumerator WeaponSwitchCoroutine(int newWeaponIndex)
    {
        isSwitchingWeapon = true;

        if (currentWeapon != null)
        {
            OnWeaponUnequipped?.Invoke(currentWeapon);
            UnequipCurrentWeapon();
        }

        if (controller?.Animator != null)
        {
            controller.TriggerAnimation(weaponSwitchParam);
        }

        yield return new WaitForSeconds(weaponSwitchTime);

        EquipWeapon(newWeaponIndex);

        isSwitchingWeapon = false;

        if (debugWeapons)
            Debug.Log($"Switched to weapon: {currentWeapon?.name ?? "None"}");
    }

    void EquipWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Length) return;

        currentWeaponIndex = weaponIndex;
        currentWeapon = availableWeapons[weaponIndex];

        WeaponType weaponType = GetWeaponTypeInt(currentWeapon);
        currentWeaponSocket = GetSocketForWeapon(weaponType);

        if (currentWeaponSocket == null)
        {
            Debug.LogWarning($"No socket found for weapon type {weaponType}");
            return;
        }

        GameObject weaponPrefab = GetWeaponModel(currentWeapon);
        if (weaponPrefab != null)
        {
            currentWeaponModel = Instantiate(weaponPrefab, currentWeaponSocket);
            currentWeaponModel.name = $"{currentWeapon.name}_Model";

            var socketConfig = GetSocketConfigForWeapon(weaponType);
            if (socketConfig != null)
            {
                currentWeaponModel.transform.localPosition = socketConfig.localPosition;
                currentWeaponModel.transform.localRotation = Quaternion.Euler(socketConfig.localRotation);
                currentWeaponModel.transform.localScale = socketConfig.localScale;
            }
            else
            {
                currentWeaponModel.transform.localPosition = defaultWeaponPosition;
                currentWeaponModel.transform.localRotation = Quaternion.Euler(defaultWeaponRotation);
                currentWeaponModel.transform.localScale = defaultWeaponScale;
            }

            // Setup weapon hitbox
            SetupWeaponHitbox(weaponType);
        }

        UpdateMeleeModuleWithWeapon(); // CHANGED from UpdateCombatModuleWithWeapon
        UpdateAnimatorWithWeapon();

        OnWeaponEquipped?.Invoke(currentWeapon);
        OnWeaponChanged?.Invoke(currentWeapon);

        if (debugWeapons)
            Debug.Log($"Equipped weapon: {currentWeapon?.name ?? "None"} with hitbox");
    }

    void UnequipCurrentWeapon()
    {
        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.OnHitTarget -= OnWeaponHitTarget;
            CurrentWeaponHitbox.OnHitWorld -= OnWeaponHitWorld;
            CurrentWeaponHitbox = null;
        }

        if (currentWeaponModel != null)
        {
            Destroy(currentWeaponModel);
            currentWeaponModel = null;
        }

        currentWeapon = null;
        currentWeaponSocket = null;
    }

    void UpdateMeleeModuleWithWeapon() // CHANGED method name
    {
        // Melee integration would go here - UPDATED comment
        if (debugWeapons)
            Debug.Log($"Updated melee stats for weapon: {currentWeapon?.name ?? "None"}");
    }

    void UpdateAnimatorWithWeapon()
    {
        if (controller?.Animator == null || currentWeapon == null) return;

        WeaponType weaponType = GetWeaponTypeInt(currentWeapon);
        controller.SetAnimationInt("WeaponType", (int)weaponType);

        if (weaponAnimators != null && currentWeaponIndex < weaponAnimators.Length)
        {
            var weaponAnimator = weaponAnimators[currentWeaponIndex];
            if (weaponAnimator != null)
            {
                controller.Animator.runtimeAnimatorController = weaponAnimator;
            }
        }
    }

    #endregion

    #region Weapon Data Adaptation

    GameObject GetWeaponModel(WeaponData weapon)
    {
        if (weapon == null) return null;

        var modelField = weapon.GetType().GetField("weaponModel");
        if (modelField != null) return modelField.GetValue(weapon) as GameObject;

        var prefabField = weapon.GetType().GetField("prefab");
        if (prefabField != null) return prefabField.GetValue(weapon) as GameObject;

        return null;
    }

    WeaponType GetWeaponTypeInt(WeaponData weapon)
    {
        if (weapon == null) return WeaponType.Unarmed;

        var typeField = weapon.GetType().GetField("weaponType");
        if (typeField != null)
        {
            var value = typeField.GetValue(weapon);
            if (value is WeaponType weaponType) return weaponType;
            if (value is int intValue) return (WeaponType)intValue;
        }

        return WeaponType.Unarmed;
    }

    #endregion

    #region Public API

    public WeaponData GetWeapon(int index)
    {
        if (index < 0 || index >= availableWeapons.Length) return null;
        return availableWeapons[index];
    }

    public int GetWeaponCount() => availableWeapons?.Length ?? 0;

    public float GetCurrentWeaponDamage()
    {
        if (currentWeapon == null) return 0f;

        var damageField = currentWeapon.GetType().GetField("damage");
        if (damageField != null && damageField.GetValue(currentWeapon) is float damage)
            return damage;

        return 10f;
    }

    public float GetCurrentWeaponReach()
    {
        if (currentWeapon == null) return 1.5f;

        var reachField = currentWeapon.GetType().GetField("reach");
        if (reachField != null && reachField.GetValue(currentWeapon) is float reach)
            return reach;

        return 1.5f;
    }

    public float GetCurrentWeaponSpeed()
    {
        if (currentWeapon == null) return 1f;

        var speedField = currentWeapon.GetType().GetField("attackSpeed");
        if (speedField != null && speedField.GetValue(currentWeapon) is float speed)
            return speed;

        return 1f;
    }

    public bool CanCurrentWeaponBlock()
    {
        if (currentWeapon == null) return true;

        var blockField = currentWeapon.GetType().GetField("canBlock");
        if (blockField != null && blockField.GetValue(currentWeapon) is bool canBlock)
            return canBlock;

        return true;
    }

    public bool CanCurrentWeaponParry()
    {
        if (currentWeapon == null) return false;

        var parryField = currentWeapon.GetType().GetField("canParry");
        if (parryField != null && parryField.GetValue(currentWeapon) is bool canParry)
            return canParry;

        return false;
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (!HasWeapons) return;

        // Show weapon reach
        if (currentWeapon != null)
        {
            Gizmos.color = Color.red;
            Vector3 weaponPos = currentWeaponSocket?.position ?? transform.position;
            Gizmos.DrawWireSphere(weaponPos, GetCurrentWeaponReach());
        }

        // Show socket configurations
        if (socketConfigs != null)
        {
            foreach (var config in socketConfigs)
            {
                if (config.showGizmos && config.socketTransform != null)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireSphere(config.socketTransform.position, 0.1f);
                }
            }
        }

        // Current weapon socket
        if (currentWeaponSocket != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentWeaponSocket.position, 0.15f);
        }
    }

    #endregion
}