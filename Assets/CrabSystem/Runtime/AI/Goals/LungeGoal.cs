using UnityEngine;

// Hold at lunging distance, circling, then leap in. Uses the Lunge ability: its range is the far
// edge of the leap, and the ability's own movement effect does the leaping. The ability's cooldown
// sets the rhythm. DS1-style spacing: sometimes it pauses mid-circle, and after each leap it rolls an
// after-act (carry on, back off, back off and strafe, or a short wait).
[CreateAssetMenu(fileName = "Goal_Lunge", menuName = "AI/GOAP/Goals/Lunge")]
public class LungeGoal : GOAPGoal
{
    public float strength = 1f;

    [Tooltip("Stick push while circling at lunging distance.")]
    public float circleStrength = 0.6f;

    [Tooltip("Seconds before circling changes direction.")]
    public float circleSwap = 2f;

    [Tooltip("Too close to leap: back off inside this fraction of the range.")]
    public float nearFraction = 0.45f;

    public float facingTolerance = 20f;

    [Header("Spacing")]
    [Range(0, 100)] public int hesitateOdds = 20;
    public float hesitateInterval = 2f;
    public AfterAct afterAct = new AfterAct();

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Lunge);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
        ctx.aiControl.GoalSign = GoalSteer.RandomSign();
        ctx.aiControl.GoalUntil = Time.time + circleSwap;
    }

    public override void Execute(GOAPContext ctx)
    {
        if (GoalSteer.RunSpacing(ctx)) return;

        AbilityDefinition lunge = ctx.aiControl.AbilityFor(AIRole.Lunge);
        float far = lunge.range;
        float near = far * nearFraction;

        ctx.aiControl.Face(ctx.toTarget);
        GoalSteer.SwapSign(ctx, circleSwap);

        if (ctx.distanceToTarget > far)
            GoalSteer.Approach(ctx, far * 0.85f, 1f, strength);
        else if (ctx.distanceToTarget < near)
            GoalSteer.Away(ctx, 0.8f);
        else if (!GoalSteer.Ready(ctx, lunge) && GoalSteer.Hesitate(ctx, hesitateOdds, hesitateInterval))
            return;
        else
            GoalSteer.Orbit(ctx, ctx.aiControl.GoalSign, circleStrength);

        if (ctx.distanceToTarget > far || ctx.distanceToTarget < near) return;
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.TryUse(ctx, lunge)) return;

        GoalSteer.RollAfterAct(ctx, afterAct);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
