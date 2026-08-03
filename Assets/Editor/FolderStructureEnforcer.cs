using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class FolderStructureEnforcer : EditorWindow
{
    private string targetRoot = "Assets/NinjaGame/Scripts";
    private Vector2 scroll;
    private List<MoveOperation> preview = new List<MoveOperation>();
    private bool previewGenerated = false;
    private bool hasConflicts = false;

    private struct MoveOperation
    {
        public string fileName;
        public string currentPath;
        public string destinationPath;
        public bool alreadyCorrect;
        public bool notFound;
    }

    [MenuItem("NinjaGame/Tools/Folder Structure Enforcer")]
    public static void Open()
    {
        var window = GetWindow<FolderStructureEnforcer>("Folder Structure Enforcer");
        window.minSize = new Vector2(700, 500);
        window.targetRoot = FolderStructureValidator.GetRootFolder();
        window.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("NinjaGame — Folder Structure Enforcer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Creates the catalogue folder structure and moves all mapped scripts.", EditorStyles.miniLabel);
        EditorGUILayout.Space(6);

        // Root selector
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Target Root:", GUILayout.Width(90));
        targetRoot = EditorGUILayout.TextField(targetRoot);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string selected = EditorUtility.OpenFolderPanel("Select Target Root Folder", targetRoot, "");
            if (!string.IsNullOrEmpty(selected))
            {
                string projectPath = Application.dataPath.Replace("/Assets", "");
                if (selected.StartsWith(projectPath))
                    selected = selected.Substring(projectPath.Length + 1);
                targetRoot = selected;
                previewGenerated = false;
                preview.Clear();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Changes", GUILayout.Height(30)))
            GeneratePreview();

        GUI.enabled = previewGenerated && !hasConflicts;
        if (GUILayout.Button("Apply — Create Folders & Move Scripts", GUILayout.Height(30)))
            Apply();
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (!previewGenerated)
        {
            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox("Click 'Preview Changes' to see what will be created and moved before anything is touched.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(8);

        // Summary
        int toMove = preview.Count(o => !o.alreadyCorrect && !o.notFound);
        int correct = preview.Count(o => o.alreadyCorrect);
        int missing = preview.Count(o => o.notFound);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"To move: {toMove}", EditorStyles.boldLabel, GUILayout.Width(100));
        EditorGUILayout.LabelField($"Already correct: {correct}", GUILayout.Width(130));
        EditorGUILayout.LabelField($"Not found in project: {missing}", GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();

        if (missing > 0)
            EditorGUILayout.HelpBox($"{missing} script(s) from the catalogue were not found anywhere in the project. They will be skipped.", MessageType.Warning);

        EditorGUILayout.Space(6);

        // Preview list — scroll view with guaranteed matching End
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawPreviewList();
        EditorGUILayout.EndScrollView();
    }

    void DrawPreviewList()
    {
        string lastCategory = null;
        foreach (var op in preview.OrderBy(o => GetCategory(o.destinationPath)).ThenBy(o => o.fileName))
        {
            string category = GetCategory(op.destinationPath);
            if (category != lastCategory)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                lastCategory = category;
            }

            EditorGUILayout.BeginHorizontal();

            if (op.notFound)
            {
                GUI.color = Color.gray;
                EditorGUILayout.LabelField($"  x  {op.fileName}", GUILayout.Width(260));
                EditorGUILayout.LabelField("not found in project", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else if (op.alreadyCorrect)
            {
                GUI.color = new Color(0.5f, 0.9f, 0.5f);
                EditorGUILayout.LabelField($"  ok  {op.fileName}", GUILayout.Width(260));
                EditorGUILayout.LabelField("already correct", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = new Color(1f, 0.85f, 0.4f);
                EditorGUILayout.LabelField($"  ->  {op.fileName}", GUILayout.Width(260));
                GUI.color = Color.white;
                EditorGUILayout.LabelField(ShortenPath(op.currentPath), EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField("->", GUILayout.Width(16));
                EditorGUILayout.LabelField(ShortenPath(op.destinationPath), EditorStyles.miniLabel);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    void GeneratePreview()
    {
        preview.Clear();
        hasConflicts = false;

        var mapping = FolderStructureValidator.GetFolderMapping();

        // Build a lookup of every .cs file in the project
        string[] allScripts = AssetDatabase.FindAssets("t:Script")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".cs"))
            .ToArray();

        var scriptLookup = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (string path in allScripts)
        {
            string name = Path.GetFileName(path);
            if (!scriptLookup.ContainsKey(name))
                scriptLookup[name] = path;
        }

        foreach (var kvp in mapping)
        {
            string fileName = kvp.Key;
            string folder = kvp.Value;
            string destPath = $"{targetRoot}/{folder}/{fileName}".Replace("\\", "/");

            var op = new MoveOperation
            {
                fileName = fileName,
                destinationPath = destPath,
            };

            if (scriptLookup.TryGetValue(fileName, out string currentPath))
            {
                op.currentPath = currentPath;
                op.alreadyCorrect = currentPath.Replace("\\", "/") == destPath;
            }
            else
            {
                op.notFound = true;
            }

            preview.Add(op);
        }

        previewGenerated = true;
        Repaint();
    }

    void Apply()
    {
        if (!EditorUtility.DisplayDialog(
            "Confirm Folder Structure Enforcement",
            $"This will:\n• Create all category folders under '{targetRoot}'\n• Move all mapped scripts to their correct folders\n\nUnity will recompile after each batch. This cannot be undone easily.\n\nProceed?",
            "Yes, enforce it",
            "Cancel"))
            return;

        // Save root to EditorPrefs so Validator stays in sync
        EditorPrefs.SetString("NinjaGame_ScriptsRoot", targetRoot);

        // Create all folders first
        CreateFolderStructure();

        // Move scripts
        int moved = 0, skipped = 0, failed = 0;
        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var op in preview)
            {
                if (op.notFound || op.alreadyCorrect) { skipped++; continue; }

                string error = AssetDatabase.MoveAsset(op.currentPath, op.destinationPath);
                if (string.IsNullOrEmpty(error))
                {
                    moved++;
                }
                else
                {
                    Debug.LogError($"[FolderStructureEnforcer] Failed to move '{op.fileName}': {error}");
                    failed++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[FolderStructureEnforcer] Done — {moved} moved, {skipped} skipped, {failed} failed.");
        EditorUtility.DisplayDialog("Done", $"Moved: {moved}\nSkipped (correct or missing): {skipped}\nFailed: {failed}\n\nCheck the Console for any errors.", "OK");

        previewGenerated = false;
        preview.Clear();
    }

    void CreateFolderStructure()
    {
        var folders = FolderStructureValidator.GetFolderMapping()
            .Values
            .Distinct()
            .OrderBy(f => f);

        // Ensure root exists
        EnsureFolderPath(targetRoot);

        foreach (string folder in folders)
            EnsureFolderPath($"{targetRoot}/{folder}");

        AssetDatabase.Refresh();
    }

    static void EnsureFolderPath(string path)
    {
        path = path.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folderName = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderPath(parent);

        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            AssetDatabase.CreateFolder(parent, folderName);
    }

    static string GetCategory(string destinationPath)
    {
        // Extract the category folder name from the destination path
        var parts = destinationPath.Replace("\\", "/").Split('/');
        // path is root/Category/FileName — category is second-to-last
        return parts.Length >= 2 ? parts[parts.Length - 2] : "Unknown";
    }

    static string ShortenPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        // Trim leading Assets/ for readability
        return path.StartsWith("Assets/") ? path.Substring(7) : path;
    }
}