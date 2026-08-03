using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ActionBarView : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("centre | bottomLeft | bottomRight")]
    [SerializeField] private string barId = "centre";

    [Header("References")]
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private ActionBarSlotView slotViewPrefab;

    [Header("Picker (optional)")]
    [SerializeField] private Button pickerToggleButton;
    [SerializeField] private AbilityPickerPanel abilityPickerPanel;

    private ControllerBrain playerBrain;
    private HotbarSystem hotbarSystem;
    private SlotTransformationSystem transformSystem;
    private readonly List<ActionBarSlotView> slotViews = new List<ActionBarSlotView>();

    void Awake()
    {
        if (gridLayoutGroup == null)
            gridLayoutGroup = GetComponent<GridLayoutGroup>();

        if (pickerToggleButton != null && abilityPickerPanel != null)
            pickerToggleButton.onClick.AddListener(() => abilityPickerPanel.Toggle(hotbarSystem, barId));
    }

    void Start()
    {
        GameEvents.OnLoadCompleted += HandleLoadCompleted;
    }

    void OnDestroy()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        if (hotbarSystem != null)
            hotbarSystem.OnSlotChanged -= HandleSlotChanged;

        if (transformSystem != null)
            transformSystem.OnOverrideChanged -= HandleOverrideChanged;
    }

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        if (playerBrain == null) return;

        hotbarSystem = playerBrain.GetModule<HotbarSystem>();
        transformSystem = playerBrain.GetModule<SlotTransformationSystem>();

        if (hotbarSystem == null) return;

        hotbarSystem.OnSlotChanged += HandleSlotChanged;
        if (transformSystem != null)
            transformSystem.OnOverrideChanged += HandleOverrideChanged;

        Refresh();
    }

    private void Refresh()
    {
        if (hotbarSystem == null) return;

        var config = hotbarSystem.GetConfig(barId);
        if (config == null) return;

        if (gridLayoutGroup != null)
            gridLayoutGroup.constraintCount = config.ColumnsPerRow;

        ResizeSlotViews(config.slotCount);
        SetupSlotViews(config);
    }

    private void ResizeSlotViews(int targetCount)
    {
        while (slotViews.Count < targetCount)
        {
            Transform parent = gridLayoutGroup != null ? gridLayoutGroup.transform : transform;
            var view = slotViewPrefab != null
                ? Instantiate(slotViewPrefab, parent)
                : CreateFallbackSlotView(parent);
            slotViews.Add(view);
        }

        while (slotViews.Count > targetCount)
        {
            int last = slotViews.Count - 1;
            Destroy(slotViews[last].gameObject);
            slotViews.RemoveAt(last);
        }
    }

    private void SetupSlotViews(ActionBarConfig config)
    {
        var inputSystem = playerBrain?.GetModule<InputSystem>();
        HotbarKeybindSet keybindSet = inputSystem != null
            ? inputSystem.GetKeybindSetForBar(barId)
            : HotbarKeybindSet.None;

        for (int i = 0; i < config.slotCount; i++)
        {
            string label = GetKeyLabel(keybindSet, i);
            slotViews[i].Setup(barId, i, hotbarSystem, transformSystem, label);
            slotViews[i].Refresh();
        }
    }

    private void HandleSlotChanged(string changedBarId, int index)
    {
        if (changedBarId != barId || index < 0 || index >= slotViews.Count) return;
        slotViews[index].Refresh();
    }

    private void HandleOverrideChanged(string changedBarId, int index)
    {
        if (changedBarId != barId || index < 0 || index >= slotViews.Count) return;
        slotViews[index].Refresh();
    }

    private ActionBarSlotView CreateFallbackSlotView(Transform parent)
    {
        var go = new GameObject("SlotView");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64, 64);
        go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        return go.AddComponent<ActionBarSlotView>();
    }

    private static string GetKeyLabel(HotbarKeybindSet set, int i)
    {
        switch (set)
        {
            case HotbarKeybindSet.ZXCV:
                return i switch { 0 => "Z", 1 => "X", 2 => "C", 3 => "V", _ => "" };
            case HotbarKeybindSet.Hotbar1234:
                return i switch { 0 => "1", 1 => "2", 2 => "3", 3 => "4", _ => "" };
            case HotbarKeybindSet.Hotbar5678:
                return i switch { 0 => "5", 1 => "6", 2 => "7", 3 => "8", _ => "" };
            case HotbarKeybindSet.Hotbar9:
                return i == 0 ? "9" : "";
            case HotbarKeybindSet.QuickslotQ:
                return i == 0 ? "Q" : "";
            default:
                return "";
        }
    }
}