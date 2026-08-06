using System;
using System.Collections.Generic;
using UnityEngine;

public class CameraModule : MonoBehaviour, IBrainModule, ICameraProvider
{
    [Serializable]
    public class CameraModeSettings
    {
        public string modeName = "Default";
        public float distance = 1.3f;
        public float pivotHeight = 1.5f;
        public float shoulderOffset = 0.4f;
        public float sensitivity = 0.15f;
        public float minPitch = -35f;
        public float maxPitch = 60f;
    }

    [SerializeField] private Transform cameraTransform;

    [SerializeField]
    private List<CameraModeSettings> modes = new List<CameraModeSettings>
    {
        new CameraModeSettings { modeName = "Default" },
        new CameraModeSettings
        {
            modeName = "Dialogue",
            distance = 0.6f,
            pivotHeight = 1.6f,
            shoulderOffset = 0.2f,
            sensitivity = 0.05f,
            minPitch = -20f,
            maxPitch = 30f
        }
    };

    [SerializeField] private string startingMode = "Default";
    [SerializeField] private float modeBlendDuration = 0.35f;

    public bool IsEnabled { get; set; } = true;

    private Transform target;
    private IInputProvider input;
    private IStateProvider stateProvider;
    private float yaw;
    private float pitch;

    // Active, possibly-blended values driving movement this frame.
    private float distance;
    private float pivotHeight;
    private float shoulderOffset;
    private float sensitivity;
    private float minPitch;
    private float maxPitch;

    private CameraModeSettings currentMode;
    private CameraModeSettings blendFrom;
    private CameraModeSettings blendTo;
    private float blendElapsed;
    private bool isBlending;

    public string CurrentModeName => currentMode?.modeName;
    public event Action<string> OnModeChanged;

    public void Initialize(ControllerBrain brain)
    {
        target = brain.EntityRoot;
        input = brain.GetModuleImplementing<IInputProvider>();
        stateProvider = brain.GetModule<IStateProvider>();

        if (cameraTransform == null)
            Debug.LogError("[CameraModule] cameraTransform is not assigned.", this);
        if (input == null)
            Debug.LogError("[CameraModule] No IInputProvider found on the brain.", this);
        if (modes == null || modes.Count == 0)
            Debug.LogError("[CameraModule] No camera modes configured.", this);

        if (target != null) yaw = target.eulerAngles.y;
        pitch = 15f;

        CameraModeSettings initial = FindMode(startingMode)
            ?? (modes != null && modes.Count > 0 ? modes[0] : new CameraModeSettings());
        currentMode = initial;
        ApplyImmediate(initial);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UpdateModule()
    {
        if (!IsEnabled || input == null) return;

        UpdateBlend();

        if (UiWantsCursor() || !CameraInputAllowed())
        {
            SetCursorLocked(false);
            return;
        }

        SetCursorLocked(true);

        Vector2 look = input.LookInput;
        yaw += look.x * sensitivity;
        pitch -= look.y * sensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    /// <summary>
    /// Switches to the named camera mode, blending distance/pivot/offset/sensitivity/pitch-clamp
    /// over modeBlendDuration seconds. Called directly, or forwarded by a CameraEventListener
    /// reacting to GameEvents.OnCameraModeChangeRequested.
    /// </summary>
    public void SetMode(string modeName)
    {
        CameraModeSettings next = FindMode(modeName);
        if (next == null)
        {
            Debug.LogWarning($"[CameraModule] No camera mode named '{modeName}' found.", this);
            return;
        }
        if (next == currentMode && !isBlending) return;

        blendFrom = new CameraModeSettings
        {
            modeName = "(blend)",
            distance = distance,
            pivotHeight = pivotHeight,
            shoulderOffset = shoulderOffset,
            sensitivity = sensitivity,
            minPitch = minPitch,
            maxPitch = maxPitch
        };
        blendTo = next;
        blendElapsed = 0f;
        isBlending = modeBlendDuration > 0f;
        currentMode = next;

        if (!isBlending) ApplyImmediate(next);

        OnModeChanged?.Invoke(next.modeName);
    }

    private void UpdateBlend()
    {
        if (!isBlending) return;

        blendElapsed += Time.deltaTime;
        float t = modeBlendDuration <= 0f ? 1f : Mathf.Clamp01(blendElapsed / modeBlendDuration);
        float eased = Mathf.SmoothStep(0f, 1f, t);

        distance = Mathf.Lerp(blendFrom.distance, blendTo.distance, eased);
        pivotHeight = Mathf.Lerp(blendFrom.pivotHeight, blendTo.pivotHeight, eased);
        shoulderOffset = Mathf.Lerp(blendFrom.shoulderOffset, blendTo.shoulderOffset, eased);
        sensitivity = Mathf.Lerp(blendFrom.sensitivity, blendTo.sensitivity, eased);
        minPitch = Mathf.Lerp(blendFrom.minPitch, blendTo.minPitch, eased);
        maxPitch = Mathf.Lerp(blendFrom.maxPitch, blendTo.maxPitch, eased);

        if (t >= 1f) isBlending = false;
    }

    private void ApplyImmediate(CameraModeSettings s)
    {
        distance = s.distance;
        pivotHeight = s.pivotHeight;
        shoulderOffset = s.shoulderOffset;
        sensitivity = s.sensitivity;
        minPitch = s.minPitch;
        maxPitch = s.maxPitch;
        isBlending = false;
    }

    private CameraModeSettings FindMode(string modeName)
    {
        if (modes == null || string.IsNullOrEmpty(modeName)) return null;
        for (int i = 0; i < modes.Count; i++)
        {
            if (string.Equals(modes[i].modeName, modeName, StringComparison.OrdinalIgnoreCase))
                return modes[i];
        }
        return null;
    }

    // Gates look input on the entity's own BrainState (Dialogue, Inventory, Crafting,
    // Reading, etc.) via the existing IStateProvider.AllowsCameraInput contract — this
    // was already defined for this purpose, just never wired to a camera before.
    private bool CameraInputAllowed() => stateProvider == null || stateProvider.AllowsCameraInput;

    private bool UiWantsCursor()
    {
        if (UIWindowManager.Instance != null && UIWindowManager.Instance.IsAnyWindowOpen) return true;
        if (UniversalWindowManager.Instance != null && UniversalWindowManager.Instance.IsAnyWindowOpen()) return true;
        if (MenuManager.Instance != null && MenuManager.Instance.IsMenuOpen) return true;
        return false;
    }

    private void SetCursorLocked(bool locked)
    {
        if (locked)
        {
            if (Cursor.lockState == CursorLockMode.Locked) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            if (Cursor.lockState == CursorLockMode.None) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void LateUpdate()
    {
        if (!IsEnabled || target == null || cameraTransform == null) return;

        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        pivot += orbit * Vector3.right * shoulderOffset;
        cameraTransform.position = pivot + orbit * new Vector3(0f, 0f, -distance);
        cameraTransform.rotation = orbit;
    }

    public Transform CameraTransform => cameraTransform;
    public float GetCameraHorizontalRotation() => yaw;
    public bool CameraDrivesFacing => true;
}
