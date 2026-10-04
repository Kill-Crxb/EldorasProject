using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NinjaGame.Stats;
using RPG.Factions;
using UnityEditor;
using UnityEngine;

// Where each kind of id comes from. The asset that defines an id is its only source, so this is
// the one place that knows which asset type and field that is. Results are cached until the
// project changes.
public static class IdSources
{
    private static readonly Dictionary<IdKind, string[]> cache = new();

    static IdSources()
    {
        EditorApplication.projectChanged += cache.Clear;
    }

    public static string[] Get(IdKind kind)
    {
        if (cache.TryGetValue(kind, out var ids)) return ids;

        ids = Gather(kind).Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id).ToArray();
        cache[kind] = ids;
        return ids;
    }

    public static bool Exists(IdKind kind, string id) => Array.BinarySearch(Get(kind), id, StringComparer.Ordinal) >= 0;

    private static IEnumerable<string> Gather(IdKind kind)
    {
        switch (kind)
        {
            case IdKind.Fact: return Facts();
            case IdKind.Stat: return Assets<StatSchema>().SelectMany(s => s.Entries).Select(e => e?.id);
            case IdKind.Ability: return Assets<AbilityDefinition>().Select(a => a.abilityId);
            case IdKind.Item: return Assets<ItemDefinition>().Select(i => i.itemId);
            case IdKind.Resource: return Assets<ResourceDefinition>().Select(r => r.resourceId);
            case IdKind.Status: return Assets<StatusDefinition>().Select(s => s.id);
            case IdKind.Faction: return Assets<FactionDefinition>().Select(f => f.FactionId);
            case IdKind.EquipmentSlot: return Assets<EquipmentSlotDefinition>().Select(s => s.slotId);
            case IdKind.Archetype: return Assets<NPCArchetype>().Select(a => a.archetypeId);
            case IdKind.Model: return Assets<ModelDatabase>().SelectMany(d => d.AllModels ?? Array.Empty<ModelDatabase.ModelVariant>()).Select(m => m?.modelId);
            case IdKind.Scene: return EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path));
        }

        Debug.LogError($"[IdSources] No source for id kind {kind}.");
        return Array.Empty<string>();
    }

    // The schema is meant to be the one fact register (Audit 3 F2). Until every fact is in it, the
    // code-written facts (BlackboardKey's constants, named after their key) and the condition
    // outputs count too, so the dropdown offers every fact that can actually be raised.
    private static IEnumerable<string> Facts()
    {
        var schema = Assets<BlackboardSchema>().SelectMany(s => s.keys).Select(k => k?.keyName);
        var conditions = Assets<BlackboardCondition>().Select(c => c.OutputFactKey);
        var constants = typeof(BlackboardKey).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(int)).Select(f => f.Name);
        return schema.Concat(conditions).Concat(constants);
    }

    private static IEnumerable<T> Assets<T>() where T : ScriptableObject
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) yield return asset;
        }
    }
}
