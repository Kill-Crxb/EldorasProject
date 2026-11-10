using System;
using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// The character's organs (Shonen_Traits.md §4a): one per body slot, each carrying its own rewards.
//
// Two ways in. A reward installs one (a trait's GrantOrganReward) and owns it: withdrawing the reward takes it back,
// and it isn't saved here, because the trait re-installs it on load. Or it's installed directly (harvested, swapped
// in by a ritual) with no source, and then it is saved here. Organs don't come with traits.
//
// Extract is the organ being taken out. It raises Extracted so whatever granted it can react; TraitModule drops the
// trait, since losing the eyes loses the trait.

namespace CrabSystem
{
    [Serializable]
    public class InstalledOrgan
    {
        public string organId;
        public string slot;
        public string sourceKey;

        public bool FromReward => !string.IsNullOrEmpty(sourceKey);
    }

    public class OrganModule : MonoBehaviour, IBrainModule, ISaveable
    {
        [SerializeField] private OrganCatalog catalog;

        public bool IsEnabled { get; set; } = true;

        private ControllerBrain brain;

        private readonly List<InstalledOrgan> organs = new();

        public IReadOnlyList<InstalledOrgan> Organs => organs;
        public OrganCatalog Catalog => catalog;

        public event Action Changed;
        public event Action<InstalledOrgan> Extracted;
        public event Action<ISaveable> Dirty;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
            if (catalog == null) Debug.LogError($"[OrganModule] No OrganCatalog on {brain.EntityName}.", this);
        }

        public void UpdateModule() { }

        // ---- Reads ----

        public InstalledOrgan InSlot(string slot)
        {
            foreach (InstalledOrgan organ in organs)
            {
                if (organ.slot == slot) return organ;
            }
            return null;
        }

        public OrganDefinition Definition(InstalledOrgan organ) => catalog != null ? catalog.Find(organ?.organId) : null;

        // ---- Entry points ----

        // sourceKey is the granting reward's key; leave it empty for an organ installed directly.
        public bool Install(string organId, string sourceKey = "")
        {
            OrganDefinition definition = catalog != null ? catalog.Find(organId) : null;
            if (definition == null)
            {
                Debug.LogError($"[OrganModule] '{organId}' isn't in the organ catalog.", this);
                return false;
            }
            if (!catalog.HasSlot(definition.slot))
            {
                Debug.LogError($"[OrganModule] '{organId}' fills slot '{definition.slot}', which isn't in {catalog.name}.", this);
                return false;
            }
            if (InSlot(definition.slot) != null) return false;

            var organ = new InstalledOrgan { organId = organId, slot = definition.slot, sourceKey = sourceKey ?? "" };
            organs.Add(organ);
            SetRewards(organ, true);
            Notify(organ);
            return true;
        }

        // A granting reward taking back what it installed. Not an extraction: nothing else is told.
        public void Withdraw(string sourceKey)
        {
            for (int i = organs.Count - 1; i >= 0; i--)
            {
                if (organs[i].sourceKey != sourceKey) continue;

                InstalledOrgan organ = organs[i];
                SetRewards(organ, false);
                organs.RemoveAt(i);
                Notify(organ);
            }
        }

        // The organ is taken out. Returns what came out, or null if the slot was empty.
        public InstalledOrgan Extract(string slot)
        {
            InstalledOrgan organ = InSlot(slot);
            if (organ == null) return null;

            SetRewards(organ, false);
            organs.Remove(organ);
            Notify(organ);
            Extracted?.Invoke(organ);
            return organ;
        }

        // ---- Applying ----

        private void SetRewards(InstalledOrgan organ, bool on)
        {
            OrganDefinition definition = Definition(organ);
            if (definition == null) return;

            for (int r = 0; r < definition.rewards.Count; r++)
            {
                Reward reward = definition.rewards[r];
                if (reward == null) continue;

                var context = new RewardContext(brain, $"organ:{organ.slot}:{r}");
                if (on) reward.Apply(context);
                else reward.Remove(context);
            }
        }

        // Only directly installed organs are saved, so only they mark the save dirty.
        private void Notify(InstalledOrgan organ)
        {
            if (!organ.FromReward) Dirty?.Invoke(this);
            Changed?.Invoke();
        }

        // ---- ISaveable ----

        public string GetSaveId() => "organs";

        public int GetSaveVersion() => 1;

        // After TraitModule (1010), so trait-granted organs hold their slots before saved ones are put back.
        public int LoadOrder => 1020;

        public string GetSaveData()
        {
            var data = new SaveData { version = GetSaveVersion() };
            foreach (InstalledOrgan organ in organs)
            {
                if (!organ.FromReward) data.organs.Add(organ);
            }
            return JsonUtility.ToJson(data);
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json) || catalog == null) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            for (int i = organs.Count - 1; i >= 0; i--)
            {
                if (organs[i].FromReward) continue;
                SetRewards(organs[i], false);
                organs.RemoveAt(i);
            }

            foreach (InstalledOrgan saved in data.organs)
            {
                if (!Install(saved.organId))
                    Debug.LogWarning($"[OrganModule] Saved organ '{saved.organId}' couldn't be put back; dropped.", this);
            }

            Changed?.Invoke();
        }

        [Serializable]
        private class SaveData
        {
            public int version;
            public List<InstalledOrgan> organs = new();
        }
    }
}
