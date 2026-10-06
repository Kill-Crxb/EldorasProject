using UnityEngine;

// Pack behaviour: when another creature on the same target is closer to it, take a place on a ring
// around the target and wait. The closest one fights (Chase And Melee); the rest spread out, so the
// target faces one attacker at a time and gets flanked. Give it a higher base weight than the melee goal.
[CreateAssetMenu(fileName = "Goal_Surround", menuName = "AI/GOAP/Goals/Surround")]
public class SurroundGoal : GOAPGoal
{
    public float ringRadius = 3f;
    public float strength = 0.6f;

    [Tooltip("Another creature must be at least this much closer to count, so two at the same " +
             "distance don't swap roles every frame.")]
    public float margin = 0.4f;

    public override bool CanExecute(GOAPContext ctx) => ctx.target != null && ctx.aiControl != null && CloserAlly(ctx);

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || !CloserAlly(ctx);

    public override void Execute(GOAPContext ctx)
    {
        ctx.aiControl.Face(ctx.toTarget);

        // Slots follow the order the AIs came alive in, which holds still while they live.
        int count = 0;
        int slot = 0;
        for (int i = 0; i < AIControlSource.Active.Count; i++)
        {
            AIControlSource other = AIControlSource.Active[i];
            if (other == ctx.aiControl) slot = count;
            if (Hunting(other, ctx.target)) count++;
        }

        float angle = 360f * slot / Mathf.Max(1, count);
        Vector3 point = ctx.target.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * ringRadius;
        GoalSteer.ToPoint(ctx, point, 0.5f, strength);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
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
