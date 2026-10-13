using UnityEditor;
using UnityEngine;
using CrabSystem;

// Tools → AI → Build Mirror NPC
//
// Builds Mirror_NPC as a prefab variant of Base_PC: same movement, abilities, statuses and animator as the player,
// but an NPC brain driven by GOAP (Fight Target goal) instead of input. Configures through its own entityId via
// PersistentNPCConfigurator, so it gets its model and faction from Mirror-Archetype and never touches the player's
// config (Known Issues B20).
//
// Re-running it rebuilds Mirror_NPC in place; scene instances keep their link.
public static class MirrorNpcBuilder
{
    private const string OutPath = "Assets/Database/Characters/Mirror/Mirror_NPC.prefab";
    private const string GoalPath = "Assets/Database/AI/Goals/Goal_FightTarget.asset";
    private const string ArchetypePath = "Assets/Database/Characters/Mirror/Mirror-Archetype.asset";
    private const string EntityId = "mirror_01";

    [MenuItem("Tools/AI/Build Mirror NPC")]
    private static void Build()
    {
        var goal = AssetDatabase.LoadAssetAtPath<GOAPGoal>(GoalPath);
        var archetype = AssetDatabase.LoadAssetAtPath<NPCArchetype>(ArchetypePath);

        if (goal == null || archetype == null)
        {
            Debug.LogError($"[MirrorNpcBuilder] Missing input — goal: {goal != null}, archetype: {archetype != null}");
            return;
        }

        GameObject root = NpcPrefabKit.InstantiateBasePc("Mirror_NPC");
        if (root == null) return;

        ControllerBrain brain = NpcPrefabKit.MakeNpc(root);
        GameObject ai = NpcPrefabKit.EnsureAiSystem(brain);

        var perception = NpcPrefabKit.Ensure<PerceptionModule>(ai);
        NpcPrefabKit.Write(perception, "requireLineOfSight", p => p.boolValue = false);
        NpcPrefabKit.Write(perception, "visionAngle", p => p.floatValue = 360f);

        var goap = NpcPrefabKit.Ensure<GOAPModule>(ai);
        goap.goalPool.Add(goal);
        goap.SelectionMode = GoalSelectionMode.HighestWeight;

        var config = root.AddComponent<PersistentNPCConfigurator>();
        NpcPrefabKit.Write(config, "entityId", p => p.stringValue = EntityId);
        NpcPrefabKit.Write(config, "archetype", p => p.objectReferenceValue = archetype);
        NpcPrefabKit.Write(config, "brain", p => p.objectReferenceValue = brain);

        PrefabUtility.SaveAsPrefabAsset(root, OutPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(OutPath);
        Debug.Log($"[MirrorNpcBuilder] Built {OutPath}. Drag it into the scene near the player.");
    }
}
