// TooltipManager.cs - Fixed version
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Tooltip Prefab")]
    [SerializeField] private ItemTooltip tooltipPrefab;

    private ItemTooltip currentTooltip;
    private Canvas canvas;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                Debug.LogError("TooltipManager must be under a Canvas!");
                return;
            }

            // Create tooltip instance
            if (tooltipPrefab != null)
            {
                currentTooltip = Instantiate(tooltipPrefab, transform);
                currentTooltip.Hide();
                Debug.Log("Tooltip instantiated successfully");
            }
            else
            {
                Debug.LogError("Tooltip prefab not assigned in TooltipManager!");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowTooltip(ItemTooltipData data, Vector2 screenPosition)
    {
        if (currentTooltip != null)
        {
            Debug.Log($"Showing tooltip for: {data.itemName}");
            currentTooltip.Show(data, screenPosition);
        }
        else
        {
            Debug.LogError("Current tooltip is null!");
        }
    }

    public void HideTooltip()
    {
        if (currentTooltip != null)
        {
            currentTooltip.Hide();
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}