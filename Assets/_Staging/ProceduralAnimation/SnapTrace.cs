using CrabSystem;
using UnityEngine;

// SnapTrace (staging debugger for ProceduralPoseModule's snap layer; diagnostics stay out of the module).
//
// Attach to: the same GameObject as ProceduralPoseModule. Enable it, reproduce, disable it, read the log.
//
// Logs a frame only when something is off, so it can stay on through a test:
//   EVAL  the snap job ran other than once this frame (the graph is evaluated more or less than expected).
//   FLICK the Animator's left-arm pose coming into snap jumped and then jumped back the next frame (the input is
//         flickering between poses before snap ever sees it).
//   HOLD  snap's output changed on a frame it should have been holding (it isn't holding).
//   REC   every frame while Record is ticked (tick it in play on the character you're watching, for a few seconds).
// Each line carries the character, the left arm's down-up and front-back muscles in and out, and whether the frame keyed.
// REC lines also carry whether the Actions layer is in Guard.Block, then every layer as name=weight:clip(clip weight),
// to find what moves the arms when the block should own them.

public class SnapTrace : MonoBehaviour
{
    [Tooltip("A muscle change bigger than this in one frame counts as a jump.")]
    [SerializeField] private float jump = 0.08f;

    [Tooltip("Log every frame while ticked. Tick it in play for a few seconds on the character you're watching.")]
    [SerializeField] private bool record;

    private static readonly int GuardBlock = Animator.StringToHash("Actions.Guard.Block");

    private ProceduralPoseModule module;
    private ControllerBrain brain;
    private float lastInDownUp;
    private float lastInFrontBack;
    private float lastOutDownUp;
    private float lastDelta;
    private bool primed;

    private void OnEnable()
    {
        module = GetComponent<ProceduralPoseModule>();
        brain = GetComponentInParent<ControllerBrain>();
        primed = false;
        if (module == null) Debug.LogError("[SnapTrace] Needs a ProceduralPoseModule on the same GameObject.", this);
    }

    private void LateUpdate()
    {
        if (module == null || !module.SnapProbe.IsCreated) return;

        var probe = module.SnapProbe;
        float evals = probe[0];
        bool keyed = probe[1] > 0f;
        float inDownUp = probe[2];
        float outDownUp = probe[3];
        float inFrontBack = probe[4];
        float outFrontBack = probe[5];
        probe[0] = 0f;

        float delta = (inDownUp - lastInDownUp) + (inFrontBack - lastInFrontBack);
        bool flick = primed && Mathf.Abs(delta) > jump && Mathf.Abs(lastDelta) > jump && Mathf.Sign(delta) != Mathf.Sign(lastDelta);
        bool notHolding = primed && !keyed && Mathf.Abs(outDownUp - lastOutDownUp) > jump;

        string line = $"{transform.root.name} f{Time.frameCount} key {(keyed ? 1 : 0)} evals {evals:0}  in {inDownUp:0.00}/{inFrontBack:0.00}  out {outDownUp:0.00}/{outFrontBack:0.00}";
        if (primed && evals != 1f) Debug.Log("[SnapTrace] EVAL " + line, this);
        if (flick) Debug.Log("[SnapTrace] FLICK " + line, this);
        if (notHolding) Debug.Log("[SnapTrace] HOLD " + line, this);
        if (record) Debug.Log("[SnapTrace] REC " + line + LayerState(), this);

        lastDelta = delta;
        lastInDownUp = inDownUp;
        lastInFrontBack = inFrontBack;
        lastOutDownUp = outDownUp;
        primed = true;
    }

    private string LayerState()
    {
        Animator animator = brain != null ? brain.EntityAnimator : null;
        if (animator == null) return "";

        int actions = animator.GetLayerIndex("Actions");
        string line = actions >= 0 && animator.GetCurrentAnimatorStateInfo(actions).fullPathHash == GuardBlock ? "  BLOCK" : "  other";

        for (int i = 0; i < animator.layerCount; i++)
            line += $" | {animator.GetLayerName(i)}={animator.GetLayerWeight(i):0.00}:{Clips(animator, i)}";

        return line;
    }

    private static string Clips(Animator animator, int layer)
    {
        string clips = "";
        foreach (var info in animator.GetCurrentAnimatorClipInfo(layer))
            clips += $"{info.clip.name}({info.weight:0.00}) ";

        return clips;
    }
}
