using UnityEngine;

// Pack behaviour: when another creature on the same target is closer to it, hang back as an encircler
// instead of joining in. Like DS1's crowd roles (DS1_AI_Study.md §5) it doesn't hold a fixed slot: it
// keeps roughly Ring Radius away, strafes a random arc for a few seconds, sometimes pauses, then picks
// again — so a group drifts around the target while the closest one fights (Chase And Melee).
// Give it a higher base weight than the melee goal. Phase 0 is strafing, phase 1 pausing.
[CreateAssetMenu(fileName = "Goal_Surround", menuName = "AI/GOAP/Goals/Surround")]
public class SurroundGoal : GOAPGoal
{
    [Tooltip("Hang about this far from the target.")]
    public float ringRadius = 4.5f;

    [Tooltip("Back off when closer than this.")]
    public float tooClose = 3f;

    public float strength = 0.5f;

    [Tooltip("Seconds per strafe before picking again.")]
    public float minStrafe = 1.5f;
    public float maxStrafe = 3f;

    [Tooltip("Chance, each time it picks, to stand and watch for a moment instead.")]
    [Range(0, 100)] public int pauseOdds = 25;

    [Tooltip("Another creature must be at least this much closer to count, so two at the same " +
             "distance don't swap roles every frame.")]
    public float margin = 0.4f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && ctx.aiControl != null && CloserAlly(ctx);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || !CloserAlly(ctx);

    public override void OnStart(GOAPContext ctx)
    {
        Pick(ctx.aiControl);
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        if (Time.time >= control.GoalUntil) Pick(control);

        if (ctx.distanceToTarget > ringRadius + 1.5f)
            GoalSteer.Approach(ctx, ringRadius, 1f, GoalSteer.Gait(ctx, ringRadius, 4f, strength));
        else if (ctx.distanceToTarget < tooClose)
            GoalSteer.Away(ctx, strength);
        else if (control.GoalPhase == 1)
            control.Stop();
        else
            GoalSteer.Orbit(ctx, control.GoalSign, strength);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    void Pick(AIControlSource control)
    {
        bool pause = GoalSteer.Roll(pauseOdds);
        control.GoalPhase = pause ? 1 : 0;
        control.GoalSign = GoalSteer.RandomSign();
        control.GoalUntil = Time.time + (pause ? Random.Range(0.5f, 1.5f) : Random.Range(minStrafe, maxStrafe));
    }

    bool CloserAlly(GOAPContext ctx)
    {
        foreach (AIControlSource other in AIControlSource.Active)
        {
            if (other == ctx.aiControl || !Hunting(other, ctx.target)) continue;
            float theirs = Vector3.Distance(other.transform.position, ctx.target.position);
            if (theirs + margin < ctx.distanceToTarget) return true;
        }
        return false;
    }

    static bool Hunting(AIControlSource other, Transform target) => other != null && other.IsAlive && other.Target == target;
}
