/// <summary>
/// Defines the type of entity for identification purposes
/// </summary>
public enum EntityType
{
    Entity,     // Generic entity (default)
    Player,     // Player-controlled character
    NPC,        // Non-player character
    Prop,       // Interactive prop
    Enemy,      // Enemy character
    Neutral     // Neutral NPC
}