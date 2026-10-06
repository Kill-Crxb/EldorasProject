using UnityEngine;

// Reactions, DS1-style. Put this in the GOAPModule's interrupt slot (Evade Goal), not the goal pool.
// Each event gets one roll, at low odds, so a creature feels dim but alive:
//   hit by the target         → step away (back / left / right)
//   my hit was guarded        → step away
//   target swung and missed   → punish with Melee (or Lunge) if close enough
// An event only counts for a moment after it happens, and each is rolled once. Nothing happens while
// the creature is mid-move. Phases: 0 step, 1 punish, 2 done.
[CreateAssetMenu(fileName = "Goal_React", menuName = "AI/GOAP/Goals/React")]
public class ReactGoal : GOAPGoal
{
    const int Step = 0;
    const int Punish = 1;

    [Header("Hit by the target → step away")]
    [Range(0, 100)] public int damagedOdds = 15;
    public float damagedRange = 3f;

    [Header("My hit was guarded → step away")]
    [Range(0, 100)] public int guardedOdds = 20;

    [Header("Target whiffed → punish")]
    [Range(0, 100)] public int whiffOdds = 25;
    public float punishRange = 2f;
    public float punishWindow = 0.6f;

    [Header("Step")]
    public float stepTime = 0.45f;
    [Range(0, 100)] public int stepBackOdds = 30;

    [Tooltip("How long after an event it can still be reacted to.")]
    public float freshFor = 0.4f;

    public override bool CanExecute(GOAPContext ctx)
    {
        if (ctx.target == null || ctx.aiControl == null) return false;
        if (GoalSteer.Busy(ctx)) return false;
        if (ctx.aiControl.Returning) return false;

        AIControlSource control = ctx.aiControl;
        if (Fresh(control, control.TargetWhiffedAt)) return Consume(control, control.TargetWhiffedAt, CanPunish(ctx) && GoalSteer.Roll(whiffOdds), Punish);
        if (Fresh(control, control.GuardedAt)) return Consume(control, control.GuardedAt, GoalSteer.Roll(guardedOdds), Step);
        if (Fresh(control, control.DamagedAt)) return Consume(control, control.DamagedAt, ctx.distanceToTarget <= damagedRange && GoalSteer.Roll(damagedOdds), Step);
        return false;
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null || ctx.aiControl.GoalPhase == 2 || Time.time >= ctx.aiControl.GoalUntil;

    public override void OnStart(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.GoalPhase = control.Reaction;
        control.GoalUntil = Time.time + (control.Reaction == Punish ? punishWindow : stepTime);
        control.GoalSign = GoalSteer.Roll(stepBackOdds) ? 0f : GoalSteer.RandomSign();
    }

    public override void Execute(GOAPContext ctx)
    {
        if (ctx.aiControl.GoalPhase == Step)
            StepAway(ctx);
        else
            Strike(ctx);
    }

    // GoalSign 0 steps straight back; ±1 steps to a side, still facing the target.
    void StepAway(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        if (control.GoalSign == 0f)
            GoalSteer.Away(ctx, 1f);
        else
            GoalSteer.Orbit(ctx, control.GoalSign, 1f);
    }

    void Strike(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);
        control.Stop();

        AbilityDefinition attack = PunishWith(ctx);
        if (attack == null) return;
        if (GoalSteer.TryUse(ctx, attack)) control.GoalPhase = 2;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }

    bool Fresh(AIControlSource control, float at) => at > control.ReactedTo && Time.time - at <= freshFor;

    // Every fresh event is used up whether the roll succeeds or not, so it is rolled exactly once.
    bool Consume(AIControlSource control, float at, bool react, int reaction)
    {
        control.ReactedTo = at;
        control.Reaction = reaction;
        return react;
    }

    bool CanPunish(GOAPContext ctx) => ctx.distanceToTarget <= punishRange && PunishWith(ctx) != null;

    AbilityDefinition PunishWith(GOAPContext ctx)
    {
        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        if (melee != null && ctx.distanceToTarget <= melee.range && GoalSteer.Ready(ctx, melee)) return melee;

        AbilityDefinition lunge = ctx.aiControl.AbilityFor(AIRole.Lunge);
        if (lunge != null && GoalSteer.Ready(ctx, lunge)) return lunge;
        return null;
    }
}
