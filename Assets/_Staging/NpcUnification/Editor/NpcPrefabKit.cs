using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// What every NPC builder shares: the two base prefab paths, turning a Base_PC copy into an NPC, and the AI_System
// child that holds its control source, perception and goals. MirrorNpcBuilder, MonsterBuilder and BaseNpcBuilder
// build on it so the steps live in one place.
public static class NpcPrefabKit
{
    public const string BasePcPath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    public const string BaseNpcPath = "Assets/Database/Characters/Base_NPC.prefab";

    public static GameObject InstantiateBasePc(string name)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePcPath);
        if (basePrefab == null)
        {
            Debug.LogError($"[NpcPrefabKit] No Base_PC at {BasePcPath}");
            return null;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        root.name = name;
        return root;
    }

    // An NPC brain, and no second view of the world: Base_PC's camera rig, listener and post-process volumes stay
    // on the prefab switched off (CameraModule and InputSystem are player-only and decline themselves).
    public static ControllerBrain MakeNpc(GameObject root)
    {
        var brain = root.GetComponentInChildren<ControllerBrain>(true);
        Write(brain, "entityType", p => p.intValue = (int)EntityType.NPC);
        DisablePlayerView(root);
        return brain;
    }

    public static void DisablePlayerView(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            cam.gameObject.SetActive(false);

        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.gameObject.SetActive(false);

        foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            volume.gameObject.SetActive(false);
    }

    // The AI_System child under the brain, made once. A rerun or a variant of Base_NPC finds the one it has.
    public static GameObject EnsureAiSystem(ControllerBrain brain)
    {
        var existing = brain.GetComponentInChildren<AIControlSource>(true);
        GameObject ai = existing != null ? existing.gameObject : new GameObject("AI_System");
        ai.transform.SetParent(brain.transform, false);
        Ensure<AIControlSource>(ai);
        return ai;
    }

    public static T Ensure<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    public static void Write(Object target, string field, System.Action<SerializedProperty> write)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[NpcPrefabKit] {target.GetType().Name} has no field '{field}'");
            return;
        }
        write(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
