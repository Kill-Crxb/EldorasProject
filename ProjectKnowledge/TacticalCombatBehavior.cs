using UnityEngine;

namespace RPG.Tactical
{
    /// <summary>
    /// Base class for combat behaviors with tactical AI and threat assessment.
    /// Integrates with TacticalPositioningSystem for intelligent positioning.
    /// 
    /// Features:
    /// - 7 tactical states (Observing, Engaging, Defensive, Aggressive, Retreating, Flanking, Recovering)
    /// - Dynamic threat assessment system
    /// - Integration with tactical positioning
    /// - Health and stamina awareness
    /// - State transition management
    /// </summary>
    public abstract class TacticalCombatBehavior : AICombatBehaviorModule, ITacticalConfiguration
    {
        [Header("Tactical Settings")]
        [SerializeField] protected float observationChance = 0.3f;
        [SerializeField] protected float observationDuration = 3f;
        [SerializeField] protected float retreatHealthPercent = 0.2f;
        [SerializeField] protected float defensiveHealthPercent = 0.4f;
        [SerializeField] protected float aggressiveTargetHealthPercent = 0.3f;
        [SerializeField] protected float recoveryStaminaPercent = 0.2f;
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

            if (healthProvider == null)
            {
                Debug.LogWarning($"[TacticalCombatBehavior] No IHealthProvider found on {gameObject.name}");
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

        /// <summary>
        /// Override this in derived classes to implement tactical behaviors
        /// </summary>
        protected abstract void ExecuteTacticalBehavior(Transform target);

        #endregion

        #region Tactical State Management

        public virtual void UpdateTacticalState()
        {
            // Get current target from AI module
            Transform target = brain?.GetModule<AISystem>()?.CurrentTarget;


            if (target == null)
            {
                CurrentTacticalState = TacticalState.Observing;
                return;
            }

            float healthPercent = GetHealthPercent();
            float targetHealthPercent = GetTargetHealthPercent();
            float staminaPercent = GetStaminaPercent();

            TacticalState previousState = CurrentTacticalState;

            // Retreating: very low health, survival priority
            if (healthPercent < retreatHealthPercent)
            {
                CurrentTacticalState = TacticalState.Retreating;
            }
            // Defensive: low health, cautious approach
            else if (healthPercent < defensiveHealthPercent)
            {
                CurrentTacticalState = TacticalState.Defensive;
            }
            // Recovering: low stamina, need to back off
            else if (staminaPercent < recoveryStaminaPercent)
            {
                CurrentTacticalState = TacticalState.Recovering;
            }
            // Aggressive: target is weak, press advantage
            else if (targetHealthPercent < aggressiveTargetHealthPercent)
            {
                CurrentTacticalState = TacticalState.Aggressive;
            }
            // Flanking: random tactical repositioning
            else if (Random.value < flankingChance)
            {
                CurrentTacticalState = TacticalState.Flanking;
            }
            // Observing: study target, circle
            else if (Random.value < observationChance && Time.time - stateEnterTime > observationDuration)
            {
                CurrentTacticalState = TacticalState.Observing;
            }
            // Default: Engaging
            else if (CurrentTacticalState != TacticalState.Observing &&
                     CurrentTacticalState != TacticalState.Flanking)
            {
                CurrentTacticalState = TacticalState.Engaging;
            }

            // Track state changes
            if (CurrentTacticalState != previousState)
            {
                stateEnterTime = Time.time;
                OnTacticalStateChanged(previousState, CurrentTacticalState);
            }

            // Update threat level
            UpdateThreatLevel();
        }

        protected virtual void OnTacticalStateChanged(TacticalState from, TacticalState to)
        {
            if (debugTactical)
            {
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} state: {from} → {to}");
            }
        }

        protected void UpdateThreatLevel()
        {
            // Decay threat over time
            ThreatLevel = Mathf.Max(0f, ThreatLevel - threatDecayRate * Time.deltaTime);
            ThreatLevel = Mathf.Clamp01(ThreatLevel);
        }

        #endregion

        #region Positioning System Integration

        protected virtual void UpdateTacticalPoint(Transform target)
        {
            // Determine desired role based on current tactical state
            TacticalRole desiredRole = GetDesiredRole();

            // Get point preference based on tactical state
            PointPreference preference = GetPointPreference();

            // Request point from positioning system
            TacticalPoint requestedPoint = targetPositioningSystem.RequestPoint(
                gameObject,
                desiredRole,
                preference,
                entityPriority
            );

            // Update assignment
            if (requestedPoint != assignedPoint)
            {
                assignedPoint = requestedPoint;
                currentRole = desiredRole;

                if (debugTactical && assignedPoint != null)
                {
                    Debug.Log($"[TacticalCombatBehavior] {gameObject.name} assigned to {assignedPoint.Direction}/{assignedPoint.Ring}");
                }
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
                TacticalState.Flanking => new PointPreference
                {
                    AllowAnyDirection = true,
                    PreferredRing = PointRing.Outer,
                    PreferEmpty = true
                },
                TacticalState.Retreating => new PointPreference
                {
                    PreferredDirection = PointDirection.Back,
                    PreferredRing = PointRing.Outer,
                    AllowAnyDirection = false
                },
                _ => PointPreference.Any
            };
        }

        #endregion

        #region Damage & Threat Callbacks

        /// <summary>
        /// Called when this entity takes damage - increases threat level
        /// </summary>
        public virtual void OnDamageTaken(int damage)
        {
            damageReceivedThisLife += damage;
            ThreatLevel = Mathf.Min(1f, ThreatLevel + threatIncreasePerHit);

            if (debugTactical)
            {
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} took {damage} damage. Threat: {ThreatLevel:F2}");
            }
        }

        #endregion

        #region Helper Methods

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

            // Try to get target's health provider
            if (targetHealthProvider == null)
            {
                var targetBrain = target.GetComponent<ControllerBrain>();
                if (targetBrain != null)
                {
                    targetHealthProvider = targetBrain.GetModuleImplementing<IHealthProvider>();
                }
            }

            if (targetHealthProvider == null) return 1f;

            float current = targetHealthProvider.GetCurrentHealth();
            float max = targetHealthProvider.GetMaxHealth();

            return max > 0 ? current / max : 1f;
        }

        protected float GetStaminaPercent()
        {
            if (resourceProvider == null) return 1f;

            float current = resourceProvider.GetResource(ResourceType.Stamina);
            float max = resourceProvider.GetMaxResource(ResourceType.Stamina);

            return max > 0 ? current / max : 1f;
        }

        protected bool HasReachedPoint(TacticalPoint point, float threshold = 1f)
        {
            if (point == null) return false;
            return Vector3.Distance(transform.position, point.WorldPosition) <= threshold;
        }

        #endregion

        #region Combat Enter/Exit

        public override void OnCombatEnter(Transform target)
        {
            base.OnCombatEnter(target);

            // Reset threat and damage tracking
            ThreatLevel = baseThreatLevel;
            damageReceivedThisLife = 0;
            stateEnterTime = Time.time;
            CurrentTacticalState = TacticalState.Observing;

            // Clear positioning assignments
            assignedPoint = null;
            targetHealthProvider = null;
            targetPositioningSystem = null;

            if (debugTactical)
            {
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} entered combat with {target.name}");
            }
        }

        public override void OnCombatExit()
        {
            base.OnCombatExit();

            // Release tactical point
            if (usePositioningSystem && targetPositioningSystem != null && assignedPoint != null)
            {
                targetPositioningSystem.ReleasePoint(gameObject);
                assignedPoint = null;
            }

            if (debugTactical)
            {
                Debug.Log($"[TacticalCombatBehavior] {gameObject.name} exited combat");
            }
        }

        #endregion
    }
}