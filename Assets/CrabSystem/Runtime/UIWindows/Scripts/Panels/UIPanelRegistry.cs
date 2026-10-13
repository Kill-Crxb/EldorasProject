using System.Collections.Generic;

// Lookup for the live panels. Keeps callers from hunting for singletons or
// walking the hierarchy to find a side.

namespace CrabSystem
{
    public static class UIPanelRegistry
    {
        private static readonly Dictionary<PanelSide, UIPanel> panels = new();

        public static UIPanel Left => Get(PanelSide.Left);
        public static UIPanel Right => Get(PanelSide.Right);

        // True while any panel is showing, so gameplay knows to release the cursor.
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

        // Other UI that holds the cursor (UniversalWindowManager's windows) registers a check here, so gameplay asks
        // one place whether to free the cursor.
        private static readonly List<System.Func<bool>> cursorClaims = new();

        public static bool CursorWanted
        {
            get
            {
                if (AnyOpen) return true;
                foreach (var claim in cursorClaims)
                    if (claim()) return true;
                return false;
            }
        }

        public static void RegisterCursorClaim(System.Func<bool> claim) => cursorClaims.Add(claim);

        public static void UnregisterCursorClaim(System.Func<bool> claim) => cursorClaims.Remove(claim);

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
}
