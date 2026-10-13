using UnityEngine;

/// <summary>
/// Bridges GameEvents to a CameraModule. Other systems (dialogue, cutscenes, interactions, etc.)
/// call GameEvents.CameraModeChangeRequested("Dialogue") without needing a direct reference to the
/// camera; this listener forwards the request to the CameraModule on the same brain.
/// </summary>

namespace CrabSystem
{
    public class CameraEventListener : MonoBehaviour
    {
        private CameraModule cameraModule;

        // Start, not Awake: the brain builds its module cache in its own Awake.
        private void Start()
        {
            cameraModule = GetComponentInParent<ControllerBrain>()?.GetModule<CameraModule>();
            if (cameraModule == null) Debug.LogError("[CameraEventListener] no CameraModule on this brain", this);
        }

        private void OnEnable()
        {
            GameEvents.OnCameraModeChangeRequested += HandleModeChangeRequested;
        }

        private void OnDisable()
        {
            GameEvents.OnCameraModeChangeRequested -= HandleModeChangeRequested;
        }

        private void HandleModeChangeRequested(string modeName)
        {
            if (cameraModule == null) return;
            cameraModule.SetMode(modeName);
        }
    }
}
