using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// World state snapshot passed to GOAP goals each tick.
/// Holds spatial data and live module references. Semantic facts
/// (IsWounded, LowMana, etc.) are read directly from brain.Blackboard.
/// </summary>
public class GOAPContext
{
    public ControllerBrain brain;
    public Transform self;
    public Transform target;

    // Spatial — computed fresh each tick, not a Blackboard concern
    public Vector3 selfPosition;
    public Vector3 targetPosition;
    public Vector3 toTarget;
    public float distanceToTarget;
    public float angleToTarget;

    // Tactical state — kept until PerceptionModule writes HasAlliesNearby to Blackboard
    public bool hasAlliesNearby;
    public int allyCount;

    // Module references — action handles, not facts
    public IAbilityProvider abilityModule;
    public MovementSystem movementSystem;
    public IHealthProvider healthModule;
    public IResourceProvider resourceModule;
    public PerceptionModule perception;
    public AIControlSource aiControl;

    // Unified action lock gate — GOAP never asks *why* it's locked, only *if* it is.
    public bool IsActionLocked
    {
        get
        {
            if (movementSystem != null)
            {
                var source = movementSystem.ActiveControlSource;
                if (source == null || !source.IsActive)
                    return true;
            }
            return false;
        }
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        self = brain.transform;

        abilityModule  = brain.Abilities;
        movementSystem = brain.Movement;
        healthModule   = brain.GetProvider<IHealthProvider>();
        resourceModule = brain.GetProvider<IResourceProvider>();
        perception     = brain.GetModule<PerceptionModule>();
        aiControl      = brain.GetModule<AIControlSource>();
    }

    public void UpdateContext()
    {
        target = perception != null ? perception.CurrentTarget : null;

        if (target != null)
        {
            selfPosition     = self.position;
            targetPosition   = target.position;
            toTarget         = targetPosition - selfPosition;
            distanceToTarget = toTarget.magnitude;
            angleToTarget    = Vector3.Angle(self.forward, toTarget);
        }
        else
        {
            distanceToTarget = float.MaxValue;
            angleToTarget    = 0f;
        }

        // Ally detection — to be replaced once PerceptionModule writes HasAlliesNearby to Blackboard
        Collider[] nearby = Physics.OverlapSphere(self.position, 10f, LayerMask.GetMask("Enemy"));
        allyCount      = Mathf.Max(0, nearby.Length - 1);
        hasAlliesNearby = allyCount > 0;
    }
}
