using UnityEngine;

/// <summary>
/// The seam for anything that wants to decide what a projectile DOES on arrival, rather than
/// what it is on launch.
///
/// The mirror of IProjectileLaunchModifier, and deliberately the same shape: a component on the
/// FIRING brain, resolved through the brain, generic about what it is for. The projectile system
/// gains one hook and learns nothing about magic, heals, or status effects.
///
/// WHY THIS EXISTS AT ALL:
/// ProjectilePayload applies the firing ability's damageEffects, and that is the right default —
/// an attack is authored once whether it arrives by blade or through the air. But a composed
/// spell's Effect slot is decided at CAST time, and the projectile only carries the shared cast
/// ability, whose effect lists are fixed on the asset. So a healing bolt and a fireball arrive
/// holding identical data.
///
/// The alternatives were worse. One ability per school x effect is 36 assets, which is exactly
/// the combinatorial explosion the whole grammar exists to avoid. Putting an effect enum in
/// ProjectileRuntime works, but then the projectile system has opinions about what magic is.
/// Asking the source brain keeps every spell decision on the spell side of the fence.
///
/// Handlers run in discovery order until one returns true. Returning FALSE means "not mine, or
/// nothing special" and the payload applies its normal damage — which is what makes this safe
/// to add to a brain that mostly throws shuriken.
/// </summary>
public interface IProjectileArrivalHandler
{
    /// <param name="ability">The ability that fired. Check it first and bail if it is not yours.</param>
    /// <param name="target">The brain that was hit. Never null when this is called.</param>
    /// <param name="hit">Where and how it landed.</param>
    /// <param name="dice">This shot's dice, already resolved by the launcher.</param>
    /// <param name="damageMultiplier">ProjectileRuntime.damageMultiplier for this shot.</param>
    /// <returns>
    /// True when this handler fully resolved the arrival and the payload should NOT apply its
    /// normal damage. False to fall through — including for a spell that really is just damage.
    /// </returns>
    bool HandleArrival(AbilityDefinition ability, ControllerBrain target, ProjectileHitInfo hit,
                       DiceProfile dice, float damageMultiplier);
}
