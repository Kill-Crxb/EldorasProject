using MoreMountains.Feedbacks;
using UnityEngine;

// Adapter from FootstepEmitter.onStep to an MMF_Player. A persistent UnityEvent binding needs an
// exact signature match, so onStep's (point, normal) cannot target PlayFeedbacks directly.
// Wire onStep → StepJuice.OnStep in the inspector.
public class StepJuice : MonoBehaviour
{
    [SerializeField] private MMF_Player step;

    public void OnStep(Vector3 point, Vector3 normal)
    {
        if (step == null) return;
        step.PlayFeedbacks(point);
    }
}
