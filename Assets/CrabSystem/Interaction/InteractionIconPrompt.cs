using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class InteractionIconPrompt : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;

    [Header("Settings")]
    [SerializeField] private Vector3 promptOffset = new Vector3(0, 1.5f, 0);
    [SerializeField] private bool alwaysFaceCamera = true;
    [SerializeField] private float fadeSpeed = 5f;

    private Transform targetTransform;
    private Camera mainCamera;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private float currentAlpha = 0f;
    private float targetAlpha = 1f;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        mainCamera = Camera.main;

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
        }

        canvasGroup.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (targetTransform != null)
        {
            transform.position = targetTransform.position + promptOffset;
        }

        if (alwaysFaceCamera && mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }

        if (currentAlpha != targetAlpha)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            canvasGroup.alpha = currentAlpha;
        }
    }

    public void Initialize(Transform target, Sprite icon)
    {
        targetTransform = target;
        SetIcon(icon);
        Show();
    }

    public void Show()
    {
        targetAlpha = 1f;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        targetAlpha = 0f;
    }

    public void HideImmediate()
    {
        targetAlpha = 0f;
        currentAlpha = 0f;
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage != null)
        {
            iconImage.sprite = icon;
        }
    }

    public void SetOffset(Vector3 offset)
    {
        promptOffset = offset;
    }
}