// Target Lock Module - Simple camera companion for targeting
using UnityEngine;
using UnityEngine.InputSystem;

public class TargetLockModule : MonoBehaviour, IPlayerModule, IInputHandler
{
    [Header("Target Lock Settings")]
    [SerializeField] private float lockOnRange = 15f;
    [SerializeField] private bool useComponentBasedTargeting = true;

    [Header("Debug")]
   // [SerializeField] private bool showDebugInfo = false;

    // Simple references - camera is right here!
    private ControllerBrain brain;
    private new SimpleThirdPersonCamera camera; // 'new' keyword to hide inherited Component.camera
    private Transform playerRoot;

    // Targeting state
    private Transform lockedTarget;
    private bool isLockedOn;

    // Properties for camera to use
    public bool IsLockedOn => isLockedOn;
    public Transform LockedTarget => lockedTarget;
    public Vector3 TargetPoint => lockedTarget != null ? GetTargetPoint() : Vector3.zero;
    public float LockOnRange => lockOnRange; // Add this property back for debug UI

    // IPlayerModule implementation
    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        camera = GetComponent<SimpleThirdPersonCamera>(); // Same GameObject!
        playerRoot = brain.transform.parent;

        // Subscribe to input
        SubscribeToInputs(brain.GetInputControls());
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;
        CheckTargetValid();
    }

    #region Input Handling

    public void SubscribeToInputs(PlayerInputControls inputControls)
    {
        if (inputControls != null && inputControls.Player.TargetLock != null)
        {
            inputControls.Player.TargetLock.performed += OnTargetLockInput;
        }
    }

    public void UnsubscribeFromInputs(PlayerInputControls inputControls)
    {
        if (inputControls != null && inputControls.Player.TargetLock != null)
        {
            inputControls.Player.TargetLock.performed -= OnTargetLockInput;
        }
    }

    private void OnTargetLockInput(InputAction.CallbackContext context)
    {
        if (isLockedOn) UnlockTarget();
        else LockOntoNearestTarget();
    }

    void OnDestroy()
    {
        UnsubscribeFromInputs(brain?.GetInputControls());
    }

    #endregion

    #region Simple Targeting Logic

    void CheckTargetValid()
    {
        if (!isLockedOn || lockedTarget == null) return;

        // Simple range check
        float distance = Vector3.Distance(playerRoot.position, lockedTarget.position);
        if (distance > lockOnRange)
        {
            UnlockTarget();
        }
    }

    void LockOntoNearestTarget()
    {
        // Find ALL colliders in range, then filter by component/tag
        Collider[] targets = Physics.OverlapSphere(playerRoot.position, lockOnRange);

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider target in targets)
        {
            if (IsValidTarget(target))
            {
                float distance = Vector3.Distance(playerRoot.position, target.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = target.transform;
                }
            }
        }

        if (nearest != null)
        {
            SetTarget(nearest);
        }
    }

    bool IsValidTarget(Collider target)
    {
        // Skip self
        if (target.transform.IsChildOf(playerRoot) || target.transform == playerRoot)
        {
            return false;
        }

        // Check targetable component if required
        if (useComponentBasedTargeting)
        {
            var targetable = target.GetComponent<ITargetable>();

            if (targetable != null)
            {
                return targetable.CanBeTargeted;
            }
            else
            {
                return false;
            }
        }

        // Fallback to tags
        return target.CompareTag("Enemy") || target.CompareTag("Targetable");
    }

    void SetTarget(Transform target)
    {
        lockedTarget = target;
        isLockedOn = true;

        // Notify target
        target.GetComponent<ITargetable>()?.OnTargeted();

        // Notify controller for movement
        brain.GetModule<ThirdPersonController>()?.SetLockOnTarget(target);
    }

    void UnlockTarget()
    {
        if (lockedTarget != null)
        {
            lockedTarget.GetComponent<ITargetable>()?.OnTargetLost();
        }

        lockedTarget = null;
        isLockedOn = false;

        // Notify controller
        brain.GetModule<ThirdPersonController>()?.SetLockOnTarget(null);
    }

    Vector3 GetTargetPoint()
    {
        var targetable = lockedTarget.GetComponent<ITargetable>();
        return targetable?.GetTargetPoint() ?? lockedTarget.position + Vector3.up;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Get all valid targets in range
    /// </summary>
    public Transform[] GetTargetsInRange()
    {
        if (playerRoot == null) return new Transform[0];

        Collider[] targets = Physics.OverlapSphere(playerRoot.position, lockOnRange);
        var validTargets = new System.Collections.Generic.List<Transform>();

        foreach (Collider target in targets)
        {
            if (IsValidTarget(target))
            {
                validTargets.Add(target.transform);
            }
        }

        return validTargets.ToArray();
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        if (playerRoot == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerRoot.position, lockOnRange);

        if (isLockedOn && lockedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(playerRoot.position, TargetPoint);
        }
    }

    #endregion
}

// Simple targetable interface - only create if it doesn't exist
public interface ITargetable
{
    bool CanBeTargeted { get; }
    Vector3 GetTargetPoint();
    void OnTargeted();
    void OnTargetLost();
}