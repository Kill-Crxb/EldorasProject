using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Toggle panel listing available AbilitySlotData SOs for drag-to-hotbar assignment.
/// Also surfaces raw AbilityDefinitions from RuntimeAbilityManager for abilities not
/// yet wrapped in an AbilitySlotData asset.
///
/// No transient ScriptableObjects are created — AbilityPickerEntry carries
/// SlotData (nullable) or AbilityDef directly.
/// </summary>
public class AbilityPickerPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform entryContainer;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Static Slot List")]
    [Tooltip("Drag AbilitySlotData SOs here. Supplemented at runtime by RuntimeAbilityManager.")]
    [SerializeField] private List<AbilitySlotData> staticSlotList = new List<AbilitySlotData>();

    private HotbarSystem hotbarSystem;
    private string targetBarId;
    private bool isOpen;

    private readonly List<AbilityPickerEntry> spawnedEntries = new List<AbilityPickerEntry>();

    void Awake() => gameObject.SetActive(false);

    // ── API ──────────────────────────────────────────────────────────────

    public void Toggle(HotbarSystem hotbar, string barId)
    {
        if (isOpen) Close(); else Open(hotbar, barId);
    }

    public void Open(HotbarSystem hotbar, string barId)
    {
        hotbarSystem = hotbar;
        targetBarId = barId;
        isOpen = true;
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        isOpen = false;
        gameObject.SetActive(false);
    }

    // ── Build ─────────────────────────────────────────────────────────────

    private void Refresh()
    {
        ClearEntries();

        // SO-backed slots from the static list
        foreach (var slot in staticSlotList)
        {
            if (slot == null) continue;
            SpawnEntry(slot, null);
        }

        // Raw abilities from RuntimeAbilityManager not covered by any static slot
        var playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        var ram = playerBrain?.GetModule<RuntimeAbilityManager>();

        if (ram != null)
        {
            foreach (var instance in ram.GetAllInstances())
            {
                if (instance?.definition == null) continue;

                bool alreadyCovered = staticSlotList.Exists(s =>
                    s != null &&
                    s.abilityChain != null &&
                    s.abilityChain.Length == 1 &&
                    s.abilityChain[0] == instance.definition);

                if (!alreadyCovered)
                    SpawnEntry(null, instance.definition);
            }
        }
    }

    private void SpawnEntry(AbilitySlotData slotData, AbilityDefinition abilityDef)
    {
        if (entryContainer == null) return;

        GameObject go;
        if (entryPrefab != null)
        {
            go = Instantiate(entryPrefab, entryContainer);
        }
        else
        {
            go = new GameObject("PickerEntry");
            go.transform.SetParent(entryContainer, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(60, 60);
            go.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        }

        var entry = go.GetComponent<AbilityPickerEntry>() ?? go.AddComponent<AbilityPickerEntry>();
        entry.Initialize(slotData, abilityDef);
        spawnedEntries.Add(entry);
    }

    private void ClearEntries()
    {
        foreach (var e in spawnedEntries)
            if (e != null) Destroy(e.gameObject);
        spawnedEntries.Clear();
    }
}

// ── Draggable Entry ───────────────────────────────────────────────────────────

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