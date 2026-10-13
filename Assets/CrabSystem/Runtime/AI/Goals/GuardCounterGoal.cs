using UnityEngine;

// The turtle. Stands at the edge of the target's reach with its guard up, blocks whatever comes, and
// punishes the moment the target's attack ends. If the target just waits, it loses patience and attacks.
// Uses the Guard ability (a Defensive one, held through AIControlSource.GuardHeld) and the Melee ability.
// Phase 0 is guarding, phase 1 the counter window.
[CreateAssetMenu(fileName = "Goal_GuardCounter", menuName = "AI/GOAP/Goals/Guard And Counter")]
public class GuardCounterGoal : GOAPGoal
{
    [Tooltip("Stand this far from the target while guarding.")]
    public float holdRange = 2.5f;

    [Tooltip("Attacks started inside this distance count as threats.")]
    public float threatRange = 3.5f;

    [Tooltip("Seconds after the target's attack ends in which it counters.")]
    public float counterWindow = 0.7f;

    [Tooltip("Seconds of nothing happening before it attacks anyway.")]
    public float patience = 4f;

    public float strength = 0.6f;
    public float facingTolerance = 35f;

    public override bool CanExecute(GOAPContext ctx)
    {
        return ctx.target != null && GoalSteer.HasRole(ctx, AIRole.Guard) && GoalSteer.HasRole(ctx, AIRole.Melee);
    }

    public override float CalculateWeight(GOAPContext ctx) => 1f;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void OnStart(GOAPContext ctx)
    {
        ctx.aiControl.GoalPhase = 0;
        ctx.aiControl.LastAttackTime = Time.time;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        bool attacking = TargetAttacking(ctx);
        if (attacking) Open(control, false);
        if (!attacking && control.GoalPhase == 0 && InHoldRange(ctx) && GoalSteer.Rested(ctx, patience)) Open(control, true);
        if (control.GoalPhase == 1 && Time.time >= control.GoalUntil) control.GoalPhase = 0;

        bool countering = control.GoalPhase == 1 && !attacking;
        if (countering)
            Counter(ctx);
        else
            Guard(ctx);
    }

    // A swing seen (or patience gone): the counter may start once the swing is over.
    void Open(AIControlSource control, bool now)
    {
        control.GoalPhase = 1;
        control.GoalUntil = Time.time + counterWindow + (now ? 0f : 0.5f);
    }

    void Counter(GOAPContext ctx)
    {
        AbilityDefinition melee = ctx.aiControl.AbilityFor(AIRole.Melee);
        ctx.aiControl.GuardHeld = false;
        GoalSteer.Approach(ctx, melee.range * 0.6f, 0.5f, 1f);

        if (ctx.distanceToTarget > melee.range || !GoalSteer.Facing(ctx, facingTolerance)) return;
        if (GoalSteer.TryUse(ctx, melee)) ctx.aiControl.GoalPhase = 0;
    }

    void Guard(GOAPContext ctx)
    {
        if (ctx.distanceToTarget < holdRange * 0.6f)
            GoalSteer.Away(ctx, 0.4f);
        else
            GoalSteer.Approach(ctx, holdRange, 1f, strength);

        ctx.aiControl.GuardHeld = true;
        if (IsBlocking(ctx)) return;

        AbilityDefinition guard = ctx.aiControl.AbilityFor(AIRole.Guard);
        if (GoalSteer.Ready(ctx, guard)) ctx.abilityModule.UseAbility(guard.abilityId);
    }

    bool InHoldRange(GOAPContext ctx) => ctx.distanceToTarget <= holdRange + 0.5f;

    bool TargetAttacking(GOAPContext ctx)
    {
        if (ctx.distanceToTarget > threatRange) return false;

        ControllerBrain targetBrain = ctx.target.GetComponent<ControllerBrain>();
        ICombatantState state = targetBrain != null ? targetBrain.GetProvider<ICombatantState>() : null;
        AbilityDefinition move = state != null ? state.CurrentAbility : null;
        if (move == null || move.IsParry) return false;
        return move.abilityType != AbilityType.Defensive;
    }

    static bool IsBlocking(GOAPContext ctx)
    {
        Blackboard blackboard = ctx.brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
