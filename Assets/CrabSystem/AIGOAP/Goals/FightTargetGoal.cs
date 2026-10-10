using UnityEngine;

/// <summary>
/// First goal: close in on the perceived target and hit it with one attack on an interval.
/// Bare bones by design — no blocking, spacing or reads yet. It exists so the player can be hit
/// by something that plays through the real ability, hit roll, soak and hit-reaction path.
///
/// Needs on the entity: PerceptionModule (the target), AIControlSource (movement), and the
/// attack ability in its AbilitySystem list.
/// </summary>
[CreateAssetMenu(fileName = "Goal_FightTarget", menuName = "AI/GOAP/Goals/Fight Target")]
public class FightTargetGoal : GOAPGoal
{
    [Header("Attack")]
    [Tooltip("The one move this goal uses. Must be in the entity's AbilitySystem list.")]
    public AbilityDefinition attack;

    [Tooltip("Start swinging inside this distance (metres, root to root).")]
    public float attackRange = 2.2f;

    [Tooltip("Seconds between strings, counted from the last press.")]
    public float attackInterval = 2f;

    [Tooltip("Most presses in one string. Each string rolls 1..this. Follow-ups walk the moveset's " +
             "Light chain; a one-step chain repeats its step.")]
    public int maxString = 3;

    [Header("Spacing")]
    [Tooltip("Stop walking inside this distance. Keep it under Attack Range so she can still swing.")]
    public float stopRange = 1.8f;

    [Tooltip("Ease off over this distance before Stop Range. Movement is momentum-first, so full " +
             "input right up to the stop point carries her well past it.")]
    public float slowRange = 1.5f;

    [Tooltip("Step back when the target is closer than this.")]
    public float backOffRange = 1.1f;

    public override bool CanExecute(GOAPContext ctx)
    {
        return ctx.target != null && ctx.aiControl != null && attack != null;
    }

    public override float CalculateWeight(GOAPContext ctx) => baseWeight;

    public override bool IsComplete(GOAPContext ctx) => ctx.target == null;

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        float gap = ctx.distanceToTarget - stopRange;

        if (ctx.distanceToTarget < backOffRange)
            control.Steer(-ctx.toTarget, 0.5f);
        else if (gap > 0f)
            control.Steer(ctx.toTarget, gap / slowRange);
        else
            control.Stop();

        if (ContinueString(ctx)) return;
        if (ctx.distanceToTarget > attackRange) return;
        if (Time.time - control.LastAttackTime < Interval(ctx)) return;
        if (!Attack(ctx)) return;

        control.LastAttackTime = Time.time;
        control.StringPressesLeft = StringPresses(ctx);
    }

    // Seconds between strings, and presses after the first. Goals that change pace override these.
    protected virtual float Interval(GOAPContext ctx) => attackInterval;

    protected virtual int StringPresses(GOAPContext ctx) => Random.Range(0, Mathf.Max(1, maxString));

    // Presses the next step of the string she started. The moveset buffers a press made while a
    // step plays, so one press per step is enough. A string ends early when she's interrupted or
    // the target leaves range. True while a string is running.
    bool ContinueString(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        if (control.StringPressesLeft <= 0) return false;

        MovesetModule moveset = ctx.brain.GetModule<MovesetModule>();
        Blackboard blackboard = ctx.brain.Blackboard;
        bool interrupted = blackboard != null && blackboard.GetBool(BlackboardKey.CannotAct);

        if (moveset == null || interrupted || ctx.distanceToTarget > attackRange)
        {
            control.StringPressesLeft = 0;
            return false;
        }

        // Press as each step ends rather than buffering early: placeholder swings outlast the buffer.
        if (moveset.StepInFlight || moveset.HasBufferedPress) return true;
        if (moveset.Perform(MovesetChain.Light)) control.StringPressesLeft--;

        control.LastAttackTime = Time.time;
        return true;
    }

    // The entity's moveset when it has one, so an NPC swings the same string as the player;
    // the authored attack otherwise.
    bool Attack(GOAPContext ctx)
    {
        MovesetModule moveset = ctx.brain.GetModule<MovesetModule>();
        if (moveset != null && moveset.ActiveMoveset != null) return moveset.Perform(MovesetChain.Light);

        if (!ctx.abilityModule.CanUseAbility(attack.abilityId)) return false;

        ctx.abilityModule.UseAbility(attack.abilityId);
        return true;
    }

    public override void OnEnd(GOAPContext ctx)
    {
        ctx.aiControl?.Release();
    }
}
