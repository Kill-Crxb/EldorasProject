// Editor-only. Three of these scan the scene with FindObjectsOfType every frame, and
// none of them belong in a player build.
#if UNITY_EDITOR
using UnityEngine;

[RequireComponent(typeof(AnimationLayerController))]
public class AnimationLayerDebugger : MonoBehaviour
{
    [Tooltip("Logs a line whenever the number of live claims changes. Noisy in combat; off by default.")]
    [SerializeField] private bool logClaimChanges;

    private AnimationLayerController controller;
    private int lastClaimCount = -1;

    void Awake()
    {
        controller = GetComponent<AnimationLayerController>();
    }

    void Update()
    {
        if (!logClaimChanges || controller == null) return;
        if (controller.ClaimCount == lastClaimCount) return;

        lastClaimCount = controller.ClaimCount;
        controller.LogLayerBindings();
    }

    [ContextMenu("Log Layer Bindings")]
    void Log()
    {
        if (controller == null) controller = GetComponent<AnimationLayerController>();
        if (controller != null) controller.LogLayerBindings();
    }
}

#endif
