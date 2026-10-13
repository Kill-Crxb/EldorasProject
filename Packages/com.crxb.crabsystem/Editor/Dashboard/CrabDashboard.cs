using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// Tools → CrabSystem → Dashboard. One window for every designer-facing editor tool, meant to sit undocked on a
// second monitor (claude/Crab_Dashboard.md). The rail on the left lists every IDashboardTab, grouped Build ·
// Content · Play; the body shows the selected tab; the strip at the bottom is the log. The last tab is remembered.
// Content tabs go read-only in play mode, because their assets shouldn't change under a running game.

namespace CrabSystem
{
    public class CrabDashboard : EditorWindow
    {
        const string TabPref = "CrabDashboard.Tab";
        const string StyleFile = "Dashboard.uss";
        const float NarrowWidth = 1600f;
        const int LogLines = 20;

        readonly List<IDashboardTab> tabs = new();
        readonly Dictionary<IDashboardTab, Button> railButtons = new();
        readonly Dictionary<IDashboardTab, VisualElement> views = new();
        readonly List<string> logHistory = new();

        VisualElement rail;
        VisualElement content;
        Label tabTitle;
        Label logStrip;
        IDashboardTab current;
        DashboardContext context;

        [MenuItem("Tools/CrabSystem/Dashboard", priority = 0)]
        static void Open()
        {
            var window = GetWindow<CrabDashboard>("Crab Dashboard");
            window.minSize = new Vector2(900, 500);
        }

        // A floating window dragged off every monitor can't be grabbed again, and Unity remembers where it was.
        // This puts it back in the middle of the main editor window.
        [MenuItem("Tools/CrabSystem/Dashboard (Bring Back)", priority = 1)]
        static void BringBack()
        {
            var window = GetWindow<CrabDashboard>("Crab Dashboard");
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            Vector2 size = Vector2.Min(Vector2.Max(window.position.size, window.minSize), main.size);
            window.position = new Rect(main.center - size / 2f, size);
        }

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            foreach (IDashboardTab tab in tabs)
                (tab as IDisposable)?.Dispose();
        }

        void CreateGUI()
        {
            AddStyle();
            rootVisualElement.AddToClassList("crab-dashboard");
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnResized);

            var frame = new VisualElement();
            frame.AddToClassList("frame");
            rootVisualElement.Add(frame);

            rail = new ScrollView();
            rail.AddToClassList("rail");
            frame.Add(rail);

            var body = new VisualElement();
            body.AddToClassList("body");
            frame.Add(body);

            tabTitle = new Label();
            tabTitle.AddToClassList("tab-title");
            body.Add(tabTitle);

            content = new VisualElement();
            content.AddToClassList("tab-content");
            body.Add(content);

            logStrip = new Label("Ready.");
            logStrip.AddToClassList("log-strip");
            rootVisualElement.Add(logStrip);

            context = new DashboardContext(WriteLog);
            FindTabs();
            BuildRail();
            Select(SavedTab());
        }

        // The stylesheet sits next to this script, so the window finds it whether CrabSystem is in Assets or Packages.
        void AddStyle()
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)));
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{folder}/{StyleFile}".Replace('\\', '/'));
            if (style == null)
            {
                Debug.LogError($"[CrabDashboard] No {StyleFile} next to CrabDashboard.cs.", this);
                return;
            }
            rootVisualElement.styleSheets.Add(style);
        }

        void FindTabs()
        {
            tabs.Clear();
            views.Clear();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IDashboardTab>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null) continue;
                tabs.Add((IDashboardTab)Activator.CreateInstance(type));
            }
            tabs.Sort(CompareTabs);
        }

        static int CompareTabs(IDashboardTab a, IDashboardTab b)
        {
            int byGroup = a.Group.CompareTo(b.Group);
            return byGroup != 0 ? byGroup : a.Order.CompareTo(b.Order);
        }

        void BuildRail()
        {
            rail.Clear();
            railButtons.Clear();
            DashboardGroup? group = null;

            foreach (IDashboardTab tab in tabs)
            {
                if (tab.Group != group)
                {
                    group = tab.Group;
                    var heading = new Label(tab.Group.ToString());
                    heading.AddToClassList("rail-group");
                    rail.Add(heading);
                }

                IDashboardTab target = tab;
                var button = new Button(() => Select(target)) { text = tab.Title };
                button.AddToClassList("rail-tab");
                rail.Add(button);
                railButtons[tab] = button;
            }
        }

        IDashboardTab SavedTab()
        {
            string saved = EditorPrefs.GetString(TabPref, "");
            foreach (IDashboardTab tab in tabs)
                if (tab.GetType().FullName == saved) return tab;

            return tabs.Count > 0 ? tabs[0] : null;
        }

        void Select(IDashboardTab tab)
        {
            if (tab == null) return;

            current = tab;
            EditorPrefs.SetString(TabPref, tab.GetType().FullName);
            foreach (var pair in railButtons)
                pair.Value.EnableInClassList("selected", pair.Key == tab);

            tabTitle.text = tab.Title;
            content.Clear();
            if (!views.TryGetValue(tab, out VisualElement view))
            {
                view = tab.Build(context);
                views[tab] = view;
            }
            content.Add(view);
            ApplyPlayMode();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            ApplyPlayMode();
        }

        void ApplyPlayMode()
        {
            if (current == null || content == null) return;

            bool locked = current.Group == DashboardGroup.Content && EditorApplication.isPlayingOrWillChangePlaymode;
            content.SetEnabled(!locked);
            tabTitle.text = locked ? $"{current.Title}  (read-only in play mode)" : current.Title;
        }

        void OnResized(GeometryChangedEvent change)
        {
            rootVisualElement.EnableInClassList("narrow", change.newRect.width < NarrowWidth);
        }

        void WriteLog(string message)
        {
            string line = $"{DateTime.Now:HH:mm:ss}  {message}";
            logHistory.Add(line);
            if (logHistory.Count > LogLines) logHistory.RemoveAt(0);

            logStrip.text = line;
            logStrip.tooltip = string.Join("\n", logHistory);
        }
    }
}
