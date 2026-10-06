using UnityEngine;

// The leash. Dragged further than Leash Range from where it started, the creature gives up the chase and
// walks home, ignoring its target until it gets there. Once home it fights again if the target is still
// in sight. Needs no target. Give it the highest base weight in the pool so it beats every fighting goal.
[CreateAssetMenu(fileName = "Goal_Leash", menuName = "AI/GOAP/Goals/Leash")]
public class LeashGoal : GOAPGoal
{
    [Tooltip("Metres from home before it gives up.")]
    public float leashRange = 15f;

    [Tooltip("Home counts as reached within this distance.")]
    public float arrive = 1f;

    public float strength = 1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.aiControl == null) return false;
        return ctx.aiControl.Returning || FromHome(ctx) > leashRange;
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => !ctx.aiControl.Returning;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.Returning = true;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(control.Home - ctx.self.position);

        if (GoalSteer.ToPoint(ctx, control.Home, arrive, strength)) control.Returning = false;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    static float FromHome(GOAPContext ctx)
    {
        Vector3 offset = ctx.self.position - ctx.aiControl.Home;
        offset.y = 0f;
        return offset.magnitude;
    }
}
