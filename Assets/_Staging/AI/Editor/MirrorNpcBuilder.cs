using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools → AI → Build Mirror NPC
///
/// Builds Mirror_NPC as a prefab variant of Base_PC: same movement, abilities, statuses and
/// animator as the player, but an NPC brain driven by GOAP (Fight Target goal) instead of input.
/// Configures through its own entityId via PersistentNPCConfigurator, so it gets its model and
/// faction from Mirror-Archetype and never touches the player's config (Known Issues B20).
///
/// Re-running it rebuilds Mirror_NPC in place; scene instances keep their link.
/// </summary>
public static class MirrorNpcBuilder
{
    private const string BasePath = "Assets/Database/Characters/PlayerCharacter/Base_PC.prefab";
    private const string OutPath = "Assets/Database/Characters/Mirror/Mirror_NPC.prefab";
    private const string GoalPath = "Assets/Database/AI/Goals/Goal_FightTarget.asset";
    private const string ArchetypePath = "Assets/Database/Characters/Mirror/Mirror-Archetype.asset";
    private const string EntityId = "mirror_01";

    [MenuItem("Tools/AI/Build Mirror NPC")]
    private static void Build()
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath);
        var goal = AssetDatabase.LoadAssetAtPath<GOAPGoal>(GoalPath);
        var archetype = AssetDatabase.LoadAssetAtPath<NPCArchetype>(ArchetypePath);

        if (basePrefab == null || goal == null || archetype == null)
        {
            Debug.LogError($"[MirrorNpcBuilder] Missing input — Base_PC: {basePrefab != null}, goal: {goal != null}, archetype: {archetype != null}");
            return;
        }

        var root = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        root.name = "Mirror_NPC";

        var brain = root.GetComponentInChildren<ControllerBrain>(true);
        Write(brain, "entityType", p => p.intValue = (int)EntityType.NPC);

        DisablePlayerView(root);

        var ai = new GameObject("AI_System");
        ai.transform.SetParent(brain.transform, false);
        ai.AddComponent<AIControlSource>();

        var perception = ai.AddComponent<PerceptionModule>();
        Write(perception, "requireLineOfSight", p => p.boolValue = false);
        Write(perception, "visionAngle", p => p.floatValue = 360f);

        var goap = ai.AddComponent<GOAPModule>();
        goap.goalPool.Add(goal);
        goap.SelectionMode = GoalSelectionMode.HighestWeight;

        var config = root.AddComponent<PersistentNPCConfigurator>();
        Write(config, "entityId", p => p.stringValue = EntityId);
        Write(config, "archetype", p => p.objectReferenceValue = archetype);
        Write(config, "brain", p => p.objectReferenceValue = brain);

        PrefabUtility.SaveAsPrefabAsset(root, OutPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(OutPath);
        Debug.Log($"[MirrorNpcBuilder] Built {OutPath}. Drag it into the scene near the player.");
    }

    // The player's camera rig, audio listener and post-process volumes come along with Base_PC.
    // An NPC must not render a second view or override the player's post-processing.
    private static void DisablePlayerView(GameObject root)
    {
        foreach (var cam in root.GetComponentsInChildren<Camera>(true))
            cam.gameObject.SetActive(false);

        foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
            listener.gameObject.SetActive(false);

        foreach (var volume in root.GetComponentsInChildren<Volume>(true))
            volume.gameObject.SetActive(false);
    }

    private static void Write(Object target, string field, System.Action<SerializedProperty> write)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[MirrorNpcBuilder] {target.GetType().Name} has no field '{field}'");
            return;
        }
        write(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
