using System;
using UnityEngine;

/// <summary>
/// Turns damage into animation. Subscribes to DamageSystem's existing events and
/// drives the Animator through AnimationSystem — no polling, no direct Animator access,
/// no coupling between the damage side and the animation side.
///
/// Wiring follows the established module pattern:
///   Initialize      — cache the brain only
///   LateInitialize  — resolve sibling modules and subscribe (other modules exist by now)
///   OnDestroy       — unsubscribe
///
/// Place on a "HitReaction_System" child of Component_Brain, alongside Damage_System.
/// </summary>
public class HitReactionSystem : MonoBehaviour, IBrainModule
{
    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Header("Severity Thresholds (fraction of max health)")]
    [SerializeField] private float heavyHitFraction = 0.15f;
    [SerializeField] private float staggerFraction = 0.30f;

    [Header("Rate Limit")]
    [Tooltip("Minimum seconds between reactions, so a multi-hit combo doesn't retrigger every frame.")]
    [SerializeField] private float minTimeBetweenReactions = 0.2f;

    [Header("Animator Parameters")]
    [SerializeField] private string hitLightParam = "HitLight";
    [SerializeField] private string hitHeavyParam = "HitHeavy";
    [SerializeField] private string staggerParam = "Stagger";
    [SerializeField] private string deathParam = "Death";
    [SerializeField] private string isDeadParam = "IsDead";
    [SerializeField] private string hitDirXParam = "HitDirX";
    [SerializeField] private string hitDirZParam = "HitDirZ";

    [Header("Reaction Layer")]
    [Tooltip("Animator layer the reaction clips live on. Leave blank to drive the base layer only.")]
    [SerializeField] private string reactionLayerName = "Full Body Actions";
    [SerializeField] private float layerFadeIn = 0.05f;
    [SerializeField] private float layerFadeOut = 0.15f;
    [Tooltip("How long the reaction layer stays at full weight after a hit.")]
    [SerializeField] private float reactionDuration = 0.4f;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private AnimationSystem anim;
    private DamageSystem damage;
    private IHealthProvider health;
    private Transform facing;

    private int reactionLayer = -1;
    private float lastReactionTime = -999f;
    private float reactionEndTime;
    private bool layerRaised;
    private bool isDead;

    /// <summary>Fired with the trigger name that was played. VFX/SFX can hang off this.</summary>
    public event Action<string> OnReactionPlayed;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        anim = brain.Animation;
        damage = brain.Damage;
        health = brain.ResourceSys;
        facing = brain.EntityRoot != null ? brain.EntityRoot : transform;

        if (damage == null)
        {
            isEnabled = false;
            Debug.LogError($"[HitReactionSystem] No DamageSystem on {brain.name} — nothing to react to.", this);
            return;
        }

        if (anim == null)
        {
            isEnabled = false;
            Debug.LogError($"[HitReactionSystem] No AnimationSystem on {brain.name} — nothing to drive.", this);
            return;
        }

        reactionLayer = string.IsNullOrEmpty(reactionLayerName) ? -1 : anim.GetLayerIndex(reactionLayerName);

        if (reactionLayer < 0 && !string.IsNullOrEmpty(reactionLayerName))
            Debug.LogWarning($"[HitReactionSystem] Layer '{reactionLayerName}' not found on {brain.name}'s controller — reactions will play on the base layer.", this);

        damage.OnDamageTaken += HandleDamageTaken;
        damage.OnDeath += HandleDeath;
    }

    private void OnDestroy()
    {
        if (damage == null) return;
        damage.OnDamageTaken -= HandleDamageTaken;
        damage.OnDeath -= HandleDeath;
    }

    public void UpdateModule()
    {
        if (!layerRaised) return;
        if (Time.time < reactionEndTime) return;
        LowerLayer();
    }

    // ── Damage → animation ────────────────────────────────────────────────

    private void HandleDamageTaken(CombatDamagePacket packet)
    {
        if (!isEnabled || isDead) return;
        if (Time.time - lastReactionTime < minTimeBetweenReactions) return;

        lastReactionTime = Time.time;

        SetHitDirection(packet.attackDirection);

        string trigger = PickTrigger(packet.finalDamage);
        SetTrigger(trigger);
        RaiseLayer();

        OnReactionPlayed?.Invoke(trigger);
    }

    private void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        SetBool(isDeadParam, true);
        SetTrigger(deathParam);

        RaiseLayer();
        reactionEndTime = float.MaxValue; // death pose holds until respawn
    }

    private string PickTrigger(float incomingDamage)
    {
        float max = health != null ? health.GetMaxHealth() : 0f;
        if (max <= 0f) return hitLightParam;

        float fraction = incomingDamage / max;
        if (fraction >= staggerFraction) return staggerParam;
        if (fraction >= heavyHitFraction) return hitHeavyParam;
        return hitLightParam;
    }

    private void SetHitDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude < 0.0001f) return;

        Vector3 local = facing.InverseTransformDirection(worldDirection.normalized);
        SetFloat(hitDirXParam, local.x);
        SetFloat(hitDirZParam, local.z);
    }

    // ── Layer weight ──────────────────────────────────────────────────────

    private void RaiseLayer()
    {
        reactionEndTime = Time.time + reactionDuration;
        if (reactionLayer < 0 || layerRaised) return;

        anim.FadeLayerWeight(reactionLayer, 1f, layerFadeIn);
        layerRaised = true;
    }

    private void LowerLayer()
    {
        layerRaised = false;
        if (reactionLayer < 0) return;
        anim.FadeLayerWeight(reactionLayer, 0f, layerFadeOut);
    }

    // ── Guarded animator writes (a missing parameter logs once, not every frame) ──

    private void SetTrigger(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        if (!anim.HasParameter(parameterName)) return;
        anim.SetTrigger(parameterName);
    }

    private void SetBool(string parameterName, bool value)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        if (!anim.HasParameter(parameterName)) return;
        anim.SetBool(parameterName, value);
    }

    private void SetFloat(string parameterName, float value)
    {
        if (string.IsNullOrEmpty(parameterName)) return;
        if (!anim.HasParameter(parameterName)) return;
        anim.SetFloat(parameterName, value);
    }

    // ── Respawn ───────────────────────────────────────────────────────────

    /// <summary>Call when the entity is brought back — clears the death pose and the rate limit.</summary>
    public void ResetReactions()
    {
        isDead = false;
        lastReactionTime = -999f;
        reactionEndTime = 0f;
        SetBool(isDeadParam, false);
        LowerLayer();
    }
}
