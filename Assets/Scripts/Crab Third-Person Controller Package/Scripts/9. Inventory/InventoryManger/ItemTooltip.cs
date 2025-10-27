// ItemTooltip.cs - Enhanced version with proper positioning
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ItemTooltip : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image backgroundImage;

    [Header("Settings")]
    [SerializeField] private Vector2 offset = new Vector2(15, -15);
    [SerializeField] private float padding = 20f;

    private RectTransform rectTransform;
    private Canvas canvas;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        // FORCE to render on top
        transform.SetAsLastSibling();

        if (canvas == null)
        {
            Debug.LogError("ItemTooltip must be under a Canvas!");
        }
        else
        {
            Debug.Log($"Tooltip canvas found: {canvas.name}, renderMode: {canvas.renderMode}");
        }

        Hide();
    }

    public void Show(ItemTooltipData data, Vector2 screenPosition)
    {
        gameObject.SetActive(true);

        nameText.text = data.itemName;
        nameText.color = GetRarityColor(data.rarity);
        descriptionText.text = data.description;

        Debug.Log($"Tooltip showing: {data.itemName} at {screenPosition}");
        Debug.Log($"Tooltip active: {gameObject.activeSelf}, scale: {transform.localScale}");
        Debug.Log($"RectTransform position: {rectTransform.anchoredPosition}, sizeDelta: {rectTransform.sizeDelta}");

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

        UpdatePosition(screenPosition);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void UpdatePosition(Vector2 screenPosition)
    {
        if (canvas == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition + offset,
            canvas.worldCamera,
            out localPoint
        );

        Vector2 tooltipSize = rectTransform.sizeDelta;
        Rect canvasRect = (canvas.transform as RectTransform).rect;

        // Clamp X
        float minX = canvasRect.xMin + padding;
        float maxX = canvasRect.xMax - tooltipSize.x - padding;
        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);

        // Clamp Y
        float minY = canvasRect.yMin + tooltipSize.y + padding;
        float maxY = canvasRect.yMax - padding;
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        rectTransform.anchoredPosition = localPoint;
    }

    private Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return new Color(0.7f, 0.7f, 0.7f);
            case ItemRarity.Uncommon: return new Color(0.2f, 0.8f, 0.2f);
            case ItemRarity.Rare: return new Color(0.2f, 0.4f, 1f);
            case ItemRarity.Epic: return new Color(0.8f, 0.2f, 0.8f);
            case ItemRarity.Legendary: return new Color(1f, 0.6f, 0.1f);
            default: return Color.white;
        }
    }
}