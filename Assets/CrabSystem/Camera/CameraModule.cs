using UnityEngine;

public class CameraModule : MonoBehaviour, IBrainModule, ICameraProvider
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float distance = 1.3f;
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private float shoulderOffset = 0.4f;
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 60f;

    public bool IsEnabled { get; set; } = true;

    private Transform target;
    private IInputProvider input;
    private float yaw;
    private float pitch;

    public void Initialize(ControllerBrain brain)
    {
        target = brain.EntityRoot;
        input = brain.GetModuleImplementing<IInputProvider>();

        if (cameraTransform == null)
            Debug.LogError("[CameraModule] cameraTransform is not assigned.", this);
        if (input == null)
            Debug.LogError("[CameraModule] No IInputProvider found on the brain.", this);

        if (target != null) yaw = target.eulerAngles.y;
        pitch = 15f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UpdateModule()
    {
        if (!IsEnabled || input == null) return;

        if (UiWantsCursor())
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