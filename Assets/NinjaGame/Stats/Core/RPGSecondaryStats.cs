// RPG Secondary Stats - Streamlined 18-stat system with modifier-based scaling
using System.Linq;
using UnityEngine;

public class RPGSecondaryStats : MonoBehaviour, IPlayerModule, ICombatStatsProvider
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugStats = false;
    [SerializeField] private bool logStatChanges = false;

    [Header("Stat Masking (Performance Optimization)")]
    [Tooltip("Mask out stats this entity doesn't use. Bears don't need magic stats, for example.")]
    [SerializeField] private StatMaskFlags enabledStats = StatMaskFlags.CalculateAll;
    [SerializeField] private bool showMaskingInfo = false;

    [Header("Physical Combat Stats")]
    [SerializeField] private StatCalculation physicalPower = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation physicalSpeed = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation physicalEfficiency = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation physicalReach = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation physicalDuration = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation physicalPenetration = new StatCalculation { baseValue = 0f };

    [Header("Magical Combat Stats")]
    [SerializeField] private StatCalculation magicalPower = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation magicalSpeed = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicalEfficiency = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation magicalReach = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicalDuration = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicalPenetration = new StatCalculation { baseValue = 0f };

    [Header("Defense Stats")]
    [SerializeField] private StatCalculation armor = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation resistance = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation evasion = new StatCalculation { baseValue = 0f };

    [Header("Resource Stats")]
    [SerializeField] private StatCalculation maxHealth = new StatCalculation { baseValue = 100f };
    [SerializeField] private StatCalculation regeneration = new StatCalculation { baseValue = 100f };
    [SerializeField] private StatCalculation maxStamina = new StatCalculation { baseValue = 100f };
    [SerializeField] private StatCalculation recovery = new StatCalculation { baseValue = 120f };
    [SerializeField] private StatCalculation maxMana = new StatCalculation { baseValue = 50f };
    [SerializeField] private StatCalculation recollection = new StatCalculation { baseValue = 80f };

    [System.Serializable]
    public class CoreStatScaling
    {
        [Header("Physical Combat")]
        public float physicalPower = 0f;
        public float physicalSpeed = 0f;
        public float physicalEfficiency = 0f;
        public float physicalReach = 0f;
        public float physicalDuration = 0f;
        public float physicalPenetration = 0f;

        [Header("Magical Combat")]
        public float magicalPower = 0f;
        public float magicalSpeed = 0f;
        public float magicalEfficiency = 0f;
        public float magicalReach = 0f;
        public float magicalDuration = 0f;
        public float magicalPenetration = 0f;

        [Header("Defense")]
        public float armor = 0f;
        public float resistance = 0f;
        public float evasion = 0f;

        [Header("Resources")]
        public float maxHealth = 0f;
        public float regeneration = 0f;
        public float maxStamina = 0f;
        public float recovery = 0f;
        public float maxMana = 0f;
        public float recollection = 0f;
    }

    [Header("Core Stat Scaling")]
    [SerializeField] private CoreStatScaling mindScaling = new CoreStatScaling();
    [SerializeField] private CoreStatScaling bodyScaling = new CoreStatScaling();
    [SerializeField] private CoreStatScaling spiritScaling = new CoreStatScaling();
    [SerializeField] private CoreStatScaling resilienceScaling = new CoreStatScaling();
    [SerializeField] private CoreStatScaling enduranceScaling = new CoreStatScaling();
    [SerializeField] private CoreStatScaling insightScaling = new CoreStatScaling();

    [Header("Level Scaling")]
    [SerializeField] private float healthPerLevel = 2f;
    [SerializeField] private float manaPerLevel = 1.5f;
    [SerializeField] private float staminaPerLevel = 1f;
    [SerializeField] private float regenerationPerLevel = 3.3f;
    [SerializeField] private float recollectionPerLevel = 2.5f;
    [SerializeField] private float recoveryPerLevel = 4f;

    // References
    private ControllerBrain brain;
    private RPGCoreStats coreStats;
    private StatAllocationSystem allocation;
    private bool isFullyInitialized = false;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }


    /// <summary>
    /// Check if a specific stat should be calculated based on the mask.
    /// </summary>
    private bool IsStatEnabled(StatMaskFlags flag)
    {
        return (enabledStats & flag) != 0;
    }

    #region Physical Properties
    public float PhysicalPowerFinal => physicalPower.FinalStat;
    public float PhysicalSpeedFinal => physicalSpeed.FinalStat;
    public float PhysicalEfficiencyFinal => physicalEfficiency.FinalStat;
    public float PhysicalReachFinal => physicalReach.FinalStat;
    public float PhysicalDurationFinal => physicalDuration.FinalStat;
    public float PhysicalPenetrationFinal => physicalPenetration.FinalStat;
    #endregion

    #region Magical Properties
    public float MagicalPowerFinal => magicalPower.FinalStat;
    public float MagicalSpeedFinal => magicalSpeed.FinalStat;
    public float MagicalEfficiencyFinal => magicalEfficiency.FinalStat;
    public float MagicalReachFinal => magicalReach.FinalStat;
    public float MagicalDurationFinal => magicalDuration.FinalStat;
    public float MagicalPenetrationFinal => magicalPenetration.FinalStat;
    #endregion

    #region Defense Properties
    public float ArmorFinal => armor.FinalStat;
    public float ResistanceFinal => resistance.FinalStat;
    public float EvasionFinal => evasion.FinalStat;
    #endregion

    #region Resource Properties
    public float MaxHealthFinal => maxHealth.FinalStat;
    public float RegenerationFinal => regeneration.FinalStat;
    public float MaxStaminaFinal => maxStamina.FinalStat;
    public float RecoveryFinal => recovery.FinalStat;
    public float MaxManaFinal => maxMana.FinalStat;
    public float RecollectionFinal => recollection.FinalStat;
    #endregion

    // Backward compatibility - keeping old property names
    public float MeleePowerFinal => PhysicalPowerFinal;
    public float MeleeSpeedFinal => PhysicalSpeedFinal;
    public float MagicPowerFinal => MagicalPowerFinal;
    public float MagicSpeedFinal => MagicalSpeedFinal;

    // Events
    public System.Action<string, float, float> OnSecondaryStatChanged;

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        coreStats = brain.GetModule<RPGCoreStats>();

        // Get allocation from coordinator
        allocation = brain.Stats?.Allocation;

        if (coreStats == null)
        {
            Debug.LogError($"[RPGSecondaryStats] RPGCoreStats module not found! Secondary stats will use base values only.");
        }
        else
        {
            coreStats.OnStatChanged += OnCoreStatChanged;
        }

        if (allocation != null)
        {
            allocation.OnLevelChanged += OnLevelChanged;
        }

        SubscribeToStatChangeEvents();
        isFullyInitialized = true;
        RecalculateAllSecondaryStats();

        if (debugStats)
        {
            Debug.Log($"[RPGSecondaryStats] Initialized with 18 secondary stats");
            if (showMaskingInfo)
            {
                int enabledCount = System.Enum.GetValues(typeof(StatMaskFlags))
                    .Cast<StatMaskFlags>()
                    .Count(flag => flag != StatMaskFlags.None && flag != StatMaskFlags.CalculateAll && IsStatEnabled(flag));
                Debug.Log($"[RPGSecondaryStats] Masking enabled: {enabledCount}/21 stats active. Mask: {enabledStats}");
            }
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled || !isFullyInitialized) return;
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToStatChangeEvents()
    {
        // Physical stats
        physicalPower.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalPower", old, newVal);
        physicalSpeed.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalSpeed", old, newVal);
        physicalEfficiency.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalEfficiency", old, newVal);
        physicalReach.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalReach", old, newVal);
        physicalDuration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalDuration", old, newVal);
        physicalPenetration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("PhysicalPenetration", old, newVal);

        // Magical stats
        magicalPower.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalPower", old, newVal);
        magicalSpeed.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalSpeed", old, newVal);
        magicalEfficiency.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalEfficiency", old, newVal);
        magicalReach.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalReach", old, newVal);
        magicalDuration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalDuration", old, newVal);
        magicalPenetration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicalPenetration", old, newVal);

        // Defense stats
        armor.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Armor", old, newVal);
        resistance.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Resistance", old, newVal);
        evasion.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Evasion", old, newVal);

        // Resource stats
        maxHealth.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MaxHealth", old, newVal);
        regeneration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Regeneration", old, newVal);
        maxStamina.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MaxStamina", old, newVal);
        recovery.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Recovery", old, newVal);
        maxMana.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MaxMana", old, newVal);
        recollection.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Recollection", old, newVal);
    }

    #endregion

    #region Core Stat Event Handlers

    private void OnCoreStatChanged(string statName, float oldValue, float newValue)
    {
        RecalculateAllSecondaryStats();

        if (logStatChanges)
            Debug.Log($"[RPGSecondaryStats] Recalculated secondary stats due to {statName} change ({oldValue:F1} → {newValue:F1})");
    }

    private void OnLevelChanged(int oldLevel, int newLevel)
    {
        RecalculateAllSecondaryStats();

        if (logStatChanges)
            Debug.Log($"[RPGSecondaryStats] Recalculated secondary stats due to level change ({oldLevel} → {newLevel})");
    }

    #endregion

    #region Stat Calculations

    private void RecalculateAllSecondaryStats()
    {
        if (!isFullyInitialized || coreStats == null) return;

        float mindValue = coreStats.Mind.FinalValue;
        float bodyValue = coreStats.Body.FinalValue;
        float spiritValue = coreStats.Spirit.FinalValue;
        float resilienceValue = coreStats.Resilience.FinalValue;
        float enduranceValue = coreStats.Endurance.FinalValue;
        float insightValue = coreStats.Insight.FinalValue;
        int level = allocation != null ? allocation.PlayerLevel : 1;

        // Physical Combat Stats
        // Physical Combat Stats (masked)
        if (IsStatEnabled(StatMaskFlags.PhysicalPower))
            physicalPower.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalPower, bodyScaling.physicalPower, spiritScaling.physicalPower, resilienceScaling.physicalPower, enduranceScaling.physicalPower, insightScaling.physicalPower));


        if (IsStatEnabled(StatMaskFlags.PhysicalSpeed))
            physicalSpeed.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalSpeed, bodyScaling.physicalSpeed, spiritScaling.physicalSpeed, resilienceScaling.physicalSpeed, enduranceScaling.physicalSpeed, insightScaling.physicalSpeed));


        if (IsStatEnabled(StatMaskFlags.PhysicalEfficiency))
            physicalEfficiency.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalEfficiency, bodyScaling.physicalEfficiency, spiritScaling.physicalEfficiency, resilienceScaling.physicalEfficiency, enduranceScaling.physicalEfficiency, insightScaling.physicalEfficiency));


        if (IsStatEnabled(StatMaskFlags.PhysicalReach))
            physicalReach.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalReach, bodyScaling.physicalReach, spiritScaling.physicalReach, resilienceScaling.physicalReach, enduranceScaling.physicalReach, insightScaling.physicalReach));


        if (IsStatEnabled(StatMaskFlags.PhysicalDuration))
            physicalDuration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalDuration, bodyScaling.physicalDuration, spiritScaling.physicalDuration, resilienceScaling.physicalDuration, enduranceScaling.physicalDuration, insightScaling.physicalDuration));


        if (IsStatEnabled(StatMaskFlags.PhysicalPenetration))
            physicalPenetration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.physicalPenetration, bodyScaling.physicalPenetration, spiritScaling.physicalPenetration, resilienceScaling.physicalPenetration, enduranceScaling.physicalPenetration, insightScaling.physicalPenetration));

        // Magical Combat Stats

        // Magical Combat Stats (masked)
        if (IsStatEnabled(StatMaskFlags.MagicalPower))
            magicalPower.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalPower, bodyScaling.magicalPower, spiritScaling.magicalPower, resilienceScaling.magicalPower, enduranceScaling.magicalPower, insightScaling.magicalPower));


        if (IsStatEnabled(StatMaskFlags.MagicalSpeed))
            magicalSpeed.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalSpeed, bodyScaling.magicalSpeed, spiritScaling.magicalSpeed, resilienceScaling.magicalSpeed, enduranceScaling.magicalSpeed, insightScaling.magicalSpeed));


        if (IsStatEnabled(StatMaskFlags.MagicalEfficiency))
            magicalEfficiency.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalEfficiency, bodyScaling.magicalEfficiency, spiritScaling.magicalEfficiency, resilienceScaling.magicalEfficiency, enduranceScaling.magicalEfficiency, insightScaling.magicalEfficiency));


        if (IsStatEnabled(StatMaskFlags.MagicalReach))
            magicalReach.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalReach, bodyScaling.magicalReach, spiritScaling.magicalReach, resilienceScaling.magicalReach, enduranceScaling.magicalReach, insightScaling.magicalReach));


        if (IsStatEnabled(StatMaskFlags.MagicalDuration))
            magicalDuration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalDuration, bodyScaling.magicalDuration, spiritScaling.magicalDuration, resilienceScaling.magicalDuration, enduranceScaling.magicalDuration, insightScaling.magicalDuration));


        if (IsStatEnabled(StatMaskFlags.MagicalPenetration))
            magicalPenetration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicalPenetration, bodyScaling.magicalPenetration, spiritScaling.magicalPenetration, resilienceScaling.magicalPenetration, enduranceScaling.magicalPenetration, insightScaling.magicalPenetration));

        // Defense Stats

        // Defense Stats (masked)
        if (IsStatEnabled(StatMaskFlags.Armor))
            armor.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.armor, bodyScaling.armor, spiritScaling.armor, resilienceScaling.armor, enduranceScaling.armor, insightScaling.armor));


        if (IsStatEnabled(StatMaskFlags.Resistance))
            resistance.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.resistance, bodyScaling.resistance, spiritScaling.resistance, resilienceScaling.resistance, enduranceScaling.resistance, insightScaling.resistance));


        if (IsStatEnabled(StatMaskFlags.Evasion))
            evasion.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.evasion, bodyScaling.evasion, spiritScaling.evasion, resilienceScaling.evasion, enduranceScaling.evasion, insightScaling.evasion));

        // Resource Stats (including level scaling)
        float healthBonus = (level * healthPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxHealth, bodyScaling.maxHealth, spiritScaling.maxHealth, resilienceScaling.maxHealth, enduranceScaling.maxHealth, insightScaling.maxHealth);

        // Resource Stats (masked)
        if (IsStatEnabled(StatMaskFlags.MaxHealth))
            maxHealth.SetCoreStatsBonus(healthBonus);

        float regenBonus = (level * regenerationPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.regeneration, bodyScaling.regeneration, spiritScaling.regeneration, resilienceScaling.regeneration, enduranceScaling.regeneration, insightScaling.regeneration);

        if (IsStatEnabled(StatMaskFlags.Regeneration))
            regeneration.SetCoreStatsBonus(regenBonus);

        float staminaBonus = (level * staminaPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxStamina, bodyScaling.maxStamina, spiritScaling.maxStamina, resilienceScaling.maxStamina, enduranceScaling.maxStamina, insightScaling.maxStamina);

        if (IsStatEnabled(StatMaskFlags.MaxStamina))
            maxStamina.SetCoreStatsBonus(staminaBonus);

        float recoveryBonus = (level * recoveryPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.recovery, bodyScaling.recovery, spiritScaling.recovery, resilienceScaling.recovery, enduranceScaling.recovery, insightScaling.recovery);

        if (IsStatEnabled(StatMaskFlags.Recovery))
            recovery.SetCoreStatsBonus(recoveryBonus);

        float manaBonus = (level * manaPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxMana, bodyScaling.maxMana, spiritScaling.maxMana, resilienceScaling.maxMana, enduranceScaling.maxMana, insightScaling.maxMana);

        if (IsStatEnabled(StatMaskFlags.MaxMana))
            maxMana.SetCoreStatsBonus(manaBonus);

        float recollectionBonus = (level * recollectionPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.recollection, bodyScaling.recollection, spiritScaling.recollection, resilienceScaling.recollection, enduranceScaling.recollection, insightScaling.recollection);

        if (IsStatEnabled(StatMaskFlags.Recollection))
            recollection.SetCoreStatsBonus(recollectionBonus);
    }

    private float CalculateStatBonus(float mind, float body, float spirit, float resilience, float endurance, float insight,
        float mindScale, float bodyScale, float spiritScale, float resilienceScale, float enduranceScale, float insightScale)
    {
        return (mind * mindScale) + (body * bodyScale) + (spirit * spiritScale) +
               (resilience * resilienceScale) + (endurance * enduranceScale) + (insight * insightScale);
    }

    #endregion

    #region Modifier Management

    public void AddItemModifier(string statName, string itemId, float value)
    {
        var stat = GetSecondaryStat(statName);
        stat?.AddItemModifier(itemId, value);
    }

    public void AddTalentModifier(string statName, string talentId, float value)
    {
        var stat = GetSecondaryStat(statName);
        stat?.AddTalentModifier(talentId, value);
    }

    public void AddBuffModifier(string statName, string buffId, float value)
    {
        var stat = GetSecondaryStat(statName);
        stat?.AddBuffModifier(buffId, value);
    }

    public void AddPercentageModifier(string statName, string sourceId, float percentage)
    {
        var stat = GetSecondaryStat(statName);
        stat?.AddPercentageModifier(sourceId, percentage);
    }

    public void RemoveAllModifiersFromSource(string sourceId)
    {
        // Physical
        physicalPower.RemoveAllModifiersFromSource(sourceId);
        physicalSpeed.RemoveAllModifiersFromSource(sourceId);
        physicalEfficiency.RemoveAllModifiersFromSource(sourceId);
        physicalReach.RemoveAllModifiersFromSource(sourceId);
        physicalDuration.RemoveAllModifiersFromSource(sourceId);
        physicalPenetration.RemoveAllModifiersFromSource(sourceId);

        // Magical
        magicalPower.RemoveAllModifiersFromSource(sourceId);
        magicalSpeed.RemoveAllModifiersFromSource(sourceId);
        magicalEfficiency.RemoveAllModifiersFromSource(sourceId);
        magicalReach.RemoveAllModifiersFromSource(sourceId);
        magicalDuration.RemoveAllModifiersFromSource(sourceId);
        magicalPenetration.RemoveAllModifiersFromSource(sourceId);

        // Defense
        armor.RemoveAllModifiersFromSource(sourceId);
        resistance.RemoveAllModifiersFromSource(sourceId);
        evasion.RemoveAllModifiersFromSource(sourceId);

        // Resources
        maxHealth.RemoveAllModifiersFromSource(sourceId);
        regeneration.RemoveAllModifiersFromSource(sourceId);
        maxStamina.RemoveAllModifiersFromSource(sourceId);
        recovery.RemoveAllModifiersFromSource(sourceId);
        maxMana.RemoveAllModifiersFromSource(sourceId);
        recollection.RemoveAllModifiersFromSource(sourceId);
    }

    private StatCalculation GetSecondaryStat(string statName)
    {
        return statName.ToLower() switch
        {
            "physicaldamage" or "physicalpower" => physicalPower,
            "physicalspeed" => physicalSpeed,
            "physicalefficiency" => physicalEfficiency,
            "physicalreach" => physicalReach,
            "physicalduration" => physicalDuration,
            "physicalpenetration" => physicalPenetration,

            "magicaldamage" or "magicalpower" => magicalPower,
            "magicalspeed" => magicalSpeed,
            "magicalefficiency" => magicalEfficiency,
            "magicalreach" => magicalReach,
            "magicalduration" => magicalDuration,
            "magicalpenetration" => magicalPenetration,

            "armor" => armor,
            "resistance" => resistance,
            "evasion" => evasion,

            "maxhealth" or "health" => maxHealth,
            "regeneration" or "regen" => regeneration,
            "maxstamina" or "stamina" => maxStamina,
            "recovery" => recovery,
            "maxmana" or "mana" => maxMana,
            "recollection" => recollection,

            _ => null
        };
    }

    #endregion

    #region Public API

    public float GetSecondaryStatFinalValue(string statName)
    {
        var stat = GetSecondaryStat(statName);
        return stat?.FinalStat ?? 0f;
    }

    #endregion

    #region ICombatStatsProvider Implementation

    public float GetAttackPower() => PhysicalPowerFinal;
    public float GetCriticalChance() => 5f;
    public float GetCriticalMultiplier() => 1.5f;
    public float GetArmorPenetration() => PhysicalPenetrationFinal / 100f;
    public float GetArmor() => ArmorFinal;
    public float GetMagicResistance() => ResistanceFinal;

    #endregion


    #region Debug

    void OnGUI()
    {
        if (!debugStats) return;

        GUILayout.BeginArea(new Rect(370, 10, 350, Screen.height - 20));
        GUILayout.Label("=== SECONDARY STATS ===");

        GUILayout.Label("Physical Combat:");
        GUILayout.Label($"  Power: {PhysicalPowerFinal:F1}");
        GUILayout.Label($"  Speed: {PhysicalSpeedFinal:F2}");
        GUILayout.Label($"  Penetration: {PhysicalPenetrationFinal:F1}");

        GUILayout.Space(5);
        GUILayout.Label("Magical Combat:");
        GUILayout.Label($"  Power: {MagicalPowerFinal:F1}");
        GUILayout.Label($"  Speed: {MagicalSpeedFinal:F2}");
        GUILayout.Label($"  Penetration: {MagicalPenetrationFinal:F1}");

        GUILayout.Space(5);
        GUILayout.Label("Defense:");
        GUILayout.Label($"  Armor: {ArmorFinal:F1}");
        GUILayout.Label($"  Resistance: {ResistanceFinal:F1}");
        GUILayout.Label($"  Evasion: {EvasionFinal:F1}");

        GUILayout.EndArea();
    }

    #endregion
}