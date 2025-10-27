// RPG Secondary Stats - Complete 25-stat system with collapsible inspector organization
using UnityEngine;
using System.Collections.Generic;

public class RPGSecondaryStats : MonoBehaviour, IPlayerModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugStats = false;
    [SerializeField] private bool logStatChanges = false;

    [Header("Physical/Melee Combat Stats")]
    [SerializeField] private StatCalculation meleePower = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation meleeSpeed = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation meleeEfficiency = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation meleeReach = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation meleeDuration = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation meleeCritChance = new StatCalculation { baseValue = 5f };
    [SerializeField] private StatCalculation meleeCritDamage = new StatCalculation { baseValue = 150f };
    [SerializeField] private StatCalculation meleePenetration = new StatCalculation { baseValue = 0f };

    [Header("Magic/Ability Combat Stats")]
    [SerializeField] private StatCalculation magicPower = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation magicSpeed = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicEfficiency = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation magicReach = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicDuration = new StatCalculation { baseValue = 1f };
    [SerializeField] private StatCalculation magicCritChance = new StatCalculation { baseValue = 5f };
    [SerializeField] private StatCalculation magicCritDamage = new StatCalculation { baseValue = 150f };
    [SerializeField] private StatCalculation magicPenetration = new StatCalculation { baseValue = 0f };

    [Header("Defense Stats")]
    [SerializeField] private StatCalculation armor = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation damageReduction = new StatCalculation { baseValue = 0f };
    [SerializeField] private StatCalculation magicResistance = new StatCalculation { baseValue = 0f };

    [Header("Resource Stats")]
    [SerializeField] private StatCalculation maxHealth = new StatCalculation { baseValue = 40f };
    [SerializeField] private StatCalculation regeneration = new StatCalculation { baseValue = 100f };
    [SerializeField] private StatCalculation maxStamina = new StatCalculation { baseValue = 50f };
    [SerializeField] private StatCalculation recovery = new StatCalculation { baseValue = 120f };
    [SerializeField] private StatCalculation maxMana = new StatCalculation { baseValue = 30f };
    [SerializeField] private StatCalculation recollection = new StatCalculation { baseValue = 80f };

    [System.Serializable]
    public class CoreStatScaling
    {
        [Header("Melee Combat Stats")]
        public float meleePower = 0f;
        public float meleeSpeed = 0f;
        public float meleeEfficiency = 0f;
        public float meleeReach = 0f;
        public float meleeDuration = 0f;
        public float meleeCritChance = 0f;
        public float meleeCritDamage = 0f;
        public float meleePenetration = 0f;

        [Header("Magic Combat Stats")]
        public float magicPower = 0f;
        public float magicSpeed = 0f;
        public float magicEfficiency = 0f;
        public float magicReach = 0f;
        public float magicDuration = 0f;
        public float magicCritChance = 0f;
        public float magicCritDamage = 0f;
        public float magicPenetration = 0f;

        [Header("Defense Stats")]
        public float armor = 0f;
        public float damageReduction = 0f;
        public float magicResistance = 0f;

        [Header("Resource Stats")]
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
    private bool isFullyInitialized = false;

    // Properties for accessing final values
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    #region Physical/Melee Properties
    public float MeleePowerFinal => meleePower.FinalStat;
    public float MeleeSpeedFinal => meleeSpeed.FinalStat;
    public float MeleeEfficiencyFinal => meleeEfficiency.FinalStat;
    public float MeleeReachFinal => meleeReach.FinalStat;
    public float MeleeDurationFinal => meleeDuration.FinalStat;
    public float MeleeCritChanceFinal => meleeCritChance.FinalStat;
    public float MeleeCritDamageFinal => meleeCritDamage.FinalStat;
    public float MeleePenetrationFinal => meleePenetration.FinalStat;
    #endregion

    #region Magic/Ability Properties
    public float MagicPowerFinal => magicPower.FinalStat;
    public float MagicSpeedFinal => magicSpeed.FinalStat;
    public float MagicEfficiencyFinal => magicEfficiency.FinalStat;
    public float MagicReachFinal => magicReach.FinalStat;
    public float MagicDurationFinal => magicDuration.FinalStat;
    public float MagicCritChanceFinal => magicCritChance.FinalStat;
    public float MagicCritDamageFinal => magicCritDamage.FinalStat;
    public float MagicPenetrationFinal => magicPenetration.FinalStat;
    #endregion

    #region Defense Properties
    public float ArmorFinal => armor.FinalStat;
    public float DamageReductionFinal => damageReduction.FinalStat;
    public float MagicResistanceFinal => magicResistance.FinalStat;
    #endregion

    #region Resource Properties
    public float MaxHealthFinal => maxHealth.FinalStat;
    public float RegenerationFinal => regeneration.FinalStat;
    public float MaxStaminaFinal => maxStamina.FinalStat;
    public float RecoveryFinal => recovery.FinalStat;
    public float MaxManaFinal => maxMana.FinalStat;
    public float RecollectionFinal => recollection.FinalStat;
    #endregion

    // Events for notifying other systems
    public System.Action<string, float, float> OnSecondaryStatChanged;

    #region IPlayerModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        coreStats = brain.GetModule<RPGCoreStats>();

        if (coreStats == null)
        {
            Debug.LogError($"[RPGSecondaryStats] RPGCoreStats module not found! Secondary stats will use base values only.");
        }
        else
        {
            coreStats.OnStatChanged += OnCoreStatChanged;
            coreStats.OnLevelChanged += OnLevelChanged;
        }

        SubscribeToStatChangeEvents();
        isFullyInitialized = true;
        RecalculateAllSecondaryStats();

        if (debugStats)
            Debug.Log($"[RPGSecondaryStats] Initialized with 25 secondary stats");
    }

    public void UpdateModule()
    {
        if (!IsEnabled || !isFullyInitialized) return;
        // Secondary stats don't need frame-by-frame updates
        // They recalculate when core stats change via events
    }

    #endregion

    #region Event Subscriptions

    private void SubscribeToStatChangeEvents()
    {
        // Physical/Melee stats
        meleePower.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleePower", old, newVal);
        meleeSpeed.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeSpeed", old, newVal);
        meleeEfficiency.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeEfficiency", old, newVal);
        meleeReach.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeReach", old, newVal);
        meleeDuration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeDuration", old, newVal);
        meleeCritChance.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeCritChance", old, newVal);
        meleeCritDamage.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleeCritDamage", old, newVal);
        meleePenetration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MeleePenetration", old, newVal);

        // Magic/Ability stats
        magicPower.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicPower", old, newVal);
        magicSpeed.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicSpeed", old, newVal);
        magicEfficiency.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicEfficiency", old, newVal);
        magicReach.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicReach", old, newVal);
        magicDuration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicDuration", old, newVal);
        magicCritChance.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicCritChance", old, newVal);
        magicCritDamage.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicCritDamage", old, newVal);
        magicPenetration.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicPenetration", old, newVal);

        // Defense stats
        armor.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("Armor", old, newVal);
        damageReduction.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("DamageReduction", old, newVal);
        magicResistance.OnStatChanged += (old, newVal) => OnSecondaryStatChanged?.Invoke("MagicResistance", old, newVal);

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
        int level = coreStats.PlayerLevel;

        // Physical/Melee Stats
        meleePower.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleePower, bodyScaling.meleePower, spiritScaling.meleePower, resilienceScaling.meleePower, enduranceScaling.meleePower, insightScaling.meleePower));

        meleeSpeed.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeSpeed, bodyScaling.meleeSpeed, spiritScaling.meleeSpeed, resilienceScaling.meleeSpeed, enduranceScaling.meleeSpeed, insightScaling.meleeSpeed));

        meleeEfficiency.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeEfficiency, bodyScaling.meleeEfficiency, spiritScaling.meleeEfficiency, resilienceScaling.meleeEfficiency, enduranceScaling.meleeEfficiency, insightScaling.meleeEfficiency));

        meleeReach.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeReach, bodyScaling.meleeReach, spiritScaling.meleeReach, resilienceScaling.meleeReach, enduranceScaling.meleeReach, insightScaling.meleeReach));

        meleeDuration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeDuration, bodyScaling.meleeDuration, spiritScaling.meleeDuration, resilienceScaling.meleeDuration, enduranceScaling.meleeDuration, insightScaling.meleeDuration));

        meleeCritChance.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeCritChance, bodyScaling.meleeCritChance, spiritScaling.meleeCritChance, resilienceScaling.meleeCritChance, enduranceScaling.meleeCritChance, insightScaling.meleeCritChance));

        meleeCritDamage.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleeCritDamage, bodyScaling.meleeCritDamage, spiritScaling.meleeCritDamage, resilienceScaling.meleeCritDamage, enduranceScaling.meleeCritDamage, insightScaling.meleeCritDamage));

        meleePenetration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.meleePenetration, bodyScaling.meleePenetration, spiritScaling.meleePenetration, resilienceScaling.meleePenetration, enduranceScaling.meleePenetration, insightScaling.meleePenetration));

        // Magic/Ability Stats
        magicPower.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicPower, bodyScaling.magicPower, spiritScaling.magicPower, resilienceScaling.magicPower, enduranceScaling.magicPower, insightScaling.magicPower));

        magicSpeed.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicSpeed, bodyScaling.magicSpeed, spiritScaling.magicSpeed, resilienceScaling.magicSpeed, enduranceScaling.magicSpeed, insightScaling.magicSpeed));

        magicEfficiency.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicEfficiency, bodyScaling.magicEfficiency, spiritScaling.magicEfficiency, resilienceScaling.magicEfficiency, enduranceScaling.magicEfficiency, insightScaling.magicEfficiency));

        magicReach.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicReach, bodyScaling.magicReach, spiritScaling.magicReach, resilienceScaling.magicReach, enduranceScaling.magicReach, insightScaling.magicReach));

        magicDuration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicDuration, bodyScaling.magicDuration, spiritScaling.magicDuration, resilienceScaling.magicDuration, enduranceScaling.magicDuration, insightScaling.magicDuration));

        magicCritChance.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicCritChance, bodyScaling.magicCritChance, spiritScaling.magicCritChance, resilienceScaling.magicCritChance, enduranceScaling.magicCritChance, insightScaling.magicCritChance));

        magicCritDamage.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicCritDamage, bodyScaling.magicCritDamage, spiritScaling.magicCritDamage, resilienceScaling.magicCritDamage, enduranceScaling.magicCritDamage, insightScaling.magicCritDamage));

        magicPenetration.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicPenetration, bodyScaling.magicPenetration, spiritScaling.magicPenetration, resilienceScaling.magicPenetration, enduranceScaling.magicPenetration, insightScaling.magicPenetration));

        // Defense Stats
        armor.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.armor, bodyScaling.armor, spiritScaling.armor, resilienceScaling.armor, enduranceScaling.armor, insightScaling.armor));

        damageReduction.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.damageReduction, bodyScaling.damageReduction, spiritScaling.damageReduction, resilienceScaling.damageReduction, enduranceScaling.damageReduction, insightScaling.damageReduction));

        magicResistance.SetCoreStatsBonus(CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.magicResistance, bodyScaling.magicResistance, spiritScaling.magicResistance, resilienceScaling.magicResistance, enduranceScaling.magicResistance, insightScaling.magicResistance));

        // Resource Stats (including level scaling)
        float healthBonus = (level * healthPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxHealth, bodyScaling.maxHealth, spiritScaling.maxHealth, resilienceScaling.maxHealth, enduranceScaling.maxHealth, insightScaling.maxHealth);
        maxHealth.SetCoreStatsBonus(healthBonus);

        float regenBonus = (level * regenerationPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.regeneration, bodyScaling.regeneration, spiritScaling.regeneration, resilienceScaling.regeneration, enduranceScaling.regeneration, insightScaling.regeneration);
        regeneration.SetCoreStatsBonus(regenBonus);

        float staminaBonus = (level * staminaPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxStamina, bodyScaling.maxStamina, spiritScaling.maxStamina, resilienceScaling.maxStamina, enduranceScaling.maxStamina, insightScaling.maxStamina);
        maxStamina.SetCoreStatsBonus(staminaBonus);

        float recoveryBonus = (level * recoveryPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.recovery, bodyScaling.recovery, spiritScaling.recovery, resilienceScaling.recovery, enduranceScaling.recovery, insightScaling.recovery);
        recovery.SetCoreStatsBonus(recoveryBonus);

        float manaBonus = (level * manaPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.maxMana, bodyScaling.maxMana, spiritScaling.maxMana, resilienceScaling.maxMana, enduranceScaling.maxMana, insightScaling.maxMana);
        maxMana.SetCoreStatsBonus(manaBonus);

        float recollectionBonus = (level * recollectionPerLevel) + CalculateStatBonus(mindValue, bodyValue, spiritValue, resilienceValue, enduranceValue, insightValue,
            mindScaling.recollection, bodyScaling.recollection, spiritScaling.recollection, resilienceScaling.recollection, enduranceScaling.recollection, insightScaling.recollection);
        recollection.SetCoreStatsBonus(recollectionBonus);
    }

    private float CalculateStatBonus(float mind, float body, float spirit, float resilience, float endurance, float insight,
        float mindMult, float bodyMult, float spiritMult, float resilienceMult, float enduranceMult, float insightMult)
    {
        return (mind * mindMult) + (body * bodyMult) + (spirit * spiritMult) +
               (resilience * resilienceMult) + (endurance * enduranceMult) + (insight * insightMult);
    }

    #endregion

    #region Secondary Stat Access

    public StatCalculation GetSecondaryStat(string statName)
    {
        switch (statName.ToLower())
        {
            // Physical/Melee
            case "meleepower": case "physicaldamage": return meleePower;
            case "meleespeed": case "attackspeed": return meleeSpeed;
            case "meleeefficiency": case "physicalefficiency": return meleeEfficiency;
            case "meleereach": case "physicalreach": return meleeReach;
            case "meleeduration": case "physicalduration": return meleeDuration;
            case "meleecritchance": case "physicalcritchance": return meleeCritChance;
            case "meleecritdamage": case "physicalcritdamage": return meleeCritDamage;
            case "meleepenetration": case "physicalpenetration": return meleePenetration;

            // Magic/Ability
            case "magicpower": case "spelldamage": return magicPower;
            case "magicspeed": case "castingspeed": return magicSpeed;
            case "magicefficiency": case "spellefficiency": return magicEfficiency;
            case "magicreach": case "spellreach": return magicReach;
            case "magicduration": case "spellduration": return magicDuration;
            case "magiccritchance": case "spellcritchance": return magicCritChance;
            case "magiccritdamage": case "spellcritdamage": return magicCritDamage;
            case "magicpenetration": case "spellpenetration": return magicPenetration;

            // Defense
            case "armor": return armor;
            case "damagereduction": return damageReduction;
            case "magicresistance": case "spellresistance": return magicResistance;

            // Resources
            case "maxhealth": case "health": return maxHealth;
            case "regeneration": case "healthregen": return regeneration;
            case "maxstamina": case "stamina": return maxStamina;
            case "recovery": case "staminaregen": return recovery;
            case "maxmana": case "mana": return maxMana;
            case "recollection": case "manaregen": return recollection;

            default: return null;
        }
    }

    public float GetSecondaryStatFinalValue(string statName)
    {
        var stat = GetSecondaryStat(statName);
        return stat?.FinalStat ?? 0f;
    }

    #endregion

    #region Modifier Management

    public void AddItemModifier(string statName, string itemId, float value)
    {
        var stat = GetSecondaryStat(statName);
        if (stat != null)
        {
            stat.AddItemModifier(itemId, value);

            if (logStatChanges)
                Debug.Log($"[RPGSecondaryStats] Added item modifier '{itemId}' ({value:+0.0;-0.0}) to {statName}");
        }
    }

    public void AddTalentModifier(string statName, string talentId, float value)
    {
        var stat = GetSecondaryStat(statName);
        if (stat != null)
        {
            stat.AddTalentModifier(talentId, value);

            if (logStatChanges)
                Debug.Log($"[RPGSecondaryStats] Added talent modifier '{talentId}' ({value:+0.0;-0.0}) to {statName}");
        }
    }

    public void AddBuffModifier(string statName, string buffId, float value)
    {
        var stat = GetSecondaryStat(statName);
        if (stat != null)
        {
            stat.AddBuffModifier(buffId, value);

            if (logStatChanges)
                Debug.Log($"[RPGSecondaryStats] Added buff modifier '{buffId}' ({value:+0.0;-0.0}) to {statName}");
        }
    }

    public void AddPercentageModifier(string statName, string sourceId, float percentage)
    {
        var stat = GetSecondaryStat(statName);
        if (stat != null)
        {
            stat.AddPercentageModifier(sourceId, percentage);

            if (logStatChanges)
                Debug.Log($"[RPGSecondaryStats] Added percentage modifier '{sourceId}' ({percentage:+0.0;-0.0}%) to {statName}");
        }
    }

  
        public void RemoveAllModifiersFromSource(string sourceId)
        {
            // Remove from all stats
            meleePower.RemoveAllModifiersFromSource(sourceId);
            meleeSpeed.RemoveAllModifiersFromSource(sourceId);
            meleeEfficiency.RemoveAllModifiersFromSource(sourceId);
            meleeReach.RemoveAllModifiersFromSource(sourceId);
            meleeDuration.RemoveAllModifiersFromSource(sourceId);
            meleeCritChance.RemoveAllModifiersFromSource(sourceId);
            meleeCritDamage.RemoveAllModifiersFromSource(sourceId);
            meleePenetration.RemoveAllModifiersFromSource(sourceId);

            magicPower.RemoveAllModifiersFromSource(sourceId);
            magicSpeed.RemoveAllModifiersFromSource(sourceId);
            magicEfficiency.RemoveAllModifiersFromSource(sourceId);
            magicReach.RemoveAllModifiersFromSource(sourceId);
            magicDuration.RemoveAllModifiersFromSource(sourceId);
            magicCritChance.RemoveAllModifiersFromSource(sourceId);
            magicCritDamage.RemoveAllModifiersFromSource(sourceId);
            magicPenetration.RemoveAllModifiersFromSource(sourceId);

            armor.RemoveAllModifiersFromSource(sourceId);
            damageReduction.RemoveAllModifiersFromSource(sourceId);
            magicResistance.RemoveAllModifiersFromSource(sourceId);

            maxHealth.RemoveAllModifiersFromSource(sourceId);
            regeneration.RemoveAllModifiersFromSource(sourceId);
            maxStamina.RemoveAllModifiersFromSource(sourceId);
            recovery.RemoveAllModifiersFromSource(sourceId);
            maxMana.RemoveAllModifiersFromSource(sourceId);
            recollection.RemoveAllModifiersFromSource(sourceId);

            if (logStatChanges)
                Debug.Log($"[RPGSecondaryStats] Removed all modifiers from source '{sourceId}'");
        }

        #endregion

        #region Public API

        public string GetStatsSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("=== SECONDARY STATS SUMMARY ===");

            summary.AppendLine("\nPhysical/Melee:");
            summary.AppendLine($"  Power: {MeleePowerFinal:F1}");
            summary.AppendLine($"  Speed: {MeleeSpeedFinal:F1}");
            summary.AppendLine($"  Efficiency: {MeleeEfficiencyFinal:F1}");
            summary.AppendLine($"  Reach: {MeleeReachFinal:F1}");
            summary.AppendLine($"  Duration: {MeleeDurationFinal:F1}");
            summary.AppendLine($"  Crit Chance: {MeleeCritChanceFinal:F1}%");
            summary.AppendLine($"  Crit Damage: {MeleeCritDamageFinal:F1}%");
            summary.AppendLine($"  Penetration: {MeleePenetrationFinal:F1}");

            summary.AppendLine("\nMagic/Ability:");
            summary.AppendLine($"  Power: {MagicPowerFinal:F1}");
            summary.AppendLine($"  Speed: {MagicSpeedFinal:F1}");
            summary.AppendLine($"  Efficiency: {MagicEfficiencyFinal:F1}");
            summary.AppendLine($"  Reach: {MagicReachFinal:F1}");
            summary.AppendLine($"  Duration: {MagicDurationFinal:F1}");
            summary.AppendLine($"  Crit Chance: {MagicCritChanceFinal:F1}%");
            summary.AppendLine($"  Crit Damage: {MagicCritDamageFinal:F1}%");
            summary.AppendLine($"  Penetration: {MagicPenetrationFinal:F1}");

            summary.AppendLine("\nDefense:");
            summary.AppendLine($"  Armor: {ArmorFinal:F1}");
            summary.AppendLine($"  Damage Reduction: {DamageReductionFinal:F1}%");
            summary.AppendLine($"  Magic Resistance: {MagicResistanceFinal:F1}%");

            summary.AppendLine("\nResources:");
            summary.AppendLine($"  Max Health: {MaxHealthFinal:F0}");
            summary.AppendLine($"  Regeneration: {RegenerationFinal:F0}");
            summary.AppendLine($"  Max Stamina: {MaxStaminaFinal:F0}");
            summary.AppendLine($"  Recovery: {RecoveryFinal:F0}");
            summary.AppendLine($"  Max Mana: {MaxManaFinal:F0}");
            summary.AppendLine($"  Recollection: {RecollectionFinal:F0}");

            return summary.ToString();
        }

        #endregion

        #region Debug

        void OnGUI()
        {
            if (!debugStats) return;

            GUILayout.BeginArea(new Rect(370, 10, 400, Screen.height - 20));
            GUILayout.Label("=== RPG SECONDARY STATS ===");

            GUILayout.Label("Physical/Melee:");
            GUILayout.Label($"  Power: {MeleePowerFinal:F1}");
            GUILayout.Label($"  Speed: {MeleeSpeedFinal:F1}");
            GUILayout.Label($"  Efficiency: {MeleeEfficiencyFinal:F1}");
            GUILayout.Label($"  Reach: {MeleeReachFinal:F1}");
            GUILayout.Label($"  Crit: {MeleeCritChanceFinal:F1}%");

            GUILayout.Space(5);
            GUILayout.Label("Magic/Ability:");
            GUILayout.Label($"  Power: {MagicPowerFinal:F1}");
            GUILayout.Label($"  Speed: {MagicSpeedFinal:F1}");
            GUILayout.Label($"  Efficiency: {MagicEfficiencyFinal:F1}");
            GUILayout.Label($"  Reach: {MagicReachFinal:F1}");
            GUILayout.Label($"  Crit: {MagicCritChanceFinal:F1}%");

            GUILayout.Space(5);
            GUILayout.Label("Defense:");
            GUILayout.Label($"  Armor: {ArmorFinal:F1}");
            GUILayout.Label($"  Dmg Reduction: {DamageReductionFinal:F1}%");
            GUILayout.Label($"  Magic Resist: {MagicResistanceFinal:F1}%");

            GUILayout.Space(5);
            GUILayout.Label("Resources:");
            GUILayout.Label($"  Health: {MaxHealthFinal:F0}");
            GUILayout.Label($"  Stamina: {MaxStaminaFinal:F0}");
            GUILayout.Label($"  Mana: {MaxManaFinal:F0}");

            GUILayout.EndArea();
        }

        #endregion
    }

