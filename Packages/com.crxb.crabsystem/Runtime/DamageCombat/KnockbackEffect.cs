using System;
using UnityEngine;

// Knockback Effect - Applies force/impulse to knock back targets
//
// Usage:
// - Called by abilities to knock back enemies
// - Works through MovementSystem and LocomotionHandler
// - Direction can be relative to attacker or absolute
//
// Example:
// var effect = new KnockbackEffect
// {
//     force = 10f,
//     direction = Vector3.back // Relative to attacker
// };
// effect.SetAttacker(attackerTransform);
// effect.Apply(targetGameObject);
//
// Updated: January 2026 - Supports ControllerBrain/MovementSystem

namespace CrabSystem
{
    [System.Serializable]
    public class KnockbackEffect
    {
        [Header("Knockback Configuration")]
        [Tooltip("Force of the knockback")]
        public float force = 10f;

        [Tooltip("Direction of knockback in the attacker's frame. +Z pushes away from the attacker, +Y lifts.")]
        public Vector3 direction = Vector3.forward;

        [Tooltip("Use direction relative to attacker? (false = world space direction)")]
        public bool useRelativeDirection = true;

        // Fired when effect completes
        public event Action OnCompleted;

        [System.NonSerialized]
        private Transform attackerTransform;

        // Set the attacker transform (for relative direction)
        public void SetAttacker(Transform attacker)
        {
            attackerTransform = attacker;
        }

        // Apply knockback to GameObject (modern - works with ControllerBrain)
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

        // Apply knockback to ControllerBrain entity
        private void ApplyToBrain(ControllerBrain brain)
        {
            var movementSystem = brain.GetModule<MovementSystem>();
            if (movementSystem == null)
            {
                Debug.LogWarning($"[KnockbackEffect] No MovementSystem on {brain.EntityName}");
                return;
            }

            // ⚠ This used to be `movementSystem.Locomotion as ARPGLocomotionHandler`. Slice 1 swapped the
            // player onto ParkourLocomotionHandler, the cast started returning null, and knockback has
            // been warning-and-returning on every player hit since. ApplyImpulse now lives on
            // LocomotionHandler — call it through the base type and do not cast to a concrete handler.
            var locomotionHandler = movementSystem.Locomotion;
            if (locomotionHandler == null)
            {
                Debug.LogWarning($"[KnockbackEffect] No LocomotionHandler on {brain.EntityName}");
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

        // Apply knockback to IDamageable target (legacy compatibility)
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

        // Cancel the effect (for interruptible abilities)
        public void Cancel()
        {
            OnCompleted?.Invoke();
        }
    }
}
