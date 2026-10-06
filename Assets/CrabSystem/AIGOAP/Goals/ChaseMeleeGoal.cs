using UnityEngine;

// Walk up and hit: the plain melee brawler. Uses the entity's Melee ability, and its range field
// is the reach — the goal stops a little inside it and swings on an interval.
[CreateAssetMenu(fileName = "Goal_ChaseMelee", menuName = "AI/GOAP/Goals/Chase And Melee")]
public class ChaseMeleeGoal : GOAPGoal
{
    public float strength = 1f;

    [Tooltip("Seconds between swings, from the last one.")]
    public float attackInterval = 1.6f;

    [Tooltip("Only swing when the target is within this many degrees of facing.")]
    public float facingTolerance = 35f;

    [Tooltip("Stop at this fraction of the ability's range.")]
    public float stopFraction = 0.75f;

    [Tooltip("Step back when closer than this fraction of the range.")]
    public float backOffFraction = 0.35f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Melee);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void Execute(GOAPContext ctx)
    {
        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        float range = melee.range;

        ctx.aiControl.Face(ctx.toTarget);

        if (ctx.distanceToTarget < range * backOffFraction)
            GoalSteer.Away(ctx, 0.5f);
        else
            GoalSteer.Approach(ctx, range * stopFraction, 1.5f, strength);

        if (ctx.distanceToTarget > range) return;
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.Rested(ctx, attackInterval)) return;

        GoalSteer.TryUse(ctx, melee);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
