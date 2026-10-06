using UnityEngine;

// Stay in a band of distance, strafe, and shoot. Uses the Ranged ability; its range is the
// furthest it will fire from.
[CreateAssetMenu(fileName = "Goal_KeepDistance", menuName = "AI/GOAP/Goals/Keep Distance")]
public class KeepDistanceGoal : GOAPGoal
{
    [Tooltip("Back away inside this.")]
    public float minRange = 5f;

    [Tooltip("Close in beyond this.")]
    public float maxRange = 9f;

    public float strength = 0.8f;
    public float strafeStrength = 0.45f;

    [Tooltip("Seconds before strafing changes direction.")]
    public float strafeSwap = 2.5f;

    [Tooltip("Seconds between shots.")]
    public float fireInterval = 2.5f;

    public float facingTolerance = 15f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Ranged);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalSign = GoalSteer.RandomSign();
        ctx.aiControl.GoalUntil = Time.time + strafeSwap;
    }

    public override void Execute(GOAPContext ctx)
    {
        AbilityDefinition shot = ctx.aiControl.AbilityFor(AIRole.Ranged);

        ctx.aiControl.Face(ctx.toTarget);
        GoalSteer.SwapSign(ctx, strafeSwap);

        if (ctx.distanceToTarget < minRange)
            GoalSteer.Away(ctx, strength);
        else if (ctx.distanceToTarget > maxRange)
            GoalSteer.Approach(ctx, maxRange - 0.5f, 1f, strength);
        else
            GoalSteer.Orbit(ctx, ctx.aiControl.GoalSign, strafeStrength);

        if (ctx.distanceToTarget > shot.range) return;
        if (!GoalSteer.Facing(ctx, facingTolerance)) return;
        if (!GoalSteer.Rested(ctx, fireInterval)) return;

        GoalSteer.TryUse(ctx, shot);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
