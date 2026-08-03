using System;
using System.Collections.Generic;

namespace RPG.Factions
{
    [Serializable]
    public class FactionRepEntry
    {
        public string factionId;
        public int value;
    }

    [Serializable]
    public class FactionSaveData
    {
        public string characterId;
        public string currentFactionId;
        public List<FactionRepEntry> reputation;
    }
}