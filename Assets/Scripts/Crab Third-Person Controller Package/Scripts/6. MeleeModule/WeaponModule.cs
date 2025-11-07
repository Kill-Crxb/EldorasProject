// Weapon Module - Enhanced for Natural Weapons (Animals)
using UnityEngine;
using System.Collections.Generic;
using static UnityEngine.Rendering.DebugUI.Table;
using static UnityEngine.UIElements.UxmlAttributeDescription;
using System.Net.Sockets;
using Unity.VisualScripting;

[System.Serializable]
public class WeaponSocketConfig
{
    [Header("Socket Identity")]
    [Tooltip("Unique name for this socket (e.g., 'LeftClaw', 'RightClaw', 'Jaw')")]
    public string socketName = "";

    public WeaponType weaponType;
    public Transform socketTransform;

    [Header("Transform Settings")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = new Vector3(-90f, 90f, 90f);
    public Vector3 localScale = new Vector3(0.01f, 0.01f, 0.01f);

    [Header("Natural Weapon Settings")]
    [Tooltip("Is this socket for a natural weapon (claws, teeth)?")]
    public bool isNaturalWeaponSocket = false;

    [Tooltip("Keep weapon visible even when not active (for animal body parts)")]
    public bool alwaysShowWeapon = true;

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

    [Header("Multi-Socket Configuration (Animals)")]
    [SerializeField] private bool useMultipleSockets = false;
    [Tooltip("If true, all natural weapons instantiate at startup and stay persistent")]
    [SerializeField] private bool preInstantiateNaturalWeapons = true;

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

    // Component references
    private ControllerBrain brain;
    private ThirdPersonController controller;
    private MeleeModule meleeModule;
    private AnimationStateModule animationState;
    private PlayerInputControls inputControls;
    private bool isSubscribedToInput;

    // Current weapon state (single weapon for players)
    private WeaponData currentWeapon;
    private GameObject currentWeaponModel;
    private bool isSwitchingWeapon;
    private Transform currentWeaponSocket;

    // Natural weapon tracking (multiple weapons for animals)
    private Dictionary<string, GameObject> naturalWeaponModels = new Dictionary<string, GameObject>();
    private Dictionary<string, SimpleWeaponHit> naturalWeaponHitboxes = new Dictionary<string, SimpleWeaponHit>();

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
    public bool UsesMultipleSockets => useMultipleSockets;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.Controller;
        meleeModule = brain.GetModule<MeleeModule>();
        animationState = brain.AnimationState;

        if (autoFindHandSocket && defaultWeaponSocket == null)
        {
            defaultWeaponSocket = FindHandSocket();
        }

        if (HasWeapons)
        {
            if (useMultipleSockets && preInstantiateNaturalWeapons)
            {
                InitializeNaturalWeapons();
            }
            else
            {
                EquipWeapon(currentWeaponIndex);
            }
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

        // Cleanup natural weapon subscriptions
        foreach (var hitbox in naturalWeaponHitboxes.Values)
        {
            if (hitbox != null)
            {
                hitbox.OnHitTarget -= OnWeaponHitTarget;
                hitbox.OnHitWorld -= OnWeaponHitWorld;
            }
        }
    }

    #region Natural Weapon System (NEW)

    // Track which sockets have been used (prevents duplicate assignments)
    private HashSet<string> usedSocketNames = new HashSet<string>();

    void InitializeNaturalWeapons()
    {
        if (debugWeapons)
            Debug.Log($"[WeaponModule] Initializing {availableWeapons.Length} natural weapons...");

        // Clear used sockets tracker
        usedSocketNames.Clear();

        for (int i = 0; i < availableWeapons.Length; i++)
        {
            WeaponData weapon = availableWeapons[i];
            if (weapon == null) continue;

            if (weapon.isNaturalWeapon)
            {
                InstantiateNaturalWeapon(weapon, i);
            }
        }

        // Set first weapon as active
        if (availableWeapons.Length > 0)
        {
            currentWeaponIndex = 0;
            currentWeapon = availableWeapons[0];

            string weaponKey = GetWeaponKey(currentWeapon, 0); // ✓ Pass index
            if (naturalWeaponModels.ContainsKey(weaponKey))
            {
                currentWeaponModel = naturalWeaponModels[weaponKey];
                CurrentWeaponHitbox = naturalWeaponHitboxes[weaponKey];
            }

            OnWeaponEquipped?.Invoke(currentWeapon);

            if (debugWeapons)
                Debug.Log($"[WeaponModule] Initialized with {currentWeapon.weaponName} active");
        
        
        }
    }

    void InstantiateNaturalWeapon(WeaponData weapon, int weaponIndex)
    {
        // Find socket for this weapon
        WeaponSocketConfig socket = FindSocketForNaturalWeapon(weapon, weaponIndex);

        if (socket == null)
        {
            Debug.LogError($"[WeaponModule] No socket found for natural weapon: {weapon.weaponName} (index {weaponIndex})");
            return;
        }

        // Mark socket as used
        usedSocketNames.Add(socket.socketName);

        // Instantiate weapon model
        GameObject weaponModel = GetWeaponModel(weapon);
        if (weaponModel == null)
        {
            Debug.LogError($"[WeaponModule] No weapon model found for: {weapon.weaponName}");
            return;
        }

        GameObject weaponInstance = Instantiate(weaponModel, socket.socketTransform);
        weaponInstance.name = $"{weapon.weaponName}_Model";
        weaponInstance.transform.localPosition = socket.localPosition;
        weaponInstance.transform.localRotation = Quaternion.Euler(socket.localRotation);
        weaponInstance.transform.localScale = socket.localScale;

        // Setup hitbox
        SimpleWeaponHit hitbox = SetupNaturalWeaponHitbox(weaponInstance, weapon.weaponType);

        // Store references with unique key (weaponName + index)
        string weaponKey = GetWeaponKey(weapon, weaponIndex); // ✓ Pass index
        naturalWeaponModels[weaponKey] = weaponInstance;
        naturalWeaponHitboxes[weaponKey] = hitbox;

        // Initially disable hitbox
        if (hitbox != null)
        {
            hitbox.ForceDisableHitting();
        }

        if (debugWeapons)
            Debug.Log($"[WeaponModule] Instantiated natural weapon: {weapon.weaponName} at socket: {socket.socketName}");
    }
    /// <summary>
    /// Find the appropriate socket for a natural weapon
    /// Priority: 1) Exact socket name match (unused), 2) Weapon type match (unused), 3) First available
    /// </summary>
    WeaponSocketConfig FindSocketForNaturalWeapon(WeaponData weapon, int weaponIndex)
    {
        // Priority 1: Exact socket name match (if specified and not used)
        if (!string.IsNullOrEmpty(weapon.preferredSocketName))
        {
            foreach (var config in socketConfigs)
            {
                if (config.socketName == weapon.preferredSocketName &&
                    config.isNaturalWeaponSocket &&
                    !usedSocketNames.Contains(config.socketName))
                {
                    if (debugWeapons)
                        Debug.Log($"[WeaponModule] Matched {weapon.weaponName} to socket by name: {config.socketName}");
                    return config;
                }
            }
        }

        // Priority 2: Weapon type match (first unused socket of matching type)
        foreach (var config in socketConfigs)
        {
            if (config.weaponType == weapon.weaponType &&
                config.isNaturalWeaponSocket &&
                !usedSocketNames.Contains(config.socketName))
            {
                if (debugWeapons)
                    Debug.Log($"[WeaponModule] Matched {weapon.weaponName} to socket by type: {config.socketName}");
                return config;
            }
        }

        // Priority 3: Use weaponIndex to directly map to socketConfigs array
        if (weaponIndex >= 0 && weaponIndex < socketConfigs.Length)
        {
            var config = socketConfigs[weaponIndex];
            if (config.isNaturalWeaponSocket && !usedSocketNames.Contains(config.socketName))
            {
                if (debugWeapons)
                    Debug.Log($"[WeaponModule] Matched {weapon.weaponName} to socket by index: {config.socketName}");
                return config;
            }
        }

        Debug.LogError($"[WeaponModule] No available socket found for {weapon.weaponName} (index {weaponIndex})");
        return null;
    }

    /// <summary>
    /// Setup hitbox for a natural weapon
    /// </summary>
    SimpleWeaponHit SetupNaturalWeaponHitbox(GameObject weaponObject, WeaponType weaponType)
    {
        if (!enableWeaponHitboxes) return null;

        // Remove existing hitbox
        var existingHitbox = weaponObject.GetComponent<SimpleWeaponHit>();
        if (existingHitbox != null)
        {
            Destroy(existingHitbox);
        }

        // Add new hitbox
        var hitbox = weaponObject.AddComponent<SimpleWeaponHit>();

        // Subscribe to events
        hitbox.OnHitTarget += OnWeaponHitTarget;
        hitbox.OnHitWorld += OnWeaponHitWorld;

        // Ensure collider is trigger
        var collider = weaponObject.GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }

        return hitbox;
    }
    /// <summary>
    /// Get unique key for weapon (for dictionary storage)
    /// Uses weaponName + index to handle duplicate weapon types
    /// </summary>
    string GetWeaponKey(WeaponData weapon, int index = -1)
    {
        // If index provided, use it for unique key
        if (index >= 0)
        {
            return $"{weapon.weaponName}_{index}";
        }

        // Fallback: just use weapon name (for backward compatibility)
        return weapon.weaponName;
    }

    #endregion

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

        // Add SimpleWeaponHit component
        var simpleHit = currentWeaponModel.AddComponent<SimpleWeaponHit>();

        // Subscribe to events
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

    #region Weapon Collision Control

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
               (meleeModule == null || !meleeModule.IsBusyWithMelee) &&
               (animationState == null || animationState.CanAct());
    }

    public void SwitchToWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Length) return;
        if (weaponIndex == currentWeaponIndex) return;

        // For natural weapons (multi-socket), switch immediately
        if (useMultipleSockets && availableWeapons[weaponIndex].isNaturalWeapon)
        {
            SwitchToNaturalWeapon(weaponIndex);
        }
        else
        {
            // For manufactured weapons, use coroutine with delay
            StartCoroutine(WeaponSwitchCoroutine(weaponIndex));
        }
    }


    void SwitchToNaturalWeapon(int newWeaponIndex)
    {
        WeaponData newWeapon = availableWeapons[newWeaponIndex];
        string weaponKey = GetWeaponKey(newWeapon, newWeaponIndex); // ✓ Pass index

        // Disable old weapon hitbox
        if (CurrentWeaponHitbox != null)
        {
            CurrentWeaponHitbox.ForceDisableHitting();
        }

        // Switch to new weapon
        currentWeaponIndex = newWeaponIndex;
        currentWeapon = newWeapon;

        if (naturalWeaponModels.ContainsKey(weaponKey))
        {
            currentWeaponModel = naturalWeaponModels[weaponKey];
            CurrentWeaponHitbox = naturalWeaponHitboxes[weaponKey];
            currentWeaponSocket = currentWeaponModel.transform.parent;
        }
        else
        {
            Debug.LogError($"[WeaponModule] Natural weapon not found: {weaponKey}");
            return;
        }

        OnWeaponChanged?.Invoke(currentWeapon);

        if (debugWeapons)
            Debug.Log($"[WeaponModule] Switched to natural weapon: {currentWeapon.weaponName} (index {newWeaponIndex})");
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

        UpdateMeleeModuleWithWeapon();
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

    void UpdateMeleeModuleWithWeapon()
    {
        // Melee integration would go here
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
        if (weapon == null) return WeaponType.Sword;

        var typeField = weapon.GetType().GetField("weaponType");
        if (typeField != null)
        {
            var value = typeField.GetValue(weapon);
            if (value is WeaponType weaponType) return weaponType;
            if (value is int intValue) return (WeaponType)intValue;
        }

        return WeaponType.Sword;
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
                    Gizmos.color = config.isNaturalWeaponSocket ? Color.yellow : Color.blue;
                    Gizmos.DrawWireSphere(config.socketTransform.position, 0.1f);

                    // Draw socket name
                    UnityEditor.Handles.Label(config.socketTransform.position, config.socketName);
                }
            }
        }

        // Current weapon socket
        if (currentWeaponSocket != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentWeaponSocket.position, 0.15f);
        }

        // Show all natural weapon positions
        if (useMultipleSockets)
        {
            foreach (var kvp in naturalWeaponModels)
            {
                if (kvp.Value != null)
                {
                    Gizmos.color = kvp.Value == currentWeaponModel ? Color.green : Color.gray;
                    Gizmos.DrawWireSphere(kvp.Value.transform.position, 0.12f);
                }
            }
        }
    }

    [ContextMenu("Debug: Print Natural Weapons")]
    void DebugPrintNaturalWeapons()
    {
        Debug.Log($"=== Natural Weapons Debug ===");
        Debug.Log($"Use Multiple Sockets: {useMultipleSockets}");
        Debug.Log($"Instantiated Weapons: {naturalWeaponModels.Count}");

        foreach (var kvp in naturalWeaponModels)
        {
            Debug.Log($"  - {kvp.Key}: {(kvp.Value != null ? "Active" : "Null")}");
        }

        if (currentWeapon != null)
        {
            string currentKey = GetWeaponKey(currentWeapon, currentWeaponIndex); // ✓ Pass index
            Debug.Log($"Current Weapon: {currentWeapon.weaponName} (Key: {currentKey})");
        }
        else
        {
            Debug.Log($"Current Weapon: None");
        }
    }
    #endregion
}