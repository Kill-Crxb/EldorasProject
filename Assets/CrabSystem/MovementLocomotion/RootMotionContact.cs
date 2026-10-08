using UnityEngine;

// Stops a root-motion move at the body it is travelling into (Souls style). Two round capsules meeting at
// lunge speed slide off each other, so a combo string carried the attacker through or past the target.
// Each step, any fighter's hurtbox the travel closes on gets the closing part of the velocity cut so the
// two bodies stop gapMetres apart. A body AHEAD (within PinAngle of the travel) also takes the sideways
// part, so the attacker stays planted on it instead of skating round it (8 Oct: an attack a little off
// centre slid past). A body off to the side only stops what heads into it, so a clear miss still carries.
// Every fighter runs it, so an NPC can't lunge through the player either.
//
// A strategy inside ParkourLocomotionHandler, which calls it while MovementSystem.RootMotionDriven is up.
public class RootMotionContact
{
    const string HurtboxLayer = "Hurtbox";

    // How far past this step's travel to look for other bodies: wider than the widest hurtbox radius.
    const float SearchMargin = 2f;

    // Degrees either side of the travel within which a body counts as ahead: reaching it pins the attacker.
    const float PinAngle = 60f;

    readonly ControllerBrain brain;
    readonly int hurtboxMask;
    readonly Collider[] nearby = new Collider[16];

    Collider ownHurtbox;

    public RootMotionContact(ControllerBrain brain)
    {
        this.brain = brain;
        hurtboxMask = LayerMask.GetMask(HurtboxLayer);
    }

    public Vector3 Limit(Vector3 velocity, float dt, float gap)
    {
        Collider own = OwnHurtbox();
        if (own == null || dt <= 0f) return velocity;

        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
        float travel = flat.magnitude * dt;
        if (travel <= 0f) return velocity;

        Bounds body = own.bounds;
        int count = Physics.OverlapSphereNonAlloc(body.center, body.extents.x + travel + SearchMargin, nearby,
                                                  hurtboxMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
            flat = StopAt(flat, body, nearby[i], dt, gap);

        return new Vector3(flat.x, velocity.y, flat.z);
    }

    // Cuts the part of flat that would close on other past the gap this step.
    private Vector3 StopAt(Vector3 flat, Bounds body, Collider other, float dt, float gap)
    {
        if (other == ownHurtbox) return flat;

        Bounds them = other.bounds;
        if (them.max.y < body.min.y || them.min.y > body.max.y) return flat;

        Vector3 to = them.center - body.center;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.0001f) return flat;

        Vector3 toward = to / distance;
        float closing = Vector3.Dot(flat, toward);
        if (closing <= 0f) return flat;

        float room = Mathf.Max(0f, distance - body.extents.x - them.extents.x - gap);
        float allowed = room / dt;
        if (closing <= allowed) return flat;

        bool ahead = Vector3.Angle(flat, toward) <= PinAngle;
        if (ahead) return toward * allowed;

        return flat - toward * (closing - allowed);
    }

    // DamageSystem builds the hurtbox in its own Awake/Initialize, which may run after the handler's.
    private Collider OwnHurtbox()
    {
        if (ownHurtbox != null) return ownHurtbox;

        DamageSystem damage = brain != null ? brain.Damage : null;
        ownHurtbox = damage != null ? damage.Hurtbox : null;
        return ownHurtbox;
    }
}
