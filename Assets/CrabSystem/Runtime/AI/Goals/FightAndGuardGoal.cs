using UnityEngine;

// Fight Target plus a guard. When the target starts an attack in range, read it once for that swing:
// back off and dash out if the dodge is ready (cooldown and costs), otherwise roll to hold block for a moment
// instead of attacking. The hold goes through AIControlSource.GuardHeld, which GuardModule reads as the
// AI's block key. Out of reach she closes with the gap closer when it's ready, or dashes in when it isn't.
//
// A Perilous move can't be guarded, so she reads it from further out, sidesteps across its line and dashes through
// its strike. Low on stamina she's winded: she keeps just outside reach, drifting side to side, until it's full,
// then presses hard for a few seconds. She presses the same way while her target is out of stamina.
[CreateAssetMenu(fileName = "Goal_FightAndGuard", menuName = "AI/GOAP/Goals/Fight And Guard")]
public class FightAndGuardGoal : FightTargetGoal
{
    const int DodgingPhase = 1;
    const int SidestepPhase = 5;

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

    [Tooltip("Seconds after the target's tell before she has read it — her reaction time. A tell closer to the " +
             "strike than this can't be parried by her.")]
    public float deflectDelay = 0.08f;

    [Tooltip("Frames before the strike lands that she presses, once she has read it. The parry window is 8 " +
             "frames, so the strike must land inside it.")]
    public int deflectLead = 4;

    [Header("Dodge")]
    [Tooltip("Dash used against a swing in Threat Range. Empty = never dodges. It goes along her travel, so she " +
             "backs off first and dashes once she's moving away.")]
    public AbilityDefinition dodge;

    [Tooltip("Speed away from the target (m/s) she must reach before the dash fires. Below the dash's 0.5 m/s it " +
             "would aim off her facing — straight at the target.")]
    public float dodgeAwaySpeed = 1f;

    [Tooltip("Seconds she gets to back off and dash before the read is dropped.")]
    public float dodgeWindow = 0.3f;

    [Tooltip("Chance, once per swing, to dodge when the dodge is ready. A failed roll falls through to the guard roll.")]
    [Range(0f, 1f)] public float dodgeChance = 1f;

    [Tooltip("Seconds after a dodge she holds still, facing the target, so walking in doesn't eat the push.")]
    public float dodgeSettle = 0.4f;

    [Header("Closing")]
    [Tooltip("Attack that travels to the target (Dash Thrust). Used when the gap is inside the band below.")]
    public AbilityDefinition gapCloser;

    [Tooltip("Gap closer band, metres root to root.")]
    public float gapCloseMin = 4f;
    public float gapCloseMax = 8f;

    [Tooltip("Dash used to close in when the gap closer is cooling down. Empty = walks.")]
    public AbilityDefinition dashIn;

    [Tooltip("Only dash in when the target is at least this far.")]
    public float dashInMin = 4f;

    [Tooltip("Both are aimed by her facing: only fire when turned to within this many degrees of the target.")]
    public float closeFacing = 20f;

    [Header("Perilous")]
    [Tooltip("Read a Perilous move started inside this distance. Wider than Threat Range: they travel.")]
    public float perilousRange = 9f;

    [Tooltip("Frames before the strike she dashes. The dash pushes (and its i-frames start) about 8 frames after " +
             "the press and last 12, so the strike must land between.")]
    public int perilousLead = 12;

    [Header("Stamina")]
    public string staminaId = "stamina";

    [Tooltip("Below this share of her stamina she's winded and keeps away.")]
    [Range(0f, 1f)] public float windedBelow = 0.35f;

    [Tooltip("At or above this share she's rested and engages.")]
    [Range(0f, 1f)] public float restedAt = 0.95f;

    [Tooltip("Winded, she holds this far from the target: just outside reach.")]
    public float keepAwayRange = 3.5f;

    [Tooltip("Sideways drift while winded, 0–1 of full input.")]
    public float keepAwayStrafe = 0.5f;

    [Tooltip("Seconds between drift direction changes, randomised up to double.")]
    public float strafeSwap = 1.5f;

    [Header("Engage")]
    [Tooltip("Seconds she presses hard once rested, or for as long as the target stays out of stamina.")]
    public float engageSeconds = 4f;

    [Tooltip("Seconds between strings while engaging. Each string runs to its full length.")]
    public float engageInterval = 0.5f;

    [Tooltip("She engages while the target's stamina is below this share. 0 = never.")]
    [Range(0f, 1f)] public float punishBelow = 0.2f;

    public override bool CanExecute(GOAPContext ctx)
    {
        return base.CanExecute(ctx) && guard != null;
    }

    public override void Execute(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        UpdateWind(ctx);
        if (ReadPerilous(ctx)) return;

        bool threatened = TargetIsAttacking(ctx);

        // Read once per swing, but only once she can answer it. Reading while she was busy (mid-swing,
        // flinched, Block cooling down) spent the read and she never guarded.
        if (threatened && !control.ReadThisSwing && (CanGuard(ctx) || CanDodge(ctx)))
        {
            control.ReadThisSwing = true;
            if (StartDodge(ctx)) return;
            if (CanGuard(ctx) && Random.value < guardChance) control.GuardUntil = Time.time + guardHold;
        }
        if (!threatened)
        {
            control.ReadThisSwing = false;
            control.DeflectReadThisSwing = false;
        }

        if (control.GoalPhase == DodgingPhase)
        {
            BackOffAndDash(ctx);
            return;
        }

        if (Time.time < control.GoalUntil)
        {
            control.Face(ctx.toTarget);
            control.Stop();
            return;
        }

        bool guarding = Time.time < control.GuardUntil && (IsBlocking(ctx) || CanGuard(ctx));
        if (!guarding) control.GuardUntil = -999f;

        control.GuardHeld = guarding;
        if (!guarding && control.Winded)
        {
            KeepAway(ctx);
            return;
        }
        if (!guarding)
        {
            base.Execute(ctx);
            TryClose(ctx);
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

    // Once per swing: she reads the target's tell after her reaction time, then, if its next strike would
    // reach her, maybe parries just before it lands. A strike that can't reach isn't worth the cooldown.
    void TryDeflect(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        if (control.DeflectReadThisSwing) return;

        ICombatantState targetState = TargetState(ctx);
        if (targetState == null || targetState.TellFor < deflectDelay) return;
        if (FramesToStrike(targetState) > deflectLead) return;
        if (!targetState.StrikeWouldReach(ctx.brain)) return;

        control.DeflectReadThisSwing = true;
        if (Random.value >= deflectChance) return;

        ctx.brain.GetModule<MovesetModule>()?.Perform(MovesetChain.Parry);
    }

    // From the move's baked frames; a move without them is pressed against at once.
    static int FramesToStrike(ICombatantState state)
    {
        AbilityDefinition move = state.CurrentAbility;
        if (move == null || !move.HasMoveData) return 0;
        return move.ActiveStart - state.CurrentMoveFrame;
    }

    bool CanGuard(GOAPContext ctx) => ctx.abilityModule.CanUseAbility(guard.abilityId);

    protected override float Interval(GOAPContext ctx) => Engaging(ctx) ? engageInterval : attackInterval;

    protected override int StringPresses(GOAPContext ctx) => Engaging(ctx) ? Mathf.Max(0, maxString - 1) : base.StringPresses(ctx);

    static bool Engaging(GOAPContext ctx) => Time.time < ctx.aiControl.EngageUntil;

    // Winded below one share of stamina, rested again only at another, so she doesn't flicker between them.
    // Getting her wind back starts an engage; so does the target running dry while she has hers.
    void UpdateWind(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        float stamina = StaminaFraction(ctx.brain);

        if (!control.Winded && stamina < windedBelow)
        {
            control.Winded = true;
            control.StringPressesLeft = 0;
            control.EngageUntil = -999f;
            return;
        }

        if (control.Winded && stamina >= restedAt)
        {
            control.Winded = false;
            Engage(control);
        }

        if (!control.Winded && StaminaFraction(ctx.target.GetComponent<ControllerBrain>()) < punishBelow) Engage(control);
    }

    // A fresh engage swings at once; one already running is just extended.
    void Engage(AIControlSource control)
    {
        if (Time.time >= control.EngageUntil) control.LastAttackTime = -999f;
        control.EngageUntil = Time.time + engageSeconds;
    }

    float StaminaFraction(ControllerBrain brain)
    {
        ResourceSystem resources = brain != null ? brain.Resources : null;
        ResourceDefinition stamina = resources != null ? resources.FindDefinition(staminaId) : null;
        if (stamina == null) return 1f;

        float max = resources.GetMaxResource(stamina);
        return max > 0f ? resources.GetResource(stamina) / max : 1f;
    }

    // Just outside reach, facing the target, drifting sideways and changing direction now and then. Too close
    // she backs off, too far she drifts back in, so she's in range when her wind returns.
    void KeepAway(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);

        if (Time.time >= control.NextDecisionAt)
        {
            control.GoalSign = GoalSteer.RandomSign();
            control.NextDecisionAt = Time.time + strafeSwap * Random.Range(1f, 2f);
        }

        Vector3 toward = ctx.toTarget.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toward) * control.GoalSign;
        Vector3 steer = side * keepAwayStrafe + toward * Mathf.Clamp(ctx.distanceToTarget - keepAwayRange, -1f, 1f);
        control.Steer(steer, steer.magnitude);
    }

    // Once per Perilous move: if the dodge is ready she sidesteps, then dashes along it just before the strike,
    // so its i-frames take the hit. True while she's doing that.
    bool ReadPerilous(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        ICombatantState targetState = TargetState(ctx);
        bool perilous = ctx.distanceToTarget <= perilousRange && IsPerilous(targetState);

        if (!perilous)
        {
            control.PerilousRead = false;
            if (control.GoalPhase == SidestepPhase) control.GoalPhase = 0;
            return false;
        }

        if (!control.PerilousRead)
        {
            control.PerilousRead = true;
            if (!CanDodge(ctx)) return false;

            control.GuardUntil = -999f;
            control.GuardHeld = false;
            control.StringPressesLeft = 0;
            control.GoalSign = GoalSteer.RandomSign();
            control.GoalPhase = SidestepPhase;
        }

        if (control.GoalPhase != SidestepPhase) return false;
        Sidestep(ctx, targetState);
        return true;
    }

    void Sidestep(GOAPContext ctx, ICombatantState targetState)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);
        GoalSteer.Orbit(ctx, control.GoalSign, 1f);

        int frames = FramesToStrike(targetState);
        if (frames < 0)
        {
            control.GoalPhase = 0;
            return;
        }
        if (frames > perilousLead) return;

        Vector3 side = Vector3.Cross(Vector3.up, ctx.toTarget.normalized) * control.GoalSign;
        Vector3 velocity = ctx.brain.Movement != null ? ctx.brain.Movement.Velocity : Vector3.zero;
        if (Vector3.Dot(velocity, side) < dodgeAwaySpeed || !CanDodge(ctx)) return;

        ctx.abilityModule.UseAbility(dodge.abilityId);
        control.GoalPhase = 0;
        control.GoalUntil = Time.time + dodgeSettle;
    }

    static bool IsPerilous(ICombatantState state)
    {
        return state != null && state.CurrentAbility != null && state.CurrentAbility.hit.guard == GuardType.Perilous;
    }

    bool CanDodge(GOAPContext ctx) => GoalSteer.Ready(ctx, dodge);

    bool StartDodge(GOAPContext ctx)
    {
        if (!CanDodge(ctx) || Random.value >= dodgeChance) return false;

        AIControlSource control = ctx.aiControl;
        control.GuardUntil = -999f;
        control.GuardHeld = false;
        control.GoalPhase = DodgingPhase;
        control.GoalUntil = Time.time + dodgeWindow;
        BackOffAndDash(ctx);
        return true;
    }

    // Backpedals facing the target; the dash fires once she's moving away fast enough for it to aim along
    // that, then she settles. Out of time, she drops the read and fights on.
    void BackOffAndDash(GOAPContext ctx)
    {
        AIControlSource control = ctx.aiControl;
        control.Face(ctx.toTarget);
        GoalSteer.Away(ctx, 1f);

        if (Time.time >= control.GoalUntil)
        {
            control.GoalPhase = 0;
            return;
        }

        Vector3 away = -ctx.toTarget.normalized;
        Vector3 velocity = ctx.brain.Movement != null ? ctx.brain.Movement.Velocity : Vector3.zero;
        if (Vector3.Dot(velocity, away) < dodgeAwaySpeed || !CanDodge(ctx)) return;

        ctx.abilityModule.UseAbility(dodge.abilityId);
        control.GoalPhase = 0;
        control.GoalUntil = Time.time + dodgeSettle;
    }

    // Out of reach: the gap closer when the gap suits it; while it's ready she walks into its band. Once it's
    // cooling down, the dash in. The dash isn't an attack, so it doesn't pace her next string.
    void TryClose(GOAPContext ctx)
    {
        if (ctx.distanceToTarget <= attackRange || GoalSteer.Busy(ctx)) return;
        if (!GoalSteer.Facing(ctx, closeFacing)) return;

        bool inBand = ctx.distanceToTarget >= gapCloseMin && ctx.distanceToTarget <= gapCloseMax;
        if (inBand && GoalSteer.TryUse(ctx, gapCloser)) return;
        if (GoalSteer.Ready(ctx, gapCloser)) return;
        if (ctx.distanceToTarget < dashInMin || !GoalSteer.Ready(ctx, dashIn)) return;

        ctx.abilityModule.UseAbility(dashIn.abilityId);
    }

    bool TargetIsAttacking(GOAPContext ctx)
    {
        if (ctx.distanceToTarget > threatRange) return false;

        ICombatantState targetState = TargetState(ctx);
        AbilityDefinition move = targetState != null ? targetState.CurrentAbility : null;
        if (move == null || move.IsParry || move.hit.guard == GuardType.Perilous) return false;

        return move.abilityType != AbilityType.Defensive;
    }

    static ICombatantState TargetState(GOAPContext ctx)
    {
        ControllerBrain targetBrain = ctx.target.GetComponent<ControllerBrain>();
        return targetBrain != null ? targetBrain.GetProvider<ICombatantState>() : null;
    }

    static bool IsBlocking(GOAPContext ctx)
    {
        Blackboard blackboard = ctx.brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
    }
}
