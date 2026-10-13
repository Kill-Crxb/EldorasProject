using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// Build → Health. Every project check in one list: the CrabSystem Standard scan (Validate), ids that no asset
// defines and assets with missing scripts (Validate Ids), character prefab lineage, and leftovers in _Staging.
// Code checks are quick; asset checks load every prefab and ScriptableObject, so they run only on request.
// Each check replaces its own categories, so running one leaves the others' results in place.

namespace CrabSystem
{
    public class HealthTab : IDashboardTab
    {
        const string Standard = "Standard";
        const string Ids = "Ids";
        const string MissingScripts = "Missing scripts";
        const string Lineage = "Prefab lineage";
        const string Staging = "Staging";
        const string StagingRoot = "Assets/_Staging";
        const string ThirdPartyRoot = "Assets/ThirdParty/";

        static readonly string[] Categories = { Standard, Ids, MissingScripts, Lineage, Staging };

        public string Title => "Health";
        public DashboardGroup Group => DashboardGroup.Build;
        public int Order => 0;

        readonly Dictionary<string, List<DashboardIssue>> byCategory = new();
        readonly Dictionary<string, string> lastRun = new();
        DashboardContext context;
        DashboardIssueList issueList;
        VisualElement categoryList;
        Label summary;
        string shown;

        public VisualElement Build(DashboardContext dashboard)
        {
            context = dashboard;

            categoryList = new VisualElement();
            var list = new VisualElement();
            list.Add(DashboardLayout.Heading("Checks"));
            list.Add(categoryList);

            issueList = new DashboardIssueList();

            var checks = new VisualElement();
            checks.Add(DashboardLayout.Heading("Run"));
            checks.Add(DashboardLayout.Note("Code checks scan the CrabSystem scripts and _Staging. Asset checks load every prefab and ScriptableObject, which takes a while."));
            checks.Add(PaneButton("Check code", RunCodeChecks));
            checks.Add(PaneButton("Check assets", RunAssetChecks));
            summary = DashboardLayout.Note("");
            checks.Add(summary);

            ShowCategory(null);
            return DashboardLayout.Columns(list, issueList, checks);
        }

        static Button PaneButton(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.AddToClassList("pane-button");
            return button;
        }

        void RunCodeChecks()
        {
            Replace(Standard, StandardIssues());
            Replace(Staging, StagingIssues());
            Finish("Code checks");
        }

        void RunAssetChecks()
        {
            IdValidator.Result ids = IdValidator.Collect();
            Replace(Ids, IdIssues(ids));
            Replace(MissingScripts, MissingScriptIssues(ids));
            Replace(Lineage, LineageIssues());
            Finish("Asset checks");
        }

        void Replace(string category, List<DashboardIssue> issues)
        {
            byCategory[category] = issues;
            lastRun[category] = DateTime.Now.ToString("HH:mm");
        }

        void Finish(string what)
        {
            ShowCategory(shown);
            context.Log($"Health: {what} done. {Count(IssueSeverity.Error)} errors, {Count(IssueSeverity.Warning)} warnings.");
        }

        int Count(IssueSeverity severity)
        {
            int count = 0;
            foreach (List<DashboardIssue> issues in byCategory.Values)
                foreach (DashboardIssue issue in issues)
                    if (issue.Severity == severity) count++;
            return count;
        }

        void RefreshCategories()
        {
            categoryList.Clear();
            categoryList.Add(CategoryButton(null, $"All  ({Total()})"));
            foreach (string category in Categories)
                categoryList.Add(CategoryButton(category, CategoryLabel(category)));

            summary.text = $"{Count(IssueSeverity.Error)} errors · {Count(IssueSeverity.Warning)} warnings · {Count(IssueSeverity.Info)} notes";
        }

        string CategoryLabel(string category)
        {
            if (!byCategory.TryGetValue(category, out List<DashboardIssue> issues)) return $"{category}  (not run)";
            return $"{category}  ({issues.Count}, {lastRun[category]})";
        }

        int Total()
        {
            int total = 0;
            foreach (List<DashboardIssue> issues in byCategory.Values) total += issues.Count;
            return total;
        }

        Button CategoryButton(string category, string label)
        {
            var button = new Button(() => ShowCategory(category)) { text = label };
            button.AddToClassList("list-entry");
            button.EnableInClassList("selected", category == shown);
            return button;
        }

        void ShowCategory(string category)
        {
            shown = category;
            var issues = new List<DashboardIssue>();
            foreach (var pair in byCategory)
                if (category == null || pair.Key == category) issues.AddRange(pair.Value);

            issues.Sort(BySeverity);
            issueList.Show(issues);
            RefreshCategories();
        }

        static int BySeverity(DashboardIssue a, DashboardIssue b) => a.Severity.CompareTo(b.Severity);

        static List<DashboardIssue> StandardIssues()
        {
            var issues = new List<DashboardIssue>();
            foreach (CrabSystemValidator.Hit hit in CrabSystemValidator.Scan())
                issues.Add(new DashboardIssue { Severity = IssueSeverity.Warning, Category = Standard, Message = $"{hit.Rule}: {hit.Text}", Path = hit.Path, Line = hit.Line });
            return issues;
        }

        static List<DashboardIssue> IdIssues(IdValidator.Result result)
        {
            var issues = new List<DashboardIssue>();
            foreach (IdValidator.Finding finding in result.Findings)
            {
                string path = finding.Asset != null ? AssetDatabase.GetAssetPath(finding.Asset) : null;
                issues.Add(new DashboardIssue { Severity = IssueSeverity.Error, Category = Ids, Message = $"{finding.Kind} '{finding.Value}' {finding.Note}: {finding.Where}", Path = path });
            }
            return issues;
        }

        static List<DashboardIssue> MissingScriptIssues(IdValidator.Result result)
        {
            var issues = new List<DashboardIssue>();
            foreach (string path in result.MissingScripts)
            {
                bool theirs = IdValidator.IsThirdParty(path);
                issues.Add(new DashboardIssue { Severity = theirs ? IssueSeverity.Info : IssueSeverity.Error, Category = MissingScripts, Message = theirs ? $"Missing script (third-party pack): {path}" : $"Missing script: {path}", Path = path });
            }
            return issues;
        }

        // Characters should be variants of a base so a base change reaches them all (A2). Roots are listed so a
        // stray hand-made character shows up; a variant that removes one of its base's components is a warning.
        static List<DashboardIssue> LineageIssues()
        {
            var issues = new List<DashboardIssue>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(ThirdPartyRoot)) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || prefab.GetComponent<ControllerBrain>() == null) continue;

                AddLineage(prefab, path, issues);
            }
            return issues;
        }

        static void AddLineage(GameObject prefab, string path, List<DashboardIssue> issues)
        {
            if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.Variant)
            {
                issues.Add(new DashboardIssue { Severity = IssueSeverity.Info, Category = Lineage, Message = $"Lineage root (not a variant): {prefab.name}", Path = path });
                return;
            }

            foreach (RemovedComponent removed in PrefabUtility.GetRemovedComponents(prefab))
            {
                string what = removed.assetComponent != null ? removed.assetComponent.GetType().Name : "a component";
                issues.Add(new DashboardIssue { Severity = IssueSeverity.Warning, Category = Lineage, Message = $"{prefab.name} removes {what} from its base", Path = path });
            }
        }

        // _Staging holds work that hasn't graduated; anything there besides the README is worth a look.
        static List<DashboardIssue> StagingIssues()
        {
            var issues = new List<DashboardIssue>();
            if (!Directory.Exists(StagingRoot)) return issues;

            foreach (string file in Directory.GetFiles(StagingRoot, "*", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (path.EndsWith(".meta") || path.EndsWith("/README.md")) continue;
                issues.Add(new DashboardIssue { Severity = IssueSeverity.Info, Category = Staging, Message = $"In staging: {path.Substring(StagingRoot.Length + 1)}", Path = path });
            }
            return issues;
        }
    }
}
