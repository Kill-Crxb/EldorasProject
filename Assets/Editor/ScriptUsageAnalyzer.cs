using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class ScriptUsageAnalyzer : EditorWindow
{
    private string folderPath = "Assets/Scripts";
    private Vector2 scrollPosition;
    private List<ScriptInfo> allScripts = new List<ScriptInfo>();
    private List<ScriptInfo> usedScripts = new List<ScriptInfo>();
    private List<ScriptInfo> unusedScripts = new List<ScriptInfo>();
    private bool showUsed = true;
    private bool showUnused = true;
    private string searchFilter = "";
    private bool hasScanned = false;

    private class ScriptInfo
    {
        public string scriptName;
        public string scriptPath;
        public int usageCount;
        public List<GameObject> usedBy = new List<GameObject>();
    }

    [MenuItem("Tools/Script Usage Analyzer")]
    public static void ShowWindow()
    {
        GetWindow<ScriptUsageAnalyzer>("Script Usage Analyzer");
    }

    void OnGUI()
    {
        GUILayout.Label("Script Usage Analyzer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Folder Path:", GUILayout.Width(80));
        folderPath = EditorGUILayout.TextField(folderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Scripts Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                {
                    folderPath = "Assets" + path.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Scene Hierarchy", GUILayout.Height(30)))
        {
            ScanScripts();
        }

        if (!hasScanned)
        {
            EditorGUILayout.HelpBox("Click 'Scan Scene Hierarchy' to analyze script usage\n\n" +
                "This will scan for MonoBehaviour and ScriptableObject scripts only.\n" +
                "Interfaces, static classes, and data classes are automatically filtered out.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Total Scripts: {allScripts.Count}", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Used: {usedScripts.Count}", new GUIStyle(EditorStyles.boldLabel) { normal = new GUIStyleState { textColor = Color.green } });
        GUILayout.Label($"Unused: {unusedScripts.Count}", new GUIStyle(EditorStyles.boldLabel) { normal = new GUIStyleState { textColor = Color.red } });
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        showUsed = EditorGUILayout.ToggleLeft("Show Used Scripts", showUsed, GUILayout.Width(150));
        showUnused = EditorGUILayout.ToggleLeft("Show Unused Scripts", showUnused, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Search:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            searchFilter = "";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (showUsed && usedScripts.Count > 0)
        {
            GUILayout.Label("Used Scripts", EditorStyles.boldLabel);
            DrawScriptList(usedScripts, Color.green);
            EditorGUILayout.Space();
        }

        if (showUnused && unusedScripts.Count > 0)
        {
            GUILayout.Label("Unused Scripts", EditorStyles.boldLabel);
            DrawScriptList(unusedScripts, Color.red);
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        if (GUILayout.Button("Export to CSV"))
        {
            ExportToCSV();
        }
    }

    void DrawScriptList(List<ScriptInfo> scripts, Color labelColor)
    {
        var filtered = scripts.Where(s => string.IsNullOrEmpty(searchFilter) ||
                                          s.scriptName.ToLower().Contains(searchFilter.ToLower())).ToList();

        foreach (var script in filtered)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.BeginHorizontal();

            GUIStyle nameStyle = new GUIStyle(EditorStyles.label);
            nameStyle.normal.textColor = labelColor;
            nameStyle.fontStyle = FontStyle.Bold;
            GUILayout.Label(script.scriptName, nameStyle, GUILayout.Width(200));

            if (script.usageCount > 0)
            {
                GUILayout.Label($"({script.usageCount} instances)", EditorStyles.miniLabel, GUILayout.Width(100));
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Ping", GUILayout.Width(50)))
            {
                MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script.scriptPath);
                if (monoScript != null)
                {
                    EditorGUIUtility.PingObject(monoScript);
                    Selection.activeObject = monoScript;
                }
            }

            EditorGUILayout.EndHorizontal();

            if (script.usedBy.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var go in script.usedBy.Take(5))
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"└─ {go.name}", EditorStyles.miniLabel);
                    if (GUILayout.Button("Select", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                if (script.usedBy.Count > 5)
                {
                    EditorGUILayout.LabelField($"   ... and {script.usedBy.Count - 5} more", EditorStyles.miniLabel);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }
    }

    void ScanScripts()
    {
        allScripts.Clear();
        usedScripts.Clear();
        unusedScripts.Clear();

        if (!Directory.Exists(folderPath))
        {
            EditorUtility.DisplayDialog("Error", $"Folder not found: {folderPath}", "OK");
            return;
        }

        string[] scriptFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);

        Dictionary<string, ScriptInfo> scriptDict = new Dictionary<string, ScriptInfo>();
        int filteredOut = 0;

        Debug.Log($"[Script Usage Analyzer] Reading {scriptFiles.Length} script files...");

        foreach (string scriptPath in scriptFiles)
        {
            string scriptName = Path.GetFileNameWithoutExtension(scriptPath);
            string relativePath = scriptPath.Replace("\\", "/");

            if (relativePath.StartsWith(Application.dataPath))
            {
                relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
            }

            // Try to load the script and check if it's a MonoBehaviour or ScriptableObject
            MonoScript monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(relativePath);
            if (monoScript != null)
            {
                System.Type scriptType = monoScript.GetClass();

                // Filter out non-component scripts
                if (scriptType != null)
                {
                    // Only include classes that derive from MonoBehaviour or ScriptableObject
                    bool isMonoBehaviour = scriptType.IsSubclassOf(typeof(MonoBehaviour));
                    bool isScriptableObject = scriptType.IsSubclassOf(typeof(ScriptableObject));

                    // Skip interfaces, abstract classes, static classes, and other non-components
                    if (!isMonoBehaviour && !isScriptableObject)
                    {
                        filteredOut++;
                        continue;
                    }

                    // Skip abstract classes (can't be instantiated)
                    if (scriptType.IsAbstract)
                    {
                        filteredOut++;
                        continue;
                    }
                }
                else
                {
                    // Couldn't get class type - likely interface, enum, or static class
                    filteredOut++;
                    continue;
                }
            }
            else
            {
                // Couldn't load as MonoScript - skip
                filteredOut++;
                continue;
            }

            scriptDict[scriptName] = new ScriptInfo
            {
                scriptName = scriptName,
                scriptPath = relativePath,
                usageCount = 0
            };
        }

        Debug.Log($"[Script Usage Analyzer] Tracking {scriptDict.Count} MonoBehaviour/ScriptableObject scripts (filtered out {filteredOut} non-component scripts)");

        // Method 1: Find all scene objects (including inactive ones)
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => go.scene.IsValid()) // Only scene objects
            .ToArray();

        Debug.Log($"[Script Usage Analyzer] Found {sceneObjects.Length} GameObjects in scene");

        // Method 2: Also scan prefabs in project
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        List<GameObject> prefabObjects = new List<GameObject>();

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabObjects.Add(prefab);
            }
        }

        Debug.Log($"[Script Usage Analyzer] Found {prefabObjects.Count} prefabs in project");

        // Combine both scene and prefab objects
        List<GameObject> allObjectsToScan = new List<GameObject>();
        allObjectsToScan.AddRange(sceneObjects);
        allObjectsToScan.AddRange(prefabObjects);

        Debug.Log($"[Script Usage Analyzer] Total objects to scan: {allObjectsToScan.Count}");

        int componentsScanned = 0;
        int scriptsFound = 0;
        HashSet<string> foundTypes = new HashSet<string>();

        foreach (GameObject go in allObjectsToScan)
        {
            Component[] components = go.GetComponents<Component>();
            componentsScanned += components.Length;

            foreach (Component comp in components)
            {
                if (comp == null) continue;

                string typeName = comp.GetType().Name;
                foundTypes.Add(typeName);

                if (scriptDict.ContainsKey(typeName))
                {
                    scriptDict[typeName].usageCount++;
                    scriptsFound++;

                    if (!scriptDict[typeName].usedBy.Contains(go))
                    {
                        scriptDict[typeName].usedBy.Add(go);
                    }
                }
            }
        }

        allScripts = scriptDict.Values.ToList();
        usedScripts = allScripts.Where(s => s.usageCount > 0).OrderByDescending(s => s.usageCount).ToList();
        unusedScripts = allScripts.Where(s => s.usageCount == 0).OrderBy(s => s.scriptName).ToList();

        hasScanned = true;

        Debug.Log($"[Script Usage Analyzer] Scan complete!\n" +
                  $"Total component scripts tracked: {allScripts.Count}\n" +
                  $"Non-component scripts filtered: {filteredOut}\n" +
                  $"Components scanned: {componentsScanned}\n" +
                  $"Unique component types found: {foundTypes.Count}\n" +
                  $"Script instances found: {scriptsFound}\n" +
                  $"Used scripts: {usedScripts.Count}\n" +
                  $"Unused scripts: {unusedScripts.Count}");

        // Sample some found types for debugging
        Debug.Log($"[Script Usage Analyzer] Sample component types found: {string.Join(", ", foundTypes.Take(10))}");
    }

    void ExportToCSV()
    {
        string path = EditorUtility.SaveFilePanel("Export Script Usage", "", "script_usage.csv", "csv");

        if (string.IsNullOrEmpty(path)) return;

        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("Script Name,Path,Usage Count,Status,Used By");

            foreach (var script in allScripts.OrderByDescending(s => s.usageCount))
            {
                string status = script.usageCount > 0 ? "Used" : "Unused";
                string usedBy = script.usedBy.Count > 0
                    ? string.Join("; ", script.usedBy.Select(go => go.name).Take(10))
                    : "None";

                writer.WriteLine($"\"{script.scriptName}\",\"{script.scriptPath}\",{script.usageCount},\"{status}\",\"{usedBy}\"");
            }
        }

        EditorUtility.DisplayDialog("Export Complete", $"Script usage exported to:\n{path}", "OK");
        EditorUtility.RevealInFinder(path);
    }
}