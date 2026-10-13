using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrabSystem;

// Tools → AI → Build Base NPC (roadmap V4 / A3, tracker AU21). Rebuilds Base_NPC as a prefab variant of Base_PC, so
// every NPC built on it carries the player's modules (parkour movement, statuses, talents, guard, moveset) and a
// module added to Base_PC reaches NPCs with no second edit. The brain is an NPC driven by AI_System
// (AIControlSource, PerceptionModule, GOAPModule with no goals); a variant adds its goals.
//
// Kept from the old Base_NPC: its DialogueSystem settings. Dropped: Base_PC's starting katana (an NPC's gear comes
// from its archetype or variant). The asset keeps its GUID, but its root object is new, so every NPCSpawnConfig that
// pointed at Base_NPC is pointed at the new root. Rerunnable.
public static class BaseNpcBuilder
{
    [MenuItem("Tools/AI/Build Base NPC")]
    static void Build()
    {
        List<NPCSpawnConfig> spawners = SpawnersUsingBaseNpc();
        GameObject old = PrefabUtility.LoadPrefabContents(NpcPrefabKit.BaseNpcPath);
        var oldDialogue = old.GetComponentInChildren<DialogueSystem>(true);

        GameObject root = NpcPrefabKit.InstantiateBasePc("Base_NPC");
        if (root == null)
        {
            PrefabUtility.UnloadPrefabContents(old);
            return;
        }

        ControllerBrain brain = NpcPrefabKit.MakeNpc(root);
        AddAi(brain);
        AddDialogue(brain, oldDialogue);
        ClearStartingGear(root);
        PrefabUtility.UnloadPrefabContents(old);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabKit.BaseNpcPath);
        Object.DestroyImmediate(root);

        foreach (NPCSpawnConfig spawner in spawners)
        {
            spawner.prefab = saved;
            EditorUtility.SetDirty(spawner);
        }
        AssetDatabase.SaveAssets();

        Selection.activeObject = saved;
        Debug.Log($"[BaseNpcBuilder] Built {NpcPrefabKit.BaseNpcPath} as a Base_PC variant; {spawners.Count} spawn config(s) re-pointed.");
    }

    // Read before the rebuild: once the root object is replaced the old reference can't be told apart from any
    // other missing one.
    static List<NPCSpawnConfig> SpawnersUsingBaseNpc()
    {
        var found = new List<NPCSpawnConfig>();
        foreach (string guid in AssetDatabase.FindAssets("t:NPCSpawnConfig"))
        {
            var spawner = AssetDatabase.LoadAssetAtPath<NPCSpawnConfig>(AssetDatabase.GUIDToAssetPath(guid));
            if (spawner == null || spawner.prefab == null) continue;
            if (AssetDatabase.GetAssetPath(spawner.prefab) == NpcPrefabKit.BaseNpcPath) found.Add(spawner);
        }
        return found;
    }

    static void AddAi(ControllerBrain brain)
    {
        GameObject ai = NpcPrefabKit.EnsureAiSystem(brain);
        NpcPrefabKit.Ensure<PerceptionModule>(ai);
        NpcPrefabKit.Ensure<GOAPModule>(ai);
    }

    static void AddDialogue(ControllerBrain brain, DialogueSystem from)
    {
        if (brain.GetComponentInChildren<DialogueSystem>(true) != null) return;

        var go = new GameObject("Dialogue_System");
        go.transform.SetParent(brain.transform, false);
        var dialogue = go.AddComponent<DialogueSystem>();
        if (from != null) EditorUtility.CopySerialized(from, dialogue);
    }

    static void ClearStartingGear(GameObject root)
    {
        var equipment = root.GetComponentInChildren<EquipmentSystem>(true);
        NpcPrefabKit.Write(equipment, "equippedItems", p => p.arraySize = 0);
    }
}
