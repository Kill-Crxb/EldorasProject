using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class AbilitySystem : MonoBehaviour, IBrainModule, IAbilityProvider, ICombatantState
{
    public int InitOrder => 80;

    [SerializeField] private bool isEnabled = true;
    [SerializeField] private AbilityLoadoutModule loadoutModule;
    [SerializeField] private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

    [Header("Strikes")]
    [Tooltip("Equipment slot whose weapon supplies a strike's reach (ItemDefinition.reach).")]
    [IdRef(IdKind.EquipmentSlot)] [SerializeField] private string weaponSlotId = "mainwep";

    [Header("Hit feel")]
    [Tooltip("GI-06: while a clean hit freezes this fighter, its model shakes sideways this far (metres). 0 = off.")]
    [SerializeField] private float hitShakeAmplitude = 0.04f;
    [Tooltip("GI-03: after a clean hit, the string's next step may start the moment the hit-stop ends, before the " +
             "clip's ChainOpen. A whiff waits for ChainOpen or Unlocked.")]
    [SerializeField] private bool hitConfirmCancel = false;

    [Header("Debug")]
    [Tooltip("Log move events and forwarder binding.")]
    [SerializeField] private bool debugLogging = false;

    private class AbilityState
    {
        public AbilityDefinition definition;
        public CountdownTimer cooldown;
        public bool IsOnCooldown => cooldown != null && !cooldown.IsFinished;

        public AbilityState(AbilityDefinition def)
        {
            definition = def;
        }
    }

    private ControllerBrain brain;
    private StateMachineModule stateMachine;
    private Blackboard blackboard;
    private RuntimeAbilityManager runtimeAbilityManager;
    private IAnimationProvider animationProvider;
    private IResourceProvider resources;
    private IHealthProvider healthProvider;
    private MovementSystem movementSystem;
    private DamageSystem damageSystem;
    private VFXSystem vfxSystem;
    private AnimationEventForwarder eventForwarder;
    private ModelModule modelModule;
    private GuardModule guard;

    private Dictionary<string, AbilityState> abilityStates = new Dictionary<string, AbilityState>();

    private ItemInstance cachedEquippedWeapon;
    private DiceProfile cachedNaturalWeapon;

    private AbilityDefinition currentAbility = null;
    private float moveClock;
    private MovePhase movePhase;
    private Animator frozenAnimator;
    private float hitStopUntil;
    private Transform shakenModel;
    private Vector3 shakenHome;
    private bool hitConfirmed;
    private Coroutine safetyTimeoutCoroutine;

    private string currentlyCastingAbility = null;
    private float castStartTime;

    private bool isAnimationLocked = false;
    private bool isInvincible = false;
    // A timed i-frame window (invulnSeconds) outlives its move; 0 = none running.
    private float invulnUntil;

    private StrikeHandler strikes;
    private int nextStrike;
    private bool told;
    private float toldAt;
    private bool chainOpen;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public AbilityLoadoutModule Loadout => loadoutModule;
    public bool IsExecuting => currentAbility != null || currentlyCastingAbility != null || isAnimationLocked;
    public bool IsCasting => currentlyCastingAbility != null;
    public AbilityDefinition CurrentAbility => currentAbility;
    public string CurrentAbilityId => currentAbility?.abilityId;
    public bool IsHitStopped => hitStopUntil > 0f;
    // Frames since the current move started, at 60 fps, from the move clock. -1 when nothing is executing.
    public int CurrentMoveFrame => currentAbility != null ? Mathf.FloorToInt(moveClock) : -1;
    // From the clip's events: the first Strike opens Active, the last strike opens Recovery.
    public MovePhase CurrentPhase => currentAbility != null ? movePhase : MovePhase.None;
    public bool IsArmored => currentAbility != null && currentAbility.HasArmorAt(CurrentMoveFrame);
    public bool IsInvincible => isInvincible;
    public float TellFor => currentAbility != null && told ? Time.time - toldAt : -1f;
    // From the clip's ChainOpen to the move's end: MovesetModule may start the string's next step.
    public bool ChainOpen => currentAbility != null && chainOpen;
    public StrikeHandler Strikes => strikes;
    public bool IsGuarding => guard != null && guard.IsGuarding;
    public bool InBlockstun => guard != null && guard.InBlockstun;

    public event Action<string> OnAbilityUsed;
    // A press that did nothing and wasn't held: on cooldown, short of a resource (named), or denied by a fact. A press
    // made mid-move is buffered, not refused. The HUD answers it (GI-23).
    public event Action<AbilityDefinition, AbilityRefusal, ResourceDefinition> OnAbilityRefused;
    public event Action<string, float> OnAbilityCooldownChanged;
    public event Action<string> OnAbilityCastStart;
    public event Action<string> OnAbilityCastComplete;
    public event Action<MoveEvent, int> OnMoveEvent;
    // The move in flight told (its Tell event, or its start when its clip has none). Feel shows the cue here.
    public event Action<AbilityDefinition> OnTell;
    // A hit from this entity's ability connected (melee or projectile). SlotTransformationSystem
    // rolls the ability's procs off it.
    public event Action<AbilityDefinition, ControllerBrain> OnHitLanded;
    private void SetFact(int key, bool value) => blackboard?.SetBool(key, value);

    private void UpdateExecutingFact()
        => SetFact(BlackboardKey.IsExecutingAbility,
            currentAbility != null || currentlyCastingAbility != null || isAnimationLocked);

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        stateMachine = brain.GetModule<StateMachineModule>();
        if (stateMachine == null)
            Debug.LogError("[AbilitySystem] StateMachineModule not found");

        runtimeAbilityManager = brain.GetModule<RuntimeAbilityManager>();

        animationProvider = brain.Animation;
        movementSystem = brain.Movement;
        resources = brain.GetProvider<IResourceProvider>();
        healthProvider = brain.GetProvider<IHealthProvider>();
        damageSystem = brain.GetModule<DamageSystem>();
        if (damageSystem != null)
            damageSystem.OnDeath += HandleDeath;
        vfxSystem = brain.GetModule<VFXSystem>();
        guard = brain.GetModule<GuardModule>();

        modelModule = brain.GetModule<ModelModule>();
        if (modelModule != null)
            modelModule.OnModelChanged += HandleModelChanged;

        SetupAnimationEventForwarder();
        brain.OnLoaded += HandleLoaded;
        BuildAbilityLookup();

        if (resources == null)
            Debug.LogWarning("[AbilitySystem] No IResourceProvider found");
        if (animationProvider == null)
            Debug.LogError("[AbilitySystem] AnimationSystem not assigned");
        if (movementSystem == null)
            Debug.LogError("[AbilitySystem] MovementSystem not assigned");
    }

    public void LateInitialize()
    {
        // Here, not in Initialize: the strike reads equipment and stance, which may initialise after this.
        strikes = new StrikeHandler(brain, this, weaponSlotId);

        // Here, not in Initialize: ControllerBrain initializes this module before BlackboardSystem,
        // which only creates its Blackboard in its own Initialize. Read earlier, this was always
        // null — every forbidden fact passed and IsBlocking / IsInvincible were never written.
        blackboard = brain?.GetModule<BlackboardSystem>()?.Blackboard;
        if (blackboard == null)
        {
            Debug.LogError("[AbilitySystem] BlackboardSystem not found");
            return;
        }

        blackboard.OnBoolChanged += HandleFactChanged;
    }

    // Hard CC interrupts (Combat_Framework §5, F10): the move ends the moment CannotAct rises, not
    // when the committed swing finishes. CannotCast breaks only a spell still casting. No armoured
    // moves exist yet — when they do, they are the exception here.
    private void HandleFactChanged(int key, bool value)
    {
        if (!value) return;

        bool hardControl = key == BlackboardKey.CannotAct;
        bool silencedMidCast = key == BlackboardKey.CannotCast && IsCastingSpell();

        if (!hardControl && !silencedMidCast) return;

        CancelCurrentAbility();
        RestActionsLayer();
    }

    // Death ends whatever was in flight, the same way hard control does. CanUseAbility refuses after.
    private void HandleDeath()
    {
        CancelCurrentAbility();
        RestActionsLayer();
    }

    // currentlyCastingAbility holds the id, not the definition.
    private bool IsCastingSpell()
    {
        if (currentlyCastingAbility == null) return false;

        AbilityDefinition casting = GetAbility(currentlyCastingAbility);
        return casting != null && casting.abilityCategory == AbilityCategory.Spell;
    }

    // Gameplay cancelled, so the pose must not play on: send the action layer home. The synced
    // Actions Upper layer follows it. v1 controllers have no Actions layer and are skipped.
    private void RestActionsLayer()
    {
        AnimationSystem anim = brain != null ? brain.Animation : null;
        if (anim == null) return;

        int layer = anim.GetLayerIndex(AnimationLayerNames.Actions);
        if (layer < 0) return;

        anim.CrossFade("Rest", 0.1f, layer);
    }

    public void UpdateModule()
    {
        TickHitStop();
        if (!isEnabled) return;

        TickHitConfirm();

        UpdateCooldownTimers();
        UpdateCasting();
        TickMoveClock();
        TickTimedInvuln();
    }

    // Until the entity clock lands (CrabSystem_Standard §9).
    private static float Delta => Time.deltaTime;

    // Animation events drive every move (decided 5 Oct): strikes, phases and the end come from the
    // clip, so timing is tuned by editing the clip. This counter only measures — frames at 60 fps since
    // the move started — for blockstun maths, armour windows and MoveClockTrace, which compares the
    // clip's events with the move's baked frames. It stops while the animator is hit-stopped, so the
    // two always pause together.
    private void TickMoveClock()
    {
        if (currentAbility == null || IsHitStopped) return;
        moveClock += Delta * 60f;
    }

    // Hit-stop is gameplay timing (CrabSystem_Standard §9: animator speed 0, never a second clock), so
    // this is the one owner of animator.speed on the entity. Called on the attacker and the defender
    // when a hit connects (NotifyHitLanded) and by GuardModule for blocks, parries and guard breaks.
    // Max-merge: a second stop extends to whichever ends later, it never adds. Unscaled, so nothing
    // that touches time can stretch it.
    public void HitStop(int frames)
    {
        if (frames <= 0) return;

        Animator animator = brain != null ? brain.EntityAnimator : null;
        if (animator == null) return;

        if (frozenAnimator != null && frozenAnimator != animator) frozenAnimator.speed = 1f;
        frozenAnimator = animator;

        float until = Time.unscaledTime + frames / 60f;
        if (until > hitStopUntil) hitStopUntil = until;
        animator.speed = 0f;
    }

    // Not gated on isEnabled: disabling mid-freeze must still thaw the animator.
    private void TickHitStop()
    {
        if (hitStopUntil <= 0f) return;
        if (Time.unscaledTime < hitStopUntil)
        {
            TickHitShake();
            return;
        }
        EndHitStop();
    }

    // The struck fighter jitters in place for the freeze: the fighting-game read that a hit landed. Moves the
    // model under the body, never the body, and puts it back when the freeze ends.
    public void HitShake()
    {
        Animator animator = brain != null ? brain.EntityAnimator : null;
        if (hitShakeAmplitude <= 0f || animator == null || shakenModel != null) return;

        shakenModel = animator.transform;
        shakenHome = shakenModel.localPosition;
    }

    private void TickHitShake()
    {
        if (shakenModel == null) return;

        float side = Time.frameCount % 2 == 0 ? 1f : -1f;
        shakenModel.localPosition = shakenHome + Vector3.right * (hitShakeAmplitude * side);
    }

    private void EndHitShake()
    {
        if (shakenModel != null) shakenModel.localPosition = shakenHome;
        shakenModel = null;
    }

    // GI-03: a clean hit opens the string's chain once the freeze is over.
    private void TickHitConfirm()
    {
        if (!hitConfirmed || IsHitStopped) return;

        hitConfirmed = false;
        if (currentAbility != null) chainOpen = true;
    }

    // Restores to 1: nothing else writes animator.speed. When attack speed becomes a stat, this
    // becomes that stat.
    private void EndHitStop()
    {
        hitStopUntil = 0f;
        if (frozenAnimator != null) frozenAnimator.speed = 1f;
        frozenAnimator = null;
        EndHitShake();
    }

    private void HandleLoaded()
    {
        brain.OnLoaded -= HandleLoaded;

        // The model (and its forwarder, which lives next to the Animator) may have
        // been spawned during the load — always re-resolve rather than keeping a
        // forwarder found before the model existed.
        SetupAnimationEventForwarder();
    }

    /// <summary>
    /// SwapModel destroys the old model — and with it the forwarder we were
    /// subscribed to. Fires after the new Animator reference is set, so we can
    /// re-resolve immediately.
    /// </summary>
    private void HandleModelChanged(string modelId)
    {
        SetupAnimationEventForwarder();
    }

    private void SetupAnimationEventForwarder()
    {
        // Unity only delivers animation events to components on the same
        // GameObject as the Animator — resolve the forwarder from there first.
        AnimationEventForwarder found = null;

        Animator animator = brain.EntityAnimator;
        if (animator != null)
            found = animator.GetComponent<AnimationEventForwarder>();

        if (found == null)
        {
            // Fallback: hierarchy search (model may not be spawned yet;
            // HandleLoaded / HandleModelChanged will re-resolve later).
            Transform playerRoot = brain.transform.parent;
            if (playerRoot != null)
                found = playerRoot.GetComponentInChildren<AnimationEventForwarder>(true);
            else
                found = brain.GetComponentInChildren<AnimationEventForwarder>(true);

            if (found != null && animator != null && found.GetComponent<Animator>() == null)
                Debug.LogWarning($"[AbilitySystem] AnimationEventForwarder on '{found.name}' is not on the Animator's GameObject ('{animator.name}') — animation events will not reach it. Move the component next to the Animator.");
        }

        BindEventForwarder(found);
    }

    private void BindEventForwarder(AnimationEventForwarder forwarder)
    {
        if (forwarder == eventForwarder && forwarder != null)
            return; // already bound to the right one

        if (eventForwarder != null)
            eventForwarder.OnMoveEvent -= HandleMoveEvent;

        eventForwarder = forwarder;

        if (eventForwarder == null)
        {
            // No model yet (an NPC's arrives with its config) is normal: HandleModelChanged binds it.
            if (brain.EntityAnimator != null)
                Debug.LogWarning($"[AbilitySystem] No AnimationEventForwarder found on {gameObject.name}");
            return;
        }

        eventForwarder.OnMoveEvent += HandleMoveEvent;
        if (debugLogging)
            Debug.Log($"[AbilitySystem] Subscribed to AnimationEventForwarder on {eventForwarder.name}");
    }

    private void BuildAbilityLookup()
    {
        abilityStates.Clear();

        foreach (var ability in abilities)
        {
            if (ability == null)
            {
                Debug.LogWarning("[AbilitySystem] Null ability in list");
                continue;
            }

            if (string.IsNullOrEmpty(ability.abilityId))
            {
                Debug.LogWarning($"[AbilitySystem] Ability '{ability.abilityName}' has no ID");
                continue;
            }

            abilityStates[ability.abilityId] = new AbilityState(ability);
        }
    }

    private void OnDisable()
    {
        EndHitStop();
    }

    private void OnDestroy()
    {
        EndHitStop();
        if (brain != null) brain.OnLoaded -= HandleLoaded;

        if (blackboard != null)
            blackboard.OnBoolChanged -= HandleFactChanged;

        if (damageSystem != null)
            damageSystem.OnDeath -= HandleDeath;

        if (modelModule != null)
            modelModule.OnModelChanged -= HandleModelChanged;

        if (eventForwarder != null)
            eventForwarder.OnMoveEvent -= HandleMoveEvent;
    }

    public void AddAbility(AbilityDefinition ability)
    {
        if (string.IsNullOrEmpty(ability.abilityId))
        {
            Debug.LogError($"[AbilitySystem] Ability {ability.abilityName} has no ID");
            return;
        }

        if (!abilities.Contains(ability))
            abilities.Add(ability);

        abilityStates[ability.abilityId] = new AbilityState(ability);
    }

    public void RemoveAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        abilities.Remove(state.definition);
        abilityStates.Remove(abilityId);
    }

    public AbilityDefinition GetAbility(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return null;
        return state.definition;
    }

    public List<AbilityDefinition> GetAllAbilities()
    {
        return new List<AbilityDefinition>(abilities);
    }

    public void ReportRefused(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;
        if (currentAbility != null || currentlyCastingAbility != null || isAnimationLocked) return;
        if (damageSystem != null && damageSystem.IsDead) return;

        AbilityDefinition ability = state.definition;
        ResourceDefinition shortOf = ShortResource(ability);

        AbilityRefusal why = state.IsOnCooldown ? AbilityRefusal.Cooldown
            : shortOf != null ? AbilityRefusal.Resource
            : AbilityRefusal.Denied;

        OnAbilityRefused?.Invoke(ability, why, why == AbilityRefusal.Resource ? shortOf : null);
    }

    private ResourceDefinition ShortResource(AbilityDefinition ability)
    {
        if (ability.resourceCosts == null || resources == null) return null;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.cost > 0 && !resources.HasResource(cost.resource, cost.cost))
                return cost.resource;
        }
        return null;
    }

    private bool HasSufficientResources(AbilityDefinition ability) => ShortResource(ability) == null;

    private void ConsumeResourceCosts(AbilityDefinition ability)
    {
        if (ability.resourceCosts == null) return;
        if (resources == null) return;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.cost > 0)
                resources.ConsumeResource(cost.resource, cost.cost);
        }
    }

    private void DrainResourceCosts(AbilityDefinition ability, float deltaTime)
    {
        if (ability.resourceCosts == null) return;
        if (resources == null) return;

        foreach (var cost in ability.resourceCosts)
        {
            if (cost.resource != null && cost.drain > 0)
            {
                float drainAmount = cost.drain * deltaTime;
                resources.ConsumeResource(cost.resource, drainAmount);
            }
        }
    }

    public bool CanUseAbility(string abilityId)
    {
        if (currentAbility != null) return false;
        return CanStart(abilityId);
    }

    // The string's next step may start from the step's ChainOpen: the step in flight ends here instead of
    // at Unlocked. False, with the step still playing, when the next can't start.
    public bool ChainInto(string abilityId)
    {
        if (!ChainOpen || !CanStart(abilityId)) return false;

        CompleteAbility(currentAbility);
        UseAbility(abilityId);
        return currentAbility != null && currentAbility.abilityId == abilityId;
    }

    // Everything CanUseAbility asks except whether a move is already in flight.
    private bool CanStart(string abilityId)
    {
        if (!isEnabled) return false;
        if (damageSystem != null && damageSystem.IsDead) return false;
        if (currentlyCastingAbility != null) return false;
        if (isAnimationLocked) return false;

        if (!abilityStates.TryGetValue(abilityId, out var state)) return false;
        if (state.IsOnCooldown) return false;
        if (guard != null && !guard.Allows(state.definition)) return false;

        var ability = state.definition;

        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null && !instance.IsUsable())
                return false;
        }

        if (!CheckBlackboardRequirements(ability))
            return false;

        if (!HasSufficientResources(ability))
            return false;

        return true;
    }

    public void UseAbility(string abilityId)
    {
        if (!CanUseAbility(abilityId))
        {
            ReportRefused(abilityId);
            return;
        }
        if (!abilityStates.TryGetValue(abilityId, out var state))
        {
            Debug.LogError($"[AbilitySystem] Ability {abilityId} not found");
            return;
        }

        var ability = state.definition;

        if (runtimeAbilityManager != null)
        {
            var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
            if (instance != null)
                runtimeAbilityManager.OnAbilityUsed(instance.instanceId);
        }

        ConsumeResourceCosts(ability);

        if (ability.castTime > 0f)
            StartCast(ability);
        else
            ExecuteAbility(ability);

        StartCooldown(abilityId, ability.cooldown);
        OnAbilityUsed?.Invoke(abilityId);
    }

    public void CancelCurrentAbility()
    {
        if (currentlyCastingAbility != null)
            currentlyCastingAbility = null;

        SetFact(BlackboardKey.MoveRooted, false);

        if (currentAbility != null)
            CompleteAbility(currentAbility);

        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        isAnimationLocked = false;
        SetInvincible(false);
        UpdateExecutingFact();
    }

    private bool CheckBlackboardRequirements(AbilityDefinition ability)
    {
        if (blackboard == null) return true;

        var (requiredAny, requiredAll, forbidden) = ability.GetBlackboardRequirements();

        if (requiredAny.Count > 0)
        {
            bool anyMet = false;
            foreach (var fact in requiredAny)
            {
                if (blackboard.GetBool(fact.GetHashCode()))
                {
                    anyMet = true;
                    break;
                }
            }

            if (!anyMet)
                return false;
        }

        foreach (var fact in requiredAll)
        {
            if (!blackboard.GetBool(fact.GetHashCode()))
                return false;
        }

        foreach (var fact in forbidden)
        {
            if (blackboard.GetBool(fact.GetHashCode()))
                return false;
        }

        return true;
    }

    private void StartCast(AbilityDefinition ability)
    {
        SetFact(BlackboardKey.MoveRooted, !ability.castWhileMoving);
        currentlyCastingAbility = ability.abilityId;
        castStartTime = Time.time;
        OnAbilityCastStart?.Invoke(ability.abilityId);
    }

    private void UpdateCasting()
    {
        if (currentlyCastingAbility == null) return;

        if (!abilityStates.TryGetValue(currentlyCastingAbility, out var state))
        {
            currentlyCastingAbility = null;
            return;
        }

        var ability = state.definition;

        if (Time.time - castStartTime >= ability.castTime)
        {
            ExecuteAbility(ability);
            currentlyCastingAbility = null;
            OnAbilityCastComplete?.Invoke(ability.abilityId);
        }
    }

    private void ExecuteAbility(AbilityDefinition ability)
    {
        // A guard is a hold, not a move: GuardModule keeps it up while the block key is down. It has
        // no clip to finish, never becomes currentAbility, and so can't time out.
        if (ability.abilityType == AbilityType.Defensive)
        {
            RaiseGuard(ability);
            return;
        }

        if (guard != null) guard.MoveStarted(ability);

        currentAbility = ability;
        moveClock = 0f;
        movePhase = MovePhase.Startup;
        nextStrike = 0;
        chainOpen = false;
        told = false;
        UpdateExecutingFact();

        // Layers need nothing: the Actions layers show while their state plays (rest rule), and
        // ActionsLayerDriver picks arms-only or full body from speed. Rooting is the fact.
        SetFact(BlackboardKey.MoveRooted, !ability.castWhileMoving);
        SetFact(BlackboardKey.RootMotionDriven, ability.useRootMotion);
        blackboard?.SetFloat(BlackboardKey.RootMotionScale, ability.rootMotionScale);
        EnterUpperBodyState(StartStateFor(ability));

        StartAnimation(ability);

        if (ability.castEffectPrefab != null)
            vfxSystem?.SpawnEffect(ability.castEffectPrefab, VFXAnchor.CastOrigin);

        // A clip with no Tell tells as the move starts (its baked tell frame is 0).
        if (ability.frames.tell <= 0) Tell();

        if (ability.effectCue <= 0)
        {
            ExecuteAbilityEffects(ability);

            if (!ability.waitForAnimUnlock)
                CompleteAbility(ability);
        }

        if (ability.maxDuration > 0f)
        {
            if (safetyTimeoutCoroutine != null)
                StopCoroutine(safetyTimeoutCoroutine);

            safetyTimeoutCoroutine = StartCoroutine(SafetyTimeoutCoroutine(ability));
        }
    }

    private void RaiseGuard(AbilityDefinition ability)
    {
        if (guard == null)
        {
            Debug.LogError($"[AbilitySystem] {brain.EntityName} used '{ability.abilityName}' but has no GuardModule", this);
            return;
        }

        guard.Raise(ability);
    }

    private IEnumerator SafetyTimeoutCoroutine(AbilityDefinition ability)
    {
        yield return new WaitForSeconds(ability.maxDuration);

        if (currentAbility == ability)
        {
            // A parry may end on its maxDuration: the deflect pose only plays from a raised guard, so its Unlocked can miss.
            if (!ability.IsParry)
                Debug.LogWarning($"[AbilitySystem] {ability.abilityName} timed out - its clip never raised Unlocked!");
            CompleteAbility(ability);
        }

        safetyTimeoutCoroutine = null;
    }

    private void ExecuteAbilityEffects(AbilityDefinition ability)
    {
        if (ability.invulnSeconds > 0f) StartTimedInvuln(ability.invulnSeconds);

        bool isMovementAbility = ability.movementEffects != null && ability.movementEffects.Count > 0;
        if (isMovementAbility && movementSystem != null)
            ability.ExecuteMovement(movementSystem);

        if (ability.targetType == AbilityTargetType.Self)
        {
            if (brain == null)
            {
                Debug.LogError("[AbilitySystem] Cannot execute self-targeted ability - no ControllerBrain!");
                return;
            }

            var effectManager = brain.GetModule<EffectManagerModule>();
            ability.ExecuteOnSelf(brain, effectManager, resources);
        }
    }

    private void CompleteAbility(AbilityDefinition ability)
    {
        if (stateMachine != null)
            stateMachine.TryTransitionUpperBody(UpperBodyState.Idle);

        SetFact(BlackboardKey.MoveRooted, false);
        SetFact(BlackboardKey.RootMotionDriven, false);
        if (isInvincible && invulnUntil <= 0f) SetInvincible(false);

        if (currentAbility == ability)
        {
            currentAbility = null;
            isAnimationLocked = false;
            chainOpen = false;
            hitConfirmed = false;
        }

        UpdateExecutingFact();

        if (safetyTimeoutCoroutine != null)
        {
            StopCoroutine(safetyTimeoutCoroutine);
            safetyTimeoutCoroutine = null;
        }
    }

    // The clip's six events (Combat_Framework §2.1) are the move's timing. An event arriving with no move in
    // flight is the tail of a clip still fading out, and is ignored.
    private void HandleMoveEvent(MoveEvent evt, int value)
    {
        if (debugLogging)
            Debug.Log($"[AbilitySystem] {evt}({value}) (currentAbility: {currentAbility?.abilityId ?? "null"})");

        OnMoveEvent?.Invoke(evt, value);
        if (currentAbility == null) return;

        switch (evt)
        {
            case MoveEvent.Tell: Tell(); return;
            case MoveEvent.Strike: Strike(value); return;
            case MoveEvent.Cue: Cue(value); return;
            case MoveEvent.ChainOpen: chainOpen = true; return;
            case MoveEvent.Unlocked: Unlock(); return;
            case MoveEvent.Invuln: SetInvincible(value != 0); return;
        }
    }

    private void Tell()
    {
        told = true;
        toldAt = Time.time;
        OnTell?.Invoke(currentAbility);
    }

    // The last of the move's strikes ends its active phase; a multi-hit move stays Active between strikes.
    private void Strike(int index)
    {
        AbilityDefinition move = currentAbility;
        nextStrike = index + 1;
        strikes?.Strike(move, index);
        if (currentAbility != move) return;

        bool last = nextStrike >= move.StrikeCount;
        movePhase = last ? MovePhase.Recovery : MovePhase.Active;
        EnterMeleePhase(last ? UpperBodyState.MeleeRecovery : UpperBodyState.MeleeSwing);
    }

    // A move that doesn't wait for Unlocked ends as its effects fire, here as on a cue-0 start (a dash: the
    // impulse is the move). Without this it held currentAbility until the safety timeout, refusing everything.
    private void Cue(int cue)
    {
        if (cue != currentAbility.effectCue) return;

        AbilityDefinition move = currentAbility;
        ExecuteAbilityEffects(move);
        if (!move.waitForAnimUnlock && currentAbility == move) CompleteAbility(move);
    }

    private void Unlock()
    {
        if (!currentAbility.waitForAnimUnlock) return;
        CompleteAbility(currentAbility);
    }

    private void StartTimedInvuln(float seconds)
    {
        invulnUntil = Time.time + seconds;
        SetInvincible(true);
    }

    private void TickTimedInvuln()
    {
        if (invulnUntil <= 0f || Time.time < invulnUntil) return;
        SetInvincible(false);
    }

    // AbilitySystem is the one writer of IsInvincible; DamageSystem refuses damage while it is up.
    private void SetInvincible(bool on)
    {
        if (!on) invulnUntil = 0f;
        isInvincible = on;
        SetFact(BlackboardKey.IsInvincible, on);
    }

    // Would the move in flight's next strike land on target from where both stand now? No damage.
    public bool StrikeWouldReach(ControllerBrain target)
    {
        if (currentAbility == null || strikes == null) return false;
        return strikes.Reaches(currentAbility, nextStrike, target);
    }

    // ── Upper-body state ──────────────────────────────────────────────────
    // The state machine is the one answer to "what are the arms doing". AI, camera and the movement
    // permission matrix read it. Wind-up at the start, swing between strikes, recovery after the last.

    private void EnterUpperBodyState(UpperBodyState state)
    {
        if (stateMachine == null) return;
        stateMachine.TryTransitionUpperBody(state);
    }

    private void EnterMeleePhase(UpperBodyState phase)
    {
        if (currentAbility == null || !IsMelee(currentAbility)) return;
        EnterUpperBodyState(phase);
    }

    private static bool IsMelee(AbilityDefinition ability)
        => ability.abilityCategory == AbilityCategory.Physical || ability.abilityCategory == AbilityCategory.Natural;

    private static UpperBodyState StartStateFor(AbilityDefinition ability)
    {
        if (ability.abilityCategory == AbilityCategory.Spell) return UpperBodyState.CastingWindUp;
        if (IsMelee(ability)) return UpperBodyState.MeleeWindUp;
        return UpperBodyState.Idle;
    }


    private void UpdateCooldownTimers()
    {
        foreach (var kvp in abilityStates)
        {
            var state = kvp.Value;
            if (state.cooldown == null) continue;

            state.cooldown.Tick(Time.deltaTime);
            OnAbilityCooldownChanged?.Invoke(kvp.Key, state.cooldown.CurrentTime);

            if (state.cooldown.IsFinished)
                state.cooldown = null;
        }
    }

    private void StartCooldown(string abilityId, float duration)
    {
        if (duration <= 0f) return;
        if (!abilityStates.TryGetValue(abilityId, out var state)) return;

        state.cooldown = new CountdownTimer(duration);
    }

    public bool IsAbilityOnCooldown(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return false;
        return state.IsOnCooldown;
    }

    public float GetAbilityCooldownRemaining(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return 0f;
        return state.cooldown?.CurrentTime ?? 0f;
    }

    public float GetAbilityCooldown(string abilityId)
    {
        return GetAbilityCooldownRemaining(abilityId);
    }

    public float GetAbilityMaxCooldown(string abilityId)
    {
        if (!abilityStates.TryGetValue(abilityId, out var state)) return 0f;
        return state.definition.cooldown;
    }


    public int GetAbilityRemainingUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.remainingUses;
    }

    public int GetAbilityMaxUses(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1;

        return instance.maxUses;
    }

    public float GetAbilityRemainingDuration(string abilityId)
    {
        if (runtimeAbilityManager == null) return -1f;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return -1f;

        return instance.GetRemainingDuration();
    }

    public bool IsAbilityConsumable(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsConsumable();
    }

    public bool IsAbilityTemporary(string abilityId)
    {
        if (runtimeAbilityManager == null) return false;

        var instance = runtimeAbilityManager.GetInstanceByDefinition(abilityId);
        if (instance == null) return false;

        return instance.IsTemporary();
    }

    // Called by StrikeHandler (after the hit has resolved) and ProjectilePayload when a hit from this
    // entity connects. A clean hit freezes both fighters for the move's hit-stop; several targets in
    // one swing max-merge, they don't add. A guarded hit is GuardModule's to freeze (block / parry).
    public void NotifyHitLanded(AbilityDefinition ability, ControllerBrain target)
    {
        if (ability == null) return;

        if (ability == currentAbility && ability.HasMoveData && !Guarded(target))
        {
            HitStop(ability.hit.hitStop);
            hitConfirmed = hitConfirmCancel;
            AbilitySystem defender = target != null ? target.Abilities : null;
            if (defender != null) defender.HitStop(ability.hit.hitStop);
            if (defender != null && ability.hit.hitStop > 0) defender.HitShake();
        }

        OnHitLanded?.Invoke(ability, target);
    }

    private static bool Guarded(ControllerBrain target)
        => target != null && target.Damage != null && target.Damage.LastHitGuarded;

    // A baked move starts its own state, so the clip begins on the frame the move does — not after
    // a transition, or after the previous swing's tail when the same move is pressed again (the
    // trigger had to wait for the state to exit first). The explicit start time restarts a state
    // that is already playing. Unbaked abilities still set their trigger.
    private void StartAnimation(AbilityDefinition ability)
    {
        if (string.IsNullOrEmpty(ability.animationTrigger)) return;

        Animator animator = brain.EntityAnimator;
        if (ability.bakedState != 0 && animator != null && ability.bakedLayer < animator.layerCount)
        {
            animator.CrossFadeInFixedTime(ability.bakedState, ability.bakedFade, ability.bakedLayer, ability.bakedStartTime);
            return;
        }

        if (animationProvider != null) animationProvider.TriggerCombatAnimation(ability.animationTrigger);
    }
}
