using UnityEngine;

// Hurt badly? Run for it, for a while, then come back. Give it a high base weight.
[CreateAssetMenu(fileName = "Goal_Flee", menuName = "AI/GOAP/Goals/Flee")]
public class FleeGoal : GOAPGoal
{
    [Tooltip("Flee at or below this fraction of max health.")]
    public float fleeBelow = 0.35f;

    public float fleeTime = 3f;

    [Tooltip("Seconds after a flee before it can flee again.")]
    public float cooldown = 8f;

    public float strength = 1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || ctx.aiControl == null) return false;
        return GoalSteer.HealthFraction(ctx) <= fleeBelow && Time.time >= ctx.aiControl.FleeReadyAt;
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || Time.time >= ctx.aiControl.GoalUntil;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalUntil = Time.time + fleeTime;
    }

    public override void Execute(GOAPContext ctx)
    {
        ctx.aiControl.Face(-ctx.toTarget);
        GoalSteer.Away(ctx, strength);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        if (ctx.aiControl == null) return;
        ctx.aiControl.FleeReadyAt = Time.time + cooldown;
        ctx.aiControl.Release();
    }
}
