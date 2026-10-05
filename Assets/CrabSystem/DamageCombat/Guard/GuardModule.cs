using System;
using UnityEngine;

// The guard (Combat_Framework §3, Parry_Build.md). A defensive ability raises it and it stays up while
// the brain's ability control source holds the block key. While up it answers DamageSystem's
// intercept: a hit inside an open parry window is parried, any other hit in the guard arc is blocked
// (stamina, posture, blockstun), and a hit that fills the posture bar breaks the guard.
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

    [Tooltip("Blockstun frames when the attacking move has no frame data authored.")]
    [SerializeField] private int defaultBlockstunFrames = 12;

    [Tooltip("Blockstun never drops below this. Placeholder clips run longer than their authored frames, " +
             "so the hit often lands after the move's frame data says it has ended.")]
    [SerializeField] private int minBlockstunFrames = 8;

    private const string StaminaId = "stamina";
    private const string DeflectionStat = "def.deflection";
    private const string DeflectSideParam = "DeflectSide";
    private const string BlockedHitTrigger = "BlockedHit";
    private const string ParriedTrigger = "Parried";

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

    public event Action OnBlockStart;
    public event Action OnBlockEnd;
    public event Action OnPerfectBlock;              // a hit landed inside an open parry window; Juice listens
    public event Action<int> OnParryWindowOpened;    // window length in frames
    public event Action<int, int, int> OnGuardedHit; // stamina cost, posture added, blockstun frames
    public event Action<float> OnPostureDamaged;     // posture a parry put on this entity as the attacker
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

    // Combat_Framework §3.1. A guarded hit costs stamina and fills posture. Running out of stamina
    // only empties the bar; a full posture bar breaks the guard and the hit lands normally.
    // DamageSystem resolves a blocked hit's damage through guard soak.
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

        int blockCost = Mathf.Max(1, cost - Mathf.FloorToInt(Stat(DeflectionStat)));
        DrainStamina(blockCost);

        if (AddPosture(cost))
        {
            args.outcome = GuardOutcome.Broken;
            BreakGuard();
            return;
        }

        int blockstun = BlockstunFrames(args.attacker);
        blockstunUntil = Now + blockstun / 60f;
        args.outcome = GuardOutcome.Blocked;

        if (brain.Animation != null) brain.Animation.TriggerCombatAnimation(BlockedHitTrigger);
        OnGuardedHit?.Invoke(blockCost, cost, blockstun);
    }

    private bool InGuardArc(Vector3 attackDirection)
    {
        if (attackDirection == Vector3.zero) return true;
        return guard.CanBlock(transform.forward, attackDirection);
    }

    // Combat_Framework §3.2: free for the defender; the attacker pays the move's block stamina and takes
    // posture, and the defender's next attack is a riposte. The attacker's string carries on unless
    // their posture breaks.
    private void Parry(DamageInterceptArgs args, int cost)
    {
        args.outcome = GuardOutcome.Parried;
        parryWindowEndsAt = -999f;
        lastParryPressAt = -999f;
        riposteUntil = Now + riposteSeconds;

        FlipDeflectSide();
        OnPerfectBlock?.Invoke();

        GuardModule attackerGuard = args.attacker != null ? args.attacker.GetModule<GuardModule>() : null;
        if (attackerGuard != null) attackerGuard.TakeParried(cost);
    }

    // This entity's swing was parried: the blade rebounds (the animator's Parried trigger, if authored).
    public void TakeParried(int cost)
    {
        DrainStamina(cost);
        TriggerIfPresent(ParriedTrigger);

        float amount = cost * parryPostureMultiplier;
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

        OnGuardBreak?.Invoke();
    }

    // True when this fills the posture bar. A fighter without a PostureModule never breaks.
    private bool AddPosture(float amount) => posture != null && posture.Damage(amount);

    // A press soon after the last one gets a shorter window; a pause, or a successful parry, resets it.
    private void OpenParryWindow(AbilityDefinition ability)
    {
        bool mashing = Now - lastParryPressAt <= parryResetSeconds;
        parryPresses = mashing ? parryPresses + 1 : 0;
        lastParryPressAt = Now;

        int frames = Mathf.Max(minParryFrames, ability.parryFrames - parryPresses * parryDecayFrames);
        parryWindowEndsAt = Now + frames / 60f;
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

    // The attacker's move is read through its AbilitySystem until ICombatantState lands (Audit 5 A5).
    private int GuardStamina(ControllerBrain attacker)
    {
        AbilityDefinition move = attacker != null && attacker.Abilities != null ? attacker.Abilities.CurrentAbility : null;

        int cost = move != null && move.HasMoveData ? move.hit.blockStamina : 0;
        return cost > 0 ? cost : defaultGuardStamina;
    }

    // Blockstun lasts the attacker's remaining recovery plus the move's on-block advantage (usually
    // minus), so "-2 on block" means the attacker recovers 2 frames after the defender.
    private int BlockstunFrames(ControllerBrain attacker)
    {
        AbilitySystem attackerAbilities = attacker != null ? attacker.Abilities : null;
        AbilityDefinition move = attackerAbilities != null ? attackerAbilities.CurrentAbility : null;
        if (move == null || !move.HasMoveData) return defaultBlockstunFrames;

        int total = move.frames.startup + move.frames.active + move.frames.recovery;
        int remaining = Mathf.Max(0, total - attackerAbilities.CurrentMoveFrame);
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
