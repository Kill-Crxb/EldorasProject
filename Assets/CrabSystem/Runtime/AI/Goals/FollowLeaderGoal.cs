using UnityEngine;

// Pack behaviour with no target of its own: keep near the nearest pack leader (Is Pack Leader on its
// AIControlSource), and when the leader is fighting, run to it — so a follower joins the fight as soon
// as it sees what the leader sees. Leaders never follow. Needs no target.
[CreateAssetMenu(fileName = "Goal_FollowLeader", menuName = "AI/GOAP/Goals/Follow Leader")]
public class FollowLeaderGoal : GOAPGoal
{
    [Tooltip("Stay within this distance of the leader while it is calm.")]
    public float keepWithin = 4f;

    [Tooltip("Only follow a leader within this distance.")]
    public float searchRadius = 25f;

    public float strength = 0.6f;

    [Tooltip("Stick push when running to a leader that is fighting.")]
    public float assistStrength = 1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        return ctx.aiControl != null && ctx.target == null && !ctx.aiControl.IsPackLeader && Leader(ctx) != null;
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target != null || Leader(ctx) == null;

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource leader = Leader(ctx);
        bool fighting = leader.Target != null;

        Vector3 point = leader.transform.position;
        Vector3 to = point - ctx.self.position;
        to.y = 0f;
        ctx.aiControl.Face(to);

        float stop = fighting ? 1.5f : keepWithin;
        GoalSteer.ToPoint(ctx, point, stop, fighting ? assistStrength : strength);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    AIControlSource Leader(GOAPContext ctx)
    {
        AIControlSource best = null;
        float bestDistance = searchRadius;

        foreach (AIControlSource other in AIControlSource.Active)
        {
            if (other == ctx.aiControl || !other.IsPackLeader || !other.IsAlive) continue;
            float distance = Vector3.Distance(other.transform.position, ctx.self.position);
            if (distance > bestDistance) continue;
            best = other;
            bestDistance = distance;
        }

        return best;
    }
}
