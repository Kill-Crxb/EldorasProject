using System;
using UnityEngine;

/// <summary>
/// Knockback Effect - Applies force/impulse to knock back targets
/// 
/// Usage:
/// - Called by abilities to knock back enemies
/// - Works through MovementSystem and LocomotionHandler
/// - Direction can be relative to attacker or absolute
/// 
/// Example:
/// var effect = new KnockbackEffect 
/// { 
///     force = 10f,
///     direction = Vector3.back // Relative to attacker
/// };
/// effect.SetAttacker(attackerTransform);
/// effect.Apply(targetGameObject);
/// 
/// Updated: January 2026 - Supports ControllerBrain/MovementSystem
/// </summary>
[System.Serializable]
public class KnockbackEffect
{
    [Header("Knockback Configuration")]
    [Tooltip("Force of the knockback")]
    public float force = 10f;

    [Tooltip("Direction of knockback (relative to attacker if SetAttacker is used)")]
    public Vector3 direction = Vector3.back;

    [Tooltip("Use direction relative to attacker? (false = world space direction)")]
    public bool useRelativeDirection = true;

    /// <summary>Fired when effect completes</summary>
    public event Action OnCompleted;

    [System.NonSerialized]
    private Transform attackerTransform;

    /// <summary>
    /// Set the attacker transform (for relative direction)
    /// </summary>
    public void SetAttacker(Transform attacker)
    {
        attackerTransform = attacker;
    }

    /// <summary>
    /// Apply knockback to GameObject (modern - works with ControllerBrain)
    /// </summary>
    public void Apply(GameObject targetObject)
    {
        if (targetObject == null)
        {
            Debug.LogWarning("[KnockbackEffect] Target GameObject is null");
            OnCompleted?.Invoke();
            return;
        }

        // Find ControllerBrain
        var brain = targetObject.GetComponent<ControllerBrain>();
        if (brain == null)
            brain = targetObject.GetComponentInParent<ControllerBrain>();

        if (brain != null)
        {
            ApplyToBrain(brain);
        }
        else
        {
            Debug.LogWarning($"[KnockbackEffect] No ControllerBrain found on {targetObject.name} - knockback requires MovementSystem");
        }

        OnCompleted?.Invoke();
    }

    /// <summary>
    /// Apply knockback to ControllerBrain entity
    /// </summary>
    private void ApplyToBrain(ControllerBrain brain)
    {
        var movementSystem = brain.GetModule<MovementSystem>();
        if (movementSystem == null)
        {
            Debug.LogWarning($"[KnockbackEffect] No MovementSystem on {brain.name}");
            return;
        }

        var locomotionHandler = movementSystem.Locomotion as ARPGLocomotionHandler;
        if (locomotionHandler == null)
        {
            Debug.LogWarning($"[KnockbackEffect] Locomotion handler is not ARPGLocomotionHandler on {brain.name}");
            return;
        }

        // Calculate knockback direction
        Vector3 knockbackDir;
        if (useRelativeDirection && attackerTransform != null)
        {
            // Direction relative to attacker's rotation
            knockbackDir = attackerTransform.TransformDirection(direction);
        }
        else if (!useRelativeDirection && attackerTransform != null)
        {
            // Direction from attacker to target (ignores direction field)
            knockbackDir = (brain.transform.position - attackerTransform.position).normalized;
        }
        else
        {
            // World space direction (no attacker set)
            knockbackDir = direction.normalized;
        }

        // Apply impulse
        locomotionHandler.ApplyImpulse(knockbackDir, force);
    }

    /// <summary>
    /// Apply knockback to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use Apply(GameObject) instead")]
    public void Apply(IDamageable target)
    {
        if (target is MonoBehaviour mb)
        {
            Apply(mb.gameObject);
        }
        else
        {
            Debug.LogWarning("[KnockbackEffect] Cannot apply knockback to non-MonoBehaviour IDamageable");
            OnCompleted?.Invoke();
        }
    }

    /// <summary>
    /// Cancel the effect (for interruptible abilities)
    /// </summary>
    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}