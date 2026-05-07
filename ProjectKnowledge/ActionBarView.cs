using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD MonoBehaviour for one action bar (centre, bottomLeft, or bottomRight).
/// Always visible — not registered with UIWindowManager.
/// Resolves the player brain via SaveManager.PlayerBrain on load completion.
/// </summary>
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
        {
            hotbarSystem.OnSlotChanged -= HandleSlotChanged;
        }
        if (transformSystem != null)
        {
            transformSystem.OnOverrideChanged -= HandleOverrideChanged;
        }
    }

    // ── Connection ───────────────────────────────────────────────────────

    private void HandleLoadCompleted()
    {
        GameEvents.OnLoadCompleted -= HandleLoadCompleted;

        var playerBrain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        if (playerBrain == null)
        {
            Debug.LogWarning($"[ActionBarView:{barId}] PlayerBrain not available at load completion.");
            return;
        }

        hotbarSystem  = playerBrain.GetModule<HotbarSystem>();
        transformSystem = playerBrain.GetModule<SlotTransformationSystem>();

        if (hotbarSystem == null)
        {
            Debug.LogWarning($"[ActionBarView:{barId}] No HotbarSystem on player brain.");
            return;
        }

        hotbarSystem.OnSlotChanged += HandleSlotChanged;

        if (transformSystem != null)
            transformSystem.OnOverrideChanged += HandleOverrideChanged;

        Refresh();
    }

    // ── Layout ───────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (hotbarSystem == null) return;

        var config = hotbarSystem.GetConfig(barId);
        if (config == null) return;

        if (gridLayoutGroup != null)
            gridLayoutGroup.constraintCount = config.ColumnsPerRow;

        // Grow
        while (slotViews.Count < config.slotCount)
        {
            var view = slotViewPrefab != null
                ? Instantiate(slotViewPrefab, transform)
                : CreateFallbackSlotView();
            slotViews.Add(view);
        }

        // Shrink
        while (slotViews.Count > config.slotCount)
        {
            int last = slotViews.Count - 1;
            Destroy(slotViews[last].gameObject);
            slotViews.RemoveAt(last);
        }

        for (int i = 0; i < config.slotCount; i++)
        {
            slotViews[i].Setup(barId, i, hotbarSystem, transformSystem);
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

    // ── Fallback ─────────────────────────────────────────────────────────

    private ActionBarSlotView CreateFallbackSlotView()
    {
        var go = new GameObject("SlotView");
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(64, 64);
        go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        return go.AddComponent<ActionBarSlotView>();
    }
}
