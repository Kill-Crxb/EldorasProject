using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace NinjaGame.Editor
{
    public class ProjectKnowledgeSync : EditorWindow
    {
        private const string SOURCE_FOLDER = "Assets/CrabSystem";
        private const string TARGET_FOLDER = "ProjectKnowledge";

        private static string TargetPath => Path.Combine(Application.dataPath, "..", TARGET_FOLDER);

        [MenuItem("Tools/Project Knowledge Sync")]
        public static void ShowWindow()
        {
            GetWindow<ProjectKnowledgeSync>("Project Knowledge Sync");
        }

        private void OnGUI()
        {
            GUILayout.Label("Project Knowledge Sync", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                $"This tool copies all scripts from '{SOURCE_FOLDER}' into a single flat folder '{TARGET_FOLDER}' " +
                "for easy project knowledge updates.\n\n" +
                "The target folder is placed at the project root (same level as Assets).",
                MessageType.Info
            );

            GUILayout.Space(10);

            if (GUILayout.Button("Sync Scripts to Project Knowledge", GUILayout.Height(40)))
            {
                SyncScripts();
            }

            GUILayout.Space(10);

            if (Directory.Exists(TargetPath))
            {
                var scriptCount = Directory.GetFiles(TargetPath, "*.cs", SearchOption.TopDirectoryOnly).Length;
                EditorGUILayout.HelpBox($"Current status: {scriptCount} scripts in ProjectKnowledge folder", MessageType.None);

                if (GUILayout.Button("Open ProjectKnowledge Folder"))
                {
                    EditorUtility.RevealInFinder(TargetPath);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("ProjectKnowledge folder doesn't exist yet. Click 'Sync Scripts' to create it.", MessageType.Warning);
            }
        }

        [MenuItem("Tools/Sync Project Knowledge %&k")] // Ctrl+Alt+K / Cmd+Alt+K
        public static void SyncScripts()
        {
            try
            {
                // Step 1: Clear target folder
                if (Directory.Exists(TargetPath))
                {
                    Debug.Log($"Clearing existing ProjectKnowledge folder at: {TargetPath}");
                    Directory.Delete(TargetPath, true);
                }

                // Step 2: Create fresh target folder
                Directory.CreateDirectory(TargetPath);
                Debug.Log($"Created ProjectKnowledge folder at: {TargetPath}");

                // Step 3: Find all C# scripts in source folder
                string sourcePath = Path.Combine(Application.dataPath, SOURCE_FOLDER.Replace("Assets/", ""));

                if (!Directory.Exists(sourcePath))
                {
                    Debug.LogError($"Source folder not found: {sourcePath}");
                    EditorUtility.DisplayDialog("Error", $"Source folder '{SOURCE_FOLDER}' doesn't exist!", "OK");
                    return;
                }

                var allScripts = Directory.GetFiles(sourcePath, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/.") && !f.EndsWith(".meta"))
                    .ToList();

                if (allScripts.Count == 0)
                {
                    Debug.LogWarning($"No C# scripts found in {SOURCE_FOLDER}");
                    EditorUtility.DisplayDialog("Warning", $"No C# scripts found in '{SOURCE_FOLDER}'", "OK");
                    return;
                }

                // Step 4: Copy all scripts to target folder (flattened)
                int copiedCount = 0;
                foreach (var scriptPath in allScripts)
                {
                    string fileName = Path.GetFileName(scriptPath);
                    string targetFilePath = Path.Combine(TargetPath, fileName);

                    // Handle duplicate filenames by appending folder name
                    if (File.Exists(targetFilePath))
                    {
                        string parentFolder = Path.GetFileName(Path.GetDirectoryName(scriptPath));
                        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        fileName = $"{nameWithoutExt}_{parentFolder}.cs";
                        targetFilePath = Path.Combine(TargetPath, fileName);
                    }

                    File.Copy(scriptPath, targetFilePath, true);
                    copiedCount++;
                }

                Debug.Log($"<color=green>Successfully synced {copiedCount} scripts to ProjectKnowledge folder</color>");

                EditorUtility.DisplayDialog(
                    "Sync Complete",
                    $"Successfully copied {copiedCount} scripts from '{SOURCE_FOLDER}' to '{TARGET_FOLDER}' folder.\n\n" +
                    $"Location: {TargetPath}",
                    "OK"
                );
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error syncing scripts: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Failed to sync scripts:\n{e.Message}", "OK");
            }
        }
    }
}