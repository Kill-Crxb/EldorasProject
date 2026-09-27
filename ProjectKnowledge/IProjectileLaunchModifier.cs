using UnityEngine;

/// <summary>
/// Everything a launch is, gathered in one place so it can be adjusted before anything is
/// pulled from the pool.
///
/// Seeded by ProjectileLauncher from the firing ability, then handed to every
/// IProjectileLaunchModifier on the brain in turn. A modifier that changes `data` owns
/// re-seeding `runtime` from it — that is not a trap, it is the honest split: whoever decided
/// to fire a different projectile is the only thing that knows which of the old numbers were
/// still wanted.
/// </summary>
public struct ProjectileLaunchPlan
{
    /// <summary>Which projectile. Starts as the ability's, and a modifier may swap it.</summary>
    public ProjectileData data;

    /// <summary>This shot's numbers. Seeded by ProjectileRuntime.FromData(data).</summary>
    public ProjectileRuntime runtime;

    /// <summary>
    /// The dice this shot rolls, passed to DamageEffect as weaponOverride. Starts as
    /// data.weaponData — a thrown weapon IS the weapon — and a spell replaces it with its
    /// tier's dice.
    /// </summary>
    public WeaponData dice;

    /// <summary>How many projectiles this one launch produces. 1 unless something says otherwise.</summary>
    public int count;

    /// <summary>Total fan angle across every instance, in degrees. Inert at count 1.</summary>
    public float spreadDegrees;
}

/// <summary>
/// The seam for anything that wants to change a projectile before it is fired.
///
/// ProjectileLauncher.Fire has always resolved an unmodified copy of the asset's numbers, with
/// a comment marking the spot: "When modifiers arrive they fill this struct here." This is
/// that door, and it is deliberately generic — talents, weapon upgrades, buffs and the spell
/// system all walk through it. THE PROJECTILE SYSTEM LEARNS NOTHING ABOUT MAGIC.
///
/// Implement on any component under the ControllerBrain. ProjectileLauncher collects them in
/// LateInitialize via GetComponentsInChildren, so no registration and no serialized field.
///
/// Modifiers run in discovery order and are cumulative. A modifier that has nothing to say
/// about this particular ability must return without touching the plan — that is the common
/// case, and it must be cheap.
/// </summary>
public interface IProjectileLaunchModifier
{
    /// <param name="ability">The ability firing. Check this first and bail if it is not yours.</param>
    /// <param name="plan">The launch so far. Adjust in place.</param>
    void ModifyLaunch(AbilityDefinition ability, ref ProjectileLaunchPlan plan);
}
