using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Tools → Characters → Extract CombatGirls Clips
//
// Copies the clip out of every CombatGirls animation FBX into its own editable .anim
// (Assets/CombatGirlsAnimations/<girl>/<Emotion|Normal|Special>/<file>.anim), then points everything that used an FBX
// clip at the copy: controller states, blend trees, synced-layer overrides, override controllers, abilities' baked
// clips. The copies are yours: a rerun never overwrites one, so events added to them stay. The folder is pack content
// and is git-ignored like the pack.
public static class CombatGirlsClips
{
    public const string Root = "Assets/CombatGirlsAnimations";
    const string PackRoot = "Assets/CombatGirlsCharacterPack";
    static readonly string[] Girls = { "Katana_Girl", "CombatGirl_Shield" };
    static readonly string[] ReferencingExtensions = { ".controller", ".overrideController", ".asset", ".prefab", ".playable", ".mask" };

    [MenuItem("Tools/Characters/Extract CombatGirls Clips")]
    static void Extract()
    {
        var map = new Dictionary<AnimationClip, AnimationClip>();
        var fbxPaths = new HashSet<string>();
        int made = 0;

        foreach (string girl in Girls)
        {
            foreach (string fbx in FbxPaths(girl))
            {
                fbxPaths.Add(fbx);
                made += ExtractOne(fbx, map);
            }
        }

        AssetDatabase.SaveAssets();
        int repointed = Repoint(map, fbxPaths);
        AssetDatabase.SaveAssets();
        Log($"{made} new clips in {Root} ({map.Count} in all, existing copies kept); {repointed} references moved off the FBX files.");
    }

    /// <summary>The editable copy of an FBX's clip, or null before extraction.</summary>
    public static AnimationClip ExtractedFor(string fbxPath)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(TargetPath(fbxPath));
    }

    /// <summary>The clip inside an FBX (its first, skipping Unity's preview clip).</summary>
    public static AnimationClip FromFbx(string fbxPath)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }

    // ".../Katana_Girl/Animations/Normal/K_Attack_1.fbx" → "Assets/CombatGirlsAnimations/Katana_Girl/Normal/K_Attack_1.anim"
    static string TargetPath(string fbxPath)
    {
        string inPack = fbxPath.Substring(PackRoot.Length).Replace("/Animations/", "/");
        return Root + Path.ChangeExtension(inPack, ".anim");
    }

    static IEnumerable<string> FbxPaths(string girl)
    {
        return AssetDatabase.FindAssets("t:Model", new[] { $"{PackRoot}/{girl}/Animations" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p);
    }

    // 1 when a new .anim was written, 0 when the copy already existed or the FBX has no clip.
    static int ExtractOne(string fbxPath, Dictionary<AnimationClip, AnimationClip> map)
    {
        AnimationClip source = FromFbx(fbxPath);
        if (source == null) return 0;

        AnimationClip existing = ExtractedFor(fbxPath);
        if (existing != null)
        {
            map[source] = existing;
            return 0;
        }

        string target = TargetPath(fbxPath);
        EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));

        var copy = new AnimationClip();
        EditorUtility.CopySerialized(source, copy);
        BakeRootIntoPose(copy);
        AssetDatabase.CreateAsset(copy, target);

        map[source] = copy;
        return 1;
    }

    // Same rule as her importers: root motion is off, so rotation and height go into the pose (a clip can't turn the
    // character, a fall reaches the floor); ground travel stays unbaked and the motor owns it.
    static void BakeRootIntoPose(AnimationClip clip)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopBlendOrientation = true;
        settings.keepOriginalOrientation = true;
        settings.loopBlendPositionY = true;
        settings.keepOriginalPositionY = true;
        settings.loopBlendPositionXZ = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    // ── Repoint ───────────────────────────────────────────────────────────

    static int Repoint(Dictionary<AnimationClip, AnimationClip> map, HashSet<string> fbxPaths)
    {
        int count = 0;

        foreach (string path in AssetsUsing(fbxPaths))
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                count += RepointObject(asset, map);

        return count;
    }

    static IEnumerable<string> AssetsUsing(HashSet<string> fbxPaths)
    {
        foreach (string path in AssetDatabase.GetAllAssetPaths())
        {
            if (!path.StartsWith("Assets/") || path.StartsWith(PackRoot) || path.StartsWith(Root)) continue;
            if (!ReferencingExtensions.Contains(Path.GetExtension(path))) continue;
            if (AssetDatabase.GetDependencies(path, false).Any(fbxPaths.Contains)) yield return path;
        }
    }

    // Every clip reference on the object, wherever it sits (state motion, blend tree child, synced layer, override pair).
    static int RepointObject(Object asset, Dictionary<AnimationClip, AnimationClip> map)
    {
        if (asset == null) return 0;

        var so = new SerializedObject(asset);
        SerializedProperty property = so.GetIterator();
        int count = 0;

        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (!(property.objectReferenceValue is AnimationClip clip)) continue;
            if (!map.TryGetValue(clip, out AnimationClip copy)) continue;

            property.objectReferenceValue = copy;
            count++;
        }

        if (count == 0) return 0;

        so.ApplyModifiedPropertiesWithoutUndo();
        Log($"{AssetDatabase.GetAssetPath(asset)} ({asset.name}): {count}.");
        return count;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void Log(string message) => Debug.Log($"[CombatGirlsClips] {message}");
}
