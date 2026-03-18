using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Effect Manager Module - Manages active effects on an entity
/// </summary>
public class EffectManagerModule : MonoBehaviour, IBrainModule
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Debug")]
    [SerializeField] private bool debugEffects = false;

    private ControllerBrain brain;

    private readonly List<DamageEffect> activeInstantDamageEffects = new();
    private readonly List<DamageOverTimeEffect> activeDoTs = new();
    private readonly List<HealEffect> activeInstantHealEffects = new();
    private readonly List<HealOverTimeEffect> activeHoTs = new();
    private readonly List<KnockbackEffect> activeKnockbackEffects = new();

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Initialized for {gameObject.name}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        float deltaTime = Time.deltaTime;

        for (int i = activeDoTs.Count - 1; i >= 0; i--)
            activeDoTs[i].Tick(deltaTime);

        for (int i = activeHoTs.Count - 1; i >= 0; i--)
            activeHoTs[i].Tick(deltaTime);
    }

    #endregion

    #region Apply Effects — Damage

    public void ApplyDamageEffect(DamageEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Instant damage → {target.gameObject.name}");

        void OnComplete()
        {
            effect.OnCompleted -= OnComplete;
            activeInstantDamageEffects.Remove(effect);
        }

        effect.OnCompleted += OnComplete;
        activeInstantDamageEffects.Add(effect);
        effect.Apply(target);
    }

    public void ApplyDamageOverTimeEffect(DamageOverTimeEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] DoT → {target.gameObject.name}");

        void OnComplete()
        {
            effect.OnCompleted -= OnComplete;
            activeDoTs.Remove(effect);
        }

        effect.OnCompleted += OnComplete;
        activeDoTs.Add(effect);
        effect.Apply(target);
    }

    #endregion

    #region Apply Effects — Healing

    public void ApplyHealEffect(HealEffect effect, IHealthProvider target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Instant heal");

        void OnComplete()
        {
            effect.OnCompleted -= OnComplete;
            activeInstantHealEffects.Remove(effect);
        }

        effect.OnCompleted += OnComplete;
        activeInstantHealEffects.Add(effect);
        effect.Apply(target);
    }

    public void ApplyHealOverTimeEffect(HealOverTimeEffect effect, IHealthProvider target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] HoT");

        void OnComplete()
        {
            effect.OnCompleted -= OnComplete;
            activeHoTs.Remove(effect);
        }

        effect.OnCompleted += OnComplete;
        activeHoTs.Add(effect);
        effect.Apply(target);
    }

    #endregion

    #region Knockback

    public void ApplyKnockbackEffect(KnockbackEffect effect, Transform attacker, GameObject target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Knockback → {target.name}");

        void OnComplete()
        {
            effect.OnCompleted -= OnComplete;
            activeKnockbackEffects.Remove(effect);
        }

        effect.OnCompleted += OnComplete;
        activeKnockbackEffects.Add(effect);

        effect.SetAttacker(attacker);
        effect.Apply(target);
    }

    #endregion

    #region Cleanup

    public void CancelAllEffects()
    {
        foreach (var dot in activeDoTs)
            dot.Cancel();

        foreach (var hot in activeHoTs)
            hot.Cancel();

        activeInstantDamageEffects.Clear();
        activeDoTs.Clear();
        activeInstantHealEffects.Clear();
        activeHoTs.Clear();
        activeKnockbackEffects.Clear();
    }

    private void OnDestroy()
    {
        CancelAllEffects();
    }

    #endregion

    #region Queries

    public bool HasActiveEffects()
    {
        return activeInstantDamageEffects.Count > 0 ||
               activeDoTs.Count > 0 ||
               activeInstantHealEffects.Count > 0 ||
               activeHoTs.Count > 0 ||
               activeKnockbackEffects.Count > 0;
    }

    #endregion
}
