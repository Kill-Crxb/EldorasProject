// The Resources folders the framework loads definitions from. A game puts its assets under any
// Resources/<folder> with these names, or assigns them on Manager_Brain's managers instead; an empty
// folder only logs a warning. Hit-state statuses are loaded by name from HitStates.

namespace CrabSystem
{
    public static class CrabResources
    {
        public const string Abilities = "AbilityDatabase";
        public const string Items = "ItemDatabase";
        public const string Stats = "StatDatabase";
        public const string Keywords = "Keywords";
        public const string HitStates = "Statuses/HitStates";
    }
}
