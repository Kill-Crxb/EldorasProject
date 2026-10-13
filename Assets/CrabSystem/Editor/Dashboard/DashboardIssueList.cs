using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// The shared checks list: a search box over a virtualised list, one row per issue. A click pings the asset; a
// double-click opens it (at the line, for a script). Every tab shows its issues through this, so they read the same.

namespace CrabSystem
{
    public class DashboardIssueList : VisualElement
    {
        const float RowHeight = 20f;

        readonly List<DashboardIssue> all = new();
        readonly List<DashboardIssue> shown = new();
        readonly ListView rows;
        readonly Label empty;
        string filter = "";

        public DashboardIssueList()
        {
            AddToClassList("issue-list");

            var search = new ToolbarSearchField();
            search.AddToClassList("issue-search");
            search.RegisterValueChangedCallback(OnSearch);
            Add(search);

            empty = new Label("Nothing to show.");
            empty.AddToClassList("issue-empty");
            Add(empty);

            rows = new ListView(shown, RowHeight, MakeRow, BindRow) { selectionType = SelectionType.Single };
            rows.AddToClassList("issue-rows");
            rows.selectionChanged += OnSelected;
            rows.itemsChosen += OnChosen;
            Add(rows);
        }

        public void Show(IEnumerable<DashboardIssue> issues)
        {
            all.Clear();
            all.AddRange(issues);
            Refilter();
        }

        void OnSearch(ChangeEvent<string> change)
        {
            filter = change.newValue ?? "";
            Refilter();
        }

        void Refilter()
        {
            shown.Clear();
            foreach (DashboardIssue issue in all)
                if (Matches(issue)) shown.Add(issue);

            empty.style.display = shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            rows.Rebuild();
        }

        bool Matches(DashboardIssue issue)
        {
            if (filter.Length == 0) return true;
            if (issue.Message != null && issue.Message.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return issue.Path != null && issue.Path.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("issue-row");

            var badge = new Label { name = "badge" };
            badge.AddToClassList("issue-badge");
            row.Add(badge);

            var text = new Label { name = "text" };
            text.AddToClassList("issue-text");
            row.Add(text);
            return row;
        }

        void BindRow(VisualElement row, int index)
        {
            DashboardIssue issue = shown[index];

            var badge = row.Q<Label>("badge");
            badge.text = issue.Severity.ToString();
            badge.EnableInClassList("error", issue.Severity == IssueSeverity.Error);
            badge.EnableInClassList("warning", issue.Severity == IssueSeverity.Warning);
            badge.EnableInClassList("info", issue.Severity == IssueSeverity.Info);

            row.Q<Label>("text").text = issue.Message;
            row.tooltip = issue.Location;
        }

        static void OnSelected(IEnumerable<object> picked)
        {
            foreach (object item in picked)
            {
                Object asset = ((DashboardIssue)item).Load();
                if (asset != null) EditorGUIUtility.PingObject(asset);
            }
        }

        static void OnChosen(IEnumerable<object> picked)
        {
            foreach (object item in picked)
            {
                var issue = (DashboardIssue)item;
                Object asset = issue.Load();
                if (asset != null) AssetDatabase.OpenAsset(asset, Mathf.Max(issue.Line, 0));
            }
        }
    }
}
