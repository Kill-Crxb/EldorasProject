using UnityEngine;
using UnityEngine.InputSystem;

public class OverShoulderCamera : MonoBehaviour, IBrainModule, ICameraProvider
{
    #region Inspector

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Rig")]
    [SerializeField] private float cameraHeight = 1.7f;
    [SerializeField] private Vector3 shoulderOffset = new Vector3(0.6f, 0f, 0f);
    [SerializeField] private Vector3 boomOffset = new Vector3(0f, 0f, -4f);

    [Header("Collision")]
    [SerializeField] private LayerMask obstacleLayer = 1;
    [SerializeField] private float collisionRadius = 0.3f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float collisionSmoothSpeed = 12f;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private Transform player;
    private bool isInitialized;

    private float yaw;
    private float pitch;
    private float currentDistance;
    private RaycastHit hitCache;

    #endregion

    #region Properties

    public bool IsEnabled { get; set; } = true;

    #endregion

    #region ICameraProvider

    public Transform CameraTransform => transform;
    public float GetCameraHorizontalRotation() => yaw;
    public bool CameraDrivesFacing => true;

    #endregion

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (brain.IsNPC)
        {
            IsEnabled = false;
            enabled = false;
            return;
        }

        player = brain.EntityRoot;
        if (player == null)
        {
            enabled = false;
            return;
        }

        yaw = player.eulerAngles.y;
        currentDistance = boomOffset.magnitude;

        SetCursorLocked(true);
        isInitialized = true;
    }

    public void UpdateModule()
    {
        if (!isInitialized || !IsEnabled) return;
        IntegrateLook();
    }

    public void LateInitialize() { }

    #endregion

    #region Look

    private void IntegrateLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        var controls = brain.GetInputControls();
        if (controls?.Player == null) return;

        Vector2 look = controls.Player.Look.ReadValue<Vector2>() * mouseSensitivity * Time.deltaTime;
        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
    }

    #endregion

    #region Rig Placement

    void LateUpdate()
    {
        if (!isInitialized || !IsEnabled) return;

        Vector3 pivot = player.position + Vector3.up * cameraHeight;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        Vector3 anchor = pivot + yawRot * shoulderOffset;
        Vector3 boom = Quaternion.Euler(pitch, 0f, 0f) * boomOffset;
        Vector3 desired = anchor + yawRot * boom;

        transform.position = ResolveCollision(anchor, desired);
        transform.rotation = Quaternion.LookRotation(pivot - transform.position);
    }

    private Vector3 ResolveCollision(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;
        if (dist < 0.0001f) return to;
        dir /= dist;

        float target = Physics.SphereCast(from, collisionRadius, dir, out hitCache, dist, obstacleLayer)
            ? Mathf.Max(hitCache.distance - collisionRadius, minDistance)
            : dist;

        currentDistance = Mathf.Lerp(currentDistance, target, collisionSmoothSpeed * Time.deltaTime);
        return from + dir * currentDistance;
    }

    #endregion

    #region Cursor

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    #endregion
}