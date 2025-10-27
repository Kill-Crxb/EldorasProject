using UnityEngine;
using UnityEngine.UI;

public class ItemOverlayVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image overlayImage;

    private string itemInstanceId;
    private GridArea currentArea;
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (overlayImage == null)
            overlayImage = GetComponent<Image>();
    }

    // Initialize method called by InventoryGridController
    public void Initialize(string itemId, GridArea area, int tier, float slotSize, float slotSpacing)
    {
        this.itemInstanceId = itemId;
        this.currentArea = area;

        // CRITICAL: Set anchor and pivot FIRST before any position calculations
        rectTransform.anchorMin = new Vector2(0, 0);
        rectTransform.anchorMax = new Vector2(0, 0);
        rectTransform.pivot = new Vector2(0, 0);

        // Calculate the size of the overlay based on grid area
        float width = area.width * slotSize + (area.width - 1) * slotSpacing;
        float height = area.height * slotSize + (area.height - 1) * slotSpacing;

        // Calculate position - bottom-left corner of the item area
        Vector2 position = new Vector2(
            area.position.x * (slotSize + slotSpacing),
            area.position.y * (slotSize + slotSpacing)
        );

        // Apply size and position
        rectTransform.sizeDelta = new Vector2(width, height);
        rectTransform.anchoredPosition = position;

        // Set color based on rarity
        if (overlayImage != null)
        {
            overlayImage.color = GetRarityColor(tier);
            overlayImage.raycastTarget = false; // Don't block raycasts
        }

        Debug.Log($"[ItemOverlayVisual] Initialized {itemId} at position {position} with size {width}x{height}");
    }

    private Color GetRarityColor(int tier)
    {
        // Semi-transparent colored backgrounds based on rarity (0-5)
        switch (tier)
        {
            case 0: // Common
                return new Color(0.5f, 0.5f, 0.5f, 0.3f); // Gray
            case 1: // Uncommon
                return new Color(0.2f, 0.8f, 0.2f, 0.3f); // Green
            case 2: // Rare
                return new Color(0.3f, 0.5f, 1f, 0.3f); // Blue
            case 3: // Epic
                return new Color(0.7f, 0.3f, 0.9f, 0.3f); // Purple
            case 4: // Legendary
                return new Color(1f, 0.6f, 0.1f, 0.3f); // Orange
            case 5: // Mythic
                return new Color(1f, 0.2f, 0.2f, 0.3f); // Red
            default:
                return new Color(0.5f, 0.5f, 0.5f, 0.3f);
        }
    }

    // Public accessor
    public string ItemInstanceId => itemInstanceId;
    public GridArea CurrentArea => currentArea;
}