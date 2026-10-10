using System.Collections.Generic;
using UnityEngine;

// What a goal wants an ability for. Goals ask for a role, not a named ability, so one goal asset
// serves every creature that has a move in that role.
public enum AIRole { Melee, Lunge, Ranged, Escape, Guard, Call, Charge }

[System.Serializable]
public struct AIRoleAbility
{
    public AIRole role;
    public AbilityDefinition ability;
}

// The AI's hands on the controls. Goals write intent here (steer, face, stop, hold guard);
// MovementSystem and GuardModule read it exactly as they read the player's InputSystem.
//
// Also holds the few pieces of per-entity AI memory a goal needs between frames. Goals are
// ScriptableObjects shared by every NPC that uses them, so nothing per-entity can live on a goal.
//
// Setup: put it on a child of the brain. At LateInitialize it makes itself the active movement
// control source, replacing InputSystem's AI mode (and its StubAIControlSource). As the ability
// control source it answers brain.GetProvider<IAbilityControlSource>() because InputSystem is
// player-only and switched off on an NPC.
public class AIControlSource : MonoBehaviour, IBrainModule, IMovementControlSource, IAbilityControlSource
{
    [SerializeField] private bool isEnabled = true;

    [Tooltip("The abilities goals use, by role. Each must also be in the entity's AbilitySystem list.")]
    [SerializeField] private List<AIRoleAbility> roleAbilities = new List<AIRoleAbility>();

    [Tooltip("Follow Leader: others of the pack gather on the nearest leader.")]
    [SerializeField] private bool isPackLeader;

    [Tooltip("Patrol: points walked in order and looped, in metres from where the creature started. " +
             "Offsets rather than scene objects, so a prefab can carry its route.")]
    [SerializeField] private List<Vector3> patrolOffsets = new List<Vector3>();

    // Every live AI, so a goal can see the others hunting the same target (Surround).
    private static readonly List<AIControlSource> active = new List<AIControlSource>();
    public static IReadOnlyList<AIControlSource> Active => active;

    private ControllerBrain brain;
    private PerceptionModule perception;
    private Vector2 moveDirection;
    private Vector2 lookDirection;

    // Per-entity memory for goals
    public float LastAttackTime { get; set; } = -999f;
    public float GuardUntil { get; set; } = -999f;
    public bool ReadThisSwing { get; set; }
    public bool DeflectReadThisSwing { get; set; }
    public int StringPressesLeft { get; set; }
    // Fight And Guard: out of stamina and keeping away; pressing hard until EngageUntil; a Perilous move read.
    public bool Winded { get; set; }
    public float EngageUntil { get; set; } = -999f;
    public bool PerilousRead { get; set; }

    // Scratch memory for whichever goal is running. Each goal sets what it uses in OnStart.
    public Vector3 Home { get; private set; }
    public Vector3 GoalPoint { get; set; }
    public float GoalUntil { get; set; }
    public int GoalPhase { get; set; }
    public float GoalSign { get; set; } = 1f;
    public float FleeReadyAt { get; set; }
    public int PatrolIndex { get; set; }

    // Set when an ambusher springs; cleared when it loses its target, so it can lie in wait again.
    public bool Engaged { get; set; }

    // Set while the Leash goal walks the creature home; it ignores its target until it gets there.
    public bool Returning { get; set; }

    // When the next optional decision (milling about) may be rolled.
    public float NextDecisionAt { get; set; }

    // What just happened, for reactions (DS1-style interrupts). Times, -999 when never.
    public float DamagedAt { get; private set; } = -999f;
    public float GuardedAt { get; private set; } = -999f;
    public float TargetWhiffedAt { get; private set; } = -999f;
    public float ReactedTo { get; set; } = -999f;
    // Which reaction the React goal rolled: 0 step away, 1 punish.
    public int Reaction { get; set; }

    private Transform trackedTarget;
    private ICombatantState trackedState;
    private AbilityDefinition trackedMove;
    private float trackedMoveStart;

    // The AI's block key. GuardModule keeps a guard up only while this is held.
    public bool GuardHeld { get; set; }

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool IsActive => isEnabled && isActiveAndEnabled;
    public string SourceName => "AIControlSource";

    public ControllerBrain Brain => brain;
    public Transform Target => perception != null ? perception.CurrentTarget : null;
    public bool IsAlive => brain != null && (brain.Health == null || brain.Health.IsAlive());
    public bool IsPackLeader => isPackLeader;
    public IReadOnlyList<Vector3> PatrolOffsets => patrolOffsets;

    // A call for help: hand this AI a target it hasn't seen. Its own eyes take over once it gets close.
    public void Alert(Transform target)
    {
        if (perception != null && Target == null) perception.SetTarget(target);
    }

    public AbilityDefinition AbilityFor(AIRole role)
    {
        foreach (AIRoleAbility entry in roleAbilities)
            if (entry.role == role && entry.ability != null) return entry.ability;
        return null;
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        perception = brain.GetModule<PerceptionModule>();
        Home = brain.transform.position;

        DamageSystem damage = brain.GetModule<DamageSystem>();
        if (damage != null) damage.OnDamageApplied += (packet, applied) => DamagedAt = Time.time;

        AbilitySystem abilities = brain.GetModule<AbilitySystem>();
        if (abilities != null) abilities.OnHitLanded += HandleHitLanded;

        if (brain.Movement == null)
        {
            Debug.LogWarning($"[AIControlSource] {brain.EntityName} has no MovementSystem");
            return;
        }
        brain.Movement.SetControlSource(this);
    }

    public void UpdateModule()
    {
        if (Engaged && Target == null) Engaged = false;
        WatchTargetSwing();
    }

    // Our hit landed on a guard.
    private void HandleHitLanded(AbilityDefinition ability, ControllerBrain target)
    {
        if (target != null && target.Damage != null && target.Damage.LastHitGuarded) GuardedAt = Time.time;
    }

    // The target's attack ended without hitting us while we were close: a whiff we can punish.
    private void WatchTargetSwing()
    {
        Transform target = Target;
        if (target != trackedTarget)
        {
            trackedTarget = target;
            ControllerBrain targetBrain = target != null ? target.GetComponent<ControllerBrain>() : null;
            trackedState = targetBrain != null ? targetBrain.GetProvider<ICombatantState>() : null;
            trackedMove = null;
        }
        if (trackedState == null) return;

        AbilityDefinition move = trackedState.CurrentAbility;
        if (move != null && trackedMove == null && IsAttack(move))
        {
            trackedMove = move;
            trackedMoveStart = Time.time;
        }
        if (move != null || trackedMove == null) return;

        if (DamagedAt < trackedMoveStart) TargetWhiffedAt = Time.time;
        trackedMove = null;
    }

    private static bool IsAttack(AbilityDefinition move) => move.abilityType != AbilityType.Defensive && !move.IsParry;

    // Intent — called by goals

    /// <summary>
    /// Move this way. Strength 0–1 is the analog stick: the locomotion handler scales its speed
    /// request by it, the same as a half-pushed gamepad stick.
    /// </summary>
    public void Steer(Vector3 worldDirection, float strength = 1f)
    {
        moveDirection = Flatten(worldDirection) * Mathf.Clamp01(strength);
    }

    public void Face(Vector3 worldDirection)
    {
        lookDirection = Flatten(worldDirection);
    }

    public void Stop()
    {
        moveDirection = Vector2.zero;
    }

    public void Release()
    {
        moveDirection = Vector2.zero;
        lookDirection = Vector2.zero;
        GuardHeld = false;
    }

    // IMovementControlSource

    public MovementInput GetMovementInput()
    {
        MovementInput input = MovementInput.Zero;
        input.MoveDirection = moveDirection;
        input.LookDirection = lookDirection;
        return input;
    }

    // IAbilityControlSource. Goals fire the moveset directly for now (roadmap A5, ability half).

    public string GetAbilitySlotToTrigger() => null;

    // Shared by both control-source interfaces.

    public void OnActivated() { }
    public void OnDeactivated() => Release();
    public void UpdateSource() { }

    private static Vector2 Flatten(Vector3 v)
    {
        Vector2 flat = new Vector2(v.x, v.z);
        return flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector2.zero;
    }
}
