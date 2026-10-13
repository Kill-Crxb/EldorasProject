using UnityEngine;
using UnityEngine.UI;

// A HUD mark where the player's aim lands: the camera's screen-centre aim ray (tilted up by its aimPitchOffset),
// which projectiles now fly toward from the cast origin. Tinted while the ray is on a character. Put it on an
// Image under the HUD canvas; it finds the player once one exists (the load event can fire before the HUD exists).
// It fades out in melee — a melee move in flight, or the soft target within meleeRange — and back in otherwise.
[RequireComponent(typeof(Image))]
public class Crosshair : MonoBehaviour
{
    [Tooltip("How far the aim reaches (the projectile probe's range). Past it the mark sits at the end of the ray.")]
    [SerializeField] private float range = 40f;

    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color onCharacterColor = new Color(1f, 0.3f, 0.25f, 0.95f);

    [Tooltip("Hidden while the soft target is this close (metres) or a melee move is in flight.")]
    [SerializeField] private float meleeRange = 3.5f;
    [Tooltip("Fade speed, full alpha per second.")]
    [SerializeField] private float fadeSpeed = 6f;

    private Image image;
    private RectTransform rect;
    private Canvas canvas;
    private ControllerBrain player;
    private Camera view;
    private ICameraProvider aim;
    private AbilitySystem abilities;
    private TargetingModule targeting;
    private float alpha = 1f;
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

        player = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        ICameraProvider cameraProvider = player != null ? player.GetModule<ICameraProvider>() : null;
        view = cameraProvider != null && cameraProvider.CameraTransform != null ? cameraProvider.CameraTransform.GetComponentInChildren<Camera>() : null;
        if (view == null && player != null) view = Camera.main;
        aim = cameraProvider;
        abilities = player != null ? player.Abilities : null;
        targeting = player != null ? player.GetModule<TargetingModule>() : null;
    }

    private void LateUpdate()
    {
        if (view == null || aim == null) TryBind();
        image.enabled = view != null && aim != null;
        if (!image.enabled) return;

        Vector3 point = aim.AimPoint(range, out Collider hit);
        bool onCharacter = hit != null && hit.GetComponentInParent<ControllerBrain>() != null;

        Vector3 screen = view.WorldToScreenPoint(point);
        if (screen.z <= 0f) return;

        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen, canvasCamera, out Vector2 local);
        rect.anchoredPosition = local;

        alpha = Mathf.MoveTowards(alpha, InMelee() ? 0f : 1f, fadeSpeed * Time.unscaledDeltaTime);
        Color color = onCharacter ? onCharacterColor : idleColor;
        color.a *= alpha;
        image.color = color;
    }

    private bool InMelee()
    {
        AbilityDefinition move = abilities != null ? abilities.CurrentAbility : null;
        if (move != null && move.HasMoveData && move.projectileData == null) return true;

        ControllerBrain target = targeting != null ? targeting.CurrentTarget : null;
        if (target == null) return false;

        Transform root = target.EntityRoot != null ? target.EntityRoot : target.transform;
        Transform self = player.EntityRoot != null ? player.EntityRoot : player.transform;
        return Vector3.Distance(root.position, self.position) <= meleeRange;
    }
}
