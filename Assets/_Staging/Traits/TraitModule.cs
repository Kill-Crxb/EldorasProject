using System;
using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// The character's traits (Character_Roll_Traits.md). Rolled at creation or granted later. A dormant trait is held but
// gives nothing until the character reaches its reveal level; once revealed it stays revealed.
//
// The save holds only ids, sources and whether each is revealed. Everything a trait grants is derived from that on
// load, under trait:{id}:{index}, so it can always be re-applied or taken back exactly.
//
// A trait that grants an organ (Shonen_Traits.md §4a) is lost when that organ is extracted.

namespace CrabSystem
{
    public enum TraitSource { Rolled, Granted }

    [Serializable]
    public class RolledTrait
    {
        public string traitId;
        public TraitSource source;
        public bool revealed;
    }

    public class TraitModule : MonoBehaviour, IBrainModule, ISaveable
    {
        [SerializeField] private TraitCatalog catalog;

        public bool IsEnabled { get; set; } = true;

        private ControllerBrain brain;
        private RPGSystem rpg;
        private OrganModule organs;

        private readonly List<RolledTrait> traits = new();

        public IReadOnlyList<RolledTrait> Traits => traits;
        public TraitCatalog Catalog => catalog;

        public event Action Changed;
        public event Action<ISaveable> Dirty;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            if (catalog == null)
            {
                Debug.LogError($"[TraitModule] No TraitCatalog on {brain.EntityName}.", this);
                return;
            }

            rpg = brain.RPG;
            if (rpg != null) rpg.OnLevelChanged += HandleLevelChanged;

            organs = brain.GetModule<OrganModule>();
            if (organs != null) organs.Extracted += HandleOrganExtracted;

            RevealDue();
            ApplyAll();
        }

        public void UpdateModule() { }

        private void OnDestroy()
        {
            if (rpg != null) rpg.OnLevelChanged -= HandleLevelChanged;
            if (organs != null) organs.Extracted -= HandleOrganExtracted;
        }

        private int Level => rpg != null ? rpg.CurrentLevel : 1;

        // ---- Reads ----

        public bool Has(string traitId) => Find(traitId) != null;

        public bool IsRevealed(string traitId) => Find(traitId)?.revealed ?? false;

        public TraitDefinition Definition(RolledTrait trait) => catalog != null ? catalog.Find(trait?.traitId) : null;

        // ---- Entry points ----

        // The creation roll: replaces every rolled trait, keeps granted ones.
        public void SetRolled(IEnumerable<string> traitIds)
        {
            for (int i = traits.Count - 1; i >= 0; i--)
            {
                if (traits[i].source != TraitSource.Rolled) continue;
                SetRewards(traits[i], false);
                traits.RemoveAt(i);
            }

            foreach (string traitId in traitIds) Add(traitId, TraitSource.Rolled);

            Notify();
        }

        public bool Grant(string traitId)
        {
            if (!Add(traitId, TraitSource.Granted)) return false;

            Notify();
            return true;
        }

        public bool Remove(string traitId)
        {
            RolledTrait trait = Find(traitId);
            if (trait == null) return false;

            SetRewards(trait, false);
            traits.Remove(trait);
            Notify();
            return true;
        }

        private bool Add(string traitId, TraitSource source)
        {
            TraitDefinition definition = catalog != null ? catalog.Find(traitId) : null;
            if (definition == null)
            {
                Debug.LogError($"[TraitModule] '{traitId}' isn't in the trait catalog.", this);
                return false;
            }
            if (Has(traitId)) return false;

            var trait = new RolledTrait { traitId = traitId, source = source, revealed = !definition.IsDormantAt(Level) };
            traits.Add(trait);
            SetRewards(trait, trait.revealed);
            return true;
        }

        // ---- Revealing and applying ----

        private void HandleLevelChanged(int oldLevel, int newLevel)
        {
            if (RevealDue()) Notify();
        }

        // The organ came from one of this trait's rewards: losing it loses the trait.
        private void HandleOrganExtracted(InstalledOrgan organ)
        {
            RolledTrait owner = OwnerOf(organ.sourceKey);
            if (owner != null) Remove(owner.traitId);
        }

        private RolledTrait OwnerOf(string sourceKey)
        {
            foreach (RolledTrait trait in traits)
            {
                TraitDefinition definition = Definition(trait);
                if (definition == null) continue;

                for (int r = 0; r < definition.rewards.Count; r++)
                {
                    if (RewardKey(trait, r) == sourceKey) return trait;
                }
            }
            return null;
        }

        private bool RevealDue()
        {
            bool any = false;
            foreach (RolledTrait trait in traits)
            {
                if (trait.revealed) continue;

                TraitDefinition definition = Definition(trait);
                if (definition == null || definition.IsDormantAt(Level)) continue;

                trait.revealed = true;
                SetRewards(trait, true);
                any = true;
            }
            return any;
        }

        private void ApplyAll()
        {
            foreach (RolledTrait trait in traits) SetRewards(trait, trait.revealed);
        }

        // The index keeps two rewards on one trait from sharing a key when they feed the same stat.
        private void SetRewards(RolledTrait trait, bool on)
        {
            TraitDefinition definition = Definition(trait);
            if (definition == null) return;

            for (int r = 0; r < definition.rewards.Count; r++)
            {
                Reward reward = definition.rewards[r];
                if (reward == null) continue;

                var context = new RewardContext(brain, RewardKey(trait, r));
                if (on) reward.Apply(context);
                else reward.Remove(context);
            }
        }

        private static string RewardKey(RolledTrait trait, int index) => $"trait:{trait.traitId}:{index}";

        private RolledTrait Find(string traitId)
        {
            foreach (RolledTrait trait in traits)
            {
                if (trait.traitId == traitId) return trait;
            }
            return null;
        }

        private void Notify()
        {
            Dirty?.Invoke(this);
            Changed?.Invoke();
        }

        // ---- ISaveable ----

        public string GetSaveId() => "traits";

        public int GetSaveVersion() => 1;

        // After RPGSystem, so reveals check the loaded level.
        public int LoadOrder => 1010;

        public string GetSaveData()
        {
            var data = new SaveData { version = GetSaveVersion() };
            data.traits.AddRange(traits);
            return JsonUtility.ToJson(data);
        }

        // A trait that no longer exists is dropped with a warning rather than failing the whole load.
        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json) || catalog == null) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            foreach (RolledTrait trait in traits) SetRewards(trait, false);
            traits.Clear();

            foreach (RolledTrait trait in data.traits)
            {
                if (catalog.Find(trait.traitId) == null)
                {
                    Debug.LogWarning($"[TraitModule] Saved trait '{trait.traitId}' isn't in {catalog.name}; dropped.", this);
                    continue;
                }
                traits.Add(trait);
            }

            RevealDue();
            ApplyAll();
            Changed?.Invoke();
        }

        [Serializable]
        private class SaveData
        {
            public int version;
            public List<RolledTrait> traits = new();
        }
    }
}
