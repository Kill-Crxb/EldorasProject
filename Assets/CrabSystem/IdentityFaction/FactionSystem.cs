using System.Collections.Generic;
using UnityEngine;

namespace RPG.Factions
{
    public class FactionSystem : MonoBehaviour, IBrainModule, ISaveable
    {
        #region Inspector

        [Header("Module Settings")]
        [SerializeField] private bool isEnabled = true;

        [Header("Faction Data")]
        [SerializeField] private string currentFactionId = "faction_neutral";

        #endregion

        #region Private Fields

        private ControllerBrain brain;
        private Dictionary<string, int> factionReputation = new Dictionary<string, int>();

        #endregion

        #region Properties

        public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

        public string CurrentFactionId
        {
            get => currentFactionId;
            set => currentFactionId = value;
        }

        public string CurrentFactionName
        {
            get => FactionManager.GetFactionDisplayName(currentFactionId);
        }

        public Color CurrentFactionColor
        {
            get => FactionManager.GetFactionDisplayColor(currentFactionId);
        }

        #endregion

        #region IBrainModule Implementation

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
            InitializeDefaultReputation();
        }

        public void UpdateModule()
        {
        }

        public void LateInitialize()
        {
        }

        #endregion

        #region Reputation Management

        private void InitializeDefaultReputation()
        {
            factionReputation.Clear();
        }

        public int GetReputation(string factionId)
        {
            return factionReputation.TryGetValue(factionId, out int rep) ? rep : 0;
        }

        public void ModifyReputation(string factionId, int delta)
        {
            if (!factionReputation.ContainsKey(factionId))
                factionReputation[factionId] = 0;

            factionReputation[factionId] += delta;
        }

        public bool IsFriendly(string factionId)
        {
            return GetReputation(factionId) > 0;
        }

        public bool IsHostile(string factionId)
        {
            return GetReputation(factionId) < 0;
        }

        public bool IsNeutral(string factionId)
        {
            return GetReputation(factionId) == 0;
        }

        #endregion

        #region ISaveable Implementation

        public string GetSaveId() => "faction";

        public string GetSaveData()
        {
            var data = new FactionSaveData
            {
                characterId = brain.Identity.EntityId,
                currentFactionId = currentFactionId,
                reputation = new List<FactionRepEntry>()
            };

            foreach (var kvp in factionReputation)
            {
                data.reputation.Add(new FactionRepEntry { factionId = kvp.Key, value = kvp.Value });
            }

            return JsonUtility.ToJson(data);
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<FactionSaveData>(json);
            currentFactionId = data.currentFactionId;

            factionReputation.Clear();
            foreach (var entry in data.reputation)
            {
                factionReputation[entry.factionId] = entry.value;
            }
        }

        public int GetSaveVersion() => 1;

        #endregion
    }
}