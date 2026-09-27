// WorldSpaceWindowAdapter.cs
// Adapter that makes a UniversalInventoryWindow work in World Space
using UnityEngine;

/// <summary>
/// Attach this to a world-space canvas to make it work with UniversalInventoryWindow
/// The window will appear at the container's position and face the camera
/// </summary>
public class WorldSpaceWindowAdapter : MonoBehaviour
{
    [Header("World Space Settings")]
    [Tooltip("The container this window is attached to")]
    [SerializeField] private Transform containerTransform;

    [Tooltip("Offset from container position")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0, 2f, 0);

    [Tooltip("Should the window always face the camera?")]
    [SerializeField] private bool faceCamera = true;

    [Tooltip("Distance from container to auto-close (0 = use window's setting)")]
    [SerializeField] private float maxDistance = 5f;

    private Canvas canvas;
    private UniversalInventoryWindow window;
    private Camera mainCamera;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        window = GetComponentInChildren<UniversalInventoryWindow>();
        mainCamera = Camera.main;

        // Ensure canvas is set to World Space
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
        }
    }

    private void Update()
    {
        if (containerTransform == null) return;

        // Position at container
        transform.position = containerTransform.position + positionOffset;

        // Face camera
        if (faceCamera && mainCamera != null)
        {
            transform.LookAt(transform.position + mainCamera.transform.rotation * Vector3.forward,
                            mainCamera.transform.rotation * Vector3.up);
        }
    }

    /// <summary>
    /// Set the container this window is attached to
    /// </summary>
    public void SetContainer(Transform container)
    {
        containerTransform = container;
    }

    /// <summary>
    /// Get the container transform
    /// </summary>
    public Transform Container => containerTransform;
}