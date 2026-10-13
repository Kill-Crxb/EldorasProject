using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ActionBarView — renders one bar's slots.
///
/// It is told what it is. It no longer holds a barId string, and no longer finds the player:
/// HotbarHud spawns it and calls Bind. That is what removes the whole class of failures where a
/// typo'd or missing string left a bar silently blank.
/// </summary>
public class ActionBarView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridLayoutGroup gridLayoutGroup;
    [SerializeField] private ActionBarSlotView slotViewPrefab;

    [Header("Picker (optional)")]
    [Tooltip("The panel itself is handed over by HotbarHud, so this prefab holds no scene refs.")]
    [SerializeField] private Button pickerToggleButton;

    private ActionBarDefinition definition;
    private HotbarSystem hotbarSystem;
    private SlotTransformationSystem transformSystem;
    private AbilityPickerPanel abilityPickerPanel;

    private readonly List<ActionBarSlotView> slotViews = new List<ActionBarSlotView>();

    public ActionBarDefinition Definition => definition;
    public string BarId => definition != null ? definition.barId : "";

    void Awake()
    {
        if (gridLayoutGroup == null)
            gridLayoutGroup = GetComponent<GridLayoutGroup>();
    }

    void OnDestroy()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Called by HotbarHud immediately after instantiation. Everything this view needs arrives
    /// here — there is no other entry point and no self-discovery.
    /// </summary>
    public void Bind(ActionBarDefinition barDefinition,
                     HotbarSystem hotbar,
                     SlotTransformationSystem transforms,
                     AbilityPickerPanel picker)
    {
        Unsubscribe();

        definition = barDefinition;
        hotbarSystem = hotbar;
        transformSystem = transforms;
        abilityPickerPanel = picker;

        if (definition == null || hotbarSystem == null)
        {
            Debug.LogError($"[ActionBarView] {name} bound with no definition or no HotbarSystem.");
            return;
        }

        if (pickerToggleButton != null && abilityPickerPanel != null)
        {
            pickerToggleButton.onClick.RemoveAllListeners();
            pickerToggleButton.onClick.AddListener(
                () => abilityPickerPanel.Toggle(hotbarSystem, definition.barId));
        }

        hotbarSystem.OnSlotChanged += HandleSlotChanged;
        hotbarSystem.OnPageChanged += HandlePageChanged;

        if (transformSystem != null)
            transformSystem.OnOverrideChanged += HandleOverrideChanged;

        Refresh();
    }

    private void Unsubscribe()
    {
        if (hotbarSystem != null)
        {
            hotbarSystem.OnSlotChanged -= HandleSlotChanged;
            hotbarSystem.OnPageChanged -= HandlePageChanged;
        }

        if (transformSystem != null)
            transformSystem.OnOverrideChanged -= HandleOverrideChanged;
    }

    private void Refresh()
    {
        if (hotbarSystem == null || definition == null) return;

        var config = hotbarSystem.GetConfig(definition.barId);
        if (config == null)
        {
            Debug.LogError($"[ActionBarView] No config for bar '{definition.barId}'. The bar asset " +
                           $"is not in HotbarSystem.availableBars on the player.");
            return;
        }

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
        var inputSystem = hotbarSystem.Brain?.GetModule<InputSystem>();
        HotbarKeybindSet keybindSet = inputSystem != null
            ? inputSystem.GetKeybindSetForBar(definition.barId)
            : HotbarKeybindSet.None;

        for (int i = 0; i < config.slotCount; i++)
        {
            string label = GetKeyLabel(keybindSet, i);
            slotViews[i].Setup(definition.barId, i, hotbarSystem, transformSystem, label);
            slotViews[i].Refresh();
        }
    }

    /// <summary>
    /// Pages can differ in slot count, so this is a full rebuild rather than a per-slot refresh.
    /// </summary>
    private void HandlePageChanged(string pageId)
    {
        Refresh();
    }

    private void HandleSlotChanged(string changedBarId, int index)
    {
        if (definition == null || changedBarId != definition.barId) return;
        if (index < 0 || index >= slotViews.Count) return;

        slotViews[index].Refresh();
    }

    private void HandleOverrideChanged(string changedBarId, int index)
    {
        if (definition == null || changedBarId != definition.barId) return;
        if (index < 0 || index >= slotViews.Count) return;

        slotViews[index].Refresh();
    }

    private ActionBarSlotView CreateFallbackSlotView(Transform parent)
    {
        var go = new GameObject("SlotView");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64, 64);
        go.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
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
            case HotbarKeybindSet.MouseLR:
                return i switch { 0 => "LMB", 1 => "RMB", _ => "" };
            case HotbarKeybindSet.ShiftCtrlQ:
                return i switch { 0 => "Shift", 1 => "Ctrl", 2 => "Q", _ => "" };
            default:
                return "";
        }
    }
}
