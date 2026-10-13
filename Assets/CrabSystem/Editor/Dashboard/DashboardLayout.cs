using UnityEngine.UIElements;

// The standard tab body: list | editor | checks. Any column may be null and is left out. Below the window's narrow
// width the checks column drops under the editor (Dashboard.uss, .narrow).

namespace CrabSystem
{
    public static class DashboardLayout
    {
        public static VisualElement Columns(VisualElement list, VisualElement editor, VisualElement checks)
        {
            var row = new VisualElement();
            row.AddToClassList("columns");
            AddPane(row, list, "pane-list");

            var main = new VisualElement();
            main.AddToClassList("columns-main");
            row.Add(main);
            AddPane(main, editor, "pane-editor");
            AddPane(main, checks, "pane-checks");
            return row;
        }

        public static Label Heading(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pane-heading");
            return label;
        }

        public static Label Note(string text)
        {
            var label = new Label(text);
            label.AddToClassList("pane-note");
            return label;
        }

        static void AddPane(VisualElement parent, VisualElement content, string paneClass)
        {
            if (content == null) return;

            var pane = new VisualElement();
            pane.AddToClassList("pane");
            pane.AddToClassList(paneClass);
            pane.Add(content);
            parent.Add(pane);
        }
    }
}
