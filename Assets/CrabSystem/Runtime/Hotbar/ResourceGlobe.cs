using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays one resource (health, mana, stamina…) as a Radial360 filled image.
/// Resolves IResourceProvider via SaveManager.PlayerBrain on load completion —
/// consistent with every other HUD system that defers until data is ready.
///
/// Inspector setup:
///   resourceDef — assign the ResourceDefinition SO (e.g. Health.asset)
///   fillImage   — set Image Type = Filled, Fill Method = Radial360, Origin = Bottom
/// </summary>
public class ResourceGlobe : MonoBehaviour
{
    [Header("Resource")]
    [SerializeField] private ResourceDefinition resourceDef;

    [Header("Visuals")]
    [SerializeField] private Image           fillImage;
    [SerializeField] private TextMeshProUGUI valueText;

    [Header("Color")]
    [SerializeField] private Color highColor    = new Color(0.18f, 0.75f, 0.25f);
    [SerializeField] private Color lowColor     = new Color(0.80f, 0.15f, 0.10f);
    [SerializeField] private float lowThreshold = 0.25f;

    private IResourceProvider resourceProvider;
    private bool bound;

    void Start()
    {
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
    }

    void Update()
    {
        if (!bound || resourceProvider == null || resourceDef == null) return;

        float current = resourceProvider.GetResource(resourceDef);
        float max     = resourceProvider.GetMaxResource(resourceDef);
        float pct     = max > 0f ? current / max : 0f;

        if (fillImage != null)
        {
            fillImage.fillAmount = pct;
            fillImage.color      = Color.Lerp(lowColor, highColor,
                                       Mathf.InverseLerp(0f, lowThreshold, pct));
        }

        if (valueText != null)
            valueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        var playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        if (playerBrain == null)
        {
            Debug.LogWarning("[ResourceGlobe] PlayerBrain not available — globe will not display.");
            return;
        }

        resourceProvider = playerBrain.GetProvider<IResourceProvider>();
        if (resourceProvider == null) { Debug.LogWarning("[ResourceGlobe] No IResourceProvider."); return; }
        if (resourceDef == null)      { Debug.LogWarning("[ResourceGlobe] resourceDef not assigned."); return; }

        bound = true;
    }
}
