using MagicaCloth2;
using UnityEngine;

// Runs Magica Cloth simulations in slow motion without touching their physics.
//
// TeamManager advances each cloth by `deltaTime * timeScale`, so this feeds the solver less time
// per frame. The motion keeps its shape and its amplitude and simply takes longer — which is what
// damping cannot do, since damping removes energy and shrinks the movement as it slows it.
//
// There is no serialized field for this in MC2; SetTimeScale is runtime-only, hence this component.
public class ClothTimeScale : MonoBehaviour
{
    [Tooltip("Leave empty to drive every MagicaCloth under this object.")]
    [SerializeField] private MagicaCloth[] targets;

    [Tooltip("1 is real time. Around 0.75 reads as weight; below about 0.4 it starts reading as " +
             "underwater, because the jiggle also lags the body motion that drives it.")]
    [SerializeField, Range(0.05f, 1f)] private float timeScale = 0.8f;

    private float applied = -1f;

    void Awake()
    {
        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<MagicaCloth>(true);
    }

    void Update()
    {
        // SetTimeScale is a no-op until the cloth has finished building, and the build completes
        // some frames after Start. Re-applying until the value reads back is simpler than racing
        // OnBuildComplete, and it doubles as live tuning from the inspector.
        if (Mathf.Approximately(applied, timeScale) && Confirmed()) return;

        foreach (MagicaCloth cloth in targets)
        {
            if (cloth == null) continue;
            cloth.SetTimeScale(timeScale);
        }

        applied = timeScale;
    }

    private bool Confirmed()
    {
        foreach (MagicaCloth cloth in targets)
        {
            if (cloth == null) continue;
            if (!Mathf.Approximately(cloth.GetTimeScale(), timeScale)) return false;
        }

        return true;
    }
}
