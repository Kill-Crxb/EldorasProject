using UnityEngine;
using UnityEngine.UI;

public class InteractionHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image interactionIcon;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 8f;

    private float currentAlpha = 0f;
    private float targetAlpha = 0f;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (currentAlpha != targetAlpha)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            canvasGroup.alpha = currentAlpha;

            if (currentAlpha < 0.01f && targetAlpha == 0f)
            {
                gameObject.SetActive(false);
            }
        }
    }

    public void ShowIcon(Sprite icon)
    {
        if (icon == null) return;

        if (interactionIcon != null)
            interactionIcon.sprite = icon;

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
}