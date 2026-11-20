using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// Holds this character's conditional rewards (ConditionalReward) and keeps each inner reward applied exactly while
// its conditions hold. Event-driven: it re-checks when a fact one of them reads changes, or when a status comes or
// goes if any of them reads statuses. Nothing polls.
//
// Rewards can arrive before LateInitialize (talents and traits apply from their own LateInitialize), so Set checks at
// once against the brain; the subscriptions only matter for later changes.
namespace CrabSystem
{
    public class ConditionModule : MonoBehaviour, IBrainModule
    {
        // A held reward can raise a fact another entry reads, which re-checks inside the check. A few passes settle
        // any real chain; past that the rewards are fighting each other and the last pass stands.
        private const int MaxPasses = 4;

        private class Entry
        {
            public ConditionalReward Reward;
            public RewardContext Context;
            public bool Held;
        }

        public bool IsEnabled { get; set; } = true;

        private ControllerBrain brain;
        private Blackboard blackboard;
        private StatusSystem statuses;

        private readonly Dictionary<string, Entry> entries = new();
        private readonly HashSet<int> watchedFacts = new();
        private readonly List<string> keys = new();
        private bool watchesStatus;
        private bool checking;
        private bool recheck;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void LateInitialize()
        {
            blackboard = brain.Blackboard;
            statuses = brain.GetModule<StatusSystem>();

            if (blackboard != null) blackboard.OnBoolChanged += HandleFactChanged;
            if (statuses != null) statuses.OnStatusApplied += HandleStatusChanged;
            if (statuses != null) statuses.OnStatusRemoved += HandleStatusChanged;

            Check();
        }

        public void UpdateModule() { }

        private void OnDestroy()
        {
            if (blackboard != null) blackboard.OnBoolChanged -= HandleFactChanged;
            if (statuses != null) statuses.OnStatusApplied -= HandleStatusChanged;
            if (statuses != null) statuses.OnStatusRemoved -= HandleStatusChanged;
        }

        // ---- Entry points (ConditionalReward) ----

        public void Set(RewardContext context, ConditionalReward reward)
        {
            if (reward == null || string.IsNullOrEmpty(context.SourceKey)) return;

            // Re-applying under one key replaces, like every reward: a level-up re-applies with the new scale.
            Remove(context.SourceKey);
            entries[context.SourceKey] = new Entry { Reward = reward, Context = context };
            RebuildWatch();
            Check();
        }

        public void Remove(string key)
        {
            if (!entries.TryGetValue(key, out Entry entry)) return;

            entries.Remove(key);
            if (entry.Held && entry.Reward.Inner != null) entry.Reward.Inner.Remove(entry.Context);
            RebuildWatch();
        }

        public bool IsHeld(string key) => entries.TryGetValue(key, out Entry entry) && entry.Held;

        // ---- Events ----

        private void HandleFactChanged(int key, bool value)
        {
            if (watchedFacts.Contains(key)) Check();
        }

        private void HandleStatusChanged(StatusInstance instance)
        {
            if (watchesStatus) Check();
        }

        // ---- Checking ----

        private void Check()
        {
            if (checking)
            {
                recheck = true;
                return;
            }

            checking = true;
            for (int pass = 0; pass < MaxPasses; pass++)
            {
                recheck = false;
                CheckAll();
                if (!recheck) break;
            }
            checking = false;
        }

        // Keys are copied first: an inner reward may itself be conditional and add an entry while this runs.
        private void CheckAll()
        {
            keys.Clear();
            keys.AddRange(entries.Keys);

            foreach (string key in keys)
            {
                if (!entries.TryGetValue(key, out Entry entry)) continue;

                bool holds = Conditions.Hold(entry.Reward.When, brain, null);
                if (holds == entry.Held) continue;

                entry.Held = holds;
                Reward inner = entry.Reward.Inner;
                if (inner == null) continue;

                if (holds) inner.Apply(entry.Context);
                else inner.Remove(entry.Context);
            }
        }

        private void RebuildWatch()
        {
            watchedFacts.Clear();
            watchesStatus = false;

            foreach (Entry entry in entries.Values)
            {
                Conditions.AddFactKeys(entry.Reward.When, watchedFacts);
                watchesStatus |= Conditions.ReadsStatus(entry.Reward.When);
            }
        }
    }
}
