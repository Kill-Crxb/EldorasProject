// Editor-only. Three of these scan the scene with FindObjectsOfType every frame, and
// none of them belong in a player build.
#if UNITY_EDITOR
using UnityEngine;

[RequireComponent(typeof(HeadLookSystem))]
public class HeadLookDebugger : MonoBehaviour
{
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private float targetGizmoRadius = 0.2f;
    [SerializeField] private bool logEveryStateChange;

    private HeadLookSystem system;
    private float lastLoggedWeight = -1f;

    void Awake()
    {
        system = GetComponent<HeadLookSystem>();
    }

    void Update()
    {
        if (!logEveryStateChange || system == null) return;

        float weight = Mathf.Round(system.CurrentWeight * 100f) / 100f;
        if (Mathf.Approximately(weight, lastLoggedWeight)) return;

        lastLoggedWeight = weight;
        Debug.Log($"[HeadLookDebugger] weight {weight:0.00}  dir {system.CurrentDirection}");
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || !Application.isPlaying) return;
        if (system == null) system = GetComponent<HeadLookSystem>();
        if (system == null || system.BoundAnimator == null) return;

        Transform head = system.BoundAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Gizmos.color = Color.Lerp(Color.grey, Color.cyan, system.CurrentWeight);
        Gizmos.DrawLine(head.position, system.CurrentTarget);
        Gizmos.DrawWireSphere(system.CurrentTarget, targetGizmoRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(head.position, head.forward * 1f);
    }

    [ContextMenu("Log Head Look State")]
    void LogState()
    {
        if (system == null) system = GetComponent<HeadLookSystem>();
        if (system == null) return;

        Animator animator = system.BoundAnimator;

        if (animator == null)
        {
            Debug.LogError("[HeadLookDebugger] No animator bound — the Brain has no EntityAnimator.");
            return;
        }

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

        Debug.Log($"[HeadLookDebugger] '{animator.name}'\n" +
                  $"  humanoid:      {animator.isHuman}\n" +
                  $"  head bone:     {(head != null ? head.name : "MISSING")}\n" +
                  $"  IK callback:   {(system.IKCallbackSeen ? "firing" : "NEVER FIRED — IK Pass off?")}\n" +
                  $"  weight:        {system.CurrentWeight:0.00}\n" +
                  $"  direction:     {system.CurrentDirection}\n" +
                  $"  target:        {system.CurrentTarget}\n" +
                  $"  ext suppress:  {system.ExternalSuppress}");
    }
}

#endif
