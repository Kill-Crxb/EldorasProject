using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a bar can live on the canvas. Add one to an empty RectTransform positioned where the
/// bar should sit; HotbarHud instantiates the bar's view prefab as a child.
///
/// The id is an enum rather than a string on purpose — the same choice UIPanelRegistry already
/// makes with PanelSide. A bar pointing at an anchor that does not exist is then a missing
/// enum member, caught at compile time, instead of a log at runtime. Adding an anchor costs one
/// line here.
/// </summary>
public enum HotbarAnchorId
{
    BottomCentre,
    BottomLeft,
    BottomRight,
    LeftEdge,
    RightEdge,
    TopLeft,
    TopRight,
}

/// <summary>
/// Lookup for the live anchors. Mirrors UIPanelRegistry — register on enable, drop on disable,
/// so nothing has to walk the hierarchy or hunt for a singleton.
/// </summary>
public static class HotbarAnchorRegistry
{
    private static readonly Dictionary<HotbarAnchorId, HotbarAnchor> anchors = new();

    public static HotbarAnchor Get(HotbarAnchorId id) => anchors.GetValueOrDefault(id);

    public static void Register(HotbarAnchor anchor)
    {
        if (anchor == null) return;

        if (anchors.TryGetValue(anchor.Id, out var existing) && existing != null && existing != anchor)
        {
            Debug.LogError($"[HotbarAnchor] Two anchors claim '{anchor.Id}': '{existing.name}' and " +
                           $"'{anchor.name}'. The second one wins and the first will never be used.");
        }

        anchors[anchor.Id] = anchor;
    }

    public static void Unregister(HotbarAnchor anchor)
    {
        if (anchor == null) return;
        if (anchors.TryGetValue(anchor.Id, out var found) && found == anchor)
            anchors.Remove(anchor.Id);
    }
}

public class HotbarAnchor : MonoBehaviour
{
    [Tooltip("Bars whose ActionBarDefinition names this id spawn here.")]
    [SerializeField] private HotbarAnchorId anchorId = HotbarAnchorId.BottomCentre;

    public HotbarAnchorId Id => anchorId;

    void OnEnable() => HotbarAnchorRegistry.Register(this);
    void OnDisable() => HotbarAnchorRegistry.Unregister(this);
}
