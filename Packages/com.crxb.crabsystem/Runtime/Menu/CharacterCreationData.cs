using System.Collections.Generic;

namespace CrabSystem
{
    [System.Serializable]
    public struct CharacterCreationData
    {
        public string characterName;
        public string accountName;
        public string modelId;

        // One option per appearance group of the chosen model. Written to model.json at creation.
        public List<AppearanceChoice> appearance;

        // Starting core stats. Leave null and SaveManager seeds a flat baseline instead.
        public StatBaseOverride[] baseStats;
    }
}
