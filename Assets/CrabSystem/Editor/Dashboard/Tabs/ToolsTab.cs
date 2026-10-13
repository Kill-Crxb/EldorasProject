using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

// Build → Tools. Every editor command the project defines, found from its [MenuItem]s and grouped by area, with a
// Run button each. Only our own assemblies are listed (CrabSystem, CrabSystem.Editor and the game's), not Unity's
// or the asset-store packs'. Context and Assets/GameObject menu entries are left out: they need a selection.

namespace CrabSystem
{
    public class ToolsTab : IDashboardTab
    {
        const string AllAreas = "All";

        static readonly string[] OurAssemblies = { "CrabSystem", "CrabSystem.Editor", "Assembly-CSharp", "Assembly-CSharp-Editor" };
        static readonly string[] SelectionMenus = { "CONTEXT/", "Assets/", "GameObject/" };
        static readonly char[] ShortcutMarks = { '%', '#', '&', '_' };

        struct Command
        {
            public string Path;
            public string Area;
            public string Name;
            public string Owner;
        }

        public string Title => "Tools";
        public DashboardGroup Group => DashboardGroup.Build;
        public int Order => 10;

        readonly List<Command> commands = new();
        readonly SortedDictionary<string, int> areaCounts = new();
        DashboardContext context;
        VisualElement areaList;
        ScrollView commandList;
        string area = AllAreas;
        string filter = "";

        public VisualElement Build(DashboardContext dashboard)
        {
            context = dashboard;
            Gather();

            areaList = new VisualElement();
            var list = new VisualElement();
            list.Add(DashboardLayout.Heading("Areas"));
            list.Add(areaList);

            var editor = new VisualElement();
            editor.style.flexGrow = 1;
            var search = new ToolbarSearchField();
            search.AddToClassList("issue-search");
            search.RegisterValueChangedCallback(OnSearch);
            editor.Add(search);
            commandList = new ScrollView();
            commandList.style.flexGrow = 1;
            editor.Add(commandList);

            ShowArea(AllAreas);
            return DashboardLayout.Columns(list, editor, null);
        }

        void Gather()
        {
            commands.Clear();
            areaCounts.Clear();
            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<MenuItem>())
            {
                if (Array.IndexOf(OurAssemblies, method.DeclaringType.Assembly.GetName().Name) < 0) continue;
                foreach (MenuItem item in method.GetCustomAttributes<MenuItem>())
                    AddCommand(item, method);
            }
            commands.Sort(ByPath);
        }

        void AddCommand(MenuItem item, MethodInfo method)
        {
            if (item.validate || IsSelectionMenu(item.menuItem)) return;

            string path = WithoutShortcut(item.menuItem);
            string[] parts = path.Split('/');
            if (parts.Length < 2) return;

            string commandArea = parts[0] == "Tools" && parts.Length > 2 ? parts[1] : parts[0];
            commands.Add(new Command { Path = path, Area = commandArea, Name = parts[parts.Length - 1], Owner = method.DeclaringType.Name });
            areaCounts.TryGetValue(commandArea, out int count);
            areaCounts[commandArea] = count + 1;
        }

        static bool IsSelectionMenu(string path)
        {
            foreach (string prefix in SelectionMenus)
                if (path.StartsWith(prefix)) return true;
            return false;
        }

        // "Tools/X %#d" → "Tools/X": ExecuteMenuItem wants the path without its shortcut.
        static string WithoutShortcut(string path)
        {
            int space = path.LastIndexOf(' ');
            if (space < 0 || space == path.Length - 1) return path;
            return Array.IndexOf(ShortcutMarks, path[space + 1]) >= 0 ? path.Substring(0, space) : path;
        }

        static int ByPath(Command a, Command b) => string.CompareOrdinal(a.Path, b.Path);

        void OnSearch(ChangeEvent<string> change)
        {
            filter = change.newValue ?? "";
            ShowCommands();
        }

        void ShowArea(string picked)
        {
            area = picked;
            areaList.Clear();
            areaList.Add(AreaButton(AllAreas, commands.Count));
            foreach (var pair in areaCounts)
                areaList.Add(AreaButton(pair.Key, pair.Value));
            ShowCommands();
        }

        Button AreaButton(string name, int count)
        {
            var button = new Button(() => ShowArea(name)) { text = $"{name}  ({count})" };
            button.AddToClassList("list-entry");
            button.EnableInClassList("selected", name == area);
            return button;
        }

        void ShowCommands()
        {
            commandList.Clear();
            foreach (Command command in commands)
            {
                if (area != AllAreas && command.Area != area) continue;
                if (filter.Length > 0 && command.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                commandList.Add(CommandRow(command));
            }
        }

        VisualElement CommandRow(Command command)
        {
            var row = new VisualElement();
            row.AddToClassList("command-row");

            string path = command.Path;
            var run = new Button(() => Run(path)) { text = "Run" };
            run.AddToClassList("command-run");
            row.Add(run);

            var name = new Label(command.Path);
            name.AddToClassList("command-name");
            row.Add(name);

            var owner = new Label(command.Owner);
            owner.AddToClassList("command-owner");
            row.Add(owner);
            return row;
        }

        void Run(string path)
        {
            bool ran = EditorApplication.ExecuteMenuItem(path);
            context.Log(ran ? $"Ran {path}" : $"Couldn't run {path}");
        }
    }
}
