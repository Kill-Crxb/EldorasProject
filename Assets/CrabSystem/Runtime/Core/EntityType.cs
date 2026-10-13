// What controls an entity. The brain's entityType is the one source; IdentitySystem forwards it.
public enum EntityType
{
    Entity,     // Generic entity (default)
    Player,     // Player-controlled character
    NPC,        // Non-player character
    Prop,       // Interactive prop
    Enemy,      // Enemy character
    Neutral     // Neutral NPC
}