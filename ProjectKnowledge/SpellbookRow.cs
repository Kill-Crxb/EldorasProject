using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One entry in the Spellbook window. Attach to the SpellEntry prefab root.
/// Implements IHotbarDraggable so ActionBarSlotView.OnDrop can receive it.
/// Drag begins on the Icon child and reparents the whole row to the root canvas
/// until dropped or cancelled.
/// </summary>
public class SpellbookRow : MonoBehaviour,
    IHotbarDraggable,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("References")]
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;

    // IHotbarDraggable
    public AbilityDefinition Ability { get; private set; }
    AbilityDefinition IHotbarDraggable.Ability => Ability;
    ItemInstance      IHotbarDraggable.Item     => null;

    private RectTransform rt;
    private CanvasGroup   canvasGroup;
    private Transform     originalParent;
    private int           originalSiblingIndex;
    private Vector2       originalPosition;

    void Awake()
    {
        rt          = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>()   ?? gameObject.AddComponent<CanvasGroup>();
    }

    public void Initialize(AbilityDefinition ability)
    {
        Ability = ability;

        if (iconImage != null)
        {
            iconImage.sprite  = ability.icon;
            iconImage.enabled = ability.icon != null;
        }

        if (nameText != null) nameText.text = ability.abilityName ?? "";
        if (descText  != null) descText.text  = ability.description ?? "";
    }

    // ── Drag ─────────────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        originalParent       = transform.parent;
        originalSiblingIndex = transform.GetSiblingIndex();
        originalPosition     = rt.anchoredPosition;

        var root = GetComponentInParent<Canvas>();
        if (root != null)
        {
            transform.SetParent(root.transform, true);
            transform.SetAsLastSibling();
        }

        canvasGroup.alpha          = 0.65f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                transform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local))
        {
            rt.localPosition = local;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        canvasGroup.alpha          = 1f;
        canvasGroup.blocksRaycasts = true;

        transform.SetParent(originalParent, true);
        transform.SetSiblingIndex(originalSiblingIndex);
        rt.anchoredPosition = originalPosition;
    }
}
