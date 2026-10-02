using UnityEngine;

/// <summary>
/// First goal: close in on the perceived target and hit it with one attack on an interval.
/// Bare bones by design — no blocking, spacing or reads yet. It exists so the player can be hit
/// by something that plays through the real ability, hit roll, soak and hit-reaction path.
///
/// Needs on the entity: PerceptionModule (the target), AIControlSource (movement), and the
/// attack ability in its AbilitySystem list.
/// </summary>
[CreateAssetMenu(fileName = "Goal_FightTarget", menuName = "AI/GOAP/Goals/Fight Target")]
public class FightTargetGoal : GOAPGoal
{
    [Header("Attack")]
    [Tooltip("The one move this goal uses. Must be in the entity's AbilitySystem list.")]
    public AbilityDefinition attack;

    [Tooltip("Start swinging inside this distance (metres, root to root).")]
    public float attackRange = 2.2f;

    [Tooltip("Seconds between swings, counted from the last swing that started.")]
    public float attackInterval = 2f;

    [Header("Spacing")]
    [Tooltip("Stop walking inside this distance. Keep it under Attack Range so she can still swing.")]
    public float stopRange = 1.8f;

    [Tooltip("Ease off over this distance before Stop Range. Movement is momentum-first, so full " +
             "input right up to the stop point carries her well past it.")]
    public float slowRange = 1.5f;

    [Tooltip("Step back when the target is closer than this.")]
    public float backOffRange = 1.1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        return ctx.target != null && ctx.aiControl != null && attack != null;
    }

    public override float CalculateWeight(GOAPContext ctx) => baseWeight;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        float gap = ctx.distanceToTarget - stopRange;

        if (ctx.distanceToTarget < backOffRange)
            control.Steer(-ctx.toTarget, 0.5f);
        else if (gap > 0f)
            control.Steer(ctx.toTarget, gap / slowRange);
        else
            control.Stop();

        if (ctx.distanceToTarget > attackRange) return;
        if (Time.time - control.LastAttackTime < attackInterval) return;
        if (!ctx.abilityModule.CanUseAbility(attack.abilityId)) return;

        ctx.abilityModule.UseAbility(attack.abilityId);
        control.LastAttackTime = Time.time;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
