using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One "label  < value >" row on the character creation panel. The panel builds one for the model and
// one per appearance group, and listens to Stepped.
public class AppearanceRowView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private TextMeshProUGUI value;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    public event Action<AppearanceRowView, int> Stepped;

    public int GroupIndex { get; private set; }

    private void OnEnable()
    {
        if (previousButton != null) previousButton.onClick.AddListener(StepBack);
        if (nextButton != null) nextButton.onClick.AddListener(StepForward);
    }

    private void OnDisable()
    {
        if (previousButton != null) previousButton.onClick.RemoveListener(StepBack);
        if (nextButton != null) nextButton.onClick.RemoveListener(StepForward);
    }

    public void Show(int groupIndex, string labelText, string valueText)
    {
        GroupIndex = groupIndex;
        if (label != null) label.text = labelText;
        SetValue(valueText);
    }

    public void SetValue(string valueText)
    {
        if (value != null) value.text = valueText;
    }

    public void SetInteractable(bool interactable)
    {
        if (previousButton != null) previousButton.interactable = interactable;
        if (nextButton != null) nextButton.interactable = interactable;
    }

    private void StepBack()
    {
        Stepped?.Invoke(this, -1);
    }

    private void StepForward()
    {
        Stepped?.Invoke(this, 1);
    }
}
