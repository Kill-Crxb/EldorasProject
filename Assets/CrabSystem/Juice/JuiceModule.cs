using MoreMountains.Feedbacks;
using UnityEngine;

// Presentation for this entity's own combat and movement events. Juice.md is the design.
//
// Two kinds of player:
//   World — sparks, sound. Everyone sees them, on every combatant.
//   View  — shake, screen effects. Only when this entity is the local player, because in PvP
//           every other fighter is a Base_PC too, and their hits must not shake your camera.
//
// Every entity juices itself. The one reach across: the target resolves what kind of hit landed
// and tells the attacker (Connect, Parried, Killed), so the attacker plays its own players.
//
// No player may change Time.timeScale. It stops the whole client and will fight the network
// tick. Hit-stop is gameplay timing, not juice: AbilitySystem.HitStop owns animator.speed, called
// by the hit itself and by GuardModule. Nothing here freezes anything.
public class JuiceModule : MonoBehaviour, IBrainModule
{
    [Header("System State")]
    [SerializeField] private bool isEnabled = true;

    [Header("World Players — target side")]
    [SerializeField] private MMF_Player hitLight;
    [SerializeField] private MMF_Player hitHeavy;
    [SerializeField] private MMF_Player blocked;
    [SerializeField] private MMF_Player hitProjectile;
    [SerializeField] private MMF_Player parry;
    [SerializeField] private MMF_Player guardBreak;
    [SerializeField] private MMF_Player death;

    [Header("World Players — attacker side")]
    [SerializeField] private MMF_Player connect;

    [Header("World Players — movement")]
    [SerializeField] private MMF_Player land;
    [SerializeField] private MMF_Player landHard;
    [SerializeField] private MMF_Player jump;
    [SerializeField] private MMF_Player airJump;
    [SerializeField] private MMF_Player wallJump;
    [SerializeField] private MMF_Player mantle;
    [SerializeField] private MMF_Player dash;
    [SerializeField] private MMF_Player sprintStart;

    [Header("View Players — local player only")]
    [SerializeField] private MMF_Player viewHit;
    [SerializeField] private MMF_Player viewConnect;
    [SerializeField] private MMF_Player viewParry;
    [SerializeField] private MMF_Player viewBlocked;
    [Tooltip("The attacker's side of a blocked hit: the blade rebounds off a guard.")]
    [SerializeField] private MMF_Player viewBlockedRebound;
    [SerializeField] private MMF_Player viewGuardBreak;
    [SerializeField] private MMF_Player viewKill;
    [SerializeField] private MMF_Player viewLandHard;
    [SerializeField] private MMF_Player viewLand;
    [SerializeField] private MMF_Player viewJump;
    [SerializeField] private MMF_Player viewWallJump;
    [SerializeField] private MMF_Player viewDash;

    [Header("Guard Break")]
    [Tooltip("Height above the root where the guard-break burst plays.")]
    [SerializeField] private float guardBreakHeight = 1.2f;
    [SerializeField] private float guardBreakFlashTime = 0.15f;

    [Header("Flash (seconds)")]
    [SerializeField] private HitFlash flash;
    [SerializeField] private float lightFlashTime = 0.08f;
    [SerializeField] private float heavyFlashTime = 0.12f;
    [SerializeField] private float projectileFlashTime = 0.08f;

    [Header("Landing")]
    [Tooltip("LandingImpactSystem strength (0-1) at or above which landHard and viewLandHard play as well.")]
    [SerializeField] private float hardLandingStrength = 0.5f;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    private ControllerBrain brain;
    private DamageSystem damage;
    private AbilitySystem abilities;
    private GuardModule guard;
    private LandingImpactSystem landing;
    private MovementSystem movement;
    private ParkourLocomotionHandler locomotion;
    private bool wasSprinting;

    private bool parryPending;
    private bool deathPending;

    // The one place "is this my character" is decided. Single local player until netcode; with
    // FishNet this becomes the ownership check.
    private bool IsLocal => brain != null && brain.IsPlayer;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        damage = brain.Damage;
        abilities = brain.Abilities;
        guard = brain.GetModule<GuardModule>();
        landing = brain.GetModule<LandingImpactSystem>();

        if (damage == null)
        {
            isEnabled = false;
            Debug.LogError($"[JuiceModule] No DamageSystem on {brain.EntityName} — nothing to juice.", this);
            return;
        }

        damage.OnDamageApplied += HandleDamageApplied;
        damage.OnDeath += HandleDeath;
        if (guard != null) guard.OnPerfectBlock += HandleParry;
        if (guard != null) guard.OnGuardBreak += HandleGuardBreak;
        if (abilities != null) abilities.OnAbilityUsed += HandleAbilityUsed;
        if (landing != null) landing.OnLanded += HandleLanded;

        movement = brain.Movement;
        BindLocomotion();
    }

    private void OnDestroy()
    {
        if (damage != null)
        {
            damage.OnDamageApplied -= HandleDamageApplied;
            damage.OnDeath -= HandleDeath;
        }
        if (guard != null) guard.OnPerfectBlock -= HandleParry;
        if (guard != null) guard.OnGuardBreak -= HandleGuardBreak;
        if (abilities != null) abilities.OnAbilityUsed -= HandleAbilityUsed;
        if (landing != null) landing.OnLanded -= HandleLanded;
        if (locomotion != null) locomotion.OnMoveAction -= HandleMoveAction;
    }

    public void UpdateModule()
    {
        if (!isEnabled || movement == null) return;

        if (movement.Locomotion != locomotion) BindLocomotion();

        bool sprinting = movement.IsSprinting;
        if (sprinting && !wasSprinting) Play(sprintStart, RootPosition(), 1f);
        wasSprinting = sprinting;
    }

    // The locomotion handler can be swapped by a control source after LateInitialize, so this is
    // re-checked every frame rather than bound once.
    private void BindLocomotion()
    {
        if (locomotion != null) locomotion.OnMoveAction -= HandleMoveAction;
        locomotion = movement != null ? movement.Locomotion as ParkourLocomotionHandler : null;
        if (locomotion != null) locomotion.OnMoveAction += HandleMoveAction;
    }

    private void HandleMoveAction(MoveAction action, Vector3 point, Vector3 normal)
    {
        if (!isEnabled) return;

        switch (action)
        {
            case MoveAction.GroundJump:
                Play(jump, point, 1f);
                PlayView(viewJump, point, 1f);
                break;
            case MoveAction.AirJump:
                Play(airJump, point, 1f);
                PlayView(viewJump, point, 1f);
                break;
            case MoveAction.WallJump:
                Play(wallJump, point, 1f);
                PlayView(viewWallJump, point, 1f);
                break;
            case MoveAction.Mantle:
                Play(mantle, point, 1f);
                break;
        }
    }

    // A dash is any ability whose movement effects push the caster — Dash or Impulse. The ability
    // has just executed when OnAbilityUsed fires, so it is CurrentAbility.
    private void HandleAbilityUsed(string abilityId)
    {
        if (!isEnabled || abilities == null) return;

        AbilityDefinition ability = abilities.CurrentAbility;
        if (ability == null || ability.abilityId != abilityId || !IsDash(ability)) return;

        Vector3 position = RootPosition();
        Play(dash, position, 1f);
        PlayView(viewDash, position, 1f);
    }

    private static bool IsDash(AbilityDefinition ability)
    {
        if (ability.movementEffects == null) return false;

        foreach (MovementEffect effect in ability.movementEffects)
        {
            if (effect == null) continue;
            if (effect.movementType == MovementEffect.MovementType.Dash) return true;
            if (effect.movementType == MovementEffect.MovementType.Impulse) return true;
        }
        return false;
    }

    // OnPerfectBlock and OnDeath both fire from inside TakeDamage, before OnDamageApplied has
    // named the attacker. They only mark themselves; the damage handler that follows resolves
    // them with the attacker known.
    private void HandleParry()
    {
        parryPending = true;
    }

    // Fires for a guard broken by a hit (inside TakeDamage) and for posture broken by being parried
    // (on the attacker, outside any hit), so it plays on its own rather than waiting for a damage event.
    private void HandleGuardBreak()
    {
        if (!isEnabled) return;

        Vector3 position = RootPosition() + Vector3.up * guardBreakHeight;
        if (flash != null) flash.Flash(guardBreakFlashTime);
        Play(guardBreak, position, 1f);
        PlayView(viewGuardBreak, position);
    }

    private void HandleDeath()
    {
        deathPending = true;
    }

    private void HandleDamageApplied(CombatDamagePacket packet, float applied)
    {
        bool parried = parryPending;
        bool died = deathPending;
        parryPending = false;
        deathPending = false;

        if (!isEnabled) return;

        JuiceModule attacker = FindJuice(packet.attacker);

        if (died) ResolveDeath(attacker);

        if (packet.source == DamageSource.Projectile)
        {
            Hit(hitProjectile, projectileFlashTime, packet.hitPoint);
            PlayView(viewHit, packet.hitPoint);
            return;
        }

        if (packet.source != DamageSource.Melee) return;

        if (parried)
        {
            Hit(parry, 0f, packet.hitPoint);
            PlayView(viewParry, packet.hitPoint);
            if (attacker != null) attacker.Parried(packet.hitPoint);
            return;
        }

        if (IsBlocking())
        {
            Hit(blocked, 0f, packet.hitPoint);
            PlayView(viewBlocked, packet.hitPoint);
            if (attacker != null) attacker.Blocked(packet.hitPoint);
            return;
        }

        if (packet.isHeavyAttack) Hit(hitHeavy, heavyFlashTime, packet.hitPoint);
        else Hit(hitLight, lightFlashTime, packet.hitPoint);
        PlayView(viewHit, packet.hitPoint);

        if (attacker != null) attacker.Connect(packet.hitPoint);
    }

    private void ResolveDeath(JuiceModule killer)
    {
        Play(death, RootPosition(), 1f);
        if (killer != null && killer != this) killer.Killed(RootPosition());
    }

    // Called by the target on the attacker.
    public void Connect(Vector3 hitPoint)
    {
        if (!isEnabled) return;
        Play(connect, hitPoint, 1f);
        PlayView(viewConnect, hitPoint);
    }

    // No connect sparks: the blade met a guard, not a body.
    public void Blocked(Vector3 hitPoint)
    {
        if (!isEnabled) return;
        PlayView(viewBlockedRebound, hitPoint);
    }

    public void Parried(Vector3 hitPoint)
    {
        if (!isEnabled) return;
        PlayView(viewParry, hitPoint);
    }

    public void Killed(Vector3 victimPosition)
    {
        if (!isEnabled) return;
        PlayView(viewKill, victimPosition);
    }

    private void Hit(MMF_Player player, float flashTime, Vector3 hitPoint)
    {
        if (flash != null && flashTime > 0f) flash.Flash(flashTime);
        Play(player, hitPoint, 1f);
    }

    private void HandleLanded(float strength)
    {
        if (!isEnabled) return;

        Vector3 position = RootPosition();
        Play(land, position, strength);
        PlayView(viewLand, position, strength);
        if (strength < hardLandingStrength) return;

        Play(landHard, position, strength);
        PlayView(viewLandHard, position, strength);
    }

    private bool IsBlocking()
    {
        Blackboard blackboard = brain.Blackboard;
        return blackboard != null && blackboard.GetBool(BlackboardKey.IsBlocking);
    }

    private Vector3 RootPosition()
    {
        return brain.EntityRoot != null ? brain.EntityRoot.position : transform.position;
    }

    private void PlayView(MMF_Player player, Vector3 position, float intensity = 1f)
    {
        if (!IsLocal) return;
        Play(player, position, intensity);
    }

    private static JuiceModule FindJuice(Transform attacker)
    {
        if (attacker == null) return null;
        ControllerBrain attackerBrain = attacker.GetComponentInParent<ControllerBrain>();
        return attackerBrain != null ? attackerBrain.GetModule<JuiceModule>() : null;
    }

    private static void Play(MMF_Player player, Vector3 position, float intensity)
    {
        if (player == null) return;
        player.PlayFeedbacks(position, intensity);
    }
}
