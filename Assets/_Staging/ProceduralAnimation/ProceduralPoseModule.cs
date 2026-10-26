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
// Graph: posture → snap → Animator. Each layer has its own weight under the master; gait comes later.
//
// Posture sources: the worn model's ModelPosture (base stance), statuses and equipped items listed in the
// PostureBindings table. They stack and fade (PostureLayer). The graph follows brain.EntityAnimator, so a model
// swap rebuilds it and picks up the new model's base posture.

public class ProceduralPoseModule : MonoBehaviour, IBrainModule
{
    [Tooltip("Scales every procedural layer. 0 = the Animator's pose, untouched.")]
    [Range(0f, 1f)] [SerializeField] private float masterWeight = 1f;

    [Header("Posture")]
    [Range(0f, 1f)] [SerializeField] private float postureWeight = 1f;

    [Tooltip("Statuses and items that bring a posture with them. Optional.")]
    [SerializeField] private PostureBindings bindings;

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
    private Animator boundAnimator;
    private PlayableGraph graph;
    private NativeArray<MuscleHandle> postureMuscles;
    private NativeArray<float> postureOffsets;
    private NativeArray<TransformStreamHandle> snapBones;
    private NativeArray<Quaternion> snapRotation;
    private NativeArray<Vector3> snapHipsPosition;
    private NativeArray<float> snapState;
    private NativeArray<float> snapControl;
    private bool wasFrozen;
    private bool subscribed;

    public bool IsEnabled { get; set; } = true;

    public PostureLayer Posture => posture;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        statuses = brain.GetModule<StatusSystem>();
        equipment = brain.GetModule<EquipmentSystem>();
        Subscribe();
    }

    public void UpdateModule()
    {
        if (brain.EntityAnimator != boundAnimator)
            Rebuild(brain.EntityAnimator);

        if (!graph.IsValid()) return;

        // Presentation fade, like the camera: plain frame time, not the entity clock.
        posture.Tick(Time.deltaTime);
        posture.Write(postureOffsets, masterWeight * postureWeight);

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
        snapBones = new NativeArray<TransformStreamHandle>(BoneHandles(animator), Allocator.Persistent);
        snapRotation = new NativeArray<Quaternion>(snapBones.Length, Allocator.Persistent);
        snapHipsPosition = new NativeArray<Vector3>(1, Allocator.Persistent);
        snapState = new NativeArray<float>(2, Allocator.Persistent);
        snapControl = new NativeArray<float>(4, Allocator.Persistent);

        graph = PlayableGraph.Create($"{animator.name}.ProceduralPose");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var postureJob = new PostureLayerJob { muscles = postureMuscles, offsets = postureOffsets };
        var snapJob = new SnapLayerJob
        {
            bones = snapBones, control = snapControl, heldRotation = snapRotation,
            heldHipsPosition = snapHipsPosition, state = snapState
        };
        var posturePlayable = AnimationScriptPlayable.Create(graph, postureJob);
        var snapPlayable = AnimationScriptPlayable.Create(graph, snapJob, 1);
        graph.Connect(posturePlayable, 0, snapPlayable, 0);
        snapPlayable.SetInputWeight(0, 1f);

        var output = AnimationPlayableOutput.Create(graph, "ProceduralPose", animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSortingOrder(outputOrder);
        output.SetSourcePlayable(snapPlayable);

        graph.Play();
    }

    // The hips first (SnapLayerJob holds its position too), then every other mapped humanoid bone.
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
        if (snapBones.IsCreated) snapBones.Dispose();
        if (snapRotation.IsCreated) snapRotation.Dispose();
        if (snapHipsPosition.IsCreated) snapHipsPosition.Dispose();
        if (snapState.IsCreated) snapState.Dispose();
        if (snapControl.IsCreated) snapControl.Dispose();
    }
}
