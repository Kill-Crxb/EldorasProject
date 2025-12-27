using System;
using UnityEngine;

[System.Serializable]
public class KnockbackEffect
{
    [Header("Knockback Configuration")]
    public float force = 10f;
    public Vector3 direction = Vector3.back;

    public event Action OnCompleted;

    [System.NonSerialized]
    private Transform attackerTransform;

    public void SetAttacker(Transform attacker)
    {
        attackerTransform = attacker;
    }

    public void Apply(IDamageable target)
    {
        if (target is MonoBehaviour mb)
        {
            // Get ControllerBrain to access movement system
            var brain = mb.GetComponent<ControllerBrain>();
            if (brain == null)
                brain = mb.GetComponentInParent<ControllerBrain>();

            if (brain != null && attackerTransform != null)
            {
                var movementSystem = brain.GetModule<MovementSystem>();
                if (movementSystem != null)
                {
                    var locomotionHandler = movementSystem.Locomotion as ARPGLocomotionHandler;
                    if (locomotionHandler != null)
                    {
                        Vector3 knockbackDir = attackerTransform.TransformDirection(direction);
                        locomotionHandler.ApplyImpulse(knockbackDir, force);
                    }
                }
            }
        }

        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}