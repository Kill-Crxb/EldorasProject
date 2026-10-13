using UnityEngine;
using UnityEngine.UI;

// A HUD mark over the player's soft target, hidden with none.
// Put it on an Image under the HUD canvas; it finds the player once one exists.
[RequireComponent(typeof(Image))]
public class TargetMarker : MonoBehaviour
{
    [Tooltip("Height above the target's root the mark sits at.")]
    [SerializeField] private float height = 2.1f;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.6f);

    private Image image;
    private RectTransform rect;
    private Canvas canvas;
    private TargetingModule targeting;
    private Camera view;
    private float nextBindAt;
    private const float BindRetrySeconds = 0.5f;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        image.raycastTarget = false;
    }

    private void TryBind()
    {
        if (Time.unscaledTime < nextBindAt) return;
        nextBindAt = Time.unscaledTime + BindRetrySeconds;

        ControllerBrain player = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        targeting = player != null ? player.GetModule<TargetingModule>() : null;
        ICameraProvider cameraProvider = player != null ? player.GetModule<ICameraProvider>() : null;
        view = cameraProvider != null && cameraProvider.CameraTransform != null ? cameraProvider.CameraTransform.GetComponentInChildren<Camera>() : null;
        if (view == null && player != null) view = Camera.main;
    }

    private void LateUpdate()
    {
        if (targeting == null || view == null) TryBind();

        ControllerBrain target = targeting != null ? targeting.CurrentTarget : null;
        Transform root = target != null ? (target.EntityRoot != null ? target.EntityRoot : target.transform) : null;
        Vector3 screen = root != null && view != null ? view.WorldToScreenPoint(root.position + Vector3.up * height) : Vector3.back;

        image.enabled = screen.z > 0f;
        if (!image.enabled) return;

        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, canvasCamera, out Vector2 local);
        rect.anchoredPosition = local;
        image.color = color;
    }
}
