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
    private IMovementProvider movement;

    public void SetMovementProvider(IMovementProvider provider)
    {
        movement = provider;
    }

    public void Apply(IMovementProvider target)
    {
        movement = target;

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
        Transform casterTransform = (movement as MonoBehaviour)?.transform;
        if (casterTransform == null) return;

        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        movement.ApplyImpulse(worldDirection, speed);
    }

    private void ApplyDash()
    {
        // Get LocomotionModule to use proper dash system
        if (movement is LocomotionModule locomotion)
        {
            Transform casterTransform = locomotion.transform;
            Vector3 worldDirection = CalculateWorldDirection(casterTransform);

            locomotion.StartDash(worldDirection, speed, duration);
        }
        else
        {
            // Fallback to impulse if not LocomotionModule
            ApplyImpulse();
        }
    }

    private void ApplyTeleport()
    {
        Transform casterTransform = (movement as MonoBehaviour)?.transform;
        if (casterTransform == null) return;

        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        Vector3 teleportPosition = casterTransform.position + worldDirection * teleportDistance;
        movement.TeleportTo(teleportPosition);
    }

    private void ApplyLaunch()
    {
        Transform casterTransform = (movement as MonoBehaviour)?.transform;
        if (casterTransform == null) return;

        Vector3 worldDirection = CalculateWorldDirection(casterTransform);
        movement.ApplyImpulse(worldDirection, speed);
    }

    private Vector3 CalculateWorldDirection(Transform casterTransform)
    {
        // Convert relative direction to world space
        if (direction == Vector3.forward)
        {
            // Forward dash - use caster's forward
            return casterTransform.forward;
        }
        else if (direction == Vector3.back)
        {
            // Backward dodge
            return -casterTransform.forward;
        }
        else
        {
            // Custom direction (side dash, etc)
            return casterTransform.TransformDirection(direction).normalized;
        }
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}