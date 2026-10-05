using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BarKind { Armour, Guard, Posture, Resource }

// One of the player's HUD bars: a resource (health, mana, stamina — any ResourceDefinition) or one of the
// three combat bars (Combat_Framework §3.5). Binds to the player brain when the game has loaded. Use a
// Filled image (Horizontal).
public class BarView : MonoBehaviour
{
    [SerializeField] private BarKind bar = BarKind.Resource;

    [Tooltip("Only for Resource bars: the resource to show, e.g. Health.asset.")]
    [SerializeField] private ResourceDefinition resource;

    [Tooltip("Colour the fill with the resource's Resource Color (and use it as the High Color when tinting).")]
    [SerializeField] private bool useResourceColor = true;

    [Header("Visuals")]
    [SerializeField] private Image fillImage;
    [SerializeField] private TextMeshProUGUI valueText;

    [Tooltip("Hide the whole bar while its maximum is 0 — e.g. the Armour bar with no armour on.")]
    [SerializeField] private GameObject hideWhenEmptyMax;

    [Header("Low Tint (optional)")]
    [Tooltip("Fade the fill towards Low Color as the bar empties — for health.")]
    [SerializeField] private bool tintWhenLow;
    [SerializeField] private Color highColor = new Color(0.18f, 0.75f, 0.25f);
    [SerializeField] private Color lowColor = new Color(0.80f, 0.15f, 0.10f);
    [SerializeField] private float lowThreshold = 0.25f;

    private IBarSource source;
    private int shownCurrent = -1;
    private int shownMax = -1;

    private void Start()
    {
        ApplyResourceColor();
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    // Shows the colour in the editor as soon as the resource is assigned.
    private void OnValidate()
    {
        ApplyResourceColor();
    }

    private void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;
    }

    private void Update()
    {
        if (source == null) return;

        float max = source.Max;
        float current = source.Current;
        float fill = max > 0f ? current / max : 0f;

        if (hideWhenEmptyMax != null && hideWhenEmptyMax.activeSelf != max > 0f) hideWhenEmptyMax.SetActive(max > 0f);
        if (fillImage != null) fillImage.fillAmount = fill;
        if (fillImage != null && tintWhenLow) fillImage.color = Color.Lerp(lowColor, highColor, Mathf.InverseLerp(0f, lowThreshold, fill));

        ShowValue(Mathf.CeilToInt(current), Mathf.CeilToInt(max));
    }

    // Text is rebuilt only when the whole numbers change.
    private void ShowValue(int current, int max)
    {
        if (valueText == null || (current == shownCurrent && max == shownMax)) return;

        shownCurrent = current;
        shownMax = max;
        valueText.text = $"{current} / {max}";
    }

    private void ApplyResourceColor()
    {
        if (!useResourceColor || bar != BarKind.Resource || resource == null) return;

        highColor = resource.resourceColor;
        if (fillImage != null) fillImage.color = highColor;
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        ControllerBrain player = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        if (player == null)
        {
            Debug.LogWarning($"[BarView] No player brain; the {name} bar will not display.", this);
            return;
        }

        source = Find(player);
        if (source == null) Debug.LogWarning($"[BarView] The player has no {bar} bar for {name}.", this);
    }

    private IBarSource Find(ControllerBrain brain)
    {
        if (bar == BarKind.Resource) return FindResource(brain);
        if (bar == BarKind.Posture) return brain.GetModule<PostureModule>();

        if (bar == BarKind.Guard)
        {
            GuardModule guard = brain.GetModule<GuardModule>();
            return guard != null ? guard.GuardBar : null;
        }

        ArmourShieldModule armour = brain.GetModule<ArmourShieldModule>();
        return armour != null ? armour.Bar : null;
    }

    private IBarSource FindResource(ControllerBrain brain)
    {
        IResourceProvider provider = brain.GetProvider<IResourceProvider>();
        if (provider == null || resource == null) return null;
        return new ResourceBar(provider, resource);
    }

    // A resource seen as a bar.
    private class ResourceBar : IBarSource
    {
        private readonly IResourceProvider provider;
        private readonly ResourceDefinition resource;

        public ResourceBar(IResourceProvider provider, ResourceDefinition resource)
        {
            this.provider = provider;
            this.resource = resource;
        }

        public float Current => provider.GetResource(resource);
        public float Max => provider.GetMaxResource(resource);
    }
}
