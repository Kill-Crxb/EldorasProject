using TMPro;
using UnityEngine;

/// <summary>
/// One resource row. Shows a live pool as "18 / 30" rather than a stat value, which is the
/// difference between this and StatEntryView: character.max_stamina is the ceiling, and the
/// only place the current value exists is ResourceSystem.
///
/// The resource it shows is authored on the instance, so adding or reordering rows never
/// needs a script change.
/// </summary>
public class ResourceEntryView : MonoBehaviour
{
    #region Inspector

    [Header("Binding")]
    [Tooltip("Resource this row displays, e.g. StaminaDefinition.")]
    [SerializeField] private ResourceDefinition resource;

    [Header("Labels")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text valueLabel;

    [Header("Display")]
    [Tooltip("Show the ceiling alongside the value: '18 / 30' rather than '18'.")]
    [SerializeField] private bool showMax = true;

    [Tooltip("Numeric format passed to float.ToString.")]
    [SerializeField] private string valueFormat = "F0";

    [Tooltip("Tint the value by the resource's authored colour.")]
    [SerializeField] private bool useResourceColor;

    #endregion

    public ResourceDefinition Resource => resource;

    /// <summary>
    /// Writes the label from the definition and the value from the provider.
    /// Returns false when there is nothing to show, so the panel can report it once.
    /// </summary>
    public bool Bind(IResourceProvider provider)
    {
        if (resource == null) return false;

        SetLabel();

        if (provider == null)
        {
            SetMissing();
            return false;
        }

        Refresh(provider);
        return true;
    }

    public void Refresh(IResourceProvider provider)
    {
        if (provider == null || resource == null) return;

        SetValue(provider.GetResource(resource), provider.GetMaxResource(resource));
    }

    public void SetValue(float current, float max)
    {
        if (valueLabel == null) return;

        valueLabel.SetText(showMax
            ? $"{current.ToString(valueFormat)} / {max.ToString(valueFormat)}"
            : current.ToString(valueFormat));
    }

    private void SetLabel()
    {
        if (nameLabel == null) return;

        nameLabel.SetText(resource.displayName);

        if (useResourceColor) nameLabel.color = resource.resourceColor;
    }

    private void SetMissing()
    {
        if (valueLabel != null) valueLabel.SetText("-");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (nameLabel == null) nameLabel = transform.Find("StatName")?.GetComponent<TMP_Text>();
        if (valueLabel == null) valueLabel = transform.Find("StatValue")?.GetComponent<TMP_Text>();
    }
#endif
}
