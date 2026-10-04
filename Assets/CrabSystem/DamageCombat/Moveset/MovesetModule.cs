using System;
using UnityEngine;

// The LMB moveset (Moveset_Build.md). A press reads the context, picks a chain from the active
// weapon's moveset and fires that chain's next step. A press made while a step plays is held and
// fires the moment the entity can act again; an interrupt (hit state, death) drops it.
public class MovesetModule : MonoBehaviour, IBrainModule
{
    [Tooltip("Seconds a held press survives while the current step plays.")]
    [SerializeField] private float bufferLifetime = 1f;

    [Tooltip("Equipment slot whose item supplies the armed moveset.")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string weaponSlotId = "mainwep";

    private ControllerBrain brain;
    private AbilitySystem abilities;
    private CombatStanceModule stance;
    private EquipmentSystem equipment;

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

        int step = Continues(requested) ? nextStep : 0;
        AbilityDefinition ability = StepAt(requested, step);
        if (ability == null) return false;

        Grant(ability);

        // Held while a step plays or the guard is in blockstun, so a punish comes out on the first free
        // frame. A parry is never held: it can be pressed in blockstun.
        bool stunned = abilities.InBlockstun && requested != MovesetChain.Parry;
        if (stepInFlight != null || stunned)
        {
            Buffer(requested, step);
            return true;
        }

        return Fire(requested, step, ability);
    }

    public AbilityDefinition PeekNext()
    {
        if (abilities == null) return null;

        MovesetChain resolved = ResolveChain();
        AbilityDefinition ability = StepAt(resolved, Continues(resolved) ? nextStep : 0);
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
        if (brain.Movement != null && !brain.Movement.IsGrounded) return MovesetChain.Air;
        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsRunning)) return MovesetChain.Running;
        if (blackboard != null && blackboard.GetBool(BlackboardKey.IsSprinting)) return MovesetChain.Running;
        return MovesetChain.Light;
    }

    private bool Continues(MovesetChain requested)
    {
        if (requested != chain) return false;
        if (nextStep <= 0) return false;

        WeaponMoveset moveset = ActiveMoveset;
        if (moveset == null || nextStep >= moveset.Chain(requested).Count) return false;
        if (stepInFlight != null) return true;

        return Time.time - stepEndedAt <= moveset.chainGrace;
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
        if (!abilities.CanUseAbility(ability.abilityId)) return false;

        abilities.UseAbility(ability.abilityId);
        if (abilities.CurrentAbility != ability) return false;

        chain = requested;
        nextStep = step + 1;
        stepInFlight = ability;
        return true;
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
        if (!buffered || stepInFlight != null) return;

        // Raising the guard cancels a held attack; only a held parry survives it.
        if (IsBlocking() && bufferedChain != MovesetChain.Parry)
        {
            buffered = false;
            return;
        }

        if (Time.time - bufferedAt > bufferLifetime)
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

        if (!abilities.CanUseAbility(ability.abilityId)) return;

        buffered = false;
        Fire(bufferedChain, bufferedStep, ability);
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
