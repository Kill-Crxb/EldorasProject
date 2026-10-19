using System;
using UnityEngine;

// Level and experience progression for one entity.
//
// Level is also published into the stat store as character.level, because
// StatDerivationSystem drives off stat ids and has no way to read a field on this module.
// Publishing it means "+5 max health per level" is authored in the derivation table
// exactly like "+5 per Body", with no second mechanism.
//
// It is published as a contribution rather than a value, so the stat store never owns it:
// the Progression schema is marked derived, nothing about level is written to stats.json,
// and this module stays the only truth.

namespace CrabSystem
{
    public class RPGSystem : MonoBehaviour, IBrainModule, ISaveable, IBarSource
    {
        public int InitOrder => 140;

        [Header("Progression")]
        [SerializeField] private int currentLevel = 1;
        [Tooltip("Cap for a character without a TalentModule. With one, TalentRules.levelCap is the cap, so talents and levels agree.")]
        [SerializeField] private int maxLevel = 20;
        [SerializeField] private int currentXP;

        [Header("XP Curve")]
        [SerializeField] private int baseXPRequired = 100;
        [SerializeField] private float xpExponent = 1.5f;

        private const string LevelStatId = StatIds.Level;
        private const string LevelSourceKey = "rpg";

        private ControllerBrain brain;
        private IStatProvider stats;
        private int xpToNextLevel;

        public bool IsEnabled { get; set; } = true;

        public int CurrentLevel => currentLevel;
        public int MaxLevel => maxLevel;
        public int CurrentXP => currentXP;
        public int XPToNextLevel => xpToNextLevel;

        // The XP bar (BarView, Experience): progress toward the next level, full at the cap.
        float IBarSource.Current => currentLevel >= maxLevel ? 1f : currentXP;
        float IBarSource.Max => currentLevel >= maxLevel ? 1f : xpToNextLevel;

        public event Action<int, int> OnLevelChanged;
        public event Action<int> OnXPChanged;

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
            stats = brain.Stats;
            var talentRules = brain.GetModule<TalentModule>()?.Rules;
            if (talentRules != null) maxLevel = talentRules.levelCap;
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);
            PublishLevel();
        }

        public void LateInitialize() { }

        public void UpdateModule() { }

        public void AddXP(int amount)
        {
            if (amount <= 0 || currentLevel >= maxLevel) return;

            currentXP += amount;
            OnXPChanged?.Invoke(currentXP);

            while (currentXP >= xpToNextLevel && currentLevel < maxLevel)
                LevelUp();
        }

        public void SetLevel(int level)
        {
            int clamped = Mathf.Clamp(level, 1, maxLevel);
            if (clamped == currentLevel) return;

            int oldLevel = currentLevel;
            currentLevel = clamped;
            currentXP = 0;
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

            PublishLevel();
            OnLevelChanged?.Invoke(oldLevel, currentLevel);
            OnXPChanged?.Invoke(currentXP);
        }

        public float GetLevelProgress()
        {
            if (currentLevel >= maxLevel || xpToNextLevel <= 0) return 1f;
            return Mathf.Clamp01((float)currentXP / xpToNextLevel);
        }

        private void LevelUp()
        {
            int oldLevel = currentLevel;
            currentLevel++;
            currentXP -= xpToNextLevel;
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

            PublishLevel();
            OnLevelChanged?.Invoke(oldLevel, currentLevel);
        }

        private int CalculateXPForLevel(int level)
        {
            if (level <= 1) return 0;
            return Mathf.RoundToInt(baseXPRequired * Mathf.Pow(level, xpExponent));
        }

        // Pushes the current level everywhere that needs to see it. Called from every path
        // that can change the level, including the save load, so a restored character
        // recalculates its derived resources rather than trusting a stale number.
        private void PublishLevel()
        {
            // Entities without the Progression schema — NPCs today — simply do not scale by
            // level. Skipping quietly avoids a warning per NPC per spawn.
            if (stats == null || !stats.HasStat(LevelStatId)) return;

            stats.AddContribution(LevelStatId, LevelSourceKey, currentLevel);
        }

        public string GetSaveId() => "rpg";

        public int GetSaveVersion() => 2;

        public string GetSaveData()
        {
            return JsonUtility.ToJson(new SaveData
            {
                version = GetSaveVersion(),
                level = currentLevel,
                xp = currentXP
            });
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return;

            currentLevel = Mathf.Clamp(data.level, 1, maxLevel);
            currentXP = Mathf.Max(0, data.xp);
            xpToNextLevel = CalculateXPForLevel(currentLevel + 1);

            PublishLevel();
        }

        [Serializable]
        private class SaveData
        {
            public int version;
            public int level;
            public int xp;
        }
    }
}
