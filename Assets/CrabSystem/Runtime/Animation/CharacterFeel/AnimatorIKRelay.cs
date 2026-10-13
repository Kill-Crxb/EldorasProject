using System.Collections.Generic;
using UnityEngine;

// Auto-attached by AnimatorIKModule. OnAnimatorIK is only delivered to a component sharing the
// Animator's GameObject, and the Animator sits under the model root rather than under the Brain,
// so no module can receive the callback itself.
[DisallowMultipleComponent]
public class AnimatorIKRelay : MonoBehaviour
{
    private readonly List<IAnimatorIKReceiver> receivers = new List<IAnimatorIKReceiver>();
    private Animator animator;
    private int lastHandledFrame = -1;

    public void Bind(IAnimatorIKReceiver receiver)
    {
        if (receiver == null || receivers.Contains(receiver)) return;

        if (animator == null) animator = GetComponent<Animator>();

        receivers.Add(receiver);
        receivers.Sort(CompareOrder);
    }

    public void Unbind(IAnimatorIKReceiver receiver)
    {
        receivers.Remove(receiver);
    }

    private static int CompareOrder(IAnimatorIKReceiver a, IAnimatorIKReceiver b) => a.IKOrder.CompareTo(b.IKOrder);

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || receivers.Count == 0) return;

        // Fires once per IK-enabled layer. Every receiver here is layer-independent, so only the
        // first call each frame does the work.
        if (lastHandledFrame == Time.frameCount) return;
        lastHandledFrame = Time.frameCount;

        for (int i = 0; i < receivers.Count; i++)
            receivers[i].ApplyIK(animator);
    }
}
