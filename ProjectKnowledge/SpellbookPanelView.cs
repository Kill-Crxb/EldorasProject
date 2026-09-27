using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lists every ability the player knows, sorted by name. Rows are SpellbookRow, which
/// implements IHotbarDraggable, so a spell can be dragged straight onto the hotbar.
///
/// Rebuilt each time the view is shown — abilities are learned mid-session, and a list
/// that is only correct at spawn is worse than no list.
/// </summary>
public class SpellbookPanelView : UIPanelView
{
    [Header("List")]
    [Tooltip("Content object of the scroll view. Rows are spawned as its children.")]
    [SerializeField] private RectTransform entryContainer;

    [Tooltip("SpellbookRow prefab.")]
    [SerializeField] private GameObject entryPrefab;

    [Tooltip("Optional. Scrolled back to the top each time the view opens.")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("Source")]
    [Tooltip("Leave empty to bind the player found at runtime.")]
    [SerializeField] private ControllerBrain sourceBrain;

    private readonly List<SpellbookRow> rows = new();

    protected override void OnShown()
    {
        if (sourceBrain == null) sourceBrain = PlayerBrainAccess.Find();

        if (sourceBrain == null)
        {
            Debug.LogWarning($"[SpellbookPanelView] no player brain for {name}");
            return;
        }

        Populate(sourceBrain);
    }

    protected override void OnHidden() => ClearRows();

    private void Populate(ControllerBrain owner)
    {
        ClearRows();

        if (entryContainer == null || entryPrefab == null)
        {
            Debug.LogWarning($"[SpellbookPanelView] {name} needs an entry container and row prefab");
            return;
        }

        var abilities = KnownAbilities(owner);
        abilities.Sort((a, b) => string.Compare(a.abilityName, b.abilityName, StringComparison.OrdinalIgnoreCase));

        foreach (var ability in abilities)
        {
            var row = Instantiate(entryPrefab, entryContainer).GetComponent<SpellbookRow>();

            if (row == null)
            {
                Debug.LogError($"[SpellbookPanelView] {entryPrefab.name} has no SpellbookRow");
                return;
            }

            row.Initialize(ability);
            rows.Add(row);
        }

        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    private static List<AbilityDefinition> KnownAbilities(ControllerBrain owner)
    {
        var result = new List<AbilityDefinition>();
        var abilities = owner.GetModule<RuntimeAbilityManager>();

        if (abilities == null) return result;

        foreach (var instance in abilities.GetAllInstances())
        {
            if (instance?.definition != null) result.Add(instance.definition);
        }

        return result;
    }

    private void ClearRows()
    {
        foreach (var row in rows)
        {
            if (row != null) Destroy(row.gameObject);
        }

        rows.Clear();
    }
}
