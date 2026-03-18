/// <summary>
/// Base interface for all entity configuration data
/// Used by CharacterConfigurationHandler to initialize entities
/// </summary>
public interface IEntityConfig
{
    // Marker interface - implementations provide specific config data
}

/// <summary>
/// Player configuration data
/// Used when loading a player from save
/// </summary>
[System.Serializable]
public class PlayerConfig : IEntityConfig
{
    public int saveSlot = 0;
    public string playerName = "Player";
}

/// <summary>
/// NPC configuration data
/// Used when spawning NPCs from archetypes
/// </summary>
[System.Serializable]
public class NPCConfig : IEntityConfig
{
    public NPCArchetype archetype;
    public int overrideLevel = -1;  // -1 = use archetype base level
}

// Note: NPCFaction enum already exists in NPCEnums.cs
// No need to redefine it here

// Note: Companion entity type not yet implemented
// CompanionConfig class removed until EntityType.Companion is added