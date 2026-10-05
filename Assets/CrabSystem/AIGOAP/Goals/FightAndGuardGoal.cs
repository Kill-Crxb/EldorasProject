using UnityEngine;

// Fight Target plus a guard. When the target starts an attack in range, roll once for that swing;
// on a success, hold block for a moment instead of attacking. The hold goes through
// AIControlSource.GuardHeld, which GuardModule reads as the AI's block key.
[CreateAssetMenu(fileName = "Goal_FightAndGuard", menuName = "AI/GOAP/Goals/Fight And Guard")]
public class FightAndGuardGoal : FightTargetGoal
{
    [Header("Guard")]
    [Tooltip("Defensive ability used to block. Must be in the entity's AbilitySystem list.")]
    public AbilityDefinition guard;

    [Tooltip("Chance to guard, rolled once per attack the target starts.")]
    [Range(0f, 1f)] public float guardChance = 0.5f;

    [Tooltip("Seconds the guard stays up once raised.")]
    public float guardHold = 0.8f;

    [Tooltip("Only read attacks started inside this distance.")]
    public float threatRange = 3f;

    [Header("Deflect")]
    [Tooltip("Chance, once per swing she is guarding against, to parry it through her moveset's Parry chain.")]
    [Range(0f, 1f)] public float deflectChance = 0.3f;

    [Tooltip("Seconds after the target's blade goes live before she presses. The blade is live a little " +
             "before it reaches her, and the parry window is only 8 frames.")]
    public float deflectDelay = 0.08f;


    public override bool CanExecute(GOAPContext ctx)
    {
        return base.CanExecute(ctx) && guard != null;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        bool threatened = TargetIsAttacking(ctx);

        // Roll once per swing, but only once a guard can actually go up. Rolling while she was
        // busy (mid-swing, flinched, Block cooling down) spent the read and she never guarded.
        if (threatened && !control.ReadThisSwing && CanGuard(ctx))
        {
            if (Random.value < guardChance) control.GuardUntil = Time.time + guardHold;
            control.ReadThisSwing = true;
        }
        if (!threatened)
        {
            control.ReadThisSwing = false;
            control.DeflectReadThisSwing = false;
        }

        bool guarding = Time.time < control.GuardUntil && (IsBlocking(ctx) || CanGuard(ctx));
        if (!guarding) control.GuardUntil = -999f;

        control.GuardHeld = guarding;
        if (!guarding)
        {
            base.Execute(ctx);
            return;
        }

        control.Face(ctx.toTarget);
        control.Stop();

        if (!IsBlocking(ctx))
        {
            ctx.abilityModule.UseAbility(guard.abilityId);
            return;
        }

        if (threatened) TryDeflect(ctx);
    }

    // Once per swing: the moment the target's blade goes live, maybe parry it. Reads the hitboxes rather
    // than the move's frame data, which placeholder clips don't match.
    void TryDeflect(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        if (control.DeflectReadThisSwing) return;

        ControllerBrain targetBrain = ctx.target.GetComponent<ControllerBrain>();
        AbilitySystem targetAbilities = targetBrain != null ? targetBrain.Abilities : null;
        if (targetAbilities == null || targetAbilities.HitboxesLiveFor < deflectDelay) return;

        control.DeflectReadThisSwing = true;
        if (Random.value >= deflectChance) return;

        ctx.brain.GetModule<MovesetModule>()?.Perform(MovesetChain.Parry);
    }

    bool CanGuard(GOAPContext ctx) => ctx.abilityModule.CanUseAbility(guard.abilityId);

    bool TargetIsAttacking(GOAPContext ctx)
    {
        if (ctx.distanceToTarget > threatRange) return false;

        ControllerBrain targetBrain = ctx.target.GetComponent<ControllerBrain>();
        AbilitySystem targetAbilities = targetBrain != null ? targetBrain.Abilities : null;
        AbilityDefinition move = targetAbilities != null ? targetAbilities.CurrentAbility : null;
        if (move == null || move.IsParry) return false;

        return move.abilityType != AbilityType.Defensive;
    }

    static bool IsBlocking(GOAPContext ctx)
    {
        Blackboard blackboard = ctx.brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
    }
}
