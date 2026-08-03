using System.Collections.Generic;
using UnityEngine;

namespace RPG.Factions
{
    [CreateAssetMenu(menuName = "RPG/Factions/Faction Definition Database")]
    public class FactionDefinitionDatabase : ScriptableObject
    {
        [SerializeField] private List<FactionDefinition> factions = new List<FactionDefinition>();

        private Dictionary<string, FactionDefinition> factionLookup;

        #region Initialization

        public void Initialize()
        {
            factionLookup = new Dictionary<string, FactionDefinition>();
            foreach (var faction in factions)
            {
                if (faction != null)
                    factionLookup[faction.FactionId] = faction;
            }
        }

        #endregion

        #region Queries

        public FactionDefinition GetFaction(string factionId)
        {
            if (factionLookup == null)
                Initialize();

            factionLookup.TryGetValue(factionId, out var faction);
            return faction;
        }

        public string GetFactionName(string factionId)
        {
            var faction = GetFaction(factionId);
            return faction != null ? faction.DisplayName : factionId;
        }

        public Color GetFactionColor(string factionId)
        {
            var faction = GetFaction(factionId);
            return faction != null ? faction.FactionColor : Color.white;
        }

        #endregion
    }
}