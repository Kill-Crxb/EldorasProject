using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace SceneAnalysis
{
    public class SceneHierarchyAnalyzer : EditorWindow
    {
        private Vector2 scrollPosition;
        private string hierarchyReport = "";
        private bool includeInactiveObjects = true;
        private bool includeBuiltInComponents = false;
        private bool groupByParent = true;
        private bool showComponentDetails = true;
        private bool exportToFile = false;
        private string exportPath = "";

        [MenuItem("Tools/Scene Analysis/Hierarchy Analyzer")]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneHierarchyAnalyzer>("Scene Hierarchy Analyzer");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Scene Hierarchy Analyzer", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Options
            EditorGUILayout.LabelField("Analysis Options", EditorStyles.boldLabel);
            includeInactiveObjects = EditorGUILayout.Toggle("Include Inactive Objects", includeInactiveObjects);
            includeBuiltInComponents = EditorGUILayout.Toggle("Include Built-in Components", includeBuiltInComponents);
            groupByParent = EditorGUILayout.Toggle("Group by Parent", groupByParent);
            showComponentDetails = EditorGUILayout.Toggle("Show Component Details", showComponentDetails);

            EditorGUILayout.Space(10);

            // Export options
            EditorGUILayout.LabelField("Export Options", EditorStyles.boldLabel);
            exportToFile = EditorGUILayout.Toggle("Export to File", exportToFile);

            if (exportToFile)
            {
                EditorGUILayout.BeginHorizontal();
                exportPath = EditorGUILayout.TextField("Export Path:", exportPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string path = EditorUtility.SaveFilePanel("Save Hierarchy Report",
                        Application.dataPath, "SceneHierarchy", "txt");
                    if (!string.IsNullOrEmpty(path))
                    {
                        exportPath = path;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Generate button
            if (GUILayout.Button("Generate Hierarchy Report", GUILayout.Height(30)))
            {
                GenerateHierarchyReport();
            }

            EditorGUILayout.Space(5);

            // Clear button
            if (!string.IsNullOrEmpty(hierarchyReport) && GUILayout.Button("Clear Report"))
            {
                hierarchyReport = "";
            }

            EditorGUILayout.Space(10);

            // Display report
            if (!string.IsNullOrEmpty(hierarchyReport))
            {
                EditorGUILayout.LabelField("Hierarchy Report", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                // Use a text area with word wrap disabled for better formatting
                GUIStyle textStyle = new GUIStyle(EditorStyles.textArea);
                textStyle.wordWrap = false;
                textStyle.font = EditorStyles.miniFont;

                hierarchyReport = EditorGUILayout.TextArea(hierarchyReport, textStyle,
                    GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

                EditorGUILayout.EndScrollView();
            }
        }

        private void GenerateHierarchyReport()
        {
            var report = new StringBuilder();
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            report.AppendLine($"=== SCENE HIERARCHY REPORT ===");
            report.AppendLine($"Scene: {currentScene.name}");
            report.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"Unity Version: {Application.unityVersion}");
            report.AppendLine();

            // Get all root objects
            var rootObjects = currentScene.GetRootGameObjects();

            if (includeInactiveObjects)
            {
                // Unity only returns active root objects by default, so we need to get all objects
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(obj => obj.scene == currentScene && obj.transform.parent == null)
                    .OrderBy(obj => obj.name)
                    .ToArray();
                rootObjects = allObjects;
            }
            else
            {
                rootObjects = rootObjects.OrderBy(obj => obj.name).ToArray();
            }

            report.AppendLine($"Total Root Objects: {rootObjects.Length}");
            report.AppendLine();

            foreach (var rootObj in rootObjects)
            {
                if (groupByParent)
                {
                    AnalyzeObjectHierarchy(rootObj, report, 0);
                }
                else
                {
                    AnalyzeObjectFlat(rootObj, report);
                }
            }

            // Add summary statistics
            AddSummaryStatistics(rootObjects, report);

            hierarchyReport = report.ToString();

            // Export to file if requested
            if (exportToFile && !string.IsNullOrEmpty(exportPath))
            {
                try
                {
                    File.WriteAllText(exportPath, hierarchyReport);
                    EditorUtility.DisplayDialog("Export Complete",
                        $"Hierarchy report exported to:\n{exportPath}", "OK");
                }
                catch (System.Exception e)
                {
                    EditorUtility.DisplayDialog("Export Failed",
                        $"Failed to export report:\n{e.Message}", "OK");
                }
            }

            Debug.Log("Scene Hierarchy Report Generated");
        }

        private void AnalyzeObjectHierarchy(GameObject obj, StringBuilder report, int depth)
        {
            if (!includeInactiveObjects && !obj.activeInHierarchy)
                return;

            // Object header
            string indent = new string(' ', depth * 2);
            string prefix = depth == 0 ? "├── " : "│   ";
            if (depth > 0) prefix = new string(' ', (depth - 1) * 4) + "├── ";

            string activeStatus = obj.activeInHierarchy ? "" : " (INACTIVE)";
            report.AppendLine($"{prefix}{obj.name}{activeStatus}");

            // Components
            var components = obj.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue; // Missing script

                if (!includeBuiltInComponents && IsBuiltInComponent(component))
                    continue;

                string componentIndent = new string(' ', (depth + 1) * 4);
                string componentName = component.GetType().Name;

                if (showComponentDetails)
                {
                    string details = GetComponentDetails(component);
                    report.AppendLine($"{componentIndent}◦ {componentName}{details}");
                }
                else
                {
                    report.AppendLine($"{componentIndent}◦ {componentName}");
                }
            }

            // Check for missing scripts
            var missingScripts = GetMissingScriptCount(obj);
            if (missingScripts > 0)
            {
                string componentIndent = new string(' ', (depth + 1) * 4);
                report.AppendLine($"{componentIndent}◦ Missing Script(s) x{missingScripts} ⚠️");
            }

            // Recurse into children
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                AnalyzeObjectHierarchy(obj.transform.GetChild(i).gameObject, report, depth + 1);
            }
        }

        private void AnalyzeObjectFlat(GameObject rootObj, StringBuilder report)
        {
            var allObjects = new List<GameObject>();
            CollectAllChildren(rootObj, allObjects);

            foreach (var obj in allObjects)
            {
                if (!includeInactiveObjects && !obj.activeInHierarchy)
                    continue;

                string path = GetFullPath(obj);
                string activeStatus = obj.activeInHierarchy ? "" : " (INACTIVE)";
                report.AppendLine($"\n{path}{activeStatus}");

                var components = obj.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;

                    if (!includeBuiltInComponents && IsBuiltInComponent(component))
                        continue;

                    string componentName = component.GetType().Name;
                    string details = showComponentDetails ? GetComponentDetails(component) : "";
                    report.AppendLine($"  ◦ {componentName}{details}");
                }

                var missingScripts = GetMissingScriptCount(obj);
                if (missingScripts > 0)
                {
                    report.AppendLine($"  ◦ Missing Script(s) x{missingScripts} ⚠️");
                }
            }
        }

        private void CollectAllChildren(GameObject obj, List<GameObject> collection)
        {
            collection.Add(obj);
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                CollectAllChildren(obj.transform.GetChild(i).gameObject, collection);
            }
        }

        private string GetFullPath(GameObject obj)
        {
            var path = new List<string>();
            Transform current = obj.transform;

            while (current != null)
            {
                path.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", path);
        }

        private bool IsBuiltInComponent(Component component)
        {
            var builtInTypes = new System.Type[]
            {
                typeof(Transform),
                typeof(RectTransform),
                typeof(MeshRenderer),
                typeof(MeshFilter),
                typeof(Collider),
                typeof(Rigidbody),
                typeof(Camera),
                typeof(Light),
                typeof(AudioSource),
                typeof(Canvas),
                typeof(CanvasRenderer)
            };

            var componentType = component.GetType();
            return builtInTypes.Any(type => type.IsAssignableFrom(componentType));
        }

        private string GetComponentDetails(Component component)
        {
            var details = new StringBuilder();

            // Add specific details for certain component types
            switch (component)
            {
                case MonoBehaviour mb:
                    details.Append($" [{(mb.enabled ? "Enabled" : "Disabled")}]");
                    break;

                case Renderer renderer:
                    details.Append($" [Material: {renderer.sharedMaterial?.name ?? "None"}]");
                    break;

                case Collider collider:
                    details.Append($" [{(collider.enabled ? "Enabled" : "Disabled")}, {(collider.isTrigger ? "Trigger" : "Solid")}]");
                    break;

                case AudioSource audio:
                    details.Append($" [Clip: {audio.clip?.name ?? "None"}]");
                    break;
            }

            return details.ToString();
        }

        private int GetMissingScriptCount(GameObject obj)
        {
            var components = obj.GetComponents<Component>();
            return components.Count(c => c == null);
        }

        private void AddSummaryStatistics(GameObject[] rootObjects, StringBuilder report)
        {
            report.AppendLine("\n=== SUMMARY STATISTICS ===");

            var allObjects = new List<GameObject>();
            foreach (var root in rootObjects)
            {
                CollectAllChildren(root, allObjects);
            }

            int totalObjects = allObjects.Count;
            int activeObjects = allObjects.Count(obj => obj.activeInHierarchy);
            int inactiveObjects = totalObjects - activeObjects;

            var componentCounts = new Dictionary<string, int>();
            int totalComponents = 0;
            int missingScripts = 0;

            foreach (var obj in allObjects)
            {
                var components = obj.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component == null)
                    {
                        missingScripts++;
                        continue;
                    }

                    string typeName = component.GetType().Name;
                    componentCounts[typeName] = componentCounts.GetValueOrDefault(typeName, 0) + 1;
                    totalComponents++;
                }
            }

            report.AppendLine($"Total GameObjects: {totalObjects}");
            report.AppendLine($"  Active: {activeObjects}");
            report.AppendLine($"  Inactive: {inactiveObjects}");
            report.AppendLine($"Total Components: {totalComponents}");
            if (missingScripts > 0)
            {
                report.AppendLine($"Missing Scripts: {missingScripts} ⚠️");
            }
            report.AppendLine();

            report.AppendLine("Component Type Distribution:");
            var sortedComponents = componentCounts.OrderByDescending(kvp => kvp.Value);
            foreach (var kvp in sortedComponents.Take(10)) // Top 10 most common
            {
                report.AppendLine($"  {kvp.Key}: {kvp.Value}");
            }

            if (componentCounts.Count > 10)
            {
                report.AppendLine($"  ... and {componentCounts.Count - 10} other component types");
            }
        }
    }
}