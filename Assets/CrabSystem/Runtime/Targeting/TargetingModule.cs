using System.Collections.Generic;
using UnityEngine;

// P5 targeting for the player: a soft target, always picked — the nearest-to-centre fighter in front of the camera.
// An attack tracks it: during startup and active frames the body turns toward it when it's inside the tracking
// cone. The turn goes through MovementSystem.FacingOverride, which this writes every frame.
//
// Parked (10 Oct, Crxb): a Tab hard lock — camera yawing onto the target, strafing round it, a wider tracking cone.
// Turning away to move while locked kept strafe speed, and a lock camera didn't suit the game. The camera half stays
// (CameraModule.LookTarget) for an ability that locks a target, e.g. a berserker rage.
public class TargetingModule : MonoBehaviour, IBrainModule
{
    private static readonly Collider[] Overlaps = new Collider[64];

    [Header("Finding")]
    [Tooltip("Fighters further than this are never picked.")]
    [SerializeField] private float range = 15f;
    [Tooltip("The full cone in front of the camera a fighter must be inside, in degrees.")]
    [SerializeField] private float softCone = 60f;
    [SerializeField] private LayerMask mask = ~0;
    [SerializeField] private float scanInterval = 0.1f;

    [Header("Attack tracking")]
    [Tooltip("Full cone around the body an attack turns toward its target, in degrees.")]
    [SerializeField] private float trackingCone = 60f;

    public bool IsEnabled { get; set; } = true;
    public bool PlayerOnly => true;

    public ControllerBrain CurrentTarget { get; private set; }

    private ControllerBrain brain;
    private CameraModule cameraModule;
    private MovementSystem movement;
    private AbilitySystem abilities;
    private float nextScanAt;
    private readonly HashSet<ControllerBrain> seen = new HashSet<ControllerBrain>();

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        cameraModule = brain.GetModule<CameraModule>();
        movement = brain.Movement;
        abilities = brain.Abilities;
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        if (Time.time >= nextScanAt)
        {
            nextScanAt = Time.time + scanInterval;
            CurrentTarget = FindTarget();
        }

        if (movement != null) movement.FacingOverride = TrackingFacing();
    }

    // Toward the target while an attack tracks it; zero (the camera decides) otherwise.
    private Vector3 TrackingFacing()
    {
        if (CurrentTarget == null || !Tracking()) return Vector3.zero;

        Vector3 to = Flat(Root(CurrentTarget).position - Root(brain).position);
        return Vector3.Angle(Flat(Root(brain).forward), to) <= trackingCone * 0.5f ? to : Vector3.zero;
    }

    // An attack with frame data, before its recovery.
    private bool Tracking()
    {
        AbilityDefinition move = abilities != null ? abilities.CurrentAbility : null;
        return move != null && move.HasMoveData && abilities.CurrentPhase != MovePhase.Recovery;
    }

    private ControllerBrain FindTarget()
    {
        Transform view = cameraModule != null ? cameraModule.CameraTransform : null;
        Vector3 forward = view != null ? view.forward : Root(brain).forward;
        Vector3 origin = Root(brain).position;

        int count = Physics.OverlapSphereNonAlloc(origin, range, Overlaps, mask, QueryTriggerInteraction.Collide);
        seen.Clear();

        ControllerBrain best = null;
        float bestAngle = softCone * 0.5f;

        for (int i = 0; i < count; i++)
        {
            ControllerBrain other = Overlaps[i].GetComponentInParent<ControllerBrain>();
            if (other == null || !seen.Add(other) || !Targetable(other)) continue;

            float angle = Vector3.Angle(Flat(forward), Flat(Root(other).position - origin));
            if (angle > bestAngle) continue;

            bestAngle = angle;
            best = other;
        }

        return best;
    }

    // Another living fighter that isn't on our side.
    private bool Targetable(ControllerBrain other)
    {
        if (other == brain || !other.isActiveAndEnabled) return false;
        if (other.Health == null || !other.Health.IsAlive()) return false;
        return brain.Faction == null || !brain.Faction.IsFriendlyTo(other);
    }

    // The moving body: EntityRoot when the brain sits under one.
    private static Transform Root(ControllerBrain b) => b.EntityRoot != null ? b.EntityRoot : b.transform;

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
