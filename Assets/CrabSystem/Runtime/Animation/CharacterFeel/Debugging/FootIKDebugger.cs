// Editor-only. Three of these scan the scene with FindObjectsOfType every frame, and
// none of them belong in a player build.
#if UNITY_EDITOR
using UnityEngine;

[RequireComponent(typeof(FootIKSystem))]
public class FootIKDebugger : MonoBehaviour
{
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float targetGizmoRadius = 0.06f;

    private FootIKSystem system;

    void Awake()
    {
        system = GetComponent<FootIKSystem>();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying) return;
        if (system == null) system = GetComponent<FootIKSystem>();
        if (system == null || system.BoundAnimator == null) return;

        DrawFoot(system.BoundAnimator, HumanBodyBones.LeftFoot, system.LeftTarget, system.LeftGrounded, system.LeftWeight);
        DrawFoot(system.BoundAnimator, HumanBodyBones.RightFoot, system.RightTarget, system.RightGrounded, system.RightWeight);
    }

    private void DrawFoot(Animator animator, HumanBodyBones bone, Vector3 target, bool grounded, float weight)
    {
        Transform foot = animator.GetBoneTransform(bone);
        if (foot == null) return;

        Gizmos.color = grounded ? Color.Lerp(Color.yellow, Color.green, weight) : Color.red;
        Gizmos.DrawWireSphere(target, targetGizmoRadius);

        if (!grounded) return;

        // Yellow means the probe found ground but the foot is mid-swing above it — the falloff
        // is holding the weight down on purpose, not failing to plant.
        Gizmos.color = Color.Lerp(Color.grey, Color.green, weight * system.CurrentWeight);
        Gizmos.DrawLine(foot.position, target);
    }

    [ContextMenu("Log Foot IK State")]
    void LogState()
    {
        if (system == null) system = GetComponent<FootIKSystem>();
        if (system == null) return;

        Animator animator = system.BoundAnimator;

        if (animator == null)
        {
            Debug.LogError("[FootIKDebugger] No animator bound — the Brain has no EntityAnimator.");
            return;
        }

        Debug.Log($"[FootIKDebugger] '{animator.name}'\n" +
                  $"  IK callback:  {(system.IKCallbackSeen ? "firing" : "NEVER FIRED — IK Pass off?")}\n" +
                  $"  weight:       {system.CurrentWeight:0.00}\n" +
                  $"  body offset:  {system.BodyOffset:0.000} m\n" +
                  $"  left foot:    w {system.LeftWeight:0.00}  {(system.LeftLocked ? "LOCKED  " : "        ")}{(system.LeftGrounded ? system.LeftTarget.ToString() : "no ground")}\n" +
                  $"  right foot:   w {system.RightWeight:0.00}  {(system.RightLocked ? "LOCKED  " : "        ")}{(system.RightGrounded ? system.RightTarget.ToString() : "no ground")}");
    }

    // Sole-to-pivot distance, measured rather than guessed. Run this in the scene with the
    // character standing on flat ground and copy the result into footHeight.
    [ContextMenu("Measure Foot Height")]
    void MeasureFootHeight()
    {
        if (system == null) system = GetComponent<FootIKSystem>();
        Animator animator = system != null ? system.BoundAnimator : GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("[FootIKDebugger] No animator to measure.");
            return;
        }

        Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);

        if (foot == null)
        {
            Debug.LogError("[FootIKDebugger] No left foot bone on the avatar.");
            return;
        }

        if (!Physics.Raycast(foot.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            Debug.LogError("[FootIKDebugger] Nothing under the left foot to measure against.");
            return;
        }

        Debug.Log($"[FootIKDebugger] Left foot pivot sits {foot.position.y - hit.point.y:0.000} m above '{hit.collider.name}'. Use that as footHeight.");
    }
}

#endif
