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
            var locomotion = mb.GetComponent<LocomotionModule>();
            if (locomotion != null && attackerTransform != null)
            {
                Vector3 knockbackDir = attackerTransform.TransformDirection(direction);
                locomotion.ApplyImpulse(knockbackDir, force);
            }
        }

        OnCompleted?.Invoke();
    }

    public void Cancel()
    {
        OnCompleted?.Invoke();
    }
}