using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One draggable row in the AbilityPickerPanel.
/// Carries SlotData (SO-backed) or AbilityDef (runtime ability with no SO).
/// ActionBarSlotView.OnDrop reads these properties to call the correct AssignSlot overload.
/// </summary>
public class AbilityPickerEntry : MonoBehaviour,
    IHotbarDraggable,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;

    public AbilitySlotData SlotData { get; private set; }
    public AbilityDefinition AbilityDef { get; private set; }

    // IHotbarDraggable — resolves Ability from SlotData chain or raw AbilityDef
    AbilityDefinition IHotbarDraggable.Ability =>
        AbilityDef ?? (SlotData?.abilityChain?.Length > 0 ? SlotData.abilityChain[0] : null);
    ItemInstance IHotbarDraggable.Item => null;

    private RectTransform rt;
    private CanvasGroup canvasGroup;
    private Transform originalParent;
    private Vector2 originalPosition;

    void Awake()
    {
        rt = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
        }
    }

    public void Initialize(AbilitySlotData slotData, AbilityDefinition abilityDef)
    {
        SlotData = slotData;
        AbilityDef = abilityDef;

        Sprite icon = slotData?.GetDisplayIcon() ?? abilityDef?.icon;
        string label = slotData?.slotName ?? abilityDef?.abilityName ?? "—";

        if (iconImage != null) { iconImage.sprite = icon; iconImage.enabled = icon != null; }
        if (nameText != null) nameText.text = label;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        originalParent = transform.parent;
        originalPosition = rt.anchoredPosition;

        var root = GetComponentInParent<Canvas>();
        if (root != null) { transform.SetParent(root.transform, true); transform.SetAsLastSibling(); }

        canvasGroup.alpha = 0.65f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData e)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform.parent as RectTransform, e.position, e.pressEventCamera, out Vector2 local))
            rt.localPosition = local;
    }

    public void OnEndDrag(PointerEventData e)
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        transform.SetParent(originalParent, true);
        rt.anchoredPosition = originalPosition;
    }
}
