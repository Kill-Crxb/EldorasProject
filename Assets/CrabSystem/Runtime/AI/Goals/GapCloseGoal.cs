using UnityEngine;

// Close the gap and hit. When the target is past melee reach but inside the Lunge ability's range, and the
// lunge is ready: face it, leap in, and strike with the Melee ability the moment it lands within reach —
// no pause between the two. The lunge's own movement effect does the travelling; its cooldown keeps it
// from chaining. Give it a higher base weight than Chase & Melee so it wins the moment the gap opens.
// Phases: 0 lining up, 1 in the air, 2 done.
[CreateAssetMenu(fileName = "Goal_GapClose", menuName = "AI/GOAP/Goals/Gap Close")]
public class GapCloseGoal : GOAPGoal
{
    [Tooltip("Don't leap when closer than this — walk instead. 0 uses the Melee ability's range (or 2.5 m without one).")]
    public float minRange;

    public float facingTolerance = 20f;

    [Tooltip("Seconds after the leap to land a strike before giving up and handing back.")]
    public float strikeWindow = 1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || !GoalSteer.HasRole(ctx, AIRole.Lunge)) return false;

        AbilityDefinition lunge = ctx.aiControl.AbilityFor(AIRole.Lunge);
        return ctx.distanceToTarget > Near(ctx) && ctx.distanceToTarget <= lunge.range && GoalSteer.Ready(ctx, lunge);
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.GoalPhase == 2;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        if (ctx.aiControl.GoalPhase == 0)
            Leap(ctx);
        else
            Strike(ctx);
    }

    void Leap(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);
        GoalSteer.Approach(ctx, Near(ctx), 1f, 1f);

        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.TryUse(ctx, control.AbilityFor(AIRole.Lunge))) return;

        control.GoalPhase = 1;
        control.GoalUntil = Time.time + strikeWindow;
    }

    // In the air: keep pressing toward the target, and swing the moment it's in reach. Without a Melee
    // ability the leap was the attack, so it just hands back.
    void Strike(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        AbilityDefinition melee = control.AbilityFor(AIRole.Melee);

        if (melee == null || Time.time >= control.GoalUntil)
        {
            control.GoalPhase = 2;
            return;
        }

        control.Face(ctx.toTarget);
        control.Steer(ctx.toTarget, 1f);

        if (ctx.distanceToTarget > melee.range) return;
        if (GoalSteer.TryUse(ctx, melee)) control.GoalPhase = 2;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    float Near(GOAPContext ctx)
    {
        if (minRange > 0f) return minRange;
        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        return melee != null ? melee.range : 2.5f;
    }
}
