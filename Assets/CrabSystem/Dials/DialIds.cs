// Stat ids for tuning dials: stats that are added, flat, to a tuned value at the point it's used, so talents,
// gear, statuses and the burden track can move any of them through the ordinary stat contributions. Every id is
// an entry in the MovementStats or CombatDials schema (made by Tools → Crab → Dials → Set Up); a dial at 0 leaves
// the authored value alone. Flat only, never a percentage.
public static class DialIds
{
    // Movement (MovementStats) — m/s, m/s², degrees/s, counts. ParkourLocomotionHandler, MovementEffect.
    public const string WalkSpeed = "mov.walk_speed";
    public const string RunSpeed = "mov.run_speed";
    public const string SprintSpeed = "mov.sprint_speed";
    public const string CrouchSpeed = "mov.crouch_speed";
    public const string Accel = "mov.accel";
    public const string Friction = "mov.friction";
    public const string JumpSpeed = "mov.jump_speed";
    public const string AirJumpSpeed = "mov.air_jump_speed";
    public const string AirJumps = "mov.air_jumps";
    public const string AirTurn = "mov.air_turn";
    public const string AirStrafe = "mov.air_strafe";
    public const string WallJumps = "mov.wall_jumps";
    public const string Mantles = "mov.mantles";
    public const string DashSpeed = "mov.dash_speed";

    // Combat (CombatDials) — frames at 60 fps, seconds, flat points. GuardModule, PostureModule, AbilitySystem.
    public const string ParryWindow = "cmb.parry_window";
    public const string ParryStun = "cmb.parry_stun";
    public const string RiposteWindow = "cmb.riposte_window";
    public const string BlockStamina = "cmb.block_stamina";
    public const string MaxPosture = "cmb.max_posture";
    public const string PostureRecovery = "cmb.posture_recovery";
    public const string DodgeInvuln = "cmb.dodge_invuln";

    // Resources (CombatDials) — per second and seconds, named on each ResourceDefinition.
    public const string HealthRegen = "res.health_regen";
    public const string HealthRegenDelay = "res.health_regen_delay";
    public const string StaminaRegen = "res.stamina_regen";
    public const string StaminaRegenDelay = "res.stamina_regen_delay";
    public const string ManaRegen = "res.mana_regen";
    public const string ManaRegenDelay = "res.mana_regen_delay";
    public const string ChargeRegen = "res.charge_regen";

    public static float Read(IStatProvider stats, string statId) => stats != null ? stats.GetValue(statId) : 0f;
}
