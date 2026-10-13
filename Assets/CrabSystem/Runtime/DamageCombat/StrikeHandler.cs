using System;
using System.Collections.Generic;
using RPG.Factions;
using UnityEngine;

// Melee hit detection (Combat_Framework.md §2.5, Strike_Build.md). On a Strike event the attacker asks one
// question: who is in front of me, within reach, inside the height band, with nothing in the way? Each
// fighter that answers yes is hit once, and the hit resolves through the ability the way a weapon contact
// always did. Nothing reads the animated blade, so the clip only has to look right, and a server needs
// positions and facing only.
//
// Reach is the weapon's (ItemDefinition.reach, or the stance's unarmed reach) plus the strike's bonus,
// measured edge to edge: from the attacker's hurtbox surface to the target's.
//
// A strategy inside AbilitySystem, which owns it and calls it on each Strike event.
public class StrikeHandler
{
    const string HurtboxLayer = "Hurtbox";
    static readonly string[] BlockerLayers = { "Terrain", "StaticProp" };

    readonly ControllerBrain brain;
    readonly AbilitySystem abilities;
    readonly DamageSystem damage;
    readonly CombatStanceModule stance;
    readonly EquipmentSystem equipment;
    readonly VFXSystem vfx;
    readonly string weaponSlotId;
    readonly int hurtboxMask;
    readonly int blockerMask;

    readonly Collider[] overlaps = new Collider[32];
    readonly HashSet<ControllerBrain> struck = new HashSet<ControllerBrain>();
    readonly Dictionary<Collider, ControllerBrain> owners = new Dictionary<Collider, ControllerBrain>();

    // Every strike, hit or miss, for StrikeTrace to draw.
    public event Action<StrikeArea> OnStrike;

    public StrikeHandler(ControllerBrain brain, AbilitySystem abilities, string weaponSlotId)
    {
        this.brain = brain;
        this.abilities = abilities;
        this.weaponSlotId = weaponSlotId;
        damage = brain.GetModule<DamageSystem>();
        stance = brain.GetModule<CombatStanceModule>();
        equipment = brain.GetModule<EquipmentSystem>();
        vfx = brain.GetModule<VFXSystem>();
        hurtboxMask = LayerMask.GetMask(HurtboxLayer);
        blockerMask = LayerMask.GetMask(BlockerLayers);
    }

    public void Strike(AbilityDefinition ability, int index)
    {
        if (damage == null || damage.Hurtbox == null)
        {
            Debug.LogError($"[StrikeHandler] {brain.EntityName} has no hurtbox to strike from.", brain);
            return;
        }

        StrikeShape shape = ability.StrikeAt(index);
        StrikeArea area = AreaOf(shape);
        struck.Clear();

        int count = Physics.OverlapCapsuleNonAlloc(AtHeight(area, area.bottom), AtHeight(area, area.top), area.radius,
                                                   overlaps, hurtboxMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            TryHit(ability, shape, area, overlaps[i]);

        area.hits = struck.Count;
        OnStrike?.Invoke(area);
    }

    // Would this strike land on target from where both stand now? The same checks, no damage.
    public bool Reaches(AbilityDefinition ability, int index, ControllerBrain target)
    {
        if (damage == null || damage.Hurtbox == null) return false;

        Collider hurtbox = target != null && target.Damage != null ? target.Damage.Hurtbox : null;
        if (hurtbox == null) return false;

        StrikeShape shape = ability.StrikeAt(index);
        StrikeArea area = AreaOf(shape);
        return InRange(area, hurtbox) && InShape(shape, area, hurtbox);
    }

    // The weapon's reach in metres. A weapon without dice swings a fist, as DamageEffect rolls it.
    public float WeaponReach()
    {
        if (stance != null && stance.IsUnarmed) return stance.UnarmedReach;

        ItemInstance item = equipment != null ? equipment.GetEquippedItem(weaponSlotId) : null;
        ItemDefinition weapon = item != null ? item.Definition : null;
        if (weapon != null && weapon.weaponData != null) return weapon.reach;

        return stance != null ? stance.UnarmedReach : 0f;
    }

    private void TryHit(AbilityDefinition ability, StrikeShape shape, StrikeArea area, Collider other)
    {
        ControllerBrain target = BrainOf(other);
        if (target == null || target == brain) return;
        if (struck.Contains(target)) return;
        if (IsFriendly(target)) return;
        if (!InRange(area, other) || !InShape(shape, area, other)) return;

        struck.Add(target);
        Hit(ability, target, other.ClosestPoint(area.centre));
    }

    private void Hit(AbilityDefinition ability, ControllerBrain target, Vector3 point)
    {
        DamageSystem targetDamage = target.Damage;
        if (targetDamage == null) return;

        float applied = ApplyDamage(ability, targetDamage, point);

        if (ability.hitEffectPrefab != null && vfx != null)
            vfx.SpawnEffectAt(ability.hitEffectPrefab, point);

        // Statuses, knockback and the hit state ride the contact; a guarded hit causes no hit state
        // (ApplyHitState asks the target's DamageSystem). Hit-stop and procs come last, once the hit has
        // resolved, so they know whether it was guarded.
        ability.ApplyStatuses(target, brain);
        ability.ApplyKnockback(target, brain.transform);
        ability.ApplyHitState(target, brain, applied);
        abilities.NotifyHitLanded(ability, target);
    }

    private float ApplyDamage(AbilityDefinition ability, DamageSystem target, Vector3 point)
    {
        if (ability.damageEffects == null) return 0f;

        float applied = 0f;
        foreach (DamageEffect effect in ability.damageEffects)
        {
            effect.SetDamageSystem(damage);
            applied += effect.Apply(target, 1f, null, DamageSource.Melee, point);
        }
        return applied;
    }

    private StrikeArea AreaOf(StrikeShape shape)
    {
        Bounds body = damage.Hurtbox.bounds;
        Vector3 forward = damage.transform.forward;
        forward.y = 0f;

        return new StrikeArea
        {
            centre = body.center,
            forward = forward.normalized,
            radius = body.extents.x + Mathf.Max(0f, WeaponReach() + shape.reachBonus),
            arc = shape.arc,
            bottom = body.min.y + shape.heightMin,
            top = body.min.y + Mathf.Max(shape.heightMin, shape.heightMax),
        };
    }

    // Edge to edge: the target's nearest surface, measured flat from the attacker's centre.
    private static bool InRange(StrikeArea area, Collider other)
    {
        Vector3 offset = other.ClosestPoint(area.centre) - area.centre;
        offset.y = 0f;
        return offset.sqrMagnitude <= area.radius * area.radius;
    }

    private bool InShape(StrikeShape shape, StrikeArea area, Collider other)
    {
        Bounds bounds = other.bounds;
        if (bounds.max.y < area.bottom || bounds.min.y > area.top) return false;
        if (!InArc(area, bounds.center)) return false;
        return !shape.lineOfSight || Clear(area.centre, bounds.center);
    }

    // A target standing inside the attacker counts as in front.
    private static bool InArc(StrikeArea area, Vector3 point)
    {
        Vector3 to = point - area.centre;
        to.y = 0f;
        if (to == Vector3.zero) return true;
        return Vector3.Angle(area.forward, to) <= area.arc * 0.5f;
    }

    private bool Clear(Vector3 from, Vector3 to)
        => !Physics.Linecast(from, to, blockerMask, QueryTriggerInteraction.Ignore);

    private bool IsFriendly(ControllerBrain target)
    {
        FactionSystem faction = brain.Faction;
        if (faction == null || target.Faction == null) return false;
        return faction.GetStanceTo(target) == FactionRelationship.Friendly;
    }

    // A hurtbox never changes owner, so each is looked up once.
    private ControllerBrain BrainOf(Collider other)
    {
        if (owners.TryGetValue(other, out ControllerBrain owner)) return owner;

        owner = other.GetComponentInParent<ControllerBrain>();
        owners[other] = owner;
        return owner;
    }

    private static Vector3 AtHeight(StrikeArea area, float height)
        => new Vector3(area.centre.x, height, area.centre.z);
}

// One strike's checked space: a cone of arc degrees around forward, radius out from the attacker's centre
// (its own hurtbox edge plus the reach), between two heights. hits counts the fighters it landed on.
public struct StrikeArea
{
    public Vector3 centre;
    public Vector3 forward;
    public float radius;
    public float arc;
    public float bottom;
    public float top;
    public int hits;
}
