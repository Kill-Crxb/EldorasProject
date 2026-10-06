using UnityEngine;

// Call for help. Stands and casts the Call ability (give it a cast time — 2 s for now). If the cast
// completes, every live AI within Call Radius that has no target is handed this one. Hit it out of the
// cast and nobody comes. Only casts when someone would answer. The Call ability's cooldown keeps it rare.
// Phase 0 is starting the cast, phase 1 casting, phase 2 done.
[CreateAssetMenu(fileName = "Goal_CallForHelp", menuName = "AI/GOAP/Goals/Call For Help")]
public class CallForHelpGoal : GOAPGoal
{
    public float callRadius = 25f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || !GoalSteer.HasRole(ctx, AIRole.Call)) return false;
        return GoalSteer.Ready(ctx, ctx.aiControl.AbilityFor(AIRole.Call)) && Answerers(ctx, false) > 0;
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.GoalPhase == 2;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        AbilityDefinition call = control.AbilityFor(AIRole.Call);
        control.Stop();

        if (control.GoalPhase == 0)
        {
            control.Face(ctx.toTarget);
            if (GoalSteer.TryUse(ctx, call)) control.GoalPhase = 1;
            return;
        }

        AbilitySystem abilities = ctx.brain.GetModule<AbilitySystem>();
        if (abilities != null && abilities.IsCasting) return;

        // The cast is over. It ran its full time, or it was cut short.
        bool completed = Time.time >= control.LastAttackTime + call.castTime - 0.05f;
        if (completed) Answerers(ctx, true);
        control.GoalPhase = 2;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    // Live AIs in range with no target. With `call` set, hands them this one.
    int Answerers(GOAPContext ctx, bool call)
    {
        int count = 0;
        foreach (AIControlSource other in AIControlSource.Active)
        {
            if (other == ctx.aiControl || !other.IsAlive || other.Target != null) continue;
            if (Vector3.Distance(other.transform.position, ctx.self.position) > callRadius) continue;
            if (call) other.Alert(ctx.target);
            count++;
        }
        return count;
    }
}
