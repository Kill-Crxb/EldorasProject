using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GOAP Context - World state representation for goal evaluation
/// Fully dynamic, module-driven, refactor-safe
/// </summary>
public class GOAPContext
{
    #region References
    public ControllerBrain brain;
    public Transform self;
    public Transform target;
    #endregion

    #region Spatial Data
    public Vector3 selfPosition;
    public Vector3 targetPosition;
    public Vector3 toTarget;
    public float distanceToTarget;
    public float angleToTarget;
    #endregion

    #region Health & Resources
    public float currentHealth;
    public float maxHealth;
    public float healthPercent;

    public Dictionary<string, float> currentResources = new();
    public Dictionary<string, float> maxResources = new();
    public Dictionary<string, float> resourcePercent = new();
    #endregion

    #region Tactical State
    public bool hasAlliesNearby;
    public int allyCount;
    #endregion

    #region Module References
    public IAbilityProvider abilityModule;
    public MovementSystem movementSystem;
    public IHealthProvider healthModule;
    public IResourceProvider resourceModule;
    public PathfindingModule pathfinding;
    public PerceptionModule perception;
    #endregion

    #region Resource Cache
    private Dictionary<string, ResourceDefinition> resourceDefs = new();
    private ResourceDefinition healthDef;
    #endregion

    #region Derived State (IMPORTANT)


    /// <summary>
    /// Unified action lock gate for GOAP.
    /// GOAP never asks *why* it's locked — only *if* it is.
    /// </summary>
    public bool IsActionLocked
    {
        get
        {
            // If movement system cannot accept input, we are locked
            if (movementSystem != null)
            {
                var source = movementSystem.ActiveControlSource;
                if (source == null || !source.IsActive)
                    return true;
            }

            return false;
        }
    }


    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        self = brain.transform;

        abilityModule = brain.Abilities;
        movementSystem = brain.Movement;
        healthModule = brain.GetModuleImplementing<IHealthProvider>();
        resourceModule = brain.GetModuleImplementing<IResourceProvider>();
        pathfinding = brain.GetModule<PathfindingModule>();
        perception = brain.GetModule<PerceptionModule>();

        // Cache all resource definitions dynamically
        foreach (var def in ResourceManager.Instance.GetAll())
        {
            resourceDefs[def.name] = def;
        }

        // Identify health resource dynamically
        foreach (var def in resourceDefs.Values)
        {
            if (def.name.Equals("Health", StringComparison.OrdinalIgnoreCase))
            {
                healthDef = def;
                break;
            }
        }
    }

    #endregion

    #region Update

    public void UpdateContext()
    {
        // Target update
        target = perception != null ? perception.CurrentTarget : null;

        // Spatial data
        if (target != null)
        {
            selfPosition = self.position;
            targetPosition = target.position;
            toTarget = targetPosition - selfPosition;
            distanceToTarget = toTarget.magnitude;
            angleToTarget = Vector3.Angle(self.forward, toTarget);
        }
        else
        {
            distanceToTarget = float.MaxValue;
            angleToTarget = 0f;
        }

        // Health
        if (resourceModule != null && healthDef != null)
        {
            currentHealth = resourceModule.GetResource(healthDef);
            maxHealth = resourceModule.GetMaxResource(healthDef);
            healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;
        }

        // All resources
        if (resourceModule != null)
        {
            foreach (var def in resourceDefs.Values)
            {
                float current = resourceModule.GetResource(def);
                float max = resourceModule.GetMaxResource(def);

                currentResources[def.name] = current;
                maxResources[def.name] = max;
                resourcePercent[def.name] = max > 0 ? current / max : 0f;
            }
        }

        // Allies
        Collider[] nearby = Physics.OverlapSphere(self.position, 10f, LayerMask.GetMask("Enemy"));
        allyCount = Mathf.Max(0, nearby.Length - 1);
        hasAlliesNearby = allyCount > 0;
    }

    #endregion
}
