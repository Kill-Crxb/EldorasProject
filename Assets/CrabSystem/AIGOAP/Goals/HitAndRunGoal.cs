using UnityEngine;

// Swoop in, strike once, peel away, come round again. Uses the Melee ability; give that ability a
// forward movement effect and the strike carries the body straight through the target.
// Phase 0 is the run in, phase 1 the retreat.
[CreateAssetMenu(fileName = "Goal_HitAndRun", menuName = "AI/GOAP/Goals/Hit And Run")]
public class HitAndRunGoal : GOAPGoal
{
    public float strength = 1f;

    [Tooltip("Keep going straight for this long after the strike before turning away.")]
    public float carryTime = 0.4f;

    [Tooltip("Turn back in once this far from the target, or when Retreat Time runs out.")]
    public float retreatDistance = 7f;

    public float retreatTime = 2.5f;

    [Tooltip("How much the retreat curves to one side, so it doesn't fly straight back the same line.")]
    public float retreatCurve = 0.6f;

    public float facingTolerance = 25f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Melee);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        if (ctx.aiControl.GoalPhase == 0)
            RunIn(ctx);
        else
            Retreat(ctx);
    }

    void RunIn(GOAPContext ctx)
    {
        AbilityDefinition strike = ctx.aiControl.AbilityFor(AIRole.Melee);

        ctx.aiControl.Face(ctx.toTarget);
        GoalSteer.Approach(ctx, 0f, 0.5f, strength);

        if (ctx.distanceToTarget > strike.range) return;
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.TryUse(ctx, strike)) return;

        ctx.aiControl.GoalPhase = 1;
        ctx.aiControl.GoalUntil = Time.time + retreatTime;
        ctx.aiControl.GoalSign = GoalSteer.RandomSign();
    }

    void Retreat(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;

        if (Time.time - control.LastAttackTime < carryTime)
        {
            control.Steer(ctx.self.forward, strength);
            return;
        }

        Vector3 side = Vector3.Cross(Vector3.up, ctx.toTarget).normalized * control.GoalSign;
        Vector3 away = -ctx.toTarget.normalized + side * retreatCurve;
        control.Face(away);
        control.Steer(away, strength);

        if (ctx.distanceToTarget >= retreatDistance || Time.time >= control.GoalUntil)
            control.GoalPhase = 0;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
