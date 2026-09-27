using UnityEngine;

/// <summary>
/// Bridges GameEvents to a CameraModule. Other systems (dialogue, cutscenes, interactions, etc.)
/// call GameEvents.CameraModeChangeRequested("Dialogue") without needing a direct reference to the
/// camera; this listener forwards the request to the CameraModule on the same brain.
/// </summary>
public class CameraEventListener : MonoBehaviour
{
    [SerializeField] private CameraModule cameraModule;

    private void Awake()
    {
        if (cameraModule == null)
            cameraModule = GetComponentInParent<CameraModule>();

        if (cameraModule == null)
            Debug.LogError("[CameraEventListener] No CameraModule assigned or found in parents.", this);
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
