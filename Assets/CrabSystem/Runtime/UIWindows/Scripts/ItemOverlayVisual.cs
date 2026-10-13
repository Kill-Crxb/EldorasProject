using UnityEngine;
using UnityEngine.UI;

public class ItemOverlayVisual : MonoBehaviour
{
    [SerializeField] private Image overlayImage;

    private string itemInstanceId;
    private GridArea currentArea;
    private RectTransform rectTransform;

    private static readonly Color[] RarityColors =
    {
        new Color(0.5f, 0.5f, 0.5f, 0.3f), // 0 Common    - Gray
        new Color(0.2f, 0.8f, 0.2f, 0.3f), // 1 Uncommon  - Green
        new Color(0.3f, 0.5f, 1f,   0.3f), // 2 Rare      - Blue
        new Color(0.7f, 0.3f, 0.9f, 0.3f), // 3 Epic      - Purple
        new Color(1f,   0.6f, 0.1f, 0.3f), // 4 Legendary - Orange
        new Color(1f,   0.2f, 0.2f, 0.3f), // 5 Mythic    - Red
    };

    public string ItemInstanceId => itemInstanceId;
    public GridArea CurrentArea => currentArea;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (overlayImage == null)
            overlayImage = GetComponent<Image>();
    }

    public void Initialize(string itemId, GridArea area, int tier, float slotSize, float slotSpacing)
    {
        itemInstanceId = itemId;
        currentArea = area;

        if (rectTransform == null)
            rectTransform = GetComponent<RectTransform>();

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = Vector2.zero;

        float width = area.width * slotSize + (area.width - 1) * slotSpacing;
        float height = area.height * slotSize + (area.height - 1) * slotSpacing;

        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = new Vector2(
            area.position.x * (slotSize + slotSpacing),
            area.position.y * (slotSize + slotSpacing)
        );

        if (overlayImage != null)
        {
            overlayImage.color = tier >= 0 && tier < RarityColors.Length ? RarityColors[tier] : RarityColors[0];
            overlayImage.raycastTarget = false;
        }
    }
}