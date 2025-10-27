using UnityEngine;
using System.Collections;

/// <summary>
/// Projectile module that handles throwing mechanics for ranged weapons.
/// Now uses the simplified generic projectile system with configurable projectile data.
/// Separated from melee combat system for better modularity.
/// </summary>
public class ProjectileModule : MonoBehaviour, IPlayerModule, IInputHandler
{
    [Header("Projectile Settings")]
    [SerializeField] private GameObject genericProjectilePrefab; // Prefab with Projectile component
    [SerializeField] private ProjectileData defaultProjectileData; // Default shuriken/kunai data
    [SerializeField] private Transform throwPoint; // Usually hand or weapon socket
    [SerializeField] private float throwForce = 15f;

    [Header("Targeting")]
    [SerializeField] private bool requireTargetToThrow = true;
    [SerializeField] private bool autoTargetNearestEnemy = true;

    [Header("Homing Behavior")]
    [SerializeField, Range(0f, 360f)] private float turningSpeed = 45f; // Degrees per second
    [SerializeField] private float homingRange = 10f;
    [SerializeField] private LayerMask targetLayers = -1;

    [Header("Animation")]
    [SerializeField] private string throwAnimationTrigger = "ThrowProjectile";

    // References
    private ControllerBrain brain;
    private ThirdPersonController controller;
    private TargetLockModule targetLock;

    // State
    private float lastThrowTime;
    private bool isInitialized;

    // Properties
    public bool IsEnabled
    {
        get => enabled;
        set => enabled = value;
    }

    // Events
    public System.Action<GameObject> OnProjectileThrown;
    public System.Action OnThrowFailed;

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        controller = brain.GetModule<ThirdPersonController>();

        // Find target lock module on camera
        var cameraComponent = brain.GetComponentInChildren<Camera>();
        if (cameraComponent != null)
        {
            targetLock = cameraComponent.GetComponent<TargetLockModule>();
        }

        // Set default throw point if none assigned
        if (throwPoint == null)
        {
            // Try to find weapon socket or use hand
            var weaponSocket = brain.GetComponentInChildren<Transform>().Find("WeaponSocket");
            if (weaponSocket != null)
                throwPoint = weaponSocket;
            else
                throwPoint = transform; // Fallback to this module's transform
        }

        isInitialized = true;

        
    }

    public void UpdateModule()
    {
        if (!isInitialized) return;

        // Handle cooldown countdown (if needed for UI or other systems)
        // Most logic is event-driven through input
    }

    #endregion

    #region IInputHandler Implementation

    public void SubscribeToInputs(PlayerInputControls inputActions)
    {
        if (inputActions?.Player.QuickslotQ != null)
        {
            inputActions.Player.QuickslotQ.performed += OnThrowInput;
        }
    }

    public void UnsubscribeFromInputs(PlayerInputControls inputActions)
    {
        if (inputActions?.Player.QuickslotQ != null)
        {
            inputActions.Player.QuickslotQ.performed -= OnThrowInput;
        }
    }

    #endregion

    #region Input Handling

    private void OnThrowInput(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (CanThrowProjectile())
        {
            ThrowProjectile();
        }
        else
        {
            OnThrowFailed?.Invoke();

            string reason = GetCannotThrowReason();
            Debug.Log($"Cannot throw projectile: {reason}");
        }
    }

    #endregion

    #region Projectile Logic

    public bool CanThrowProjectile()
    {
        if (!isInitialized || !enabled) return false;
        if (genericProjectilePrefab == null || defaultProjectileData == null) return false;

        float cooldownTime = defaultProjectileData.cooldownTime;
        if (Time.time - lastThrowTime < cooldownTime) return false;

        // Check if we need a target and have one
        if (requireTargetToThrow)
        {
            Transform target = GetPreferredTarget();
            if (target == null) return false;
        }

        // Check stamina if controller available
        float staminaCost = defaultProjectileData.staminaCost;
        if (controller != null && !HasEnoughStamina(staminaCost)) return false;

        // Check animation state if available
        var animModule = brain.GetModule<AnimationStateModule>();
        if (animModule != null && animModule.CurrentState == AnimationState.Locked)
            return false;

        return true;
    }

    private string GetCannotThrowReason()
    {
        if (!isInitialized) return "Not initialized";
        if (genericProjectilePrefab == null) return "No projectile prefab assigned";
        if (defaultProjectileData == null) return "No projectile data assigned";

        float cooldownTime = defaultProjectileData.cooldownTime;
        if (Time.time - lastThrowTime < cooldownTime) return "On cooldown";

        if (requireTargetToThrow)
        {
            Transform target = GetPreferredTarget();
            if (target == null)
            {
                if (targetLock != null && !HasTargetLocked())
                    return "No target locked - use right mouse to target";
                else
                    return "No valid target in range";
            }
        }

        float staminaCost = defaultProjectileData.staminaCost;
        if (controller != null && !HasEnoughStamina(staminaCost)) return "Not enough stamina";

        var animModule = brain.GetModule<AnimationStateModule>();
        if (animModule != null && animModule.CurrentState == AnimationState.Locked) return "Animation locked";

        return "Unknown reason";
    }

    public void ThrowProjectile(ProjectileData projectileData = null)
    {
        if (!CanThrowProjectile()) return;

        // Use provided data or fall back to default
        var dataToUse = projectileData ?? defaultProjectileData;

        // Consume stamina
        if (controller != null)
        {
            TryConsumeStamina(dataToUse.staminaCost);
        }

        // Get throw direction and target
        Vector3 throwDirection = GetThrowDirection();
        Transform targetTransform = GetPreferredTarget();

        // Instantiate projectile
        GameObject projectile = Instantiate(genericProjectilePrefab, throwPoint.position, Quaternion.LookRotation(throwDirection));

        // Set up projectile component
        var projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript == null)
        {
            Debug.LogError("Generic projectile prefab missing Projectile component!");
            Destroy(projectile);
            return;
        }

        // Configure projectile with new system
        projectileScript.Initialize(
            throwDirection * throwForce,
            targetTransform,
            brain.gameObject,
            dataToUse
        );

        // Subscribe to spell triggers if the projectile supports it
        projectileScript.OnSpellTrigger += HandleSpellTrigger;

        // Play animation if available
        if (controller != null && !string.IsNullOrEmpty(throwAnimationTrigger))
        {
            TryTriggerAnimation(throwAnimationTrigger);
        }

        // Update state
        lastThrowTime = Time.time;

        // Fire event
        OnProjectileThrown?.Invoke(projectile);

        Debug.Log($"Threw {dataToUse.projectileName} with turning speed: {turningSpeed}°/s, target: {(targetTransform ? targetTransform.name : "none")}");
    }

    private void HandleSpellTrigger(Vector3 position, GameObject thrower)
    {
        // Integration point for your spell system
        // You can implement this based on your spell system architecture
        Debug.Log($"Projectile triggered spell at position {position}");

        // Example integration:
        // var spellSystem = thrower.GetComponent<YourSpellSystem>();
        // spellSystem?.CastSpellAtPosition(defaultProjectileData.SpellToTrigger, position);
    }

    private Vector3 GetThrowDirection()
    {
        Transform target = GetPreferredTarget();

        // If we have a target, always throw directly at it
        if (target != null)
        {
            Vector3 targetPos;

            // Use ITargetable's target point if available (this should give the best aim point)
            var targetable = target.GetComponent<ITargetable>();
            if (targetable != null)
            {
                targetPos = targetable.GetTargetPoint();
            }
            else
            {
                // Fallback: aim for upper portion of the collider (chest/head area)
                var targetCollider = target.GetComponent<Collider>();
                if (targetCollider != null)
                {
                    Vector3 colliderCenter = targetCollider.bounds.center;
                    Vector3 colliderTop = new Vector3(colliderCenter.x, targetCollider.bounds.max.y, colliderCenter.z);
                    targetPos = Vector3.Lerp(colliderCenter, colliderTop, 0.75f); // 75% up from center to top
                }
                else
                {
                    // Last resort: aim significantly above the transform position
                    targetPos = target.position + Vector3.up * 2f; // 2 units up instead of 1.5f
                }
            }

            return (targetPos - throwPoint.position).normalized;
        }

        // No target - don't throw or use fallback behavior
        if (!requireTargetToThrow)
        {
            // Fallback: throw forward from player
            return brain.transform.forward;
        }

        // Should not reach here if requireTargetToThrow is true
        return brain.transform.forward;
    }

    private Transform GetPreferredTarget()
    {
        // First priority: Locked target from your targeting system
        if (targetLock != null && HasTargetLocked())
        {
            return GetLockedTarget();
        }

        // Second priority: Auto-target nearest enemy if enabled
        if (autoTargetNearestEnemy)
        {
            return FindClosestTargetableEnemy();
        }

        // No target available
        return null;
    }

    private Transform FindClosestTargetableEnemy()
    {
        Collider[] nearby = Physics.OverlapSphere(throwPoint.position, homingRange * 2f, targetLayers);
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach (var col in nearby)
        {
            // Skip if it's the player
            if (col.transform.IsChildOf(brain.transform)) continue;

            // Must have ITargetable component and be targetable
            var targetable = col.GetComponent<ITargetable>();
            if (targetable == null || !targetable.CanBeTargeted) continue;

            float distance = Vector3.Distance(throwPoint.position, col.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = col.transform;
            }
        }

        return closest;
    }

    #endregion

    #region Helper Methods

    private bool HasEnoughStamina(float cost)
    {
        // Try multiple common stamina property names
        var stamina = controller.GetType().GetProperty("CurrentStamina");
        if (stamina != null)
        {
            float current = (float)stamina.GetValue(controller);
            return current >= cost;
        }

        // Fallback - assume we have enough stamina
        return true;
    }

    private void TryConsumeStamina(float cost)
    {
        // Try common method names
        var method = controller.GetType().GetMethod("ConsumeStamina");
        if (method != null)
        {
            method.Invoke(controller, new object[] { cost });
            return;
        }

        // Try alternative method name
        method = controller.GetType().GetMethod("UseStamina");
        if (method != null)
        {
            method.Invoke(controller, new object[] { cost });
        }
    }

    private void TryTriggerAnimation(string trigger)
    {
        // Try common animation method names
        var method = controller.GetType().GetMethod("TriggerAnimation");
        if (method != null)
        {
            method.Invoke(controller, new object[] { trigger });
            return;
        }

        method = controller.GetType().GetMethod("SetAnimationTrigger");
        if (method != null)
        {
            method.Invoke(controller, new object[] { trigger });
        }
    }

    private bool HasTargetLocked()
    {
        // Try common target lock properties
        var property = targetLock.GetType().GetProperty("IsLockedOn");
        if (property != null)
        {
            return (bool)property.GetValue(targetLock);
        }

        property = targetLock.GetType().GetProperty("HasTarget");
        if (property != null)
        {
            return (bool)property.GetValue(targetLock);
        }

        return false;
    }

    private Transform GetLockedTarget()
    {
        // Try common target properties
        var property = targetLock.GetType().GetProperty("LockedTarget");
        if (property != null)
        {
            return (Transform)property.GetValue(targetLock);
        }

        property = targetLock.GetType().GetProperty("CurrentTarget");
        if (property != null)
        {
            var target = property.GetValue(targetLock);
            if (target is Transform transform)
                return transform;
            if (target != null)
                return ((MonoBehaviour)target).transform;
        }

        return null;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get current cooldown remaining
    /// </summary>
    public float GetCooldownRemaining()
    {
        if (defaultProjectileData == null) return 0f;
        return Mathf.Max(0f, defaultProjectileData.cooldownTime - (Time.time - lastThrowTime));
    }

    /// <summary>
    /// Check if projectile is on cooldown
    /// </summary>
    public bool IsOnCooldown()
    {
        return GetCooldownRemaining() > 0f;
    }

    /// <summary>
    /// Throw a specific projectile type
    /// </summary>
    public void ThrowSpecificProjectile(ProjectileData projectileData)
    {
        ThrowProjectile(projectileData);
    }

    /// <summary>
    /// Force throw projectile (ignores some restrictions)
    /// </summary>
    public void ForceThrow()
    {
        if (genericProjectilePrefab != null && defaultProjectileData != null && isInitialized)
        {
            ThrowProjectile();
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmosSelected()
    {
        if (!isInitialized || throwPoint == null) return;

        // Draw homing range (smaller, for tracking)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(throwPoint.position, homingRange);

        // Draw targeting range (larger, for initial target detection)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(throwPoint.position, homingRange * 2f);

        // Draw throw direction to current target
        Transform target = GetPreferredTarget();
        if (target != null)
        {
            Vector3 throwDir = GetThrowDirection();
            Gizmos.color = Color.red;
            Gizmos.DrawRay(throwPoint.position, throwDir * Vector3.Distance(throwPoint.position, target.position));

            // Draw target
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(target.position + Vector3.up * 1.5f, Vector3.one * 0.5f);

            // Draw targeting line
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(throwPoint.position, target.position + Vector3.up * 1.5f);
        }
        else
        {
            // No target available - show this visually
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(throwPoint.position + brain.transform.forward * 2f, Vector3.one * 0.3f);
        }
    }

    private void OnDestroy()
    {
        // Clean up any subscriptions
        if (isInitialized)
        {
            isInitialized = false;
        }
    }

    #endregion
}