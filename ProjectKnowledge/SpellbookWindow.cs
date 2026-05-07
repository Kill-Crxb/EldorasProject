using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Spellbook window — lists all known abilities alphabetically and allows drag-to-hotbar.
/// UIWindow subclass: open/close, drag to move, and position memory (PlayerPrefs)
/// are all handled by the base class.
///
/// Inspector wiring:
///   windowBackground → Spellbook root RectTransform   (UIWindow base — moves with drag)
///   headerBar        → Foreground/TitleBar RectTransform (UIWindow base — needed for drag init)
///   entryContainer   → SpellList/Viewport/Content RectTransform
///   entryPrefab      → SpellbookRow prefab
///   scrollRect       → SpellList ScrollRect component
///   titleText        → Foreground/TitleBar/Text (TMP)  (optional)
///
/// The window starts inactive. UIWindowManager.Toggle("Spellbook") or P key shows it.
/// </summary>
public class SpellbookWindow : UIWindow
{
    [Header("Spellbook")]
    [SerializeField] private RectTransform   entryContainer;
    [SerializeField] private GameObject      entryPrefab;
    [SerializeField] private ScrollRect      scrollRect;
    [SerializeField] private TextMeshProUGUI titleText;

    [Header("Scroll Stub")]
    [SerializeField] private float scrollSensitivity = 0.1f;

    private readonly List<SpellbookRow> spawnedRows = new List<SpellbookRow>();

    // ── UIWindow ──────────────────────────────────────────────────────────

    protected override void SetupWindow()
    {
        if (titleText != null) titleText.text = "Spellbook";
    }

    public override void OnClose()
    {
        base.OnClose(); // saves position to PlayerPrefs
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Called by UIWindowManager.ToggleSpellbookWindow after SetActive(true).</summary>
    public void Open()
    {
        Populate();
        ResetScroll();
    }

    // ── Population ────────────────────────────────────────────────────────

    private void Populate()
    {
        ClearRows();

        if (entryContainer == null || entryPrefab == null) return;

        var abilities = CollectAbilities();
        abilities.Sort((a, b) =>
            string.Compare(a.abilityName, b.abilityName, StringComparison.OrdinalIgnoreCase));

        foreach (var ability in abilities)
        {
            var go  = Instantiate(entryPrefab, entryContainer);
            var row = go.GetComponent<SpellbookRow>();
            if (row == null) row = go.AddComponent<SpellbookRow>();

            row.Initialize(ability);
            spawnedRows.Add(row);
        }
    }

    private List<AbilityDefinition> CollectAbilities()
    {
        var result = new List<AbilityDefinition>();

        var brain = ManagerBrain.Instance?.GetManager<SaveManager>()?.PlayerBrain;
        var ram   = brain?.GetModule<RuntimeAbilityManager>();
        if (ram == null) return result;

        foreach (var instance in ram.GetAllInstances())
        {
            if (instance?.definition != null)
                result.Add(instance.definition);
        }

        return result;
    }

    private void ClearRows()
    {
        foreach (var row in spawnedRows)
            if (row != null) Destroy(row.gameObject);
        spawnedRows.Clear();
    }

    private void ResetScroll()
    {
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    // ── Scroll Stub ───────────────────────────────────────────────────────

    // STUB: Wire to InputSystem.ScrollDelta when scroll wheel input is added.
    // Mouse scroll already works automatically via Unity's EventSystem — this
    // is only for keyboard/gamepad scroll support later.
    public void OnScrollInput(float delta)
    {
        // TODO: scrollRect.verticalNormalizedPosition += delta * scrollSensitivity;
    }
}
