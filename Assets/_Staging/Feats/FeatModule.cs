using System;
using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// The character's feats (Feats.md). Every feat is held by a grant: the source key of whatever granted it (a trait
// reward, a talent node). Withdrawing that key takes the grant back.
//
// Nothing is saved here. Each granter re-applies its GrantFeatReward on load, which rebuilds the grants.
//
// A feat's rewards apply with Scale = character level and re-apply on level-up, so a Stat Rate reward on a feat is
// "per level". A non-repeatable feat applies once under feat:{id}:{index} while any grant holds it; a repeatable one
// applies per grant under feat:{id}:{grantKey}:{index}.

namespace CrabSystem
{
    [Serializable]
    public class FeatGrant
    {
        public FeatDefinition feat;
        public string sourceKey;
    }

    public class FeatModule : MonoBehaviour, IBrainModule
    {
        public bool IsEnabled { get; set; } = true;

        private ControllerBrain brain;
        private RPGSystem rpg;

        private readonly List<FeatGrant> grants = new();

        public IReadOnlyList<FeatGrant> Grants => grants;

        public event Action Changed;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            rpg = brain.RPG;
            if (rpg != null) rpg.OnLevelChanged += HandleLevelChanged;
        }

        public void UpdateModule() { }

        private void OnDestroy()
        {
            if (rpg != null) rpg.OnLevelChanged -= HandleLevelChanged;
        }

        // Grants can arrive before LateInitialize (a trait applying on load), so the level is read from the brain.
        private int Level => brain.RPG != null ? brain.RPG.CurrentLevel : 1;

        // ---- Reads ----

        public bool Has(string featId) => FirstGrantOf(featId) != null;

        // ---- Entry points ----

        public void Grant(FeatDefinition feat, string sourceKey)
        {
            if (feat == null || string.IsNullOrEmpty(sourceKey)) return;

            // Re-applying the same reward under the same key replaces, like every other reward.
            Withdraw(sourceKey);

            var grant = new FeatGrant { feat = feat, sourceKey = sourceKey };
            bool firstOfFeat = !Has(feat.featId);
            grants.Add(grant);

            if (feat.repeatable || firstOfFeat) SetRewards(grant, true);
            Changed?.Invoke();
        }

        public void Withdraw(string sourceKey)
        {
            FeatGrant grant = FindBySource(sourceKey);
            if (grant == null) return;

            grants.Remove(grant);
            if (grant.feat.repeatable || !Has(grant.feat.featId)) SetRewards(grant, false);
            Changed?.Invoke();
        }

        // ---- Applying ----

        private void HandleLevelChanged(int oldLevel, int newLevel)
        {
            foreach (FeatGrant grant in grants)
            {
                if (grant.feat.repeatable || FirstGrantOf(grant.feat.featId) == grant) SetRewards(grant, true);
            }
        }

        private void SetRewards(FeatGrant grant, bool on)
        {
            List<Reward> rewards = grant.feat.rewards;
            for (int r = 0; r < rewards.Count; r++)
            {
                if (rewards[r] == null) continue;

                var context = new RewardContext(brain, RewardKey(grant, r), Level);
                if (on) rewards[r].Apply(context);
                else rewards[r].Remove(context);
            }
        }

        private static string RewardKey(FeatGrant grant, int index)
        {
            return grant.feat.repeatable
                ? $"feat:{grant.feat.featId}:{grant.sourceKey}:{index}"
                : $"feat:{grant.feat.featId}:{index}";
        }

        private FeatGrant FirstGrantOf(string featId)
        {
            foreach (FeatGrant grant in grants)
            {
                if (grant.feat.featId == featId) return grant;
            }
            return null;
        }

        private FeatGrant FindBySource(string sourceKey)
        {
            foreach (FeatGrant grant in grants)
            {
                if (grant.sourceKey == sourceKey) return grant;
            }
            return null;
        }
    }
}
