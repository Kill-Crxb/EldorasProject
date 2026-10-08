using UnityEngine;

/// <summary>
/// What happens to whatever the projectile hits. Reactive — driven by
/// ProjectileBrain.ReportHit, not per frame.
///
/// Two stages: decide whether the contact counts, then apply the firing ability's damage.
///
/// The return value matters. False means the contact did not count, and the brain treats it
/// as a pass-through rather than an impact — which is what stops an ally standing in a
/// doorway from eating a shuriken meant for the guard behind them.
///
/// DAMAGE COMES FROM THE ABILITY. This applies the same DamageEffect list StrikeHandler
/// applies for melee, so an attack is authored once whether it arrives by blade or through
/// the air. Weapon dice, mitigation, damage numbers, hit reactions and hit procs all
/// come with it. The projectile's own DiceProfile supplies the dice, because a thrown weapon
/// is the weapon — a shuriken must not roll the katana still in the main-hand slot.
/// </summary>
[RequireComponent(typeof(ProjectileBrain))]
public class ProjectilePayload : MonoBehaviour, IProjectileModule
{
    private ProjectileBrain brain;

    public void Initialize(ProjectileBrain projectileBrain)
    {
        brain = projectileBrain;
    }

    public void UpdateModule() { }

    /// <summary>True when the contact counted. False tells the brain to keep flying.</summary>
    public bool Resolve(ProjectileHitInfo hit)
    {
        // A decorative projectile is not a weapon. It passes through everything, counts no
        // contact, and never reaches a damage path — which is what lets a Stream spray visible
        // bolts while the volume behind them does the damage exactly once per tick.
        if (brain.Data.cosmeticOnly) return false;

        if (!Accepts(hit)) return false;

        ControllerBrain source = brain.Source;

        // The thrower died mid-flight. The contact still counts — the projectile stops — but
        // nothing can be attributed, so no damage is credited to a destroyed entity.
        if (source == null) return true;

        DamageSystem sourceDamage = source.GetModule<DamageSystem>();

        if (sourceDamage == null)
        {
            Debug.LogError($"[ProjectilePayload] {source.name} has no DamageSystem — cannot deal damage.", this);
            return true;
        }

        if (hit.targetBrain != null)
            DamageBrainTarget(hit, source, sourceDamage);
        else if (hit.legacyTarget != null)
            DamageLegacyTarget(hit, sourceDamage);

        SpawnAbilityHitVfx(source, hit.point);
        return true;
    }

    // ── Filtering ─────────────────────────────────────────────────────────

    /// <summary>
    /// Whether this contact counts. The stance comes from the RUNTIME, not the asset, so a
    /// spell composed at cast time decides who its projectile is for.
    /// Legacy props have no faction and are always valid targets.
    /// </summary>
    private bool Accepts(ProjectileHitInfo hit)
    {
        if (hit.isEnvironment) return brain.Data.hitEnvironment;
        if (hit.targetBrain == null) return true;

        return ProjectileAim.StanceAllows(brain.Runtime.targetStance, brain.Source, hit.targetBrain);
    }

    // ── Brain entities ────────────────────────────────────────────────────

    /// <summary>
    /// Apply this projectile to one entity, with no contact involved.
    ///
    /// The seam a VOLUME resolves through. A ticking area has no collision — it found its
    /// targets by overlapping them — but everything after that point must be identical to a
    /// contact hit: the same stance rule, the same arrival handler, the same dice, the same
    /// hit procs. Sharing this method is what guarantees a Field burns exactly like a fireball
    /// rather than approximately like one.
    /// </summary>
    public void ResolveOnBrain(ControllerBrain target, Vector3 point)
    {
        if (target == null) return;
        if (brain.Source == null) return;
        if (!ProjectileAim.StanceAllows(brain.Runtime.targetStance, brain.Source, target)) return;

        DamageSystem sourceDamage = brain.Source.GetModule<DamageSystem>();
        if (sourceDamage == null) return;

        var hit = new ProjectileHitInfo
        {
            point = point,
            normal = Vector3.up,
            travelDirection = brain.Direction,
            targetBrain = target,
        };

        DamageBrainTarget(hit, brain.Source, sourceDamage);
    }

    private void DamageBrainTarget(ProjectileHitInfo hit, ControllerBrain source, DamageSystem sourceDamage)
    {
        DamageSystem targetDamage = hit.targetBrain.GetModule<DamageSystem>();
        if (targetDamage == null) return;

        AbilityDefinition ability = brain.SourceAbility;
        float multiplier = brain.Runtime.damageMultiplier;

        // Hit procs — the same call StrikeHandler makes, so a thrown weapon can transform a
        // hotbar slot exactly like a melee hit does.
        source.GetModule<AbilitySystem>()?.NotifyHitLanded(ability, hit.targetBrain);

        // THE ARRIVAL SEAM. Whatever on the firing brain claims the right to decide what this
        // hit means — a composed spell's Effect slot decides heal vs damage vs bind, and only
        // the spell system knows which was cast. Returning true means it handled everything;
        // almost every shot in the game falls straight through to the damage below.
        //
        // Resolved by the launcher and carried on the shot, not looked up here: an interface
        // lookup can never hit ControllerBrain's provider cache, so doing it per contact meant
        // a hierarchy walk per pierce.
        if (brain.Arrival != null &&
            brain.Arrival.HandleArrival(ability, hit.targetBrain, hit, brain.Dice, multiplier))
            return;

        if (ability?.damageEffects != null && ability.damageEffects.Count > 0)
        {
            // The projectile's own dice, not the thrower's equipped weapon — a shuriken must not
            // roll the katana still in the main-hand slot. Resolved by the brain at launch: the
            // per-cast override when one was supplied, otherwise the asset's own. A spell's tier
            // arrives through the override, which is what makes spells weapons.
            DiceProfile dice = brain.Dice;

            for (int i = 0; i < ability.damageEffects.Count; i++)
            {
                var effect = ability.damageEffects[i];
                if (effect == null) continue;

                effect.SetDamageSystem(sourceDamage);
                effect.Apply(targetDamage, multiplier, dice, DamageSource.Projectile, hit.point);
            }
        }
        else
        {
            CombatDamagePacket packet = sourceDamage.CalculateDamage(BuildAttack(sourceDamage, hit, multiplier));
            float applied = targetDamage.TakeDamage(packet);

            DamageNumberManager.Spawn(applied, NumberPoint(hit.targetBrain, hit.point));
        }

        // Statuses ride the arrival, whichever damage branch ran — the branch is about where the
        // numbers came from, not about what the ability does. Above the arrival seam's early
        // return on purpose: a handler that returns true resolved the hit entirely, statuses
        // included, and this must not apply them a second time.
        ability?.ApplyStatuses(hit.targetBrain, source);
        ability?.ApplyKnockback(hit.targetBrain, brain.transform);
    }

    // ── Legacy IDamageable — breakable props ──────────────────────────────

    private void DamageLegacyTarget(ProjectileHitInfo hit, DamageSystem sourceDamage)
    {
        CombatDamagePacket packet = sourceDamage.CalculateDamage(BuildAttack(sourceDamage, hit, 1f));
        float damage = packet.finalDamage * brain.Runtime.damageMultiplier;

        hit.legacyTarget.TakeDamage(damage, hit.point);

        DamageNumberManager.Spawn(damage, hit.point);
    }

    // ── Shared ────────────────────────────────────────────────────────────

    /// <summary>
    /// The no-damageEffects path. Rolls the projectile's own dice when it has any, so a
    /// shuriken authored with 1d4 still rolls on an ability with nothing configured.
    /// </summary>
    private CombatAttackData BuildAttack(DamageSystem sourceDamage, ProjectileHitInfo hit, float weaponMultiplier)
    {
        ProjectileData data = brain.Data;
        DiceProfile dice = brain.Dice;

        float baseDamage = dice != null ? dice.RollDamage() : data.fallbackDamage;

        return new CombatAttackData
        {
            baseDamage = baseDamage,
            damageType = data.fallbackDamageType,
            attackerTransform = sourceDamage.transform,
            hitPoint = hit.point,
            hitNormal = hit.normal,
            attackDirection = hit.travelDirection,
            comboCount = 0,
            comboMultiplier = 1f,
            weaponDamageMultiplier = weaponMultiplier,
            source = DamageSource.Projectile,
        };
    }

    /// <summary>
    /// Where a damage number appears. Matches DamageEffect, which uses the target's Overhead
    /// anchor rather than the contact point, so numbers land in the same place on both paths.
    /// </summary>
    private Vector3 NumberPoint(ControllerBrain target, Vector3 fallback)
    {
        VFXSystem vfx = target != null ? target.GetModule<VFXSystem>() : null;

        return vfx != null ? vfx.GetAnchorPosition(VFXAnchor.Overhead) : fallback;
    }

    /// <summary>
    /// Spawns through the ATTACKER'S VFXSystem, matching StrikeHandler — the effect belongs to
    /// the attack, not to the thing being hit.
    /// </summary>
    private void SpawnAbilityHitVfx(ControllerBrain source, Vector3 point)
    {
        GameObject prefab = brain.SourceAbility?.hitEffectPrefab;
        if (prefab == null) return;

        source.GetModule<VFXSystem>()?.SpawnEffectAt(prefab, point);
    }
}
