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
// Graph: posture → gait → snap → Animator. Each layer has its own weight under the master.
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

    [Tooltip("At or below this speed (m/s) the gait is off and idle shows.")]
    [Min(0f)] [SerializeField] private float gaitStartSpeed = 0.2f;

    [Tooltip("At or above this speed (m/s) the gait is fully on.")]
    [Min(0.01f)] [SerializeField] private float gaitFullSpeed = 0.8f;

    [Header("Snap")]
    [Range(0f, 1f)] [SerializeField] private float snapWeight = 1f;

    [Tooltip("Held poses per second. 12 = on twos at 24 (decided 2026-10-26).")]
    [Min(1f)] [SerializeField] private float snapFramesPerSecond = 12f;

    [Tooltip("Hold the legs too. Off keeps the legs smooth if planted feet slide during a held pose.")]
    [SerializeField] private bool snapLegs = true;

    [Tooltip("Graph output order on the Animator. Below Animation Rigging's 1000, so rig constraints see the posed body.")]
    [SerializeField] private ushort outputOrder = 900;

    private static readonly HumanBodyBones[] LegBones =
    {
        HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
        HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes
    };

    private readonly PostureLayer posture = new PostureLayer();
    private ControllerBrain brain;
    private StatusSystem statuses;
    private EquipmentSystem equipment;
    private MovementSystem movement;
    private Animator boundAnimator;
    private PlayableGraph graph;
    private NativeArray<MuscleHandle> postureMuscles;
    private NativeArray<float> postureOffsets;
    private NativeArray<TransformStreamHandle> gaitBones;
    private NativeArray<byte> gaitUpper;
    private NativeArray<Quaternion> gaitRotations;
    private NativeArray<Vector3> gaitHips;
    private NativeArray<float> gaitControl;
    private NativeArray<TransformStreamHandle> snapBones;
    private NativeArray<Quaternion> snapRotation;
    private NativeArray<Vector3> snapHipsPosition;
    private NativeArray<float> snapState;
    private NativeArray<float> snapControl;
    private int actionsUpperLayer = -1;
    private float strideMetres;
    private float hipsScale = 1f;
    private float gaitPhase;
    private bool wasFrozen;
    private bool subscribed;

    public bool IsEnabled { get; set; } = true;

    public PostureLayer Posture => posture;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        statuses = brain.GetModule<StatusSystem>();
        equipment = brain.GetModule<EquipmentSystem>();
        movement = brain.Movement;
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

        // Key on the frame a hit-stop starts or ends, so the freeze shows the impact pose, not one up to a step old.
        bool frozen = boundAnimator.speed <= 0f;
        snapControl[0] = 1f / snapFramesPerSecond;
        snapControl[1] = masterWeight * snapWeight;
        snapControl[2] = boundAnimator.speed;
        snapControl[3] = frozen != wasFrozen ? 1f : 0f;
        wasFrozen = frozen;
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
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
        float moving = rooted ? 0f : Mathf.InverseLerp(gaitStartSpeed, gaitFullSpeed, speed);
        float weight = strideMetres > 0f ? masterWeight * gaitWeight * moving : 0f;
        float action = actionsUpperLayer >= 0 ? boundAnimator.GetLayerWeight(actionsUpperLayer) : 0f;

        if (strideMetres > 0f)
            gaitPhase = Mathf.Repeat(gaitPhase + speed * Time.deltaTime * boundAnimator.speed / strideMetres, 1f);

        gaitControl[0] = gaitPhase;
        gaitControl[1] = weight;
        gaitControl[2] = weight * (1f - action);
        gaitControl[3] = hipsScale;
    }

    // Binds the cycle's bones on this model. Any missing bone leaves the gait empty (off) rather than misaligned.
    private void BuildGait(Animator animator)
    {
        strideMetres = 0f;
        hipsScale = 1f;
        actionsUpperLayer = animator.GetLayerIndex(AnimationLayerNames.ActionsUpper);

        var handles = new List<TransformStreamHandle>();
        var upper = new List<byte>();
        HumanBodyBones[] order = gaitCycle != null ? gaitCycle.Bones : new HumanBodyBones[0];

        foreach (var bone in order)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t == null)
            {
                Debug.LogError($"[ProceduralPoseModule] '{animator.name}' has no {bone}, which {gaitCycle.name} was baked with; gait is off.", animator);
                handles.Clear();
                upper.Clear();
                break;
            }

            handles.Add(animator.BindStreamTransform(t));
            upper.Add(IsUpper(bone) ? (byte)1 : (byte)0);
        }

        bool ready = handles.Count > 0;
        gaitBones = new NativeArray<TransformStreamHandle>(handles.ToArray(), Allocator.Persistent);
        gaitUpper = new NativeArray<byte>(upper.ToArray(), Allocator.Persistent);
        gaitRotations = new NativeArray<Quaternion>(ready ? gaitCycle.Rotations : new Quaternion[0], Allocator.Persistent);
        gaitHips = new NativeArray<Vector3>(ready ? gaitCycle.HipsPositions : new Vector3[0], Allocator.Persistent);
        gaitControl = new NativeArray<float>(4, Allocator.Persistent);
        if (!ready) return;

        float legLength = GaitCycle.LegLength(animator);
        strideMetres = gaitCycle.StrideLegLengths * legLength;
        hipsScale = gaitCycle.BakedLegLength > 0f ? legLength / gaitCycle.BakedLegLength : 1f;
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

        graph = PlayableGraph.Create($"{animator.name}.ProceduralPose");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var postureJob = new PostureLayerJob { muscles = postureMuscles, offsets = postureOffsets };
        var gaitJob = new GaitLayerJob
        {
            bones = gaitBones, upper = gaitUpper, rotations = gaitRotations, hipsPositions = gaitHips, control = gaitControl
        };
        var snapJob = new SnapLayerJob
        {
            bones = snapBones, control = snapControl, heldRotation = snapRotation,
            heldHipsPosition = snapHipsPosition, state = snapState
        };

        var posturePlayable = AnimationScriptPlayable.Create(graph, postureJob);
        var gaitPlayable = AnimationScriptPlayable.Create(graph, gaitJob, 1);
        var snapPlayable = AnimationScriptPlayable.Create(graph, snapJob, 1);
        graph.Connect(posturePlayable, 0, gaitPlayable, 0);
        graph.Connect(gaitPlayable, 0, snapPlayable, 0);
        gaitPlayable.SetInputWeight(0, 1f);
        snapPlayable.SetInputWeight(0, 1f);

        var output = AnimationPlayableOutput.Create(graph, "ProceduralPose", animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSortingOrder(outputOrder);
        output.SetSourcePlayable(snapPlayable);

        graph.Play();
    }

    // Snap: the hips first (SnapLayerJob holds its position too), then every other mapped humanoid bone.
    private TransformStreamHandle[] BoneHandles(Animator animator)
    {
        var handles = new List<TransformStreamHandle> { animator.BindStreamTransform(animator.GetBoneTransform(HumanBodyBones.Hips)) };

        for (var bone = HumanBodyBones.Spine; bone < HumanBodyBones.LastBone; bone++)
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
        if (gaitRotations.IsCreated) gaitRotations.Dispose();
        if (gaitHips.IsCreated) gaitHips.Dispose();
        if (gaitControl.IsCreated) gaitControl.Dispose();
        if (snapBones.IsCreated) snapBones.Dispose();
        if (snapRotation.IsCreated) snapRotation.Dispose();
        if (snapHipsPosition.IsCreated) snapHipsPosition.Dispose();
        if (snapState.IsCreated) snapState.Dispose();
        if (snapControl.IsCreated) snapControl.Dispose();
    }
}
