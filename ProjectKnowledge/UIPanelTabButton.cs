using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Switches its slot to one view. Drop on a button inside the slot's tab strip.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIPanelTabButton : MonoBehaviour
{
    [SerializeField] private UIPanelSlot slot;
    [SerializeField] private string viewId;

    [Header("Selected State")]
    [SerializeField] private Graphic highlight;
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.35f);

    private Button button;

    private void Awake() => button = GetComponent<Button>();

    private void OnEnable()
    {
        if (slot == null) return;

        button.onClick.AddListener(Select);
        slot.ViewChanged += HandleViewChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (slot == null) return;

        button.onClick.RemoveListener(Select);
        slot.ViewChanged -= HandleViewChanged;
    }

    private void Select() => slot.Show(viewId);

    private void HandleViewChanged(UIPanelSlot changed) => Refresh();

    private void Refresh()
    {
        if (highlight == null) return;

        bool selected = slot.Current != null && slot.Current.ViewId == viewId;
        highlight.color = selected ? selectedColor : unselectedColor;
    }
}
