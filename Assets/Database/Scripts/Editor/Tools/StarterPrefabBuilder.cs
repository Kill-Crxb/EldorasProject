using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Tools → CrabSystem → Build Starter Prefabs. Turns the scene objects every game needs into CrabSystem base prefabs,
// each with a NinjaGame variant in Database/Prefabs, and swaps the scene objects for their variants.
//
// A base may only point at CrabSystem assets, Unity's own, and the TextMesh Pro essentials (they import with the same
// GUIDs in any project). Every other asset reference moves to the variant as an override, so the base ships with
// that slot empty for the next game to fill. A reference to an already split base (Manager_Brain) stays on the base
// and points at the variant in the variant. Diagnostics (Trace, Probe, Hotkeys) ship off in the base and keep their
// state in the variant. Links to other scene objects stay on the scene instance as overrides.
//
// Rerunnable: a scene object that is already a prefab instance is skipped, and so is a base whose variant exists.
public static class StarterPrefabBuilder
{
    const string BaseFolder = "Assets/CrabSystem/Runtime/Prefabs";
    const string VariantFolder = "Assets/Database/Prefabs";
    const string VariantSuffix = "_NinjaGame";
    const string ManagerBrainPath = BaseFolder + "/Manager_Brain.prefab";
    const string FrameworkRoot = "Assets/CrabSystem/";
    const string TmpEssentials = "Assets/ThirdParty/Text and Fonts/";
    const string MenuScene = "Assets/Database/Scenes/MenuScene.unity";
    const string ZooScene = "Assets/Database/Scenes/Zoo.unity";
    const string DebugToolsName = "Debugtools";
    const string EnabledProperty = "m_Enabled";

    static readonly string[] MenuObjects = { "SceneLoader", "MenuUi" };
    static readonly string[] ZooObjects = { "Bootstrapper", "UIManager", DebugToolsName };
    static readonly string[] DiagnosticSuffixes = { "Trace", "Probe", "Hotkeys" };

    struct Pick
    {
        public int[] path;
        public Type type;
        public int index;
        public string property;
        public Object value;
    }

    [MenuItem("Tools/CrabSystem/Build Starter Prefabs")]
    static void Build()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!AssetDatabase.IsValidFolder(VariantFolder)) AssetDatabase.CreateFolder("Assets/Database", "Prefabs");

        var remap = new Dictionary<GameObject, GameObject>();
        var managerBrain = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerBrainPath);
        if (managerBrain == null)
        {
            Debug.LogError($"[StarterPrefabBuilder] No prefab at {ManagerBrainPath}.");
            return;
        }
        remap[managerBrain] = Split(ManagerBrainPath, remap);

        BuildScene(MenuScene, MenuObjects, remap);
        BuildScene(ZooScene, ZooObjects, remap);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StarterPrefabBuilder] Done. Bases in {BaseFolder}, NinjaGame variants in {VariantFolder}.");
    }

    static void BuildScene(string scenePath, string[] objectNames, Dictionary<GameObject, GameObject> remap)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == DebugToolsName) AddTraces(root);
            if (Array.IndexOf(objectNames, root.name) >= 0)
            {
                Convert(root, remap);
                continue;
            }
            ReplaceSplitInstance(root, remap);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // MoveClockTrace and StrikeTrace don't spawn themselves; Debugtools carries every trace.
    static void AddTraces(GameObject debugTools)
    {
        if (debugTools.GetComponent<MoveClockTrace>() == null) debugTools.AddComponent<MoveClockTrace>();
        if (debugTools.GetComponent<StrikeTrace>() == null) debugTools.AddComponent<StrikeTrace>();
    }

    static void Convert(GameObject sceneRoot, Dictionary<GameObject, GameObject> remap)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(sceneRoot))
        {
            Debug.Log($"[StarterPrefabBuilder] {sceneRoot.name} is already a prefab instance; skipped.", sceneRoot);
            return;
        }

        List<Pick> links = SceneLinks(sceneRoot);
        string basePath = $"{BaseFolder}/{sceneRoot.name}.prefab";
        PrefabUtility.SaveAsPrefabAssetAndConnect(sceneRoot, basePath, InteractionMode.AutomatedAction);
        GameObject variant = Split(basePath, remap);
        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(sceneRoot, variant, ReplaceSettings(), InteractionMode.AutomatedAction);
        foreach (Pick link in links) Restore(sceneRoot, link);
    }

    // References from inside the object to other scene objects can't live in a prefab asset, so they stay on the
    // scene instance. They're set again after the swap, because a child whose name repeats among its siblings
    // doesn't match by hierarchy and would lose them.
    static List<Pick> SceneLinks(GameObject root)
    {
        var links = new List<Pick>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null) AddSceneLinks(component, root.transform, links);
        }
        return links;
    }

    static void AddSceneLinks(Component component, Transform root, List<Pick> links)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (IsOutsideLink(property.objectReferenceValue, root))
                links.Add(NewPick(component, root, property.propertyPath, property.objectReferenceValue));
        }
    }

    static bool IsOutsideLink(Object value, Transform root)
    {
        if (value == null || EditorUtility.IsPersistent(value)) return false;

        Transform target = value is Component component ? component.transform : value is GameObject gameObject ? gameObject.transform : null;
        return target != null && !target.IsChildOf(root);
    }

    static void ReplaceSplitInstance(GameObject root, Dictionary<GameObject, GameObject> remap)
    {
        if (!PrefabUtility.IsOutermostPrefabInstanceRoot(root)) return;

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(root);
        if (source == null || !remap.TryGetValue(source, out GameObject variant)) return;

        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(root, variant, ReplaceSettings(), InteractionMode.AutomatedAction);
    }

    static PrefabReplacingSettings ReplaceSettings() => new PrefabReplacingSettings
    {
        changeRootNameToAssetName = false,
        logInfo = false,
        objectMatchMode = ObjectMatchMode.ByHierarchy,
        prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides
    };

    static GameObject Split(string basePath, Dictionary<GameObject, GameObject> remap)
    {
        string variantPath = $"{VariantFolder}/{Path.GetFileNameWithoutExtension(basePath)}{VariantSuffix}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
        if (existing != null) return existing;

        GameObject contents = PrefabUtility.LoadPrefabContents(basePath);
        List<Pick> picks = TakeGameState(contents, remap);
        PrefabUtility.SaveAsPrefabAsset(contents, basePath);
        PrefabUtility.UnloadPrefabContents(contents);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
        foreach (Pick pick in picks) Restore(instance, pick);
        GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
        Object.DestroyImmediate(instance);

        Debug.Log($"[StarterPrefabBuilder] {basePath}: {picks.Count} game value(s) moved to {variantPath}.");
        return variant;
    }

    // Clears, in the base, every value that belongs to the game, and returns them for the variant.
    static List<Pick> TakeGameState(GameObject root, Dictionary<GameObject, GameObject> remap)
    {
        var picks = new List<Pick>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null) continue;
            TakeReferences(component, root.transform, remap, picks);
            TakeDiagnostic(component, root.transform, picks);
        }
        return picks;
    }

    static void TakeReferences(Component component, Transform root, Dictionary<GameObject, GameObject> remap, List<Pick> picks)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;

            Object value = property.objectReferenceValue;
            Object variantValue = Remapped(value, remap);
            if (variantValue != null)
            {
                picks.Add(NewPick(component, root, property.propertyPath, variantValue));
                continue;
            }
            if (!IsGameAsset(value)) continue;

            picks.Add(NewPick(component, root, property.propertyPath, value));
            property.objectReferenceValue = null;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void TakeDiagnostic(Component component, Transform root, List<Pick> picks)
    {
        if (!(component is Behaviour behaviour) || !behaviour.enabled || !IsDiagnostic(component.GetType())) return;

        picks.Add(NewPick(component, root, EnabledProperty, null));
        behaviour.enabled = false;
    }

    static bool IsDiagnostic(Type type) => Array.Exists(DiagnosticSuffixes, suffix => type.Name.EndsWith(suffix));

    static Object Remapped(Object value, Dictionary<GameObject, GameObject> remap)
    {
        if (value is Component component)
            return remap.TryGetValue(component.gameObject, out GameObject owner) ? owner.GetComponent(component.GetType()) : null;
        if (value is GameObject gameObject)
            return remap.TryGetValue(gameObject, out GameObject variant) ? variant : null;
        return null;
    }

    static bool IsGameAsset(Object value)
    {
        if (value == null || value is MonoScript || !EditorUtility.IsPersistent(value)) return false;

        string path = AssetDatabase.GetAssetPath(value);
        return path.StartsWith("Assets/") && !path.StartsWith(FrameworkRoot) && !path.StartsWith(TmpEssentials);
    }

    static Pick NewPick(Component component, Transform root, string property, Object value)
    {
        Component[] same = component.GetComponents(component.GetType());
        return new Pick
        {
            path = SiblingPath(component.transform, root),
            type = component.GetType(),
            index = Array.IndexOf(same, component),
            property = property,
            value = value
        };
    }

    static int[] SiblingPath(Transform transform, Transform root)
    {
        var path = new List<int>();
        for (Transform t = transform; t != root; t = t.parent) path.Insert(0, t.GetSiblingIndex());
        return path.ToArray();
    }

    static void Restore(GameObject instance, Pick pick)
    {
        Transform t = instance.transform;
        foreach (int child in pick.path) t = t.GetChild(child);
        Component component = t.GetComponents(pick.type)[pick.index];

        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.FindProperty(pick.property);
        if (pick.property == EnabledProperty) property.boolValue = true;
        else property.objectReferenceValue = pick.value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
