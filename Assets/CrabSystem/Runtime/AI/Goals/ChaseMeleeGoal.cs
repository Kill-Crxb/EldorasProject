using UnityEngine;

// Walk up and hit: the plain melee brawler. Uses the entity's Melee ability, and its range field
// is the reach — the goal stops a little inside it and swings on an interval.
//
// Dark Souls-style spacing (DS1_AI_Study.md): it walks when close and runs only when well out of reach;
// in the middle distance it sometimes hesitates (a pause or a short strafe) instead of pressing in; and
// after each swing it rolls an after-act — carry on, back off, back off and strafe, or a short wait.
[CreateAssetMenu(fileName = "Goal_ChaseMelee", menuName = "AI/GOAP/Goals/Chase And Melee")]
public class ChaseMeleeGoal : GOAPGoal
{
    public float strength = 1f;

    [Tooltip("Stick push while walking in. Runs only beyond Run Beyond metres past its stop point.")]
    public float walkStrength = 0.55f;
    public float runBeyond = 3f;

    [Tooltip("Seconds between swings, from the last one.")]
    public float attackInterval = 1.6f;

    [Tooltip("Only swing when the target is within this many degrees of facing.")]
    public float facingTolerance = 35f;

    [Tooltip("Stop at this fraction of the ability's range.")]
    public float stopFraction = 0.75f;

    [Tooltip("Step back when closer than this fraction of the range.")]
    public float backOffFraction = 0.35f;

    [Header("Spacing")]
    [Tooltip("Chance, rolled every Hesitate Interval while the target is just out of reach, to pause or strafe instead.")]
    [Range(0, 100)] public int hesitateOdds = 25;
    public float hesitateInterval = 1.5f;
    [Tooltip("Hesitate only within this many metres past the reach.")]
    public float hesitateBand = 3f;
    public AfterAct afterAct = new AfterAct();

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Melee);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        if (GoalSteer.RunSpacing(ctx)) return;

        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        float range = melee.range;
        float stop = range * stopFraction;

        ctx.aiControl.Face(ctx.toTarget);

        bool justOutOfReach = ctx.distanceToTarget > range && ctx.distanceToTarget <= range + hesitateBand;
        if (justOutOfReach && GoalSteer.Hesitate(ctx, hesitateOdds, hesitateInterval)) return;

        if (ctx.distanceToTarget < range * backOffFraction)
            GoalSteer.Away(ctx, 0.5f);
        else
            GoalSteer.Approach(ctx, stop, 1.5f, GoalSteer.Gait(ctx, stop, runBeyond, walkStrength) * strength);

        if (ctx.distanceToTarget > range) return;
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.Rested(ctx, attackInterval)) return;
        if (!GoalSteer.TryUse(ctx, melee)) return;

        GoalSteer.RollAfterAct(ctx, afterAct);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
