using UnityEngine;

// The charge: a long, readable wind-up, a straight rush, and a strike the moment it arrives.
// Faces the target and starts the Charge ability (its cast time is the wind-up; its movement effect is
// the rush). From the moment the cast starts it stops turning, so the rush goes where the target WAS:
// sidestep during the wind-up and it misses. Once the rush brings it within Strike Range it turns and
// hits with its Melee ability (or Lunge, without one).
// Phases: 0 lining up, 1 winding up, 2 rushing, 3 done.
[CreateAssetMenu(fileName = "Goal_Charge", menuName = "AI/GOAP/Goals/Charge")]
public class ChargeGoal : GOAPGoal
{
    [Tooltip("Too close to charge: walk instead.")]
    public float minRange = 4f;

    public float facingTolerance = 10f;

    [Tooltip("Strike when the rush brings it this close (metres, root to root).")]
    public float strikeRange = 2f;

    [Tooltip("Seconds of rushing before it gives up on striking.")]
    public float rushTime = 1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || !GoalSteer.HasRole(ctx, AIRole.Charge)) return false;

        AbilityDefinition charge = ctx.aiControl.AbilityFor(AIRole.Charge);
        return ctx.distanceToTarget >= minRange && ctx.distanceToTarget <= charge.range && GoalSteer.Ready(ctx, charge);
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.GoalPhase == 3;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Stop();

        if (control.GoalPhase == 0) LineUp(ctx);
        else if (control.GoalPhase == 1) WindUp(ctx);
        else Rush(ctx);
    }

    void LineUp(GOAPContext ctx)
    {
        ctx.aiControl.Face(ctx.toTarget);
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (GoalSteer.TryUse(ctx, ctx.aiControl.AbilityFor(AIRole.Charge))) ctx.aiControl.GoalPhase = 1;
    }

    // The cast is running. When it ends the push has fired; the rush is on.
    void WindUp(GOAPContext ctx)
    {
        AbilitySystem abilities = ctx.brain.GetModule<AbilitySystem>();
        if (abilities != null && abilities.IsCasting) return;

        ctx.aiControl.GoalPhase = 2;
        ctx.aiControl.GoalUntil = Time.time + rushTime;
    }

    void Rush(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        if (Time.time >= control.GoalUntil)
        {
            control.GoalPhase = 3;
            return;
        }

        if (ctx.distanceToTarget > strikeRange) return;

        control.Face(ctx.toTarget);
        if (GoalSteer.TryUse(ctx, Strike(ctx))) control.GoalPhase = 3;
    }

    static AbilityDefinition Strike(GOAPContext ctx)
    {
        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        return melee != null ? melee : ctx.aiControl.AbilityFor(AIRole.Lunge);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
