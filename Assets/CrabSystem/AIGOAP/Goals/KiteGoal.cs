using UnityEngine;

// The kiter: a ranged fighter that backs away while it keeps shooting, instead of strafing. Backpedals
// inside Kite Range, closes in beyond Max Range, otherwise holds. Uses the Ranged ability — give it one
// that casts while moving, or it stops to shoot.
[CreateAssetMenu(fileName = "Goal_Kite", menuName = "AI/GOAP/Goals/Kite")]
public class KiteGoal : GOAPGoal
{
    [Tooltip("Back away when the target is closer than this.")]
    public float kiteRange = 7f;

    [Tooltip("Close in when the target is further than this.")]
    public float maxRange = 10f;

    public float backStrength = 0.8f;
    public float approachStrength = 0.7f;

    [Tooltip("Seconds between shots.")]
    public float fireInterval = 1.8f;

    public float facingTolerance = 20f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Ranged);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void Execute(GOAPContext ctx)
    {
        AbilityDefinition shot = ctx.aiControl.AbilityFor(AIRole.Ranged);
        ctx.aiControl.Face(ctx.toTarget);

        if (ctx.distanceToTarget < kiteRange)
            GoalSteer.Away(ctx, backStrength);
        else if (ctx.distanceToTarget > maxRange)
            GoalSteer.Approach(ctx, maxRange - 1f, 1f, approachStrength);
        else
            ctx.aiControl.Stop();

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
