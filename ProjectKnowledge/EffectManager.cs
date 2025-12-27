using System.Collections.Generic;
using UnityEngine;

public class EffectManagerModule : MonoBehaviour, IBrainModule
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Debug")]
    [SerializeField] private bool debugEffects = false;

    private ControllerBrain brain;

    private List<DamageEffect> activeInstantDamageEffects = new List<DamageEffect>();
    private List<DamageOverTimeEffect> activeDoTs = new List<DamageOverTimeEffect>();
    private List<HealEffect> activeInstantHealEffects = new List<HealEffect>();
    private List<HealOverTimeEffect> activeHoTs = new List<HealOverTimeEffect>();
    private List<KnockbackEffect> activeKnockbackEffects = new List<KnockbackEffect>();

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
        {
            if (i < activeDoTs.Count)
                activeDoTs[i].Tick(deltaTime);
        }

        for (int i = activeHoTs.Count - 1; i >= 0; i--)
        {
            if (i < activeHoTs.Count)
                activeHoTs[i].Tick(deltaTime);
        }
    }

    public void ApplyDamageEffect(DamageEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant damage effect");

        effect.OnCompleted += () => RemoveDamageEffect(effect);
        activeInstantDamageEffects.Add(effect);
        effect.Apply(target);
    }

    public void ApplyDamageOverTimeEffect(DamageOverTimeEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying DoT: {effect.duration}s, {effect.damagePerTick} per {effect.tickInterval}s");

        effect.OnCompleted += () => RemoveDamageOverTimeEffect(effect);
        activeDoTs.Add(effect);
        effect.Apply(target);
    }

    public void ApplyHealEffect(HealEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant heal effect");

        effect.OnCompleted += () => RemoveHealEffect(effect);
        activeInstantHealEffects.Add(effect);
        effect.Apply(target);
    }

    public void ApplyHealOverTimeEffect(HealOverTimeEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying HoT: {effect.duration}s, {effect.healPerTick} per {effect.tickInterval}s");

        effect.OnCompleted += () => RemoveHealOverTimeEffect(effect);
        activeHoTs.Add(effect);
        effect.Apply(target);
    }

    public void ApplyKnockbackEffect(KnockbackEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying knockback effect");

        effect.OnCompleted += () => RemoveKnockbackEffect(effect);
        activeKnockbackEffects.Add(effect);
        effect.Apply(target);
    }

    private void RemoveDamageEffect(DamageEffect effect)
    {
        effect.OnCompleted -= () => RemoveDamageEffect(effect);
        activeInstantDamageEffects.Remove(effect);
    }

    private void RemoveDamageOverTimeEffect(DamageOverTimeEffect effect)
    {
        effect.OnCompleted -= () => RemoveDamageOverTimeEffect(effect);
        activeDoTs.Remove(effect);

        if (debugEffects)
            Debug.Log($"[EffectManagerModule] DoT expired or cancelled");
    }

    private void RemoveHealEffect(HealEffect effect)
    {
        effect.OnCompleted -= () => RemoveHealEffect(effect);
        activeInstantHealEffects.Remove(effect);
    }

    private void RemoveHealOverTimeEffect(HealOverTimeEffect effect)
    {
        effect.OnCompleted -= () => RemoveHealOverTimeEffect(effect);
        activeHoTs.Remove(effect);

        if (debugEffects)
            Debug.Log($"[EffectManagerModule] HoT expired or cancelled");
    }

    private void RemoveKnockbackEffect(KnockbackEffect effect)
    {
        effect.OnCompleted -= () => RemoveKnockbackEffect(effect);
        activeKnockbackEffects.Remove(effect);
    }

    public void CancelAllEffects()
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Cancelling all effects");

        foreach (var effect in activeDoTs)
            effect.Cancel();

        foreach (var effect in activeHoTs)
            effect.Cancel();

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
}