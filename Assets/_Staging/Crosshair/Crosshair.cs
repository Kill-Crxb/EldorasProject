using UnityEngine;
using UnityEngine.UI;

// A HUD mark where the player's aim line lands: from the cast origin (where projectiles spawn by default) along
// the camera's forward, the line ProjectileAim fires down. With an over-the-shoulder camera that line runs beside
// the screen centre, so the mark sits where a shot would actually go, not where the camera looks. Tinted while
// the line is on a character. Put it on an Image under the HUD canvas; it finds the player when the game loads.
[RequireComponent(typeof(Image))]
public class Crosshair : MonoBehaviour
{
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    [Tooltip("How far the aim line reaches (the projectile probe's range). Past it the mark sits at the end of the line.")]
    [SerializeField] private float range = 40f;

    [SerializeField] private LayerMask mask = ~0;

    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color onCharacterColor = new Color(1f, 0.3f, 0.25f, 0.95f);

    private Image image;
    private RectTransform rect;
    private Canvas canvas;
    private ControllerBrain player;
    private Camera view;
    private VFXSystem anchors;

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        image.raycastTarget = false;
        image.enabled = false;
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    private void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
    }

    private void HandleLoadCompleted()
    {
        player = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        ICameraProvider cameraProvider = player != null ? player.GetModule<ICameraProvider>() : null;
        view = cameraProvider != null && cameraProvider.CameraTransform != null ? cameraProvider.CameraTransform.GetComponent<Camera>() : null;
        anchors = player != null ? player.GetModule<VFXSystem>() : null;

        image.enabled = view != null;
        if (view == null) Debug.LogWarning("[Crosshair] No player camera; the crosshair is hidden.", this);
    }

    private void LateUpdate()
    {
        if (view == null || player == null) return;

        Vector3 origin = anchors != null ? anchors.GetAnchorPosition(VFXAnchor.CastOrigin) : player.transform.position + Vector3.up * 1.4f;
        Vector3 direction = view.transform.forward;
        bool onCharacter = Cast(origin, direction, out Vector3 point);

        Vector3 screen = view.WorldToScreenPoint(point);
        if (screen.z <= 0f) return;

        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, canvasCamera, out Vector2 local);
        rect.anchoredPosition = local;
        image.color = onCharacter ? onCharacterColor : idleColor;
    }

    // The nearest hit that isn't the player; true when it belongs to another character.
    private bool Cast(Vector3 origin, Vector3 direction, out Vector3 point)
    {
        point = origin + direction * range;
        int count = Physics.RaycastNonAlloc(origin, direction, Hits, range, mask, QueryTriggerInteraction.Ignore);

        Transform self = player.transform.root;
        float nearest = float.MaxValue;
        Collider best = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.distance >= nearest || hit.collider.transform.root == self) continue;

            nearest = hit.distance;
            best = hit.collider;
            point = hit.point;
        }

        return best != null && best.GetComponentInParent<ControllerBrain>() != null;
    }
}
