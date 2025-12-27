using System;
using UnityEngine;

[System.Serializable]
public class MovementEffect
{
    public enum MovementType
    {
        Impulse,      // Instant velocity boost (additive, for knockback)
        Dash,         // Override movement for duration (dash abilities)
        Teleport,     // Instant position change
        Launch        // Upward/directional launch
    }

    [Header("Movement Configuration")]
    public MovementType movementType = MovementType.Dash;

    [Header("Direction (relative to caster)")]
    [Tooltip("Use Vector3.forward for forward dash, or custom direction")]
    public Vector3 direction = Vector3.forward;

    [Header("Dash/Impulse Settings")]
    public float speed = 15f;
    public float duration = 0.2f;

    [Header("Teleport Settings")]
    public float teleportDistance = 10f;

    public event Action OnCompleted;

    [System.NonSerialized]
    private MovementSystem movementSystem;

    [System.NonSerialized]
    private Transform casterTransform;

    public void SetMovementSystem(MovementSystem system)
    {
        movementSystem = system;
        // Get root transform (ControllerBrain's parent)
        casterTransform = system?.transform.parent;
    }

    // Backward compatibility - deprecated
    [System.Obsolete("Use SetMovementSystem instead")]
    public void SetMovementProvider(object provider)
    {
        // Try to get MovementSystem from provider
        if (provider is MonoBehaviour mb)
        {
            movementSystem = mb.GetComponent<MovementSystem>();
            casterTransform = mb.transform.parent;
        }
    }

    public void Apply(MovementSystem target)
    {
        SetMovementSystem(target);
        ExecuteEffect();
    }

    // Backward compatibility - deprecated
    [System.Obsolete("Use Apply(MovementSystem) instead")]
    public void Apply(object target)
    {
        if (target is MonoBehaviour mb)
        {
            movementSystem = mb.GetComponent<MovementSystem>();
            casterTransform = mb.transform.parent;
            ExecuteEffect();
        }
    }

    private void ExecuteEffect()
    {
        if (movementSystem == null || casterTransform == null)
        {
            Debug.LogWarning("[MovementEffect] MovementSystem or casterTransform not set!");
            return;
        }

        switch (movementType)
        {
            case MovementType.Impulse:
                ApplyImpulse();
                break;

            case MovementType.Dash:
                ApplyDash();
                break;

            case MovementType.Teleport:
                ApplyTeleport();
                break;

            case MovementType.Launch:
                ApplyLaunch();
                break;
        }

        OnCompleted?.Invoke();
    }

    private void ApplyImpulse()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);

        if (movementSystem.Locomotion is ARPGLocomotionHandler arpg)
        {
            arpg.ApplyImpulse(worldDirection, speed);
        }
    }

    private void ApplyDash()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);

        if (movementSystem.Locomotion is ARPGLocomotionHandler arpg)
        {
            arpg.StartDash(worldDirection, speed, duration);
        }
        else
        {
            // Fallback to impulse
            ApplyImpulse();
        }
    }

    private void ApplyTeleport()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        Vector3 teleportPosition = casterTransform.position + worldDirection * teleportDistance;

        if (movementSystem.Locomotion is ARPGLocomotionHandler arpg)
        {
            arpg.TeleportTo(teleportPosition);
        }
    }

    private void ApplyLaunch()
    {
        Vector3 worldDirection = CalculateWorldDirection(casterTransform);

        if (movementSystem.Locomotion is ARPGLocomotionHandler arpg)
        {
            arpg.ApplyImpulse(worldDirection, speed);
        }
    }

    private Vector3 CalculateWorldDirection(Transform transform)
    {
        // Convert relative direction to world space
        if (direction == Vector3.forward)
        {
            // Forward dash - use caster's forward
            return transform.forward;
        }
        else if (direction == Vector3.back)
        {
            // Backward dodge
            return -transform.forward;
        }
        else
        {
            // Custom direction (side dash, etc)
            return transform.TransformDirection(direction).normalized;
        }
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}