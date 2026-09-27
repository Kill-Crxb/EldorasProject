using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Sorts loose assets into the Database/Assets category roots purely by file extension.
// No content inspection, no heuristics. Extension in -> category root out.
public class AssetTypeSorter : EditorWindow
{
    const string Root2D = "Assets/Database/Assets/2D Database";
    const string Root3D = "Assets/Database/Assets/3D Database";
    const string RootUI = "Assets/Database/Assets/UIDatabase";
    const string RootSound = "Assets/Database/Assets/Sound";

    const string PrefGroup = "NinjaGame_Sorter_GroupByParent";
    const string PrefResources = "NinjaGame_Sorter_SkipResources";

    // ---- extension maps -------------------------------------------------------

    static readonly HashSet<string> Ext3D = new HashSet<string>
    {
        ".fbx", ".obj", ".blend", ".blend1", ".dae", ".3ds", ".dxf",
        ".max", ".ma", ".mb", ".c4d", ".gltf", ".glb", ".stl", ".ply", ".abc"
    };

    static readonly HashSet<string> Ext2D = new HashSet<string>
    {
        ".png", ".jpg", ".jpeg", ".tga", ".psd", ".psb", ".tif", ".tiff",
        ".bmp", ".gif", ".exr", ".hdr", ".iff", ".pict", ".webp", ".svg"
    };

    static readonly HashSet<string> ExtSound = new HashSet<string>
    {
        ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".flac", ".mod", ".it", ".s3m", ".xm"
    };

    // Nothing identifies a "UI asset" by extension alone, so this map is deliberately
    // small: UI Toolkit documents, legacy GUI skins, and fonts.
    static readonly HashSet<string> ExtUI = new HashSet<string>
    {
        ".uxml", ".uss", ".tss", ".guiskin", ".ttf", ".otf"
    };

    // ---- blacklists -----------------------------------------------------------

    // Folders that are never read from. Anything under these is left alone.
    static readonly string[] BlockedFolders =
    {
        "Assets/ImportedAssets",   // downloaded / third party
        "Assets/Database",         // destination + the ScriptableObject databases
        "Assets/Text and Fonts",   // TextMesh Pro essentials, resolves partly by path
        "Assets/Plugins",
        "Assets/StreamingAssets",
        "Assets/Editor Default Resources",
        "Assets/ProBuilder Data"
    };

    // Filename prefixes that are generated bake data. Moving them just makes Unity
    // rebuild them in the scene folder next bake.
    static readonly string[] BlockedPrefixes =
    {
        "Lightmap-",
        "ReflectionProbe-",
        "LightingData"
    };

    // ---- state ----------------------------------------------------------------

    List<string> sources = new List<string>();
    List<string> targets = new List<string>();
    Vector2 scroll;
    bool groupByParent;
    bool skipResources = true;
    string summary = "";

    [MenuItem("NinjaGame/Tools/Asset Type Sorter")]
    static void ShowWindow()
    {
        GetWindow<AssetTypeSorter>("Asset Type Sorter").minSize = new Vector2(620, 420);
    }

    void OnEnable()
    {
        groupByParent = EditorPrefs.GetBool(PrefGroup, false);
        skipResources = EditorPrefs.GetBool(PrefResources, true);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Sorts by file extension only. Unmapped types are never touched.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        groupByParent = EditorGUILayout.ToggleLeft("Keep source folder name as a subfolder", groupByParent);
        skipResources = EditorGUILayout.ToggleLeft("Skip anything inside a Resources folder (recommended)", skipResources);
        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(PrefGroup, groupByParent);
            EditorPrefs.SetBool(PrefResources, skipResources);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Preview", GUILayout.Height(28))) BuildPlan();

        GUI.enabled = sources.Count > 0;
        if (GUILayout.Button($"Move {sources.Count} files", GUILayout.Height(28))) ExecutePlan();
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < sources.Count; i++)
            EditorGUILayout.LabelField(sources[i], "-> " + targets[i], EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();
    }

    // ---- planning -------------------------------------------------------------

    void BuildPlan()
    {
        sources.Clear();
        targets.Clear();

        string[] all = AssetDatabase.GetAllAssetPaths();
        int count2D = 0, count3D = 0, countUI = 0, countSound = 0;

        for (int i = 0; i < all.Length; i++)
        {
            string path = all[i];
            if (!path.StartsWith("Assets/")) continue;
            if (AssetDatabase.IsValidFolder(path)) continue;
            if (IsBlocked(path)) continue;

            string root = RootForPath(path);
            if (root == null) continue;

            string target = TargetPath(root, path);
            if (target == path) continue;

            sources.Add(path);
            targets.Add(target);

            if (root == Root2D) count2D++;
            if (root == Root3D) count3D++;
            if (root == RootUI) countUI++;
            if (root == RootSound) countSound++;
        }

        summary = $"{sources.Count} files queued.   2D: {count2D}    3D: {count3D}    UI: {countUI}    Sound: {countSound}";
        Debug.Log("[AssetTypeSorter] " + summary);
    }

    static string RootForPath(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (Ext3D.Contains(ext)) return Root3D;
        if (Ext2D.Contains(ext)) return Root2D;
        if (ExtSound.Contains(ext)) return RootSound;
        if (ExtUI.Contains(ext)) return RootUI;
        return null;
    }

    string TargetPath(string root, string path)
    {
        string file = Path.GetFileName(path);
        if (!groupByParent) return root + "/" + file;

        string parent = Path.GetFileName(Path.GetDirectoryName(path));
        return root + "/" + parent + "/" + file;
    }

    bool IsBlocked(string path)
    {
        for (int i = 0; i < BlockedFolders.Length; i++)
            if (path.StartsWith(BlockedFolders[i] + "/")) return true;

        string file = Path.GetFileName(path);
        for (int i = 0; i < BlockedPrefixes.Length; i++)
            if (file.StartsWith(BlockedPrefixes[i])) return true;

        if (skipResources && path.Contains("/Resources/")) return true;

        return false;
    }

    // ---- execution ------------------------------------------------------------

    void ExecutePlan()
    {
        if (sources.Count == 0) return;

        bool go = EditorUtility.DisplayDialog(
            "Move assets",
            $"Move {sources.Count} files into the Database category roots?\n\nReferences are preserved (GUIDs are kept), but this cannot be undone from the editor. Commit or back up first.",
            "Move", "Cancel");
        if (!go) return;

        var log = new StringBuilder();
        log.AppendLine("result,from,to");
        int moved = 0;
        int failed = 0;

        AssetDatabase.StartAssetEditing();

        for (int i = 0; i < sources.Count; i++)
        {
            EditorUtility.DisplayProgressBar("Asset Type Sorter", sources[i], (float)i / sources.Count);

            string from = sources[i];
            string folder = Path.GetDirectoryName(targets[i]).Replace("\\", "/");
            EnsureFolder(folder);

            string to = AssetDatabase.GenerateUniqueAssetPath(targets[i]);
            string error = AssetDatabase.MoveAsset(from, to);

            if (string.IsNullOrEmpty(error))
            {
                moved++;
                log.AppendLine($"moved,{from},{to}");
                continue;
            }

            failed++;
            log.AppendLine($"FAILED ({error}),{from},{to}");
            Debug.LogWarning($"[AssetTypeSorter] {from} -> {to}: {error}");
        }

        AssetDatabase.StopAssetEditing();
        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        string logPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../AssetTypeSorter_Log.csv"));
        File.WriteAllText(logPath, log.ToString());

        summary = $"Moved {moved}, failed {failed}. Log: {logPath}";
        Debug.Log("[AssetTypeSorter] " + summary);

        sources.Clear();
        targets.Clear();
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
