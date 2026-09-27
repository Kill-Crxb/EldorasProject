using NinjaGame.Stats;
using TMPro;
using UnityEngine;

/// <summary>
/// One stat row. The stat it shows is authored on the instance, not in code,
/// so adding or reordering rows never needs a script change.
/// </summary>
public class StatEntryView : MonoBehaviour
{
    #region Inspector

    [Header("Binding")]
    [Tooltip("Stat id this row displays, e.g. 'core.body'.")]
    [SerializeField] private string statId;

    [Header("Labels")]
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text valueLabel;

    [Header("Display")]
    [Tooltip("Use the schema's short label (BDY) instead of its display name (Body).")]
    [SerializeField] private bool useShortName = true;

    [Tooltip("Numeric format passed to float.ToString.")]
    [SerializeField] private string valueFormat = "F0";

    #endregion

    public string StatId => statId;

    /// <summary>
    /// Writes the label from the schema and the value from the provider.
    /// Returns false when the stat is missing so the panel can report it once.
    /// </summary>
    public bool Bind(IStatProvider stats)
    {
        if (string.IsNullOrEmpty(statId)) return false;

        SetLabel();

        if (stats == null || !stats.HasStat(statId))
        {
            SetMissing();
            return false;
        }

        SetValue(stats.GetValue(statId));
        return true;
    }

    public void SetValue(float value)
    {
        if (valueLabel != null) valueLabel.SetText(value.ToString(valueFormat));
    }

    private void SetLabel()
    {
        if (nameLabel == null) return;

        var entry = StatsManager.Instance != null ? StatsManager.Instance.GetEntry(statId) : null;
        if (entry == null) return;

        nameLabel.SetText(useShortName ? entry.ShortLabel : entry.displayName);
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
