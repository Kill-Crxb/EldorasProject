using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A titled block of rows. Rows are authored as children in the prefab; this only supplies
/// the heading and hands its rows to the panel. A group may hold stat rows, resource rows
/// or both — the Resources group is the one that mixes them.
/// </summary>
public class StatGroupView : MonoBehaviour
{
    #region Inspector

    [Header("Heading")]
    [SerializeField] private string title = "Stats";
    [SerializeField] private TMP_Text titleLabel;

    [Header("Layout")]
    [Tooltip("Parent the rows live under. Defaults to this transform.")]
    [SerializeField] private RectTransform entryHolder;

    [Header("Behaviour")]
    [Tooltip("Hide the whole group when none of its rows resolve to a live stat or resource.")]
    [SerializeField] private bool hideWhenEmpty = true;

    #endregion

    public string Title => title;

    public void ApplyTitle()
    {
        if (titleLabel != null) titleLabel.SetText(title);
    }

    public void CollectEntries(List<StatEntryView> into)
    {
        var root = entryHolder != null ? entryHolder : (RectTransform)transform;
        root.GetComponentsInChildren(true, entryBuffer);
        into.AddRange(entryBuffer);
    }

    public void CollectResourceEntries(List<ResourceEntryView> into)
    {
        var root = entryHolder != null ? entryHolder : (RectTransform)transform;
        root.GetComponentsInChildren(true, resourceBuffer);
        into.AddRange(resourceBuffer);
    }

    public void SetEmpty(bool isEmpty)
    {
        if (hideWhenEmpty) gameObject.SetActive(!isEmpty);
    }

    private static readonly List<StatEntryView> entryBuffer = new();
    private static readonly List<ResourceEntryView> resourceBuffer = new();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (titleLabel == null) titleLabel = transform.Find("GroupName")?.GetComponent<TMP_Text>();
        if (entryHolder == null) entryHolder = transform.Find("StatGroupHolder") as RectTransform;
    }
#endif
}
