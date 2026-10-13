using System;
using System.Collections.Generic;
using UnityEngine;

// Turns hit states into animation (Combat_Framework §5, roadmap ST3). The move decides the hit state
// (AbilityDefinition.ApplyHitState) and the status decides how long it lasts, so the reaction follows
// the status: a hit-state status applied plays its reaction, and the Reactions layer goes home the
// moment CannotAct drops. What you see is the real stun — a long reaction clip can no longer hide the
// next wind-up and tell once the fighter is free to act.
//
// A hit that applies no hit state (a projectile, an armoured target) plays no reaction.
// Death still comes from DamageSystem.
//
// Place on a "HitReaction_System" child of Component_Brain, alongside Damage_System.

namespace CrabSystem
{
    public class HitReactionSystem : MonoBehaviour, IBrainModule
    {
        const string RestState = "Rest";

        [Header("System State")]
        [SerializeField] private bool isEnabled = true;

        [Header("Rate Limit")]
        [Tooltip("Minimum seconds between reactions, so a multi-hit combo doesn't retrigger every frame.")]
        [SerializeField] private float minTimeBetweenReactions = 0.2f;

        [Header("Reaction Blend")]
        [Tooltip("Seconds the Reactions layer takes to blend home when the hit state ends.")]
        [SerializeField] private float restFadeSeconds = 0.1f;

        [Header("Animator Parameters")]
        [Tooltip("Played for a Flinch.")]
        [IdRef(IdKind.AnimatorParam)] [SerializeField] private string hitLightParam = "HitLight";
        [Tooltip("Played for Stagger, Guard Break, Knockdown and Launch until those have their own clips.")]
        [IdRef(IdKind.AnimatorParam)] [SerializeField] private string staggerParam = "Stagger";
        [IdRef(IdKind.AnimatorParam)] [SerializeField] private string deathParam = "Death";
        [IdRef(IdKind.AnimatorParam)] [SerializeField] private string isDeadParam = "IsDead";

        public bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }

        private ControllerBrain brain;

        // Gameplay time (EntityClock).
        private float Now => brain.Clock.Now;
        private float Delta => brain.Clock.Delta;
        private AnimationSystem anim;
        private DamageSystem damage;
        private StatusSystem statuses;
        private Blackboard blackboard;

        private readonly Dictionary<StatusDefinition, string> reactionByStatus = new();

        private float lastReactionTime = -999f;
        private bool isDead;

        // Fired with the trigger name that was played. VFX/SFX can hang off this.
        public event Action<string> OnReactionPlayed;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            anim = brain.Animation;
            damage = brain.Damage;
            statuses = brain.GetModule<StatusSystem>();
            blackboard = brain.Blackboard;

            if (damage == null)
            {
                isEnabled = false;
                Debug.LogError($"[HitReactionSystem] No DamageSystem on {brain.EntityName} — nothing to react to.", this);
                return;
            }

            if (anim == null)
            {
                isEnabled = false;
                Debug.LogError($"[HitReactionSystem] No AnimationSystem on {brain.EntityName} — nothing to drive.", this);
                return;
            }

            MapReactions();

            damage.OnDeath += HandleDeath;
            damage.OnRevived += ResetReactions;

            if (statuses != null)
            {
                statuses.OnStatusApplied += HandleStatus;
                statuses.OnStatusReapplied += HandleStatus;
            }

            if (blackboard != null) blackboard.OnBoolChanged += HandleFactChanged;
        }

        private void OnDestroy()
        {
            if (damage != null)
            {
                damage.OnDeath -= HandleDeath;
                damage.OnRevived -= ResetReactions;
            }

            if (statuses != null)
            {
                statuses.OnStatusApplied -= HandleStatus;
                statuses.OnStatusReapplied -= HandleStatus;
            }

            if (blackboard != null) blackboard.OnBoolChanged -= HandleFactChanged;
        }

        // The Reactions layer shows itself while a reaction plays and rests at 0 otherwise
        // (AnimationLayerController's rest rule), so there is no weight to manage here.
        public void UpdateModule() { }

        private void MapReactions()
        {
            AddReaction(HitState.Flinch, hitLightParam);
            AddReaction(HitState.Stagger, staggerParam);
            AddReaction(HitState.GuardBreak, staggerParam);
            AddReaction(HitState.Knockdown, staggerParam);
            AddReaction(HitState.Launch, staggerParam);
        }

        private void AddReaction(HitState state, string trigger)
        {
            StatusDefinition status = AbilityDefinition.LoadHitState(state);
            if (status != null) reactionByStatus[status] = trigger;
        }

        // ── Hit state → animation ─────────────────────────────────────────────

        private void HandleStatus(StatusInstance instance)
        {
            if (!isEnabled || isDead) return;
            if (!reactionByStatus.TryGetValue(instance.Definition, out string trigger)) return;
            if (Now - lastReactionTime < minTimeBetweenReactions) return;

            lastReactionTime = Now;
            anim.SetTrigger(trigger);
            OnReactionPlayed?.Invoke(trigger);
        }

        // The hit state is over: whatever the clip still has to play would hide the fighter's next move.
        // The layer is looked up here, not cached: a spawned fighter's model (and animator) arrives after
        // LateInitialize, when the lookup still returns -1 (Known Issues B10).
        private void HandleFactChanged(int key, bool value)
        {
            if (key != BlackboardKey.CannotAct || value || isDead) return;

            int layer = anim.GetLayerIndex(AnimationLayerNames.Reactions);
            if (layer < 0) return;

            anim.CrossFade(RestState, restFadeSeconds, layer);
        }

        private void HandleDeath()
        {
            if (isDead) return;
            isDead = true;

            anim.SetBool(isDeadParam, true);
            anim.SetTrigger(deathParam);
        }

        // ── Respawn ───────────────────────────────────────────────────────────

        // Brought back (DamageSystem.OnRevived): clears the death pose and the rate limit. AnimationSystem skips
        // parameters the controller lacks, so these writes need no guard.
        public void ResetReactions()
        {
            isDead = false;
            lastReactionTime = -999f;
            anim.SetBool(isDeadParam, false);
        }
    }
}
