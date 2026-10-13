using UnityEditor;
using UnityEngine;

// One finding from a check, shown in a DashboardIssueList. It names its asset by path, so a list of thousands holds
// no loaded objects; clicking loads it.

namespace CrabSystem
{
    public enum IssueSeverity
    {
        Error,
        Warning,
        Info
    }

    public class DashboardIssue
    {
        public IssueSeverity Severity;
        public string Category;
        public string Message;
        public string Path;
        public int Line;

        public string Location => Line > 0 ? $"{Path}:{Line}" : Path;

        public Object Load() => string.IsNullOrEmpty(Path) ? null : AssetDatabase.LoadMainAssetAtPath(Path);
    }
}
