using UnityEngine;

/// <summary>
/// TooltipManager - Lives on UIManager GameObject alongside UniversalWindowManager.
/// 
/// Canvas is injected by UniversalWindowManager.Awake() — never searches the scene.
/// External callers use UniversalWindowManager.Instance.ShowTooltip / HideTooltip.
/// Direct access via TooltipManager.Instance is still valid for legacy call sites.
/// </summary>
public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [Header("Tooltip Prefab")]
    [SerializeField] private ItemTooltip tooltipPrefab;

    private Canvas tooltipCanvas;
    private ItemTooltip currentTooltip;
    private bool initialized = false;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        // Canvas injected by UniversalWindowManager — do not initialize here
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Called by UniversalWindowManager.Awake() to provide the overlay canvas.
    /// </summary>
    public void InjectCanvas(Canvas canvas)
    {
        if (initialized) return;
        if (canvas == null) { Debug.LogError("[TooltipManager] Injected canvas is null!"); return; }
        if (tooltipPrefab == null) { Debug.LogError("[TooltipManager] Tooltip prefab not assigned!"); return; }

        tooltipCanvas = canvas;
        currentTooltip = Instantiate(tooltipPrefab, tooltipCanvas.transform);
        currentTooltip.Hide();
        initialized = true;
    }

    public void ShowTooltip(ItemTooltipData data, Vector2 screenPosition)
    {
        if (!initialized) { Debug.LogWarning("[TooltipManager] Not initialized yet."); return; }
        currentTooltip.Show(data, screenPosition);
    }

    public void HideTooltip()
    {
        if (!initialized) return;
        currentTooltip?.Hide();
    }
}