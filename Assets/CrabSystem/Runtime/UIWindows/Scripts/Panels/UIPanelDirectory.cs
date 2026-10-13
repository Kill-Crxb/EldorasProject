using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The button bar that decides what the bottom window is showing. A page is a set of
/// slots: Bag shows inventory and stats together, Spellbook shows the spellbook alone.
///
/// The directory owns these slots outright — they are deliberately NOT listed in
/// UIPanel.slots, because the panel activates every slot it owns when it opens and the
/// two would fight over the same objects.
/// </summary>
public class UIPanelDirectory : MonoBehaviour
{
    [Serializable]
    public class Page
    {
        [Tooltip("Stable id, e.g. 'bag', 'spellbook'.")]
        public string pageId;

        public Button button;

        [Tooltip("Slots visible on this page. Everything else the directory knows about is hidden.")]
        public UIPanelSlot[] slots;

        [Tooltip("Optional. Tinted when this page is the active one.")]
        public Graphic highlight;
    }

    [Header("Pages")]
    [SerializeField] private List<Page> pages = new();

    [Tooltip("Page shown when the panel opens. Falls back to the first page.")]
    [SerializeField] private string defaultPageId;

    [Header("Selected State")]
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color unselectedColor = new Color(1f, 1f, 1f, 0.35f);

    public string CurrentPageId { get; private set; }

    public event Action<string> PageChanged;

    private void OnEnable()
    {
        foreach (var page in pages)
        {
            if (page.button == null) continue;

            var captured = page;
            page.button.onClick.AddListener(() => Show(captured.pageId));
        }

        Show(string.IsNullOrEmpty(defaultPageId) && pages.Count > 0 ? pages[0].pageId : defaultPageId);
    }

    private void OnDisable()
    {
        foreach (var page in pages)
        {
            if (page.button != null) page.button.onClick.RemoveAllListeners();
        }
    }

    public void Show(string pageId)
    {
        var target = Find(pageId);

        if (target == null)
        {
            Debug.LogWarning($"[UIPanelDirectory] no page called '{pageId}' on {name}");
            return;
        }

        HideEverySlot();

        foreach (var slot in target.slots)
        {
            if (slot == null) continue;

            slot.gameObject.SetActive(true);
            slot.ShowDefault();
        }

        CurrentPageId = target.pageId;
        RefreshHighlights();
        PageChanged?.Invoke(CurrentPageId);
    }

    private Page Find(string pageId)
    {
        if (string.IsNullOrEmpty(pageId)) return null;

        foreach (var page in pages)
        {
            if (page.pageId == pageId) return page;
        }

        return null;
    }

    // Every slot any page mentions, so a slot dropped from a page is still hidden properly.
    private void HideEverySlot()
    {
        foreach (var page in pages)
        {
            foreach (var slot in page.slots)
            {
                if (slot == null) continue;

                slot.HideAll();
                slot.gameObject.SetActive(false);
            }
        }
    }

    private void RefreshHighlights()
    {
        foreach (var page in pages)
        {
            if (page.highlight == null) continue;

            page.highlight.color = page.pageId == CurrentPageId ? selectedColor : unselectedColor;
        }
    }
}
