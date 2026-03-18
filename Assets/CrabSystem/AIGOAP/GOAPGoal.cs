using UnityEngine;

/// <summary>
/// GOAP Goal - Base class for all goal definitions
/// 
/// Architecture:
/// - Goals are ScriptableObjects (data-driven, reusable)
/// - Each goal has permission check (CanExecute) and desire calculation (CalculateWeight)
/// - Goals execute behavior and check for completion
/// 
/// Design Principles:
/// - PERMISSION (CanExecute): Hard constraints only - distance, resources, cooldowns
///   Return FALSE if physically impossible. Do NOT consider desire/priority here.
/// 
/// - DESIRE (CalculateWeight): How much do I want to do this?
///   Assumes CanExecute() returned true. Higher weight = more desirable.
///   Consider: tactical advantage, context, situational benefits.
/// 
/// PRODUCTION FIX: Separation of permission and desire prevents goals from being
/// evaluated twice and keeps logic clean.
/// </summary>
[CreateAssetMenu(fileName = "New GOAP Goal", menuName = "AI/GOAP/Goal")]
public abstract class GOAPGoal : ScriptableObject
{
    [Header("Goal Settings")]
    [Tooltip("Display name for debugging")]
    public string goalName = "Unnamed Goal";

    [Tooltip("Base desirability weight (modified by CalculateWeight)")]
    public float baseWeight = 1.0f;

    [Header("Requirements")]
    [Tooltip("Does this goal need a target to execute?")]
    public bool requiresTarget = true;

    [Tooltip("Minimum distance to target (0 = no minimum)")]
    public float minDistance = 0f;

    [Tooltip("Maximum distance to target (very large = unlimited)")]
    public float maxDistance = 100f;

    [Header("Goal Commitment (Anti-Thrashing)")]
    [Tooltip("Minimum time this goal must run before switching (0 = use module default)")]
    public float minimumActiveDuration = 0f;

    #region Abstract Methods - Override in Derived Classes

    /// <summary>
    /// PERMISSION CHECK: Can this goal physically execute right now?
    /// 
    /// Return FALSE if:
    /// - Not enough stamina/mana/resources
    /// - On cooldown
    /// - Target too far/too close
    /// - Required ability not available
    /// - Any other hard constraint
    /// 
    /// DO NOT consider desirability here - that's CalculateWeight's job.
    /// This is purely "is it possible?" not "do I want to?"
    /// 
    /// Called before CalculateWeight - if this returns false, weight won't be calculated.
    /// </summary>
    public abstract bool CanExecute(GOAPContext ctx);

    /// <summary>
    /// DESIRE CALCULATION: How much do I want to do this?
    /// 
    /// Assumes CanExecute() returned true.
    /// 
    /// Return higher weight for more desirable situations:
    /// - Low HP? DefendGoal returns higher weight
    /// - Target low HP? AttackGoal returns higher weight
    /// - Allies nearby? CoordinateGoal returns higher weight
    /// 
    /// Consider:
    /// - Tactical advantage
    /// - Context (health, stamina, position)
    /// - Situational benefits
    /// - Risk vs reward
    /// 
    /// Base weight is multiplied by this value.
    /// Return 0 to reject goal even if CanExecute passed.
    /// </summary>
    public abstract float CalculateWeight(GOAPContext ctx);

    /// <summary>
    /// EXECUTION: Run this goal's behavior
    /// 
    /// Called every frame while this goal is active.
    /// Use ctx to access entity state and execute actions.
    /// 
    /// Examples:
    /// - ApproachGoal: Move toward target
    /// - AttackGoal: Execute ability
    /// - DefendGoal: Activate block
    /// </summary>
    public abstract void Execute(GOAPContext ctx);

    /// <summary>
    /// COMPLETION CHECK: Is this goal finished?
    /// 
    /// Return true when:
    /// - Goal achieved (reached destination, killed target, etc)
    /// - Goal failed (target died, lost sight, etc)
    /// - Goal interrupted (took damage, target changed)
    /// 
    /// Return false to keep executing.
    /// </summary>
    public abstract bool IsComplete(GOAPContext ctx);

    #endregion

    #region Virtual Methods - Override if Needed

    /// <summary>
    /// Called when this goal becomes active (state enter)
    /// Override to set up initial state, play animations, etc.
    /// </summary>
    public virtual void OnStart(GOAPContext ctx)
    {
        // Default: Do nothing
    }

    /// <summary>
    /// Called when this goal is interrupted or completes (state exit)
    /// Override to clean up state, stop animations, etc.
    /// </summary>
    public virtual void OnEnd(GOAPContext ctx)
    {
        // Default: Do nothing
    }

    #endregion

    #region Helper Methods for Derived Classes

    /// <summary>
    /// Helper: Check if target exists and is in distance range
    /// Common permission check for many goals
    /// </summary>
    protected bool IsTargetInRange(GOAPContext ctx)
    {
        if (requiresTarget && ctx.target == null)
            return false;

        float dist = ctx.distanceToTarget;
        return dist >= minDistance && dist <= maxDistance;
    }

    /// <summary>
    /// Helper: Calculate weight multiplier based on health urgency
    /// Lower health = higher multiplier for defensive goals
    /// Uses dynamic health tracking from GOAPContext
    /// </summary>
    protected float GetHealthUrgencyMultiplier(GOAPContext ctx, bool inverseUrgency = false)
    {
        float healthPercent = ctx.healthPercent;

        if (inverseUrgency)
        {
            // Aggressive goals - higher weight when healthy
            return Mathf.Lerp(0.5f, 1.5f, healthPercent);
        }
        else
        {
            // Defensive goals - higher weight when damaged
            return Mathf.Lerp(2.0f, 0.5f, healthPercent);
        }
    }

    /// <summary>
    /// Helper: Calculate weight multiplier based on any resource availability dynamically
    /// Example usage: GetResourceMultiplier(ctx, "Stamina") or "Chaos"
    /// </summary>
    protected float GetResourceMultiplier(GOAPContext ctx, string resourceName, float minWeight = 0.2f, float maxWeight = 1.2f)
    {
        if (ctx.resourcePercent != null && ctx.resourcePercent.TryGetValue(resourceName, out float percent))
        {
            return Mathf.Lerp(minWeight, maxWeight, percent);
        }

        // Resource not found, return fallback multiplier
        return minWeight;
    }

    /// <summary>
    /// Convenience for stamina-specific goals using dynamic system
    /// </summary>
    protected float GetStaminaMultiplier(GOAPContext ctx)
    {
        return GetResourceMultiplier(ctx, "Stamina", 0.2f, 1.2f);
    }

    /// <summary>
    /// Helper: Calculate weight bonus if allies are nearby
    /// Encourages coordination
    /// </summary>
    protected float GetAllyCoordinationBonus(GOAPContext ctx, float bonusAmount = 0.3f)
    {
        return ctx.hasAlliesNearby ? bonusAmount : 0f;
    }

    #endregion


    #region Debug

    /// <summary>
    /// Get a debug string for this goal's current state
    /// </summary>
    public virtual string GetDebugInfo(GOAPContext ctx)
    {
        return $"{goalName} [Base Weight: {baseWeight:F2}]";
    }

    #endregion
}