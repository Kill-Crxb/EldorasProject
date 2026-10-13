// Where an entity's ability intent comes from: InputSystem for the player, AIControlSource for an
// AI-driven NPC. Gameplay reads intent through brain.GetProvider<IAbilityControlSource>(), never the
// keyboard or the AI directly; a player-only source switched off on an NPC doesn't answer.

namespace CrabSystem
{
    public interface IAbilityControlSource
    {
        // The block key. GuardModule keeps a guard up only while this is true.
        bool GuardHeld { get; }

        bool IsActive { get; }
    }
}
