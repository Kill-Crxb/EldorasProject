using System;
using UnityEngine;

// The LMB moveset (Moveset_Build.md). A press reads the context, picks a chain from the active
// weapon's moveset and fires that chain's next step. A press made while a step plays is held (one
// press; the latest wins) and fires the moment the step ends, or from the step's ChainOpen event when
// its clip has one; an interrupt (hit state, death) drops it. Like Dark Souls, a running or air attack
// carries on into the light string's next step.
public class MovesetModule : MonoBehaviour, IBrainModule
{
    [Tooltip("Seconds a held press survives once the entity is free to act and the step still can't fire " +
             "(blockstun, a refused ability). While a step plays the press waits however long it takes.")]
    [SerializeField] private float bufferLifetime = 1f;

    [Tooltip("Equipment slot whose item supplies the armed moveset.")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string weaponSlotId = "mainwep";

    private ControllerBrain brain;
    private AbilitySystem abilities;
    private CombatStanceModule stance;
    private EquipmentSystem equipment;
    private GuardModule guard;

    private MovesetChain chain;
    private int nextStep;
    private AbilityDefinition stepInFlight;
    private float stepEndedAt = -999f;

    private bool buffered;
    private MovesetChain bufferedChain;
    private int bufferedStep;
    private float bufferedAt;

    private AbilityDefinition lastPreview;
    private AbilityDefinition lastBlockPreview;

    public bool IsEnabled { get; set; } = true;

    // The step LMB would fire, or the block RMB would raise, changed (stance, weapon, context, chain position). The hotbar redraws the slot.
    public event Action OnPreviewChanged;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
    }

    public void LateInitialize()
    {
        abilities = brain.GetModule<AbilitySystem>();
        stance = brain.GetModule<CombatStanceModule>();
        equipment = brain.GetModule<EquipmentSystem>();
        guard = brain.GetModule<GuardModule>();

        if (abilities == null)
            Debug.LogError($"[MovesetModule] No AbilitySystem on {brain.EntityName}");
    }

    public void UpdateModule()
    {
        if (!IsEnabled || abilities == null) return;

        TrackStepInFlight();
        FireBuffered();
        TrackPreview();
    }

    public WeaponMoveset ActiveMoveset
    {
        get
        {
            if (stance != null && stance.IsUnarmed) return stance.UnarmedMoveset;
            if (equipment == null) return null;

            ItemInstance item = equipment.GetEquippedItem(weaponSlotId);
            if (item == null || item.Definition == null) return null;
            return item.Definition.moveset;
        }
    }

    public void Press() => Perform(ResolveChain());

    public bool HasBufferedPress => buffered;
    public bool StepInFlight => stepInFlight != null;

    public bool Perform(MovesetChain requested)
    {
        if (!IsEnabled || abilities == null) return false;

        int step = StepFor(ref requested);
        AbilityDefinition ability = StepAt(requested, step);
        if (ability == null) return false;

        Grant(ability);

        // Held while a step plays, the guard is in blockstun or a hit state holds the fighter, so it comes out on
        // the first free frame instead of being dropped. A parry is never held: it can be pressed in blockstun.
        bool stunned = Stunned() && requested != MovesetChain.Parry;
        if (stepInFlight != null || stunned)
        {
            Buffer(requested, step);
            return true;
        }

        if (Fire(requested, step, ability)) return true;

        abilities.ReportRefused(ability.abilityId);
        return false;
    }

    public AbilityDefinition PeekNext()
    {
        if (abilities == null) return null;

        MovesetChain resolved = ResolveChain();
        int step = StepFor(ref resolved);
        AbilityDefinition ability = StepAt(resolved, step);
        if (ability != null) Grant(ability);
        return ability;
    }

    public AbilityDefinition PeekBlock()
    {
        if (abilities == null) return null;

        WeaponMoveset moveset = ActiveMoveset;
        if (moveset == null || moveset.block == null) return null;

        Grant(moveset.block);
        return moveset.block;
    }

    // Called every frame RMB is held; CanUseAbility refuses while the guard is already up.
    public void PressBlock()
    {
        if (!IsEnabled) return;

        AbilityDefinition block = PeekBlock();
        if (block == null || !abilities.CanUseAbility(block.abilityId)) return;

        abilities.UseAbility(block.abilityId);
    }

    private MovesetChain ResolveChain()
    {
        Blackboard blackboard = brain.Blackboard;

        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking)) return MovesetChain.Parry;

        // The context picks a string when it starts; once going it stays that string. A root-motion
        // lunge moves the attacker at running speed, so re-reading the context made the next press
        // the Running chain's first step. A parry is not a string: with the guard down, the next press is a fresh attack.
        if (InString() && chain != MovesetChain.Parry) return chain;

        if (brain.Movement != null && !brain.Movement.IsGrounded) return MovesetChain.Air;
        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsRunning)) return MovesetChain.Running;
        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsSprinting)) return MovesetChain.Running;
        return MovesetChain.Light;
    }

    // Where a press lands: the string's next step; once a running or air attack is spent, the light
    // string's step after it (Dark Souls: a running attack chains into the second light); otherwise a
    // new string from step 0.
    private int StepFor(ref MovesetChain requested)
    {
        if (requested != chain || !InString()) return 0;
        if (nextStep < StepCount(chain)) return nextStep;

        bool carriesIntoLight = chain == MovesetChain.Running || chain == MovesetChain.Air;
        if (!carriesIntoLight || nextStep >= StepCount(MovesetChain.Light)) return 0;

        requested = MovesetChain.Light;
        return nextStep;
    }

    private int StepCount(MovesetChain requested)
    {
        WeaponMoveset moveset = ActiveMoveset;
        return moveset != null ? moveset.Chain(requested).Count : 0;
    }

    // A step is playing, or ended within the moveset's chainGrace.
    private bool InString()
    {
        if (nextStep <= 0) return false;
        if (stepInFlight != null) return true;

        WeaponMoveset moveset = ActiveMoveset;
        return moveset != null && Time.time - stepEndedAt <= moveset.chainGrace;
    }

    private AbilityDefinition StepAt(MovesetChain requested, int step)
    {
        WeaponMoveset moveset = ActiveMoveset;
        if (moveset == null) return null;

        var steps = moveset.Chain(requested);
        if (step < 0 || step >= steps.Count) return null;
        return steps[step];
    }

    private void Grant(AbilityDefinition ability)
    {
        if (abilities.GetAbility(ability.abilityId) != null) return;
        abilities.AddAbility(ability);
    }

    private bool Fire(MovesetChain requested, int step, AbilityDefinition ability)
    {
        if (!StartStep(ability)) return false;

        chain = requested;
        nextStep = step + 1;
        stepInFlight = ability;
        return true;
    }

    // From the step's ChainOpen the step in flight hands straight over; otherwise the next waits for a free entity.
    private bool StartStep(AbilityDefinition ability)
    {
        if (stepInFlight != null) return abilities.ChainInto(ability.abilityId);
        if (!abilities.CanUseAbility(ability.abilityId)) return false;

        abilities.UseAbility(ability.abilityId);
        return abilities.CurrentAbility == ability;
    }

    private void Buffer(MovesetChain requested, int step)
    {
        buffered = true;
        bufferedChain = requested;
        bufferedStep = step;
        bufferedAt = Time.time;
    }

    private void TrackStepInFlight()
    {
        if (stepInFlight == null) return;
        if (abilities.CurrentAbility == stepInFlight) return;

        stepInFlight = null;
        stepEndedAt = Time.time;

        if (!Interrupted()) return;

        buffered = false;
        nextStep = 0;
    }

    private void FireBuffered()
    {
        if (!buffered) return;
        if (stepInFlight != null && !abilities.ChainOpen) return;

        // Raising the guard cancels a held attack; only a held parry survives it.
        if (IsBlocking() && bufferedChain != MovesetChain.Parry)
        {
            buffered = false;
            return;
        }

        // Still held: the lifetime starts when the fighter is free again.
        if (Stunned())
        {
            bufferedAt = Time.time;
            return;
        }

        // Counted from whichever came later, the press or the step ending, so a long step can't age it out.
        bool expired = stepInFlight == null && Time.time - Mathf.Max(bufferedAt, stepEndedAt) > bufferLifetime;
        if (expired)
        {
            buffered = false;
            return;
        }

        AbilityDefinition ability = StepAt(bufferedChain, bufferedStep);
        if (ability == null)
        {
            buffered = false;
            return;
        }

        if (!Fire(bufferedChain, bufferedStep, ability)) return;
        buffered = false;
    }

    private void TrackPreview()
    {
        AbilityDefinition preview = PeekNext();
        AbilityDefinition blockPreview = PeekBlock();
        if (preview == lastPreview && blockPreview == lastBlockPreview) return;

        lastPreview = preview;
        lastBlockPreview = blockPreview;
        OnPreviewChanged?.Invoke();
    }

    private bool Stunned()
    {
        if (guard != null && guard.InBlockstun) return true;

        Blackboard blackboard = brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.CannotAct);
    }

    private bool IsBlocking()
    {
        Blackboard blackboard = brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
    }

    private bool Interrupted()
    {
        if (brain.Damage != null && brain.Damage.IsDead) return true;

        Blackboard blackboard = brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.CannotAct);
    }
}
