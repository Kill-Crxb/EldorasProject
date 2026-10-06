using UnityEngine;

// Crowded? Get out. Fires the Escape ability (a blink, a hop back) once when the target comes inside
// that ability's range, then hands back to the other goals. The ability's cooldown keeps it rare.
// Give it a high base weight so it wins the moment it's allowed.
[CreateAssetMenu(fileName = "Goal_Evade", menuName = "AI/GOAP/Goals/Evade")]
public class EvadeGoal : GOAPGoal
{
    [Tooltip("Escapes aimed by facing (a blink backwards) need the body turned to the target first.")]
    public float facingTolerance = 45f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || !GoalSteer.HasRole(ctx, AIRole.Escape)) return false;

        AbilityDefinition escape = ctx.aiControl.AbilityFor(AIRole.Escape);
        return ctx.distanceToTarget <= escape.range && GoalSteer.Ready(ctx, escape);
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.GoalPhase == 1;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        ctx.aiControl.Face(ctx.toTarget);
        ctx.aiControl.Stop();

        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (GoalSteer.TryUse(ctx, ctx.aiControl.AbilityFor(AIRole.Escape))) ctx.aiControl.GoalPhase = 1;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
