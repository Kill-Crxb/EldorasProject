// A module that can refuse an interaction with its brain, per actor: a corpse whose loot belongs to its killer
// for a while (LootModule). InteractionSystem asks it after the range and lock checks.

namespace CrabSystem
{
    public interface IInteractionGate
    {
        bool Allows(ControllerBrain actor);
    }
}
