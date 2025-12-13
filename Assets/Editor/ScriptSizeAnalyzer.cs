using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class ScriptSizeAnalyzer : EditorWindow
{
    private string targetFolder = "Assets/";
    private Vector2 scrollPosition;
    private List<ScriptInfo> scriptList = new List<ScriptInfo>();
    private bool includeSubfolders = true;
    private long totalSize = 0;
    private int totalScripts = 0;

    private class ScriptInfo
    {
        public string path;
        public string name;
        public long sizeInBytes;
        public int lineCount;
        public int codeLineCount;
    }

    [MenuItem("Tools/Script Size Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<ScriptSizeAnalyzer>("Script Size Analyzer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Script Size Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Folder:", GUILayout.Width(100));
        targetFolder = EditorGUILayout.TextField(targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder", targetFolder, "");
            if (!string.IsNullOrEmpty(path))
            {
                // Convert absolute path to relative Assets path
                if (path.StartsWith(Application.dataPath))
                {
                    targetFolder = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);

        EditorGUILayout.Space();

        if (GUILayout.Button("Analyze Scripts", GUILayout.Height(30)))
        {
            AnalyzeScripts();
        }

        EditorGUILayout.Space();

        if (scriptList.Count > 0)
        {
            EditorGUILayout.LabelField($"Total Scripts: {totalScripts} | Total Size: {FormatFileSize(totalSize)}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Column headers
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Script Name", EditorStyles.toolbarButton, GUILayout.Width(250));
            GUILayout.Label("Size", EditorStyles.toolbarButton, GUILayout.Width(100));
            GUILayout.Label("Lines", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("Code", EditorStyles.toolbarButton, GUILayout.Width(60));
            GUILayout.Label("Path", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var script in scriptList)
            {
                EditorGUILayout.BeginHorizontal();

                // Script name (clickable)
                if (GUILayout.Button(script.name, EditorStyles.label, GUILayout.Width(250)))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(script.path);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }

                EditorGUILayout.LabelField(FormatFileSize(script.sizeInBytes), GUILayout.Width(100));
                EditorGUILayout.LabelField(script.lineCount.ToString(), GUILayout.Width(60));
                EditorGUILayout.LabelField(script.codeLineCount.ToString(), GUILayout.Width(60));
                EditorGUILayout.LabelField(script.path, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
        else
        {
            EditorGUILayout.HelpBox("Click 'Analyze Scripts' to scan for C# scripts in the selected folder.", MessageType.Info);
        }
    }

    private void AnalyzeScripts()
    {
        scriptList.Clear();
        totalSize = 0;
        totalScripts = 0;

        if (!Directory.Exists(targetFolder))
        {
            EditorUtility.DisplayDialog("Error", $"Folder not found: {targetFolder}", "OK");
            return;
        }

        SearchOption searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string[] scriptFiles = Directory.GetFiles(targetFolder, "*.cs", searchOption);

        foreach (string filePath in scriptFiles)
        {
            FileInfo fileInfo = new FileInfo(filePath);

            // Count lines
            int lineCount = 0;
            int codeLineCount = 0;
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                lineCount = lines.Length;

                // Count code lines (non-empty, non-comment lines)
                bool inMultiLineComment = false;
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    // Skip empty lines
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    // Handle multi-line comments
                    if (trimmed.StartsWith("/*"))
                    {
                        inMultiLineComment = true;
                    }

                    if (inMultiLineComment)
                    {
                        if (trimmed.EndsWith("*/") || trimmed.Contains("*/"))
                        {
                            inMultiLineComment = false;
                        }
                        continue;
                    }

                    // Skip single-line comments
                    if (trimmed.StartsWith("//"))
                        continue;

                    // This is a code line
                    codeLineCount++;
                }
            }
            catch
            {
                lineCount = 0;
                codeLineCount = 0;
            }

            scriptList.Add(new ScriptInfo
            {
                path = filePath.Replace("\\", "/"),
                name = Path.GetFileName(filePath),
                sizeInBytes = fileInfo.Length,
                lineCount = lineCount,
                codeLineCount = codeLineCount
            });

            totalSize += fileInfo.Length;
            totalScripts++;
        }

        // Sort by size (largest first)
        scriptList = scriptList.OrderByDescending(s => s.sizeInBytes).ToList();

        Debug.Log($"Analysis complete: Found {totalScripts} scripts with total size of {FormatFileSize(totalSize)}");
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}