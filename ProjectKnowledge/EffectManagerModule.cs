using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Effect Manager Module - Manages active effects on an entity
/// 
/// Responsibilities:
/// - Track active instant and overtime effects
/// - Tick DoT/HoT effects each frame
/// - Clean up expired effects
/// 
/// Integration:
/// - Works with DamageSystem (modern Brain entities)
/// - Still supports IDamageable (legacy simple destructibles)
/// 
/// Phase 1.7b: Updated to use DamageSystem
/// Updated: January 2026
/// </summary>
public class EffectManagerModule : MonoBehaviour, IBrainModule
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;
    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    [Header("Debug")]
    [SerializeField] private bool debugEffects = false;

    private ControllerBrain brain;

    // Active effects tracking
    private List<DamageEffect> activeInstantDamageEffects = new List<DamageEffect>();
    private List<DamageOverTimeEffect> activeDoTs = new List<DamageOverTimeEffect>();
    private List<HealEffect> activeInstantHealEffects = new List<HealEffect>();
    private List<HealOverTimeEffect> activeHoTs = new List<HealOverTimeEffect>();
    private List<KnockbackEffect> activeKnockbackEffects = new List<KnockbackEffect>();

    #region IBrainModule Implementation

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

        // Tick all DoT effects
        for (int i = activeDoTs.Count - 1; i >= 0; i--)
        {
            if (i < activeDoTs.Count)
                activeDoTs[i].Tick(deltaTime);
        }

        // Tick all HoT effects
        for (int i = activeHoTs.Count - 1; i >= 0; i--)
        {
            if (i < activeHoTs.Count)
                activeHoTs[i].Tick(deltaTime);
        }
    }

    #endregion

    #region Apply Effects (DamageSystem - Modern)

    /// <summary>
    /// Apply instant damage effect to DamageSystem target
    /// </summary>
    public void ApplyDamageEffect(DamageEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant damage effect to {target.Brain.name}");

        effect.OnCompleted += () => RemoveDamageEffect(effect);
        activeInstantDamageEffects.Add(effect);
        effect.Apply(target);
    }

    /// <summary>
    /// Apply damage over time effect to DamageSystem target
    /// </summary>
    public void ApplyDamageOverTimeEffect(DamageOverTimeEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying DoT to {target.Brain.name}: {effect.duration}s, {effect.damagePerTick} per {effect.tickInterval}s");

        effect.OnCompleted += () => RemoveDamageOverTimeEffect(effect);
        activeDoTs.Add(effect);
        effect.Apply(target);
    }

    /// <summary>
    /// Apply instant heal effect to target
    /// </summary>
    public void ApplyHealEffect(HealEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant heal effect to {target.Brain.name}");

        effect.OnCompleted += () => RemoveHealEffect(effect);
        activeInstantHealEffects.Add(effect);

        // Use IHealthProvider overload - DamageSystem implements IHealthProvider
        effect.Apply(target);
    }

    /// <summary>
    /// Apply heal over time effect to target
    /// </summary>
    public void ApplyHealOverTimeEffect(HealOverTimeEffect effect, DamageSystem target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying HoT to {target.Brain.name}: {effect.duration}s, {effect.healPerTick} per {effect.tickInterval}s");

        effect.OnCompleted += () => RemoveHealOverTimeEffect(effect);
        activeHoTs.Add(effect);

        // Use IHealthProvider overload - DamageSystem implements IHealthProvider
        effect.Apply(target);
    }

    /// <summary>
    /// Apply knockback effect to target
    /// </summary>
    public void ApplyKnockbackEffect(KnockbackEffect effect, Transform attacker, GameObject targetObject)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying knockback effect to {targetObject.name}");

        effect.OnCompleted += () => RemoveKnockbackEffect(effect);
        activeKnockbackEffects.Add(effect);

        effect.SetAttacker(attacker);
        effect.Apply(targetObject); // Uses GameObject overload
    }

    #endregion

    #region Apply Effects (IDamageable - Legacy Compatibility)

    /// <summary>
    /// Apply instant damage effect to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use ApplyDamageEffect(DamageEffect, DamageSystem) for Brain entities")]
    public void ApplyDamageEffect(DamageEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant damage effect (legacy IDamageable)");

        effect.OnCompleted += () => RemoveDamageEffect(effect);
        activeInstantDamageEffects.Add(effect);

#pragma warning disable CS0618 // Using obsolete method intentionally for backward compatibility
        effect.Apply(target);
#pragma warning restore CS0618
    }

    /// <summary>
    /// Apply DoT effect to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use ApplyDamageOverTimeEffect(DamageOverTimeEffect, DamageSystem) for Brain entities")]
    public void ApplyDamageOverTimeEffect(DamageOverTimeEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying DoT (legacy IDamageable): {effect.duration}s");

        effect.OnCompleted += () => RemoveDamageOverTimeEffect(effect);
        activeDoTs.Add(effect);
        // Note: DamageOverTimeEffect doesn't support IDamageable anymore
        Debug.LogWarning("[EffectManagerModule] DoT on IDamageable not supported - convert to DamageSystem");
    }

    /// <summary>
    /// Apply heal effect to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use ApplyHealEffect(HealEffect, DamageSystem) for Brain entities")]
    public void ApplyHealEffect(HealEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying instant heal effect (legacy IDamageable)");

        effect.OnCompleted += () => RemoveHealEffect(effect);
        activeInstantHealEffects.Add(effect);
        effect.Apply(target);
    }

    /// <summary>
    /// Apply HoT effect to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use ApplyHealOverTimeEffect(HealOverTimeEffect, DamageSystem) for Brain entities")]
    public void ApplyHealOverTimeEffect(HealOverTimeEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying HoT (legacy IDamageable): {effect.duration}s");

        effect.OnCompleted += () => RemoveHealOverTimeEffect(effect);
        activeHoTs.Add(effect);
        effect.Apply(target);
    }

    /// <summary>
    /// Apply knockback effect to IDamageable target (legacy compatibility)
    /// </summary>
    [System.Obsolete("Use ApplyKnockbackEffect(KnockbackEffect, Transform, GameObject) for Brain entities")]
    public void ApplyKnockbackEffect(KnockbackEffect effect, IDamageable target)
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Applying knockback effect (legacy IDamageable)");

        effect.OnCompleted += () => RemoveKnockbackEffect(effect);
        activeKnockbackEffects.Add(effect);

        if (target is MonoBehaviour mb)
        {
            effect.Apply(mb.gameObject);
        }
    }

    #endregion

    #region Effect Removal

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

    #endregion

    #region Cleanup

    /// <summary>
    /// Cancel all active effects (used on death or disable)
    /// </summary>
    public void CancelAllEffects()
    {
        if (debugEffects)
            Debug.Log($"[EffectManagerModule] Cancelling all effects");

        // Cancel all DoTs
        foreach (var effect in activeDoTs)
            effect.Cancel();

        // Cancel all HoTs
        foreach (var effect in activeHoTs)
            effect.Cancel();

        // Clear all lists
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

    #region Query Methods

    /// <summary>
    /// Get count of active DoT effects
    /// </summary>
    public int GetActiveDoTCount() => activeDoTs.Count;

    /// <summary>
    /// Get count of active HoT effects
    /// </summary>
    public int GetActiveHoTCount() => activeHoTs.Count;

    /// <summary>
    /// Check if any effects are active
    /// </summary>
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