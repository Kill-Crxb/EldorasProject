using UnityEngine;

public enum ViewSpan { Half, Full }

/// <summary>
/// A swappable screen inside a slot. Bind data in OnShown and release it in OnHidden —
/// a view is shown and hidden repeatedly as the player switches tabs.
/// </summary>
public class UIPanelView : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string viewId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Layout")]
    [Tooltip("Full takes the whole panel and hides the other slots while it is up.")]
    [SerializeField] private ViewSpan span = ViewSpan.Half;

    public string ViewId => viewId;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? viewId : displayName;
    public Sprite Icon => icon;
    public ViewSpan Span => span;
    public bool IsShown { get; private set; }

    public void Show()
    {
        gameObject.SetActive(true);

        if (IsShown) return;

        IsShown = true;
        OnShown();
    }

    public void Hide()
    {
        bool wasShown = IsShown;
        IsShown = false;

        if (wasShown) OnHidden();

        gameObject.SetActive(false);
    }

    protected virtual void OnShown() { }

    protected virtual void OnHidden() { }
}
