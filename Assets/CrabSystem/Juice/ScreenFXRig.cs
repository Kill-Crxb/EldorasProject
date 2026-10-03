using UnityEngine;
using UnityEngine.Rendering;

// This character's screen-space effects. Juice.md §7.
//
//   Speed volume — weight follows MovementSystem.SpeedBlend: a vignette that closes in as you
//                  go from run to sprint. Continuous, so it is driven here, not by Feel.
//   Pulse volume — carries Feel's chromatic-aberration and lens-distortion shakers. Only View_
//                  players pulse it, and its overrides rest at zero.
//   Speed lines  — the ScreenSpeedLines quad. SpeedLineDriver sets its intensity; this only keeps
//                  it filling the view, because the shader reads the quad's UVs as screen UVs
//                  and the sprint FOV change would otherwise shrink it off the edges.
//
// Both volumes are global and live inside the prefab, so every Base_PC in a PvP scene carries a
// pair. Anything that is not the local player switches them off — otherwise a remote player's
// sprint would vignette your screen.
public class ScreenFXRig : MonoBehaviour
{
    [SerializeField] private Volume speedVolume;
    [SerializeField] private Volume pulseVolume;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform speedLines;

    [Tooltip("Off until the speed-line effect is authored by hand. The quad stays in the prefab; " +
             "this only keeps it hidden.")]
    [SerializeField] private bool speedLinesEnabled = false;

    [Range(0f, 1f)]
    [SerializeField] private float maxSpeedWeight = 1f;

    [Tooltip("Metres in front of the camera the speed-line quad sits. Must be beyond the near plane.")]
    [SerializeField] private float speedLineDistance = 0.1f;

    private MovementSystem movement;

    private void Start()
    {
        ControllerBrain brain = GetComponentInParent<ControllerBrain>();
        movement = brain != null ? brain.Movement : null;

        // The one "is this mine" check here; JuiceModule.IsLocal is the other. Both become the
        // ownership check with netcode.
        bool local = brain != null && brain.IsPlayer;

        if (speedVolume != null) speedVolume.gameObject.SetActive(local);
        if (pulseVolume != null) pulseVolume.gameObject.SetActive(local);
        if (speedLines != null) speedLines.gameObject.SetActive(local && speedLinesEnabled);
        enabled = local;
    }

    private void LateUpdate()
    {
        if (speedVolume != null) speedVolume.weight = movement != null ? movement.SpeedBlend * maxSpeedWeight : 0f;
        FitSpeedLines();
    }

    private void FitSpeedLines()
    {
        if (!speedLinesEnabled || speedLines == null || targetCamera == null) return;

        float height = 2f * speedLineDistance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        speedLines.localPosition = new Vector3(0f, 0f, speedLineDistance);
        speedLines.localRotation = Quaternion.identity;
        speedLines.localScale = new Vector3(height * targetCamera.aspect, height, 1f);
    }
}
