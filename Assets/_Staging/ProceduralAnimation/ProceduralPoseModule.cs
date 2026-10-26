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
// Layers so far: posture. Snap (12 fps hold) and gait come next, each with its own weight under the master.
// The graph follows brain.EntityAnimator, so a model swap rebuilds it.

public class ProceduralPoseModule : MonoBehaviour, IBrainModule
{
    [Tooltip("Scales every procedural layer. 0 = the Animator's pose, untouched.")]
    [Range(0f, 1f)] [SerializeField] private float masterWeight = 1f;

    [Header("Posture")]
    [Range(0f, 1f)] [SerializeField] private float postureWeight = 1f;
    [SerializeField] private PostureProfile posture;

    [Tooltip("Graph output order on the Animator. Below Animation Rigging's 1000, so rig constraints see the posed body.")]
    [SerializeField] private ushort outputOrder = 900;

    private ControllerBrain brain;
    private Animator boundAnimator;
    private PlayableGraph graph;
    private NativeArray<MuscleHandle> muscles;
    private NativeArray<float> offsets;
    private NativeArray<int> humanStream;
    private bool warnedNotHuman;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void UpdateModule()
    {
        if (brain.EntityAnimator != boundAnimator)
            Rebuild(brain.EntityAnimator);

        if (!graph.IsValid()) return;

        WritePosture();
        WarnIfNotHuman();
    }

    private void OnDisable()
    {
        DestroyGraph();
        boundAnimator = null;
    }

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

        muscles = new NativeArray<MuscleHandle>(PostureProfile.Muscles(), Allocator.Persistent);
        offsets = new NativeArray<float>(muscles.Length, Allocator.Persistent);
        humanStream = new NativeArray<int>(1, Allocator.Persistent);
        warnedNotHuman = false;

        graph = PlayableGraph.Create($"{animator.name}.ProceduralPose");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        var job = new PostureLayerJob { muscles = muscles, offsets = offsets, humanStream = humanStream };
        var posturePlayable = AnimationScriptPlayable.Create(graph, job);

        var output = AnimationPlayableOutput.Create(graph, "ProceduralPose", animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSortingOrder(outputOrder);
        output.SetSourcePlayable(posturePlayable);

        graph.Play();
    }

    private void WritePosture()
    {
        float weight = posture != null ? masterWeight * postureWeight : 0f;
        if (posture != null)
        {
            posture.Write(offsets, weight);
            return;
        }

        for (int i = 0; i < offsets.Length; i++)
            offsets[i] = 0f;
    }

    private void WarnIfNotHuman()
    {
        if (warnedNotHuman || humanStream[0] != -1) return;

        warnedNotHuman = true;
        Debug.LogError($"[ProceduralPoseModule] The layered stream on '{boundAnimator.name}' is not humanoid, so muscles can't be edited from it. The layering test failed: see Procedural_Animation_Theory.md.", boundAnimator);
    }

    private void DestroyGraph()
    {
        if (graph.IsValid()) graph.Destroy();
        if (muscles.IsCreated) muscles.Dispose();
        if (offsets.IsCreated) offsets.Dispose();
        if (humanStream.IsCreated) humanStream.Dispose();
    }
}
