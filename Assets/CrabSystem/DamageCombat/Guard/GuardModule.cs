using System;
using UnityEngine;

// The guard (Combat_Framework §3, Parry_Build.md). A defensive ability raises it and it stays up while
// the brain's ability control source holds the block key. While up it answers DamageSystem's
// intercept: a hit inside an open parry window is parried, any other hit in the guard arc is blocked
// (stamina, the Guard bar, posture on both fighters, blockstun), and a hit that fills the posture bar
// breaks the guard. A guarded hit never touches health (no chip, 2026-10-05).
//
// The Guard bar (§3.5) is soak dice — guardDice × d(guardDieFaces) plus the Deflection die. A blocked
// hit drains it; once it's empty the rest of the hit becomes posture damage. It refills while the
// fighter goes without blocking.
//
// AbilitySystem hands over: Raise for a defensive ability, MoveStarted for every other move (a parry
// opens the window, an attack started soon after a parry is the riposte), and asks Allows before
// anything fires.
public class GuardModule : MonoBehaviour, IBrainModule
{
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Frames a parry window loses for each press made soon after the last one. Mashing " +
             "shrinks the window; a successful parry resets it.")]
    [SerializeField] private int parryDecayFrames = 3;

    [Tooltip("The window never shrinks below this many frames.")]
    [SerializeField] private int minParryFrames = 2;

    [Tooltip("A press more than this many seconds after the last one gets the full window again.")]
    [SerializeField] private float parryResetSeconds = 0.5f;

    [Tooltip("Seconds after a parry in which this entity's next attack is a riposte: its first hit rolls with advantage.")]
    [SerializeField] private float riposteSeconds = 1f;

    [Tooltip("Stamina and posture a guarded hit costs when the attacking move has no block stamina authored.")]
    [SerializeField] private int defaultGuardStamina = 5;

    [Tooltip("A parried attacker takes the move's block stamina times this as posture.")]
    [SerializeField] private float parryPostureMultiplier = 1.5f;

    [Tooltip("A blocked attacker takes this share of the block's posture cost; the defender takes all of it.")]
    [SerializeField] private float attackerBlockPostureShare = 0.25f;

    [Tooltip("Blockstun frames when the attacking move has no frame data authored.")]
    [SerializeField] private int defaultBlockstunFrames = 12;

    [Tooltip("Blockstun never drops below this. Placeholder clips run longer than their authored frames, " +
             "so the hit often lands after the move's frame data says it has ended.")]
    [SerializeField] private int minBlockstunFrames = 8;

    [Header("Guard Bar (placeholder numbers — CF3b tuning)")]
    [Tooltip("Soak dice in the Guard bar, before the Deflection die.")]
    [SerializeField] private int guardDice = 2;

    [SerializeField] private int guardDieFaces = 4;

    [SerializeField] private SoakBar guardBar = new SoakBar();

    [Header("Hit-Stop (frames at 60 fps)")]
    [Tooltip("Both fighters freeze this long when a hit is blocked.")]
    [SerializeField] private int blockedStopFrames = 2;

    [Tooltip("Both fighters freeze this long when a hit is parried.")]
    [SerializeField] private int parryStopFrames = 10;

    [Tooltip("The fighter whose guard breaks freezes this long.")]
    [SerializeField] private int guardBreakStopFrames = 12;

    [Header("Parried")]
    // Counts through the parry hit-stop. Short of a guaranteed riposte: the riposte's strike lands after
    // it ends, so the attacker can still guard if they read it.
    [Tooltip("A parried attacker is flinched this long (frames at 60 fps): their string ends and the parryer moves first.")]
    [SerializeField] private int parriedStunFrames = 36;

    private const string StaminaId = "stamina";
    private const string DeflectionStat = "def.deflection";
    private const string DeflectSideParam = "DeflectSide";
    private const string BlockedHitTrigger = "BlockedHit";
    private const string ParriedTrigger = "Parried";
    // The deflect pose (Guard → Block → Parry). Fired here, not as the parry ability's animationTrigger, so the move
    // bake never treats a parry as a move (cleared from LSParry 5 Oct, which left the pose unplayed until 8 Oct).
    private const string ParryTrigger = "Parry";
    // The guard is only entered from Rest, so a guard raised during an attack's tail cuts straight to it.
    private static readonly int GuardPoseState = Animator.StringToHash("Actions.Guard.Block");
    private static readonly int RestState = Animator.StringToHash("Actions.Rest");
    private const float GuardCutSeconds = 0.1f;

    private ControllerBrain brain;
    private AbilitySystem abilities;
    private DamageSystem damage;
    private PostureModule posture;
    private StatusSystem statuses;
    private StateMachineModule stateMachine;
    private IResourceProvider resources;
    private IAbilityControlSource control;
    private Blackboard blackboard;

    private AbilityDefinition guard;
    private float parryWindowEndsAt = -999f;
    private float lastParryPressAt = -999f;
    private int parryPresses;
    private float riposteUntil = -999f;
    private AbilityDefinition riposteMove;
    private float blockstunUntil = -999f;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool IsGuarding => guard != null;
    public bool ParryWindowOpen => Now <= parryWindowEndsAt;
    public bool InBlockstun => Now < blockstunUntil;
    public IBarSource GuardBar => guardBar;

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnPerfectBlock;                // a hit landed inside an open parry window; Juice listens
    public event Action<int> OnParryWindowOpened;      // window length in frames
    public event Action<int, float, int> OnGuardedHit; // stamina cost, posture added, blockstun frames
    public event Action<float> OnPostureDamaged;       // posture this entity took as the attacker: parried or blocked
    public event Action OnGuardBreak;                // the posture bar filled
    public event Action<float> OnGuardFlanked;       // a hit landed outside the guard arc; its angle off facing

    // Until the entity clock lands (CrabSystem_Standard §9).
    private static float Now => Time.time;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        abilities = brain.Abilities;
        damage = brain.Damage;
        posture = brain.GetModule<PostureModule>();
        statuses = brain.GetModule<StatusSystem>();
        stateMachine = brain.StateMachine;
        resources = brain.GetProvider<IResourceProvider>();
        control = brain.GetProvider<IAbilityControlSource>();

        if (abilities == null || damage == null)
        {
            Debug.LogError($"[GuardModule] {brain.EntityName} needs an AbilitySystem and a DamageSystem", this);
            return;
        }

        damage.OnDeath += HandleDeath;
    }

    public void LateInitialize()
    {
        blackboard = brain.Blackboard;
        if (blackboard == null)
        {
            Debug.LogError($"[GuardModule] No blackboard on {brain.EntityName}", this);
            return;
        }

        blackboard.OnBoolChanged += HandleFactChanged;
    }

    private void OnDestroy()
    {
        if (damage != null)
        {
            damage.OnDeath -= HandleDeath;
            damage.OnDamageIntercept -= HandleDamageIntercept;
        }

        if (blackboard != null)
            blackboard.OnBoolChanged -= HandleFactChanged;
    }

    public void UpdateModule()
    {
        guardBar.Configure(0f, guardDice, guardDieFaces, Mathf.FloorToInt(Stat(DeflectionStat)));
        guardBar.Tick(Now);

        if (guard == null) return;

        if (!GuardHeld)
        {
            Lower();
            return;
        }

        DrainHold();
    }

    private bool GuardHeld => control != null && control.GuardHeld;

    // While the guard is up, or in blockstun, only a parry can fire: lower the guard to attack.
    public bool Allows(AbilityDefinition ability)
    {
        if (ability.abilityType == AbilityType.Defensive) return guard == null;
        if (ability.IsParry) return true;
        return guard == null && !InBlockstun;
    }

    public void Raise(AbilityDefinition ability)
    {
        if (guard != null) return;

        guard = ability;
        blackboard?.SetBool(BlackboardKey.IsBlocking, true);
        if (stateMachine != null) stateMachine.TryTransitionUpperBody(UpperBodyState.Blocking);
        damage.OnDamageIntercept += HandleDamageIntercept;
        CutToGuardPose();

        OnBlockStart?.Invoke();
    }

    public void Lower()
    {
        if (guard == null) return;

        damage.OnDamageIntercept -= HandleDamageIntercept;
        blackboard?.SetBool(BlackboardKey.IsBlocking, false);
        guard = null;

        if (stateMachine != null && stateMachine.GetUpperBodyState() == UpperBodyState.Blocking)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        OnBlockEnd?.Invoke();
    }

    public void MoveStarted(AbilityDefinition ability)
    {
        if (ability.IsParry)
        {
            OpenParryWindow(ability);
            return;
        }

        MarkRiposte(ability);
    }

    // Hard control and death drop the guard the moment they land (F10, F11).
    private void HandleFactChanged(int key, bool value)
    {
        if (value && key == BlackboardKey.CannotAct) Lower();
    }

    private void HandleDeath() => Lower();

    // A guard with a stamina drain lowers itself when it can't pay the next frame.
    private void DrainHold()
    {
        if (resources == null || guard.resourceCosts == null) return;

        foreach (var cost in guard.resourceCosts)
        {
            if (cost.drain <= 0f) continue;

            float amount = cost.drain * Time.deltaTime;
            if (!resources.HasResource(cost.resource, amount))
            {
                Lower();
                return;
            }

            resources.ConsumeResource(cost.resource, amount);
        }
    }

    // Combat_Framework §3.1. A block costs the defender stamina, drains the Guard bar (the rest of the
    // hit becomes posture once it's empty) and fills both fighters' posture, the defender's more.
    // Running out of stamina only empties the pool; a full posture bar breaks the guard and the hit lands
    // normally. DamageSystem puts nothing of a blocked hit on health.
    private void HandleDamageIntercept(DamageInterceptArgs args)
    {
        if (guard == null) return;
        if (!InGuardArc(args.attackDirection))
        {
            OnGuardFlanked?.Invoke(Vector3.Angle(transform.forward, -args.attackDirection));
            return;
        }

        int cost = GuardStamina(args.attacker);

        if (ParryWindowOpen)
        {
            Parry(args, cost);
            return;
        }

        int blockCost = Mathf.Max(1, cost - Mathf.FloorToInt(Stat(DeflectionStat)) + Mathf.RoundToInt(Stat(DialIds.BlockStamina)));
        DrainStamina(blockCost);

        float overflow = guardBar.Absorb(args.damage, Now);
        float postureCost = cost + overflow;
        GuardModule attackerGuard = args.attacker != null ? args.attacker.GetModule<GuardModule>() : null;
        if (attackerGuard != null) attackerGuard.TakePosture(cost * attackerBlockPostureShare);

        if (AddPosture(postureCost))
        {
            args.outcome = GuardOutcome.Broken;
            BreakGuard();
            return;
        }

        int blockstun = BlockstunFrames(args.attacker);
        blockstunUntil = Now + blockstun / 60f;
        args.outcome = GuardOutcome.Blocked;
        FreezeBoth(args.attacker, blockedStopFrames);

        if (brain.Animation != null) brain.Animation.TriggerCombatAnimation(BlockedHitTrigger);
        OnGuardedHit?.Invoke(blockCost, postureCost, blockstun);
    }

    private bool InGuardArc(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;
        return guard.CanBlock(transform.forward, attackDirection);
    }

    // Combat_Framework §3.2: free for the defender; the attacker pays the move's block stamina, takes
    // posture and a short flinch that ends their string, and the defender's next attack is a riposte.
    private void Parry(DamageInterceptArgs args, int cost)
    {
        args.outcome = GuardOutcome.Parried;
        parryWindowEndsAt = -999f;
        lastParryPressAt = -999f;
        riposteUntil = Now + Mathf.Max(0f, riposteSeconds + Stat(DialIds.RiposteWindow));

        FlipDeflectSide();
        FreezeBoth(args.attacker, parryStopFrames);
        OnPerfectBlock?.Invoke();

        GuardModule attackerGuard = args.attacker != null ? args.attacker.GetModule<GuardModule>() : null;
        if (attackerGuard != null) attackerGuard.TakeParried(cost, Mathf.RoundToInt(Stat(DialIds.ParryStun)));
    }

    // This entity's swing was parried: the blade rebounds (the animator's Parried trigger, if authored)
    // and the fighter is flinched for parriedStunFrames plus the parryer's cmb.parry_stun. A posture break on
    // top replaces it with Guard Break.
    public void TakeParried(int cost, int extraStunFrames)
    {
        DrainStamina(cost);
        TriggerIfPresent(ParriedTrigger);
        FlinchParried(parriedStunFrames + extraStunFrames);
        TakePosture(cost * parryPostureMultiplier);
    }

    private void FlinchParried(int frames)
    {
        if (frames <= 0 || statuses == null) return;

        StatusDefinition status = AbilityDefinition.LoadHitState(HitState.Flinch);
        if (status != null) statuses.Apply(status, null, frames / 60f);
    }

    // Posture this entity takes as the attacker — its swing was parried or blocked.
    public void TakePosture(float amount)
    {
        bool broken = AddPosture(amount);
        OnPostureDamaged?.Invoke(amount);
        if (broken) BreakGuard();
    }

    // Guard Break (§3.3): the guard drops and the fighter is open for the GuardBroken hit state, for as
    // long as the PostureModule says.
    private void BreakGuard()
    {
        Lower();

        float seconds = posture != null ? posture.BreakSeconds : 0f;
        StatusDefinition status = AbilityDefinition.LoadHitState(HitState.GuardBreak);
        if (status != null && statuses != null) statuses.Apply(status, null, seconds);

        abilities.HitStop(guardBreakStopFrames);
        OnGuardBreak?.Invoke();
    }

    // A guarded hit is frozen here rather than by the attacker's NotifyHitLanded, which skips it.
    private void FreezeBoth(ControllerBrain attacker, int frames)
    {
        abilities.HitStop(frames);
        AbilitySystem attackerAbilities = attacker != null ? attacker.Abilities : null;
        if (attackerAbilities != null) attackerAbilities.HitStop(frames);
    }

    // True when this fills the posture bar. A fighter without a PostureModule never breaks.
    private bool AddPosture(float amount) => posture != null && posture.Damage(amount);

    // A press soon after the last one gets a shorter window; a pause, or a successful parry, resets it.
    private void OpenParryWindow(AbilityDefinition ability)
    {
        bool mashing = Now - lastParryPressAt <= parryResetSeconds;
        parryPresses = mashing ? parryPresses + 1 : 0;
        lastParryPressAt = Now;

        int frames = Mathf.Max(minParryFrames, ability.parryFrames + Mathf.RoundToInt(Stat(DialIds.ParryWindow)) - parryPresses * parryDecayFrames);
        parryWindowEndsAt = Now + frames / 60f;
        // Only from a raised guard: the Parry state is entered from Block, and a trigger set without one would
        // stay armed and fire the next time the guard went up.
        if (guard != null) TriggerIfPresent(ParryTrigger);
        OnParryWindowOpened?.Invoke(frames);
    }

    // The riposte belongs to the first attack STARTED inside the window, so a slow swing that lands
    // after the window has closed still counts.
    private void MarkRiposte(AbilityDefinition ability)
    {
        riposteMove = Now <= riposteUntil ? ability : null;
        riposteUntil = -999f;
    }

    // True once, for the first hit of the riposte move. Asked by this entity's own DamageSystem as it
    // builds the packet, which carries the advantage to the defender.
    public bool TakeRiposte()
    {
        if (riposteMove == null || abilities.CurrentAbility != riposteMove) return false;

        riposteMove = null;
        return true;
    }

    // The attacker's move is read through ICombatantState (Audit 5 A5).
    private int GuardStamina(ControllerBrain attacker)
    {
        ICombatantState state = attacker != null ? attacker.GetProvider<ICombatantState>() : null;
        AbilityDefinition move = state != null ? state.CurrentAbility : null;

        int cost = move != null && move.HasMoveData ? move.hit.blockStamina : 0;
        return cost > 0 ? cost : defaultGuardStamina;
    }

    // Blockstun lasts the attacker's remaining recovery plus the move's on-block advantage (usually
    // minus), so "-2 on block" means the attacker recovers 2 frames after the defender.
    private int BlockstunFrames(ControllerBrain attacker)
    {
        ICombatantState state = attacker != null ? attacker.GetProvider<ICombatantState>() : null;
        AbilityDefinition move = state != null ? state.CurrentAbility : null;
        if (move == null || !move.HasMoveData) return defaultBlockstunFrames;

        int total = move.frames.startup + move.frames.active + move.frames.recovery;
        int remaining = Mathf.Max(0, total - state.CurrentMoveFrame);
        return Mathf.Max(minBlockstunFrames, remaining + move.hit.blockAdvantage);
    }

    private float Stat(string statId) => brain.Stats != null ? brain.Stats.GetValue(statId) : 0f;

    // Spends the cost, or as much as there is.
    private void DrainStamina(int cost)
    {
        ResourceSystem pools = brain.Resources;
        ResourceDefinition stamina = pools != null ? pools.FindDefinition(StaminaId) : null;
        if (stamina == null) return;
        if (pools.ConsumeResource(stamina, cost)) return;

        pools.ConsumeResource(stamina, pools.GetResource(stamina));
    }

    // An attack's flourish plays on past Unlocked until its clip ends, and the guard state is only reached
    // from Rest, so raising the guard in that tail left her sheathing the blade while blocking.
    private void CutToGuardPose()
    {
        Animator animator = brain.EntityAnimator;
        int layer = animator != null ? animator.GetLayerIndex(AnimationLayerNames.Actions) : -1;
        if (layer < 0 || !animator.HasState(layer, GuardPoseState)) return;
        if (animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == RestState) return;

        animator.CrossFadeInFixedTime(GuardPoseState, GuardCutSeconds, layer);
    }

    private void TriggerIfPresent(string trigger)
    {
        Animator animator = brain.EntityAnimator;
        if (animator == null || !HasParameter(animator, trigger)) return;

        animator.SetTrigger(trigger);
    }

    // Alternates the deflect pose on each parry, if the animator has the parameter.
    private void FlipDeflectSide()
    {
        Animator animator = brain.EntityAnimator;
        if (animator == null || !HasParameter(animator, DeflectSideParam)) return;

        animator.SetBool(DeflectSideParam, !animator.GetBool(DeflectSideParam));
    }

    private static bool HasParameter(Animator animator, string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == name) return true;
        }
        return false;
    }
}
