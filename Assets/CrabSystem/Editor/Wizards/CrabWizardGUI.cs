using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Shared chrome for the CrabSystem wizards. Kept deliberately small — a handful of layout helpers
// and one validation box, so every wizard reports problems the same way instead of each inventing
// its own HelpBox soup.
public static class CrabWizardGUI
{
    public class Issues
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public bool Blocked => Errors.Count > 0;

        public void Error(string message) => Errors.Add(message);
        public void Warn(string message) => Warnings.Add(message);

        public void Require(bool condition, string message)
        {
            if (!condition) Error(message);
        }
    }

    public static void Title(string title, string subtitle)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.largeLabel);

        if (!string.IsNullOrEmpty(subtitle))
            EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);

        Rule();
    }

    public static void Section(string heading)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
    }

    public static void Rule()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
        EditorGUILayout.Space(2);
    }

    public static void DrawIssues(Issues issues)
    {
        if (issues == null) return;

        foreach (string error in issues.Errors)
            EditorGUILayout.HelpBox(error, MessageType.Error);

        foreach (string warning in issues.Warnings)
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    // A create button that refuses rather than going grey with no explanation — a disabled button
    // with no reason attached is the most annoying thing an editor tool can do.
    public static bool CreateButton(string label, Issues issues)
    {
        using (new EditorGUI.DisabledScope(issues != null && issues.Blocked))
        {
            return GUILayout.Button(label, GUILayout.Height(32));
        }
    }

    public static string FolderField(string label, string path)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        EditorGUILayout.SelectableLabel(path, EditorStyles.textField, GUILayout.Height(18));

        if (GUILayout.Button("…", GUILayout.Width(26)))
        {
            string picked = EditorUtility.OpenFolderPanel("Choose folder", Application.dataPath, "");

            if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                path = "Assets" + picked.Substring(Application.dataPath.Length);
        }

        EditorGUILayout.EndHorizontal();
        return path;
    }

    /// <summary>Creates every missing folder along an Assets-relative path.</summary>
    public static void EnsureFolder(string assetFolder)
    {
        if (string.IsNullOrEmpty(assetFolder) || AssetDatabase.IsValidFolder(assetFolder)) return;

        string[] parts = assetFolder.Split('/');
        string running = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = running + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(running, parts[i]);
            running = next;
        }
    }

    public static string ToItemId(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "";

        System.Text.StringBuilder builder = new System.Text.StringBuilder(displayName.Length);

        foreach (char c in displayName.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');

        while (builder.ToString().Contains("__"))
            builder.Replace("__", "_");

        return builder.ToString().Trim('_');
    }

    public static string ToAssetName(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return "Unnamed";

        string spaced = displayName.Replace('_', ' ').Replace('-', ' ');
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (string word in spaced.Split(' '))
        {
            if (word.Length == 0) continue;
            builder.Append(char.ToUpperInvariant(word[0])).Append(word.Substring(1));
        }

        return builder.ToString();
    }

    public static List<T> LoadAll<T>() where T : Object
    {
        List<T> found = new List<T>();

        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }

        return found;
    }
}
