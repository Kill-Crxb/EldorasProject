using System;
using UnityEngine;

[Serializable]
public class CharacterConfigData
{
    [Header("Identity")]
    public string characterId;
    // What kind of thing this is (wolf, bandit), from its NPCArchetype; empty for the player. Quests, loot and
    // per-archetype rules key on it, since characterId is the instance (Known Issues B19).
    [IdRef(IdKind.Archetype)] public string archetypeId;
    public string displayName;
    // Empty means "whoever configured this entity already decided" — the prefab's own
    // FactionSystem, or an NPC archetype. "faction_neutral" was a default that matched no
    // FactionDefinition in the database, so any caller that left it alone was asking for a
    // faction that does not exist.
    public string factionId = "";
    public int level = 1;

    [Header("Visuals")]
    public string modelId;

    [Header("Stats")]
    public StatBaseOverride[] baseStatOverrides;

    [Header("Persistence (for unique NPCs)")]
    public bool isPersistent = false;
    public string saveFilePath;

    public CharacterConfigData() { }

    public CharacterConfigData(string displayName, string factionId, string modelId, StatBaseOverride[] baseStatOverrides, int level)
    {
        this.displayName = displayName;
        this.factionId = factionId;
        this.modelId = modelId;
        this.baseStatOverrides = baseStatOverrides;
        this.level = level;
    }
}