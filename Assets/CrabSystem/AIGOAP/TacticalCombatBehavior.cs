using System.Collections.Generic;
using UnityEngine;

namespace RPG.Tactical
{
    /// <summary>
    /// Fully dynamic Tactical Combat Behavior
    /// - Data-driven: no hardcoded resources
    /// - Caches all ResourceDefinitions from ResourceManager
    /// - GOAP/AI goals can use any resource dynamically
    /// - Threat, health, stamina, or any custom resource percentages are fully generic
    /// </summary>
    public abstract class TacticalCombatBehavior : AICombatBehaviorModule, ITacticalConfiguration
    {
        [Header("Tactical Settings")]
        [SerializeField] protected float observationChance = 0.3f;
        [SerializeField] protected float observationDuration = 3f;
        [SerializeField] protected float retreatHealthPercent = 0.2f;
        [SerializeField] protected float defensiveHealthPercent = 0.4f;
        [SerializeField] protected float aggressiveTargetHealthPercent = 0.3f;
        [SerializeField] protected float recoveryResourcePercent = 0.2f; // generic threshold for any resource
        [SerializeField] protected float flankingChance = 0.05f;

        [Header("Threat Assessment")]
        [SerializeField] protected float baseThreatLevel = 0.5f;
        [SerializeField] protected float threatIncreasePerHit = 0.1f;
        [SerializeField] protected float threatDecayRate = 0.05f;

        [Header("Positioning")]
        [SerializeField] protected bool usePositioningSystem = true;
        [SerializeField] protected int entityPriority = 0;

        [Header("Debug")]
        [SerializeField] protected bool debugTactical = false;

        // Tactical state
        public TacticalState CurrentTacticalState { get; protected set; }
        public float ThreatLevel { get; protected set; }

        // Positioning
        protected TacticalPositioningSystem targetPositioningSystem;
        protected TacticalPoint assignedPoint;
        protected TacticalRole currentRole;

        // Target access
        protected Transform currentTarget => brain?.GetModule<AISystem>()?.CurrentTarget;

        // Providers
        protected IHealthProvider healthProvider;
        protected IHealthProvider targetHealthProvider;
        protected IResourceProvider resourceProvider;

        // Resource caches
        protected Dictionary<string, ResourceDefinition> resourceDefs = new();
        protected Dictionary<string, float> currentResources = new();
        protected Dictionary<string, float> maxResources = new();
        protected Dictionary<string, float> resourcePercent = new();

        // State tracking
        protected float stateEnterTime;
        protected int damageReceivedThisLife;
        protected Vector3 lastKnownTargetPosition;

        #region Initialization

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // Get providers
            healthProvider = brain.GetModuleImplementing<IHealthProvider>();
            resourceProvider = brain.GetModuleImplementing<IResourceProvider>();

            // Cache all ResourceDefinitions dynamically
            if (resourceProvider != null)
            {
                foreach (var def in ResourceManager.Instance.GetAll())
                    resourceDefs[def.name] = def;
            }

            // Initialize state
            CurrentTacticalState = TacticalState.Observing;
            ThreatLevel = baseThreatLevel;
            stateEnterTime = Time.time;
            damageReceivedThisLife = 0;

            if (debugTactical)
            {
                Debug.Log($"[TacticalCombatBehavior] Initialized on {gameObject.name}");
            }
        }

        #endregion

        #region Combat Update

        public override void UpdateCombat(Transform target)
        {
            if (!isEnabled || target == null) return;

            // Cache target position
            lastKnownTargetPosition = target.position;

            // Find/cache positioning system on target
            if (usePositioningSystem && targetPositioningSystem == null)
            {
                targetPositioningSystem = target.GetComponent<TacticalPositioningSystem>();

                if (targetPositioningSystem == null && debugTactical)
                {
                    Debug.LogWarning($"[TacticalCombatBehavior] No TacticalPositioningSystem on target {target.name}");
                }
            }

            // Update resource cache
            UpdateResourceCache();

            // Update tactical state
            UpdateTacticalState();

            // Request/maintain tactical point
            if (usePositioningSystem && targetPositioningSystem != null)
            {
                UpdateTacticalPoint(target);
            }

            // Execute behavior based on current tactical state
            ExecuteTacticalBehavior(target);
        }

        protected abstract void ExecuteTacticalBehavior(Transform target);

        #endregion

        #region Resource Management

        /// <summary>
        /// Updates all cached resource values for this entity
        /// </summary>
        protected void UpdateResourceCache()
        {
            if (resourceProvider == null) return;

            foreach (var kvp in resourceDefs)
            {
                var def = kvp.Value;
                float current = resourceProvider.GetResource(def);
                float max = resourceProvider.GetMaxResource(def);
                float percent = max > 0f ? current / max : 0f;

                currentResources[def.name] = current;
                maxResources[def.name] = max;
                resourcePercent[def.name] = percent;
            }
        }

        /// <summary>
        /// Returns the current normalized percentage of any resource
        /// </summary>
        protected float GetResourcePercent(string resourceName, float fallback = 1f)
        {
            if (resourcePercent.TryGetValue(resourceName, out var value))
                return value;
            return fallback;
        }

        #endregion

        #region Tactical State Management

        public virtual void UpdateTacticalState()
        {
            Transform target = brain?.GetModule<AISystem>()?.CurrentTarget;
            if (target == null)
            {
                CurrentTacticalState = TacticalState.Observing;
                return;
            }

            float healthPercent = GetHealthPercent();
            float targetHealthPercent = GetTargetHealthPercent();

            // Example: dynamically pick a key resource for recovery (can be configured per entity/goal)
            float anyResourcePercent = 1f;
            if (resourceDefs.Count > 0)
            {
                foreach (var key in resourceDefs.Keys)
                {
                    float percent = GetResourcePercent(key, 1f);
                    if (percent < recoveryResourcePercent)
                    {
                        anyResourcePercent = percent;
                        break;
                    }
                }
            }

            TacticalState previousState = CurrentTacticalState;

            // Decision-making dynamically
            if (healthPercent < retreatHealthPercent)
                CurrentTacticalState = TacticalState.Retreating;
            else if (healthPercent < defensiveHealthPercent)
                CurrentTacticalState = TacticalState.Defensive;
            else if (anyResourcePercent < recoveryResourcePercent)
                CurrentTacticalState = TacticalState.Recovering;
            else if (targetHealthPercent < aggressiveTargetHealthPercent)
                CurrentTacticalState = TacticalState.Aggressive;
            else if (Random.value < flankingChance)
                CurrentTacticalState = TacticalState.Flanking;
            else if (Random.value < observationChance && Time.time - stateEnterTime > observationDuration)
                CurrentTacticalState = TacticalState.Observing;
            else if (CurrentTacticalState != TacticalState.Observing &&
                     CurrentTacticalState != TacticalState.Flanking)
                CurrentTacticalState = TacticalState.Engaging;

            if (CurrentTacticalState != previousState)
            {
                stateEnterTime = Time.time;
                OnTacticalStateChanged(previousState, CurrentTacticalState);
            }

            UpdateThreatLevel();
        }

        protected virtual void OnTacticalStateChanged(TacticalState from, TacticalState to)
        {
            if (debugTactical)
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} state: {from} → {to}");
        }

        protected void UpdateThreatLevel()
        {
            ThreatLevel = Mathf.Max(0f, ThreatLevel - threatDecayRate * Time.deltaTime);
            ThreatLevel = Mathf.Clamp01(ThreatLevel);
        }

        #endregion

        #region Health Helpers

        protected float GetHealthPercent()
        {
            if (healthProvider == null) return 1f;

            float current = healthProvider.GetCurrentHealth();
            float max = healthProvider.GetMaxHealth();

            return max > 0 ? current / max : 1f;
        }

        protected float GetTargetHealthPercent()
        {
            Transform target = brain?.GetModule<AISystem>()?.CurrentTarget;
            if (target == null) return 1f;

            if (targetHealthProvider == null)
            {
                var targetBrain = target.GetComponent<ControllerBrain>();
                if (targetBrain != null)
                    targetHealthProvider = targetBrain.GetModuleImplementing<IHealthProvider>();
            }

            if (targetHealthProvider == null) return 1f;

            float current = targetHealthProvider.GetCurrentHealth();
            float max = targetHealthProvider.GetMaxHealth();

            return max > 0 ? current / max : 1f;
        }

        #endregion

        #region Positioning Integration

        protected virtual void UpdateTacticalPoint(Transform target)
        {
            TacticalRole desiredRole = GetDesiredRole();
            PointPreference preference = GetPointPreference();

            TacticalPoint requestedPoint = targetPositioningSystem.RequestPoint(
                gameObject,
                desiredRole,
                preference,
                entityPriority
            );

            if (requestedPoint != assignedPoint)
            {
                assignedPoint = requestedPoint;
                currentRole = desiredRole;

                if (debugTactical && assignedPoint != null)
                    Debug.Log($"[TacticalCombatBehavior] {gameObject.name} assigned to {assignedPoint.Direction}/{assignedPoint.Ring}");
            }
        }

        protected virtual TacticalRole GetDesiredRole()
        {
            return CurrentTacticalState switch
            {
                TacticalState.Observing => TacticalRole.Circling,
                TacticalState.Engaging => TacticalRole.ActiveFighter,
                TacticalState.Defensive => TacticalRole.Waiting,
                TacticalState.Aggressive => TacticalRole.Pressuring,
                TacticalState.Retreating => TacticalRole.Retreating,
                TacticalState.Flanking => TacticalRole.Flanking,
                TacticalState.Recovering => TacticalRole.Waiting,
                _ => TacticalRole.Circling
            };
        }

        protected virtual PointPreference GetPointPreference()
        {
            return CurrentTacticalState switch
            {
                TacticalState.Flanking => new PointPreference { AllowAnyDirection = true, PreferredRing = PointRing.Outer, PreferEmpty = true },
                TacticalState.Retreating => new PointPreference { PreferredDirection = PointDirection.Back, PreferredRing = PointRing.Outer, AllowAnyDirection = false },
                _ => PointPreference.Any
            };
        }

        #endregion

        #region Damage & Threat

        public virtual void OnDamageTaken(int damage)
        {
            damageReceivedThisLife += damage;
            ThreatLevel = Mathf.Min(1f, ThreatLevel + threatIncreasePerHit);

            if (debugTactical)
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} took {damage} damage. Threat: {ThreatLevel:F2}");
        }

        #endregion

        #region Combat Enter/Exit

        public override void OnCombatEnter(Transform target)
        {
            base.OnCombatEnter(target);

            ThreatLevel = baseThreatLevel;
            damageReceivedThisLife = 0;
            stateEnterTime = Time.time;
            CurrentTacticalState = TacticalState.Observing;

            assignedPoint = null;
            targetHealthProvider = null;
            targetPositioningSystem = null;

            if (debugTactical)
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} entered combat with {target.name}");
        }

        public override void OnCombatExit()
        {
            base.OnCombatExit();

            if (usePositioningSystem && targetPositioningSystem != null && assignedPoint != null)
            {
                targetPositioningSystem.ReleasePoint(gameObject);
                assignedPoint = null;
            }

            if (debugTactical)
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} exited combat");
        }

        #endregion
    }
}
