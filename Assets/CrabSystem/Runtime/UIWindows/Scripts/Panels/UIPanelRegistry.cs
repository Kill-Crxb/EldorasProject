using System.Collections.Generic;

/// <summary>
/// Lookup for the live panels. Keeps callers from hunting for singletons or
/// walking the hierarchy to find a side.
/// </summary>
public static class UIPanelRegistry
{
    private static readonly Dictionary<PanelSide, UIPanel> panels = new();

    public static UIPanel Left => Get(PanelSide.Left);
    public static UIPanel Right => Get(PanelSide.Right);

    /// <summary>True while any panel is showing, so gameplay knows to release the cursor.</summary>
    public static bool AnyOpen
    {
        get
        {
            foreach (var panel in panels.Values)
            {
                if (panel != null && panel.IsOpen) return true;
            }

            return false;
        }
    }

    public static UIPanel Get(PanelSide side) => panels.GetValueOrDefault(side);

    public static void Register(UIPanel panel)
    {
        if (panel != null) panels[panel.Side] = panel;
    }

    public static void Unregister(UIPanel panel)
    {
        if (panel == null) return;
        if (panels.TryGetValue(panel.Side, out var found) && found == panel)
            panels.Remove(panel.Side);
    }

    public static void CloseAll()
    {
        foreach (var panel in panels.Values)
            panel?.Close();
    }
}
