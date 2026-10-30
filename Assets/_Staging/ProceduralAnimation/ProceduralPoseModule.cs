using System;
using System.Collections.Generic;
using CrabSystem;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

// ProceduralPoseModule (staging; Procedural_Animation_Theory.md)
//
// Attach to: a child of the character's brain, like the other modules.
//
// The procedural half of the hybrid animation plan. It does not take over the Animator: it runs a second graph on the
// same Animator whose output reads the controller's finished pose (AnimationStreamSource.PreviousInputs) and edits it,
// the way Animation Rigging does. Parameters, cross-fades, AbilitySystem.HitStop, clip events, root motion and
// OnAnimatorIK all stay with the controller. Master weight 0, or removing this module, leaves the pose untouched.
//
// Graph: posture → gait → head look → snap → foot planting → Animator. Each layer has its own weight under the master.
//
// Posture sources: the worn model's ModelPosture (base stance), statuses and equipped items listed in the
// PostureBindings table. They stack and fade (PostureLayer). The graph follows brain.EntityAnimator, so a model
// swap rebuilds it and picks up the new model's base posture.
//
// Gait (Procedural_Gait_Design.md, approach B, build step 2): plays a baked GaitCycle by step phase, advanced by the
// distance travelled, so the feet keep pace with the body at any speed. It fades in with speed, stays off while a
// rooted move owns the body, and leaves the upper body to an action showing on Actions Upper (a block or cast on the
// move). Off by default (gaitWeight 0): the clip locomotion stays until gait is signed off.

public class ProceduralPoseModule : MonoBehaviour, IBrainModule
{
    [Tooltip("Scales every procedural layer. 0 = the Animator's pose, untouched.")]
    [Range(0f, 1f)] [SerializeField] private float masterWeight = 1f;

    [Header("Posture")]
    [Range(0f, 1f)] [SerializeField] private float postureWeight = 1f;

    [Tooltip("Statuses and items that bring a posture with them. Optional.")]
    [SerializeField] private PostureBindings bindings;

    [Header("Gait")]
    [Tooltip("How much the gait cycle drives the body while moving. 0 = the Animator's locomotion, untouched.")]
    [Range(0f, 1f)] [SerializeField] private float gaitWeight;

    [Tooltip("The baked loop to play (Tools → Characters → Gait → Bake Cycle).")]
    [SerializeField] private GaitCycle gaitCycle;

    [Tooltip("Optional run loop, baked on the same model with the same sample count. Blended in by speed.")]
    [SerializeField] private GaitCycle runCycle;

    [Tooltip("Optional sprint loop, baked on the same model with the same sample count. Blended in by speed.")]
    [SerializeField] private GaitCycle sprintCycle;

    [Tooltip("Speed (m/s) the walk cycle is matched to; below it only the walk plays.")]
    [Min(0f)] [SerializeField] private float walkCycleSpeed = 2.5f;

    [Tooltip("Speed (m/s) the run cycle is matched to.")]
    [Min(0.01f)] [SerializeField] private float runCycleSpeed = 5f;

    [Tooltip("Speed (m/s) the sprint cycle is matched to; above it only the sprint plays.")]
    [Min(0.01f)] [SerializeField] private float sprintCycleSpeed = 9f;

    [Tooltip("How much of a speed change goes into longer or shorter strides rather than faster or slower steps, " +
             "measured against the cycle's own authored speed. 0 = cadence only, 1 = stride only.")]
    [Range(0f, 1f)] [SerializeField] private float strideStretch = 0.4f;

    [Tooltip("Strides never stretch or shrink past this factor of the cycle's own.")]
    [Range(1f, 2f)] [SerializeField] private float maxStrideStretch = 1.4f;

    [Tooltip("At or below this speed (m/s) the gait is off and idle shows.")]
    [Min(0f)] [SerializeField] private float gaitStartSpeed = 0.2f;

    [Tooltip("At or above this speed (m/s) the gait is fully on.")]
    [Min(0.01f)] [SerializeField] private float gaitFullSpeed = 0.8f;

    [Header("Foot Planting")]
    [Tooltip("How strongly planted feet hold their ground point while the gait drives the legs. 0 = off.")]
    [Range(0f, 1f)] [SerializeField] private float plantWeight = 1f;

    [Tooltip("A planted foot starts letting go when the cycle's own foot is this far from its locked point, metres.")]
    [Min(0f)] [SerializeField] private float slipStart = 0.06f;

    [Tooltip("At this distance the foot has fully let go and follows the cycle again, metres.")]
    [Min(0.01f)] [SerializeField] private float slipEnd = 0.18f;

    [Tooltip("How far above the foot the ground ray starts. Must clear the tallest step.")]
    [Min(0f)] [SerializeField] private float probeUp = 0.5f;

    [Tooltip("How far below the foot the ground ray reaches.")]
    [Min(0f)] [SerializeField] private float probeDown = 0.6f;

    [Tooltip("How far the planted feet may move the hips up or down to set the body's height, metres.")]
    [Min(0f)] [SerializeField] private float maxBodyDrop = 0.4f;

    [Tooltip("Seconds a foot takes to blend onto or off its ground point.")]
    [Min(0f)] [SerializeField] private float plantFadeSeconds = 0.08f;

    [Header("Stop Momentum")]
    [Tooltip("Forward torso pitch per m/s² of braking, degrees. The torso carries on forward as the body stops, then " +
             "springs back.")]
    [Min(0f)] [SerializeField] private float stopPitchPerDeceleration = 1.2f;

    [Tooltip("Most the torso pitches forward from braking, degrees.")]
    [Range(0f, 45f)] [SerializeField] private float maxStopPitch = 15f;

    [Tooltip("How fast the torso swings back, cycles per second.")]
    [Min(0.1f)] [SerializeField] private float stopSpringFrequency = 2.5f;

    [Tooltip("How quickly the swing dies out. Lower overshoots more on the way back; 1 settles without overshoot.")]
    [Range(0.05f, 1f)] [SerializeField] private float stopSpringDamping = 0.35f;

    [Header("Flinch")]
    [Tooltip("How hard a hit kicks the torso away from the attacker, degrees per second.")]
    [Min(0f)] [SerializeField] private float flinchKick = 250f;

    [Tooltip("Kick scale for a heavy attack.")]
    [Min(0f)] [SerializeField] private float heavyFlinchScale = 1.6f;

    [Tooltip("Kick scale for a hit taken on the guard (blocked or parried).")]
    [Min(0f)] [SerializeField] private float guardedFlinchScale = 0.4f;

    [Tooltip("Most the torso is knocked over by a flinch, degrees.")]
    [Range(0f, 45f)] [SerializeField] private float maxFlinch = 20f;

    [Tooltip("How fast the torso swings back from a flinch, cycles per second.")]
    [Min(0.1f)] [SerializeField] private float flinchFrequency = 4f;

    [Tooltip("How quickly the flinch dies out. Lower wobbles more; 1 settles without overshoot.")]
    [Range(0.05f, 1f)] [SerializeField] private float flinchDamping = 0.4f;

    [Header("Head Look")]
    [Tooltip("Head-look applied in this graph (held by snap with the body) instead of by HeadLookSystem's IK. " +
             "HeadLookSystem still decides where to look. 0 = HeadLookSystem's own IK, as before.")]
    [Range(0f, 1f)] [SerializeField] private float lookWeight = 1f;

    [Tooltip("Share of the look turn the chest takes. The three shares add up to the turn the head ends with (1 = the " +
             "full look).")]
    [Range(0f, 1f)] [SerializeField] private float lookChestShare = 0.15f;

    [Tooltip("Share of the look turn the neck takes.")]
    [Range(0f, 1f)] [SerializeField] private float lookNeckShare = 0.35f;

    [Tooltip("Share of the look turn the head takes.")]
    [Range(0f, 1f)] [SerializeField] private float lookHeadShare = 0.5f;

    [Header("Standing Steps")]
    [Tooltip("While standing, a foot this far from where the animated pose wants it steps there (after a stop, or as " +
             "the body turns on the spot), metres.")]
    [Min(0.01f)] [SerializeField] private float stepDistance = 0.12f;

    [Tooltip("How long one standing step takes, seconds.")]
    [Min(0.05f)] [SerializeField] private float stepSeconds = 0.25f;

    [Tooltip("How high a standing step lifts the foot at its middle, metres.")]
    [Min(0f)] [SerializeField] private float stepHeight = 0.08f;

    [Header("Lean and Load")]
    [Tooltip("Forward / back lean per m/s² of acceleration along facing, degrees.")]
    [Min(0f)] [SerializeField] private float leanPerAcceleration = 1.5f;

    [Tooltip("Most the body leans forward or back, degrees.")]
    [Range(0f, 30f)] [SerializeField] private float maxLean = 10f;

    [Tooltip("Sideways bank into turns per (rad/s of turning × m/s of speed), degrees.")]
    [Min(0f)] [SerializeField] private float bankPerTurn = 2f;

    [Tooltip("Most the body banks into a turn, degrees.")]
    [Range(0f, 30f)] [SerializeField] private float maxBank = 10f;

    [Tooltip("How quickly lean and bank follow, seconds.")]
    [Min(0.01f)] [SerializeField] private float leanSmoothingSeconds = 0.15f;

    [Tooltip("Carried load, 0..1. Stand-in until the mov.* stats exist; heavier load shortens the stride and deepens the bob.")]
    [Range(0f, 1f)] [SerializeField] private float load;

    [Tooltip("Stride length at full load, as a factor of the normal stride.")]
    [Range(0.5f, 1f)] [SerializeField] private float strideAtFullLoad = 0.85f;

    [Tooltip("Hips bob at full load, as a factor of the cycle's own bob.")]
    [Range(1f, 2f)] [SerializeField] private float bobAtFullLoad = 1.4f;

    [Header("Snap")]
    [Range(0f, 1f)] [SerializeField] private float snapWeight = 1f;

    [Tooltip("Held poses per second. 12 = on twos at 24 (decided 2026-10-26).")]
    [Min(1f)] [SerializeField] private float snapFramesPerSecond = 12f;

    [Tooltip("Hold the legs too. Off keeps the legs smooth if planted feet slide during a held pose.")]
    [SerializeField] private bool snapLegs = true;

    [Tooltip("Graph output order on the Animator. Below Animation Rigging's 1000, so rig constraints see the posed body.")]
    [SerializeField] private ushort outputOrder = 900;

    // How long the Animator's hips position is averaged over, so the gait keeps its height without the clip's bob.
    private const float HipsSmoothingSeconds = 0.4f;

    private static readonly HumanBodyBones[] LegBones =
    {
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };

    private readonly PostureLayer posture = new PostureLayer();
    private readonly FootPlanter planter = new FootPlanter();
    private readonly List<GaitCycle> cycles = new List<GaitCycle>();
    private NativeArray<TransformStreamHandle> plantLegs;
    private NativeArray<float> plantControl;
    private NativeArray<float> plantState;
    private ControllerBrain brain;
    private StatusSystem statuses;
    private EquipmentSystem equipment;
    private MovementSystem movement;
    private FootIKSystem footIK;
    private HeadLookSystem headLook;
    private DamageSystem damage;
    private NativeArray<TransformStreamHandle> lookBones;
    private NativeArray<float> lookControl;
    private readonly List<int> lookKinds = new List<int>();
    private Vector3 flinch;
    private Vector3 flinchSpeed;
    private bool footIKMuted;
    private bool footIKWasEnabled;
    private Animator boundAnimator;
    private PlayableGraph graph;
    private NativeArray<MuscleHandle> postureMuscles;
    private NativeArray<float> postureOffsets;
    private NativeArray<TransformStreamHandle> gaitBones;
    private NativeArray<byte> gaitUpper;
    private NativeArray<Quaternion> gaitRotations;
    private NativeArray<Vector3> gaitHips;
    private NativeArray<float> gaitControl;
    private NativeArray<Vector3> gaitHipsState;
    private readonly List<float> cycleSpeeds = new List<float>();
    private readonly List<float> cycleStrides = new List<float>();
    private readonly List<float> cycleNaturalSpeeds = new List<float>();
    private NativeArray<TransformStreamHandle> snapBones;
    private NativeArray<Quaternion> snapRotation;
    private NativeArray<Vector3> snapHipsPosition;
    private NativeArray<float> snapState;
    private NativeArray<float> snapControl;
    private int actionsUpperLayer = -1;
    private int movementActionsLayer = -1;
    private int actionsLayer = -1;
    private int reactionsLayer = -1;
    private float strideMetres;
    private float hipsScale = 1f;
    private float gaitPhase;
    private Vector3 lastVelocity;
    private Vector3 lastForward;
    private float lean;
    private float bank;
    private float forwardAcceleration;
    private Vector3 bodyRight = Vector3.right;
    private float torsoPitch;
    private float torsoPitchSpeed;
    private NativeArray<TransformStreamHandle> torsoBones;
    private Transform hipsParent;
    private Transform leftFoot;
    private Transform rightFoot;
    private bool wasStill = true;
    private bool wasFrozen;
    private bool subscribed;

    public bool IsEnabled { get; set; } = true;

    public PostureLayer Posture => posture;

    public float Load
    {
        get => load;
        set => load = Mathf.Clamp01(value);
    }

    public float GaitWeight
    {
        get => gaitWeight;
        set => gaitWeight = Mathf.Clamp01(value);
    }

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        statuses = brain.GetModule<StatusSystem>();
        equipment = brain.GetModule<EquipmentSystem>();
        movement = brain.Movement;
        footIK = brain.GetModule<FootIKSystem>();
        headLook = brain.GetModule<HeadLookSystem>();
        damage = brain.GetModule<DamageSystem>();
        CharacterMotor motor = FeelReferences.FindMotor(brain);
        if (motor != null) planter.Ground = motor.CollisionLayers;
        Subscribe();
    }

    public void UpdateModule()
    {
        if (brain.EntityAnimator != boundAnimator)
            Rebuild(brain.EntityAnimator);

        if (!graph.IsValid()) return;

        // Presentation fades and clocks, like the camera: plain frame time, not the entity clock.
        posture.Tick(Time.deltaTime);
        posture.Write(postureOffsets, masterWeight * postureWeight);

        WriteGait();
        WriteLook();

        // Key on the frame a hit-stop starts or ends, so the freeze shows the impact pose, not one up to a step old.
        bool frozen = boundAnimator.speed <= 0f;
        snapControl[0] = 1f / snapFramesPerSecond;
        snapControl[1] = masterWeight * snapWeight;
        snapControl[2] = boundAnimator.speed;
        snapControl[3] = frozen != wasFrozen ? 1f : 0f;
        wasFrozen = frozen;

        // Foot planting keys on the same frames as snap, so its targets move in held poses too.
        plantControl[5] = snapControl[0];
        plantControl[6] = snapControl[1] > 0f && snapLegs ? 1f : 0f;
        plantControl[7] = snapControl[2];
        plantControl[8] = snapControl[3];
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        MuteFootIK(false);
        if (headLook != null) headLook.ExternalSolver = false;
        Unsubscribe();
        DestroyGraph();
        boundAnimator = null;
    }

    // ── Gait ──────────────────────────────────────────────────────────────────

    // The phase moves one cycle per stride of ground covered, at the Animator's speed, so a hit-stop holds it.
    private void WriteGait()
    {
        float speed = movement != null ? movement.Speed : 0f;
        bool rooted = movement != null && movement.IsRooted;
        float ramp = Ramp(speed);
        // A full-body action (a dash on Movement Actions, a standing attack, a hit reaction) overrides the gait and
        // its foot planting, as it overrode the clip locomotion.
        float fullBody = Mathf.Max(LayerWeight(movementActionsLayer), LayerWeight(actionsLayer), LayerWeight(reactionsLayer));
        float moving = rooted ? 0f : ramp * (1f - fullBody);
        float weight = strideMetres > 0f ? masterWeight * gaitWeight * moving : 0f;
        float action = LayerWeight(actionsUpperLayer);

        int from = 0;
        while (from < cycleSpeeds.Count - 2 && speed > cycleSpeeds[from + 1]) from++;
        int to = Mathf.Min(from + 1, cycleSpeeds.Count - 1);
        float blend = from == to ? 0f : Mathf.InverseLerp(cycleSpeeds[from], cycleSpeeds[to], speed);

        if (wasStill && ramp > 0f && cycles.Count > 0) gaitPhase = LeadPhase(cycles[from]);
        wasStill = ramp <= 0f;

        float stride = StretchedStride(from, to, blend, speed) * Mathf.Lerp(1f, strideAtFullLoad, load);
        if (stride > 0f)
            gaitPhase = Mathf.Repeat(gaitPhase + speed * Time.deltaTime * boundAnimator.speed / stride, 1f);

        // While the Animator is frozen (hit-stop) the additive rotations are left off: with the clip held, the stream
        // can carry last frame's result back in, and adding to it every frame winds the body round.
        bool frozen = boundAnimator.speed <= 0f;
        bool reacting = brain.Blackboard != null && brain.Blackboard.GetBool(BlackboardKey.CannotAct);
        Quaternion leanBank = frozen ? Quaternion.identity : LeanAndBank(speed);
        Vector3 torso = frozen ? Vector3.zero : bodyRight * StopMomentum(!rooted && !reacting && fullBody < 0.5f) +
                                         Flinch() * (1f - LayerWeight(reactionsLayer));
        float torsoAngle = torso.magnitude;
        Vector3 torsoAxis = torsoAngle > 0f ? torso / torsoAngle : bodyRight;
        Vector3 up = hipsParent != null ? hipsParent.InverseTransformDirection(Vector3.up).normalized : Vector3.up;

        gaitControl[0] = gaitPhase;
        gaitControl[1] = weight;
        gaitControl[2] = weight * (1f - action);
        gaitControl[3] = hipsScale;
        gaitControl[4] = 1f - Mathf.Exp(-Time.deltaTime / HipsSmoothingSeconds);
        gaitControl[5] = blend;
        gaitControl[6] = from;
        gaitControl[7] = to;
        gaitControl[9] = leanBank.x;
        gaitControl[10] = leanBank.y;
        gaitControl[11] = leanBank.z;
        gaitControl[12] = leanBank.w;
        gaitControl[13] = Mathf.Lerp(1f, bobAtFullLoad, load);
        gaitControl[14] = up.x;
        gaitControl[15] = up.y;
        gaitControl[16] = up.z;
        gaitControl[17] = torsoAngle * masterWeight;
        gaitControl[18] = torsoAxis.x;
        gaitControl[19] = torsoAxis.y;
        gaitControl[20] = torsoAxis.z;

        bool grounded = movement != null && movement.IsGrounded;
        float standing = grounded && !rooted ? 1f - ramp : 0f;
        WritePlanting(from, to, blend, gaitWeight * moving, standing);
    }

    private float LayerWeight(int layer)
    {
        return layer >= 0 ? boundAnimator.GetLayerWeight(layer) : 0f;
    }

    // Gait fades in with speed: 0 at gaitStartSpeed, full at gaitFullSpeed.
    private float Ramp(float speed)
    {
        return speed >= gaitFullSpeed ? 1f : Mathf.InverseLerp(gaitStartSpeed, gaitFullSpeed, speed);
    }

    // Starting from standing: the phase where the foot behind (along the way of travel) lifts off, so it steps first.
    private float LeadPhase(GaitCycle cycle)
    {
        if (leftFoot == null || rightFoot == null) return gaitPhase;

        Vector3 travel = movement != null ? movement.Velocity : Vector3.zero;
        travel.y = 0f;
        if (travel.sqrMagnitude < 0.0001f) travel = boundAnimator.transform.forward;

        bool leftBehind = Vector3.Dot(leftFoot.position - rightFoot.position, travel) < 0f;
        bool[] planted = leftBehind ? cycle.LeftPlanted : cycle.RightPlanted;
        int n = planted.Length;
        for (int s = 0; s < n; s++)
            if (planted[s] && !planted[(s + 1) % n]) return (float)s / n;

        return gaitPhase;
    }

    // Lean from acceleration along facing, bank from turning at speed, both smoothed. World space, applied to the hips.
    private Quaternion LeanAndBank(float speed)
    {
        float dt = Time.deltaTime;
        Vector3 velocity = movement != null ? movement.Velocity : Vector3.zero;
        velocity.y = 0f;
        Vector3 forward = Vector3.ProjectOnPlane(boundAnimator.transform.forward, Vector3.up).normalized;
        if (lastForward == Vector3.zero) lastForward = forward;

        float leanTarget = 0f;
        float bankTarget = 0f;
        if (dt > 0f)
        {
            Vector3 acceleration = (velocity - lastVelocity) / dt;
            float turnRate = Vector3.SignedAngle(lastForward, forward, Vector3.up) * Mathf.Deg2Rad / dt;
            forwardAcceleration = Vector3.Dot(acceleration, forward);
            leanTarget = Mathf.Clamp(forwardAcceleration * leanPerAcceleration, -maxLean, maxLean);
            bankTarget = Mathf.Clamp(turnRate * speed * bankPerTurn, -maxBank, maxBank);
        }

        lastVelocity = velocity;
        lastForward = forward;
        float follow = 1f - Mathf.Exp(-dt / leanSmoothingSeconds);
        lean = Mathf.Lerp(lean, leanTarget, follow);
        bank = Mathf.Lerp(bank, bankTarget, follow);

        bodyRight = Vector3.Cross(Vector3.up, forward);
        return Quaternion.AngleAxis(lean, bodyRight) * Quaternion.AngleAxis(-bank, forward);
    }

    // A hit kicks the torso spring away from the attacker (the top of the body tips along the attack). It shows only as
    // far as no reaction clip does: the light flinch for hits that play none (guarded, armoured, projectiles), so it
    // doesn't fight the Reactions layer.
    private void HandleDamageApplied(CombatDamagePacket packet, float amount)
    {
        if (boundAnimator == null) return;

        Vector3 direction = packet.attackDirection;
        if (direction.sqrMagnitude < 0.0001f && packet.attacker != null)
            direction = boundAnimator.transform.position - packet.attacker.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        float scale = (packet.isHeavyAttack ? heavyFlinchScale : 1f) * (damage.LastHitGuarded ? guardedFlinchScale : 1f);
        flinchSpeed += Vector3.Cross(Vector3.up, direction.normalized) * (flinchKick * scale);
    }

    // The flinch spring: a world rotation vector (axis × degrees) that swings back to rest. Same capped frame time as
    // the stop spring.
    private Vector3 Flinch()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float omega = 2f * Mathf.PI * flinchFrequency;
        flinchSpeed += (-omega * omega * flinch - 2f * flinchDamping * omega * flinchSpeed) * dt;
        flinch = Vector3.ClampMagnitude(flinch + flinchSpeed * dt, maxFlinch);
        return flinch;
    }

    // ── Head look ─────────────────────────────────────────────────────────────

    // HeadLookSystem's direction and weight (a frame old: its IK callback runs after this), applied by LookLayerJob.
    private void WriteLook()
    {
        bool on = headLook != null && lookBones.Length > 0 && masterWeight * lookWeight > 0f;
        if (headLook != null) headLook.ExternalSolver = on;

        Vector3 look = headLook != null ? headLook.CurrentDirection : Vector3.zero;
        // A reaction clip owns the head: the look fades out with it rather than holding the head against it.
        float weight = on && look != Vector3.zero ? masterWeight * lookWeight * headLook.CurrentWeight : 0f;
        lookControl[0] = weight * (1f - LayerWeight(reactionsLayer));
        lookControl[1] = look.x;
        lookControl[2] = look.y;
        lookControl[3] = look.z;
        // The same facing HeadLookSystem measures its turn from.
        Transform body = brain.ModelRoot != null ? brain.ModelRoot : boundAnimator.transform;
        Vector3 facing = Vector3.ProjectOnPlane(body.forward, Vector3.up).normalized;
        lookControl[4] = facing.x;
        lookControl[5] = facing.y;
        lookControl[6] = facing.z;
        for (int i = 0; i < lookKinds.Count; i++)
            lookControl[7 + i] = LookShare(lookKinds[i]);
    }

    private float LookShare(int kind)
    {
        return kind == 0 ? lookChestShare : kind == 1 ? lookNeckShare : lookHeadShare;
    }

    // Chest (the upper chest if the model has one), neck, head; none without a head.
    private void BuildLook(Animator animator)
    {
        lookKinds.Clear();
        var handles = new List<TransformStreamHandle>();
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);

        if (head != null && chest != null) AddLookBone(animator, handles, chest, 0);
        if (head != null && neck != null) AddLookBone(animator, handles, neck, 1);
        if (head != null) AddLookBone(animator, handles, head, 2);

        lookBones = new NativeArray<TransformStreamHandle>(handles.ToArray(), Allocator.Persistent);
        lookControl = new NativeArray<float>(LookLayerJob.Size, Allocator.Persistent);
    }

    private void AddLookBone(Animator animator, List<TransformStreamHandle> handles, Transform bone, int kind)
    {
        handles.Add(animator.BindStreamTransform(bone));
        lookKinds.Add(kind);
    }

    // A spring pulled forward by the body's own braking: the torso carries on as the body stops, overshoots back and
    // settles. Not fed while a move roots the body or a hit state holds it (knockback, lunges), so only stops from
    // moving push it. The frame time is capped so a hitch can't blow the spring up, and the pitch is capped.
    private float StopMomentum(bool feed)
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        float braking = feed ? Mathf.Max(0f, -forwardAcceleration) : 0f;
        float target = Mathf.Min(braking * stopPitchPerDeceleration, maxStopPitch);
        float omega = 2f * Mathf.PI * stopSpringFrequency;
        torsoPitchSpeed += (omega * omega * (target - torsoPitch) - 2f * stopSpringDamping * omega * torsoPitchSpeed) * dt;
        torsoPitch = Mathf.Clamp(torsoPitch + torsoPitchSpeed * dt, -maxStopPitch, maxStopPitch * 1.5f);
        return torsoPitch;
    }

    // Moving: the planted flags come from whichever cycle dominates the blend, at the current phase.
    // Standing: both feet are planted, which replaces FootIKSystem (it stays switched off while planting is active).
    private void WritePlanting(int from, int to, float blend, float drive, float standing)
    {
        planter.SlipStart = slipStart;
        planter.SlipEnd = slipEnd;
        planter.ProbeUp = probeUp;
        planter.ProbeDown = probeDown;
        planter.MaxBodyDrop = maxBodyDrop;
        planter.FadeSeconds = plantFadeSeconds;
        planter.StepDistance = stepDistance;
        planter.StepSeconds = stepSeconds;
        planter.StepHeight = stepHeight;

        bool leftPlanted = false;
        bool rightPlanted = false;
        float leftLift = 0f;
        float rightLift = 0f;
        bool standingMode = standing > 0f && standing >= drive;
        if (standingMode)
        {
            leftPlanted = true;
            rightPlanted = true;
        }
        else if (cycles.Count > 0)
        {
            GaitCycle cycle = cycles[blend < 0.5f ? from : to];
            int sample = (int)(gaitPhase * cycle.Samples) % cycle.Samples;
            leftPlanted = cycle.LeftPlanted[sample];
            rightPlanted = cycle.RightPlanted[sample];
            leftLift = cycle.LeftLift.Length > sample ? cycle.LeftLift[sample] * hipsScale : 0f;
            rightLift = cycle.RightLift.Length > sample ? cycle.RightLift[sample] * hipsScale : 0f;
        }

        float active = masterWeight * plantWeight * Mathf.Clamp01(drive + standing);
        MuteFootIK(masterWeight * plantWeight > 0f && plantLegs.Length > 0);
        planter.Tick(plantControl, plantState, active, Time.deltaTime, standingMode, leftPlanted, leftLift, rightPlanted,
                     rightLift);
    }

    // Away from the cycle's authored speed, part of the change goes into stride length instead of cadence.
    private float StretchedStride(int from, int to, float blend, float speed)
    {
        if (cycleStrides.Count == 0) return 0f;

        float stride = Mathf.Lerp(cycleStrides[from], cycleStrides[to], blend);
        float natural = Mathf.Lerp(cycleNaturalSpeeds[from], cycleNaturalSpeeds[to], blend);
        if (natural <= 0f || speed <= 0f) return stride;

        float stretch = Mathf.Pow(speed / natural, strideStretch);
        return stride * Mathf.Clamp(stretch, 1f / maxStrideStretch, maxStrideStretch);
    }

    // FootIKSystem's goals are solved after this graph and pull the legs back onto the clip's feet, so it is switched
    // off while the gait drives the legs and put back as it was after. Gait's own foot planter (build step 3) takes over.
    private void MuteFootIK(bool mute)
    {
        if (footIK == null || mute == footIKMuted) return;

        footIKMuted = mute;
        if (mute) footIKWasEnabled = footIK.IsEnabled;
        footIK.IsEnabled = mute ? false : footIKWasEnabled;
    }

    private static TransformStreamHandle[] TorsoHandles(Animator animator)
    {
        var handles = new List<TransformStreamHandle>();
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (spine != null) handles.Add(animator.BindStreamTransform(spine));
        if (chest != null) handles.Add(animator.BindStreamTransform(chest));
        return handles.ToArray();
    }

    // Binds the cycle's bones this model has and copies only their rotations. A model without the hips gets no gait.
    private void BuildGait(Animator animator)
    {
        strideMetres = 0f;
        hipsScale = 1f;
        actionsUpperLayer = animator.GetLayerIndex(AnimationLayerNames.ActionsUpper);
        movementActionsLayer = animator.GetLayerIndex(AnimationLayerNames.MovementActions);
        actionsLayer = animator.GetLayerIndex(AnimationLayerNames.Actions);
        reactionsLayer = animator.GetLayerIndex(AnimationLayerNames.Reactions);

        var handles = new List<TransformStreamHandle>();
        var upper = new List<byte>();
        var kept = new List<int>();
        HumanBodyBones[] order = gaitCycle != null ? gaitCycle.Bones : new HumanBodyBones[0];

        for (int i = 0; i < order.Length; i++)
        {
            Transform t = animator.GetBoneTransform(order[i]);
            if (t == null) continue;

            handles.Add(animator.BindStreamTransform(t));
            upper.Add(IsUpper(order[i]) ? (byte)1 : (byte)0);
            kept.Add(i);
        }

        bool ready = kept.Count > 0 && order[kept[0]] == HumanBodyBones.Hips;
        if (gaitCycle != null && !ready)
            Debug.LogError($"[ProceduralPoseModule] '{animator.name}' has no hips bound for {gaitCycle.name}; gait is off.", animator);

        if (!ready)
        {
            handles.Clear();
            upper.Clear();
            kept.Clear();
        }

        cycleSpeeds.Clear();
        cycleStrides.Clear();
        cycleNaturalSpeeds.Clear();
        cycles.Clear();
        var used = new List<GaitCycle>();
        var speeds = new List<float>();
        if (ready) AddCycle(used, speeds, gaitCycle, walkCycleSpeed);
        if (ready && Matches(runCycle, animator)) AddCycle(used, speeds, runCycle, runCycleSpeed);
        if (ready && Matches(sprintCycle, animator)) AddCycle(used, speeds, sprintCycle, sprintCycleSpeed);

        var rotations = new List<Quaternion>();
        var hips = new List<Vector3>();
        foreach (var cycle in used)
        {
            rotations.AddRange(KeptRotations(cycle.Rotations, order.Length, kept));
            hips.AddRange(cycle.HipsPositions);
        }

        gaitBones = new NativeArray<TransformStreamHandle>(handles.ToArray(), Allocator.Persistent);
        gaitUpper = new NativeArray<byte>(upper.ToArray(), Allocator.Persistent);
        gaitRotations = new NativeArray<Quaternion>(rotations.ToArray(), Allocator.Persistent);
        gaitHips = new NativeArray<Vector3>(hips.ToArray(), Allocator.Persistent);
        gaitControl = new NativeArray<float>(21, Allocator.Persistent);
        torsoBones = new NativeArray<TransformStreamHandle>(TorsoHandles(animator), Allocator.Persistent);
        torsoPitch = 0f;
        torsoPitchSpeed = 0f;
        gaitHipsState = new NativeArray<Vector3>(used.Count + 1, Allocator.Persistent);
        cycles.AddRange(used);
        if (!ready) return;

        float legLength = GaitCycle.LegLength(animator);
        strideMetres = gaitCycle.StrideLegLengths * legLength;
        hipsScale = gaitCycle.BakedLegLength > 0f ? legLength / gaitCycle.BakedLegLength : 1f;
        gaitControl[8] = gaitCycle.HipsPositions.Length;

        for (int c = 0; c < used.Count; c++)
        {
            float stride = used[c].StrideLegLengths * legLength;
            float length = used[c].Source != null ? used[c].Source.length : 0f;
            cycleStrides.Add(stride);
            cycleNaturalSpeeds.Add(length > 0f ? stride / length : 0f);
            cycleSpeeds.Add(speeds[c]);
            gaitHipsState[c + 1] = Average(used[c].HipsPositions);
        }
    }

    private static void AddCycle(List<GaitCycle> used, List<float> speeds, GaitCycle cycle, float speed)
    {
        used.Add(cycle);
        speeds.Add(speed);
    }

    // Extra cycles blend sample for sample with the walk, so they need the same samples and bones.
    private bool Matches(GaitCycle cycle, Animator animator)
    {
        if (cycle == null) return false;

        bool matches = cycle.HipsPositions.Length == gaitCycle.HipsPositions.Length
                       && cycle.Bones.Length == gaitCycle.Bones.Length;
        if (!matches)
            Debug.LogError($"[ProceduralPoseModule] {cycle.name} must be baked on the same model with the same samples as {gaitCycle.name}; it is left out.", animator);

        return matches;
    }

    private static Vector3 Average(Vector3[] values)
    {
        Vector3 sum = Vector3.zero;
        foreach (var v in values) sum += v;
        return values.Length > 0 ? sum / values.Length : Vector3.zero;
    }

    // The cycle's rotations (samples × baked bones) cut down to the bones this model has, in the same order.
    private static Quaternion[] KeptRotations(Quaternion[] baked, int bakedCount, List<int> kept)
    {
        var result = new Quaternion[baked.Length / bakedCount * kept.Count];

        for (int i = 0; i < result.Length; i++)
            result[i] = baked[i / kept.Count * bakedCount + kept[i % kept.Count]];

        return result;
    }

    // Hips and legs are lower body; the spine, arms, neck, head and fingers are upper body.
    private static bool IsUpper(HumanBodyBones bone)
    {
        return bone != HumanBodyBones.Hips && Array.IndexOf(LegBones, bone) < 0;
    }

    // ── Posture sources ───────────────────────────────────────────────────────

    private void Subscribe()
    {
        if (subscribed || brain == null) return;
        subscribed = true;

        if (statuses != null)
        {
            statuses.OnStatusApplied += HandleStatusApplied;
            statuses.OnStatusRemoved += HandleStatusRemoved;
        }

        if (equipment != null)
            equipment.OnEquipmentChanged += HandleEquipmentChanged;

        if (damage != null)
            damage.OnDamageApplied += HandleDamageApplied;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        subscribed = false;

        if (statuses != null)
        {
            statuses.OnStatusApplied -= HandleStatusApplied;
            statuses.OnStatusRemoved -= HandleStatusRemoved;
        }

        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;

        if (damage != null)
            damage.OnDamageApplied -= HandleDamageApplied;
    }

    private void HandleStatusApplied(StatusInstance status)
    {
        if (bindings != null) posture.Set(status, bindings.ForStatus(status.Definition.id));
    }

    private void HandleStatusRemoved(StatusInstance status)
    {
        posture.Clear(status);
    }

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        if (bindings == null) return;
        posture.Set(slot, item != null ? bindings.ForItem(item.definitionId) : null);
    }

    // ── Graph ─────────────────────────────────────────────────────────────────

    private void Rebuild(Animator animator)
    {
        DestroyGraph();
        boundAnimator = animator;

        if (animator == null) return;

        if (!animator.isHuman)
        {
            Debug.LogError($"[ProceduralPoseModule] Animator '{animator.name}' is not humanoid; the procedural layers need a humanoid Avatar.", animator);
            return;
        }

        var model = animator.GetComponentInParent<ModelPosture>(true);
        posture.Set(this, model != null ? model.Posture : null);

        postureMuscles = new NativeArray<MuscleHandle>(PostureProfile.Muscles(), Allocator.Persistent);
        postureOffsets = new NativeArray<float>(postureMuscles.Length, Allocator.Persistent);
        BuildGait(animator);
        snapBones = new NativeArray<TransformStreamHandle>(BoneHandles(animator), Allocator.Persistent);
        snapRotation = new NativeArray<Quaternion>(snapBones.Length, Allocator.Persistent);
        snapHipsPosition = new NativeArray<Vector3>(1, Allocator.Persistent);
        snapState = new NativeArray<float>(2, Allocator.Persistent);
        snapControl = new NativeArray<float>(4, Allocator.Persistent);
        BuildPlanting(animator);
        BuildLook(animator);

        graph = PlayableGraph.Create($"{animator.name}.ProceduralPose");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var postureJob = new PostureLayerJob { muscles = postureMuscles, offsets = postureOffsets };
        var gaitJob = new GaitLayerJob
        {
            bones = gaitBones, upper = gaitUpper, torso = torsoBones, rotations = gaitRotations, hipsPositions = gaitHips, control = gaitControl,
            hipsState = gaitHipsState
        };
        var snapJob = new SnapLayerJob
        {
            bones = snapBones, control = snapControl, heldRotation = snapRotation,
            heldHipsPosition = snapHipsPosition, state = snapState
        };

        var plantJob = new FootPlantJob { legs = plantLegs, control = plantControl, state = plantState };

        var posturePlayable = AnimationScriptPlayable.Create(graph, postureJob);
        var gaitPlayable = AnimationScriptPlayable.Create(graph, gaitJob, 1);
        var lookJob = new LookLayerJob { bones = lookBones, control = lookControl };
        var lookPlayable = AnimationScriptPlayable.Create(graph, lookJob, 1);
        var snapPlayable = AnimationScriptPlayable.Create(graph, snapJob, 1);
        var plantPlayable = AnimationScriptPlayable.Create(graph, plantJob, 1);
        graph.Connect(posturePlayable, 0, gaitPlayable, 0);
        graph.Connect(gaitPlayable, 0, lookPlayable, 0);
        graph.Connect(lookPlayable, 0, snapPlayable, 0);
        lookPlayable.SetInputWeight(0, 1f);
        graph.Connect(snapPlayable, 0, plantPlayable, 0);
        gaitPlayable.SetInputWeight(0, 1f);
        snapPlayable.SetInputWeight(0, 1f);
        plantPlayable.SetInputWeight(0, 1f);

        var output = AnimationPlayableOutput.Create(graph, "ProceduralPose", animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSortingOrder(outputOrder);
        output.SetSourcePlayable(plantPlayable);

        graph.Play();
    }

    // Foot planting binds the hips and both legs; a model missing any of them gets no planting.
    private void BuildPlanting(Animator animator)
    {
        HumanBodyBones[] order =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
        };

        var handles = new List<TransformStreamHandle>();
        foreach (var bone in order)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null) break;
            handles.Add(animator.BindStreamTransform(t));
        }

        if (handles.Count < order.Length) handles.Clear();
        plantLegs = new NativeArray<TransformStreamHandle>(handles.ToArray(), Allocator.Persistent);
        plantControl = new NativeArray<float>(FootPlantJob.Size, Allocator.Persistent);
        plantState = new NativeArray<float>(FootPlantJob.StateSize, Allocator.Persistent);
        planter.Bind(animator);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        hipsParent = hips != null ? hips.parent : null;
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        wasStill = true;
        lastForward = Vector3.zero;
        lastVelocity = Vector3.zero;
        lean = 0f;
        bank = 0f;
    }

    // Snap: the hips first (SnapLayerJob holds its position too), then every other mapped humanoid bone.
    private TransformStreamHandle[] BoneHandles(Animator animator)
    {
        var handles = new List<TransformStreamHandle> { animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.Hips)) };

        for (var bone = HumanBodyBones.LeftUpperLeg; bone < HumanBodyBones.LastBone; bone++)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null || (!snapLegs && Array.IndexOf(LegBones, bone) >= 0)) continue;
            handles.Add(animator.BindStreamTransform(t));
        }

        return handles.ToArray();
    }

    private void DestroyGraph()
    {
        if (graph.IsValid()) graph.Destroy();
        if (postureMuscles.IsCreated) postureMuscles.Dispose();
        if (postureOffsets.IsCreated) postureOffsets.Dispose();
        if (gaitBones.IsCreated) gaitBones.Dispose();
        if (gaitUpper.IsCreated) gaitUpper.Dispose();
        if (torsoBones.IsCreated) torsoBones.Dispose();
        if (gaitRotations.IsCreated) gaitRotations.Dispose();
        if (gaitHips.IsCreated) gaitHips.Dispose();
        if (gaitControl.IsCreated) gaitControl.Dispose();
        if (gaitHipsState.IsCreated) gaitHipsState.Dispose();
        if (snapBones.IsCreated) snapBones.Dispose();
        if (plantLegs.IsCreated) plantLegs.Dispose();
        if (plantControl.IsCreated) plantControl.Dispose();
        if (plantState.IsCreated) plantState.Dispose();
        if (snapRotation.IsCreated) snapRotation.Dispose();
        if (snapHipsPosition.IsCreated) snapHipsPosition.Dispose();
        if (snapState.IsCreated) snapState.Dispose();
        if (snapControl.IsCreated) snapControl.Dispose();
        if (lookBones.IsCreated) lookBones.Dispose();
        if (lookControl.IsCreated) lookControl.Dispose();
    }
}
