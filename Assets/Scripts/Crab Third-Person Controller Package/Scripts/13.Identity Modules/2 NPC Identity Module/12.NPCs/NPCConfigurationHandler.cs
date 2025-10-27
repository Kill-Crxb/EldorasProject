using CrabThirdPerson.Character;
using UnityEngine;
using RPG.Factions;

/// <summary>
/// Handles procedural configuration of NPCs from archetypes.
/// This is the "3 dropdown" magic: Faction + Type + Importance = Configured NPC
/// 
/// Example: Wildlife + Beast + Soldier = Wolf with appropriate stats/model/AI
/// 
/// OPTION B FIX APPLIED:
/// - Explicitly pushes faction to FactionAffiliationHandler
/// - Single source of truth: NPCConfigurationHandler owns the faction configuration
/// - No dependency on initialization order
/// </summary>
public class NPCConfigurationHandler : MonoBehaviour, INPCHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Archetype Configuration")]
    [Tooltip("The three-dimensional definition of this NPC")]
    [SerializeField] private NPCFaction faction = NPCFaction.Wildlife;
    [SerializeField] private NPCType npcType = NPCType.Beast;
    [SerializeField] private NPCImportance importance = NPCImportance.Soldier;

    [Header("Archetype Override")]
    [Tooltip("Use a specific archetype instead of auto-lookup")]
    [SerializeField] private NPCArchetype customArchetype;

    [Header("Optional Overrides")]
    [Tooltip("Override the generated/archetype name")]
    [SerializeField] private string overrideName;

    [Tooltip("Override the model selection")]
    [SerializeField] private string overrideModelId;

    [Header("Configuration Timing")]
    [SerializeField] private bool configureOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // References
    private NPCModule parentNPC;
    private ControllerBrain brain;
    private NPCArchetype resolvedArchetype;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public NPCArchetype ResolvedArchetype => resolvedArchetype;

    // ========================================
    // INPCHandler Implementation
    // ========================================

    public void Initialize(NPCModule parent)
    {
        parentNPC = parent;
        brain = parent.Brain;

        if (configureOnStart)
        {
            ConfigureNPC();
        }

        if (showDebugLogs)
        {
            Debug.Log($"[NPCConfigurationHandler] Initialized for {parent.NPCName}");
        }
    }

    public void UpdateHandler()
    {
        // Configuration is typically one-time on spawn
        // No per-frame updates needed
    }

    public string GetHandlerSaveData()
    {
        var saveData = new ConfigurationSaveData
        {
            faction = faction,
            npcType = npcType,
            importance = importance,
            appliedArchetypeId = resolvedArchetype?.archetypeId,
            overrideName = overrideName,
            overrideModelId = overrideModelId
        };

        return JsonUtility.ToJson(saveData);
    }

    public void LoadHandlerData(string json)
    {
        var saveData = JsonUtility.FromJson<ConfigurationSaveData>(json);

        faction = saveData.faction;
        npcType = saveData.npcType;
        importance = saveData.importance;
        overrideName = saveData.overrideName;
        overrideModelId = saveData.overrideModelId;

        // Re-apply configuration
        ConfigureNPC();
    }

    public void ResetHandler()
    {
        // Reset to default archetype configuration
        overrideName = null;
        overrideModelId = null;
        ConfigureNPC();
    }

    // ========================================
    // Configuration System
    // ========================================

    /// <summary>
    /// Main configuration method - applies archetype to NPC
    /// </summary>
    public void ConfigureNPC()
    {
        if (!isEnabled) return;

        // Step 1: Resolve archetype
        resolvedArchetype = ResolveArchetype();

        if (resolvedArchetype == null)
        {
            Debug.LogError($"[NPCConfigurationHandler] Failed to resolve archetype for {faction}/{npcType}/{importance}");
            return;
        }

        // Step 2: Apply configuration to parent
        parentNPC.SetArchetype(resolvedArchetype);

        // Step 3: Configure individual systems (OPTION B: Faction FIRST)
        ConfigureFaction();  // NEW: Explicit faction configuration
        ConfigureStats();
        ConfigureModel();
        ConfigureEquipment();
        ConfigureName();

        if (showDebugLogs)
        {
            Debug.Log($"[NPCConfigurationHandler] Configured NPC as {resolvedArchetype.archetypeName}");
        }
    }

    /// <summary>
    /// Resolve archetype from configuration or override
    /// </summary>
    private NPCArchetype ResolveArchetype()
    {
        // Use custom archetype if specified
        if (customArchetype != null)
        {
            return customArchetype;
        }

        // TODO: Look up archetype from database using faction/type/importance
        // For MVP, we'll load from Resources
        string archetypeId = $"archetype_{faction}_{npcType}_{importance}".ToLower();
        NPCArchetype archetype = Resources.Load<NPCArchetype>($"NPCArchetypes/{archetypeId}");

        if (archetype == null)
        {
            Debug.LogWarning($"[NPCConfigurationHandler] Archetype not found: {archetypeId}, creating default");
            archetype = CreateDefaultArchetype();
        }

        return archetype;
    }

    /// <summary>
    /// Create a default archetype if none exists
    /// </summary>
    private NPCArchetype CreateDefaultArchetype()
    {
        var archetype = ScriptableObject.CreateInstance<NPCArchetype>();
        archetype.archetypeId = $"default_{faction}_{npcType}_{importance}";
        archetype.archetypeName = $"{faction} {npcType}";
        archetype.faction = faction;
        archetype.npcType = npcType;
        archetype.importance = importance;

        // Set default stats based on importance
        archetype.baseStats = new StatAllocation();

        switch (importance)
        {
            case NPCImportance.Soldier:
                archetype.baseStats.body = 12;
                archetype.baseStats.healthMultiplier = 1f;
                break;
            case NPCImportance.Elite:
                archetype.baseStats.body = 16;
                archetype.baseStats.healthMultiplier = 1.5f;
                break;
            case NPCImportance.Hero:
                archetype.baseStats.body = 20;
                archetype.baseStats.healthMultiplier = 2f;
                break;
            case NPCImportance.Boss:
                archetype.baseStats.body = 30;
                archetype.baseStats.healthMultiplier = 3f;
                break;
        }

        return archetype;
    }

    // ========================================
    // OPTION B: Explicit Faction Configuration
    // ========================================

    /// <summary>
    /// Configure faction from archetype
    /// Explicitly pushes faction to FactionAffiliationHandler (single source of truth)
    /// This is called FIRST in ConfigureNPC() to ensure faction is set before other systems
    /// </summary>
    private void ConfigureFaction()
    {
        var factionHandler = parentNPC.GetHandler<FactionAffiliationHandler>();
        if (factionHandler == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning($"[NPCConfigurationHandler] No FactionAffiliationHandler found for {parentNPC.NPCName}");
            }
            return;
        }

        // Convert NPCFaction to FactionType
        RPG.Factions.FactionType factionType = ConvertNPCFactionToFactionType(resolvedArchetype.faction);

        // Explicitly set faction (single source of truth)
        factionHandler.SetFaction(factionType);

        if (showDebugLogs)
        {
            Debug.Log($"[NPCConfigurationHandler] Set faction to: {factionType}");
        }
    }

    /// <summary>
    /// Convert NPCFaction enum to FactionType enum using string matching
    /// This is safe even if enum values don't match exactly
    /// </summary>
    private RPG.Factions.FactionType ConvertNPCFactionToFactionType(NPCFaction npcFaction)
    {
        // Convert to string for comparison
        string factionString = npcFaction.ToString().ToLower();

        // Match common patterns
        if (factionString.Contains("wildlife") || factionString.Contains("beast"))
            return RPG.Factions.FactionType.Wildlife;

        if (factionString.Contains("human"))
            return RPG.Factions.FactionType.Humans;

        if (factionString.Contains("elf") || factionString.Contains("elves"))
            return RPG.Factions.FactionType.Elves;

        if (factionString.Contains("dwarf") || factionString.Contains("dwarves"))
            return RPG.Factions.FactionType.Dwarves;

        if (factionString.Contains("warlock"))
            return RPG.Factions.FactionType.Warlocks;

        if (factionString.Contains("undead") || factionString.Contains("skeleton") || factionString.Contains("zombie"))
            return RPG.Factions.FactionType.Undead;

        if (factionString.Contains("bandit") || factionString.Contains("raider"))
            return RPG.Factions.FactionType.Bandits;

        if (factionString.Contains("monster"))
            return RPG.Factions.FactionType.Monsters;

        // Default to Neutral for unknown factions
        if (showDebugLogs)
        {
            Debug.LogWarning($"[NPCConfigurationHandler] Unknown NPCFaction: {npcFaction}. Defaulting to Neutral.");
        }
        return RPG.Factions.FactionType.Neutral;
    }

    // ========================================
    // Individual System Configuration
    // ========================================

    private void ConfigureStats()
    {
        var coreStats = brain.GetModule<RPGCoreStats>();
        if (coreStats == null) return;

        var stats = resolvedArchetype.baseStats;

        // Apply base stats - CORRECTED METHOD NAME
        coreStats.SetStatBaseValue("Mind", stats.mind);
        coreStats.SetStatBaseValue("Body", stats.body);
        coreStats.SetStatBaseValue("Spirit", stats.spirit);
        coreStats.SetStatBaseValue("Resilience", stats.resilience);
        coreStats.SetStatBaseValue("Endurance", stats.endurance);
        coreStats.SetStatBaseValue("Insight", stats.insight);

        // Apply multipliers (stored as percentage modifiers)
        if (stats.healthMultiplier != 1f)
        {
            coreStats.AddPercentageModifier("Endurance", "archetype_health_mult",
                (stats.healthMultiplier - 1f) * 100f);
        }

        if (stats.damageMultiplier != 1f)
        {
            coreStats.AddPercentageModifier("Body", "archetype_damage_mult",
                (stats.damageMultiplier - 1f) * 100f);
        }

        if (showDebugLogs)
        {
            Debug.Log($"[NPCConfigurationHandler] Applied stats: Body={stats.body}, Health x{stats.healthMultiplier}");
        }
    }

    /// <summary>
    /// Load model from archetype
    /// </summary>
    private void ConfigureModel()
    {
        var modelModule = brain.GetModule<ModelModule>();
        if (modelModule == null) return;

        string modelId = overrideModelId;

        // If no override, select from archetype's model pool
        if (string.IsNullOrEmpty(modelId))
        {
            if (resolvedArchetype.modelPool != null && resolvedArchetype.modelPool.Count > 0)
            {
                if (resolvedArchetype.randomizeModel)
                {
                    int randomIndex = Random.Range(0, resolvedArchetype.modelPool.Count);
                    modelId = resolvedArchetype.modelPool[randomIndex];
                }
                else
                {
                    modelId = resolvedArchetype.modelPool[0];
                }
            }
        }

        if (!string.IsNullOrEmpty(modelId))
        {
            modelModule.SwapModel(modelId);

            if (showDebugLogs)
            {
                Debug.Log($"[NPCConfigurationHandler] Loaded model: {modelId}");
            }
        }
    }

    /// <summary>
    /// Equip weapons from archetype
    /// </summary>
    private void ConfigureEquipment()
    {
        // TODO: Implement when PlayerItemsModule is ready for NPCs
        // For now, weapons are configured manually on the NPC prefab

        if (showDebugLogs && !string.IsNullOrEmpty(resolvedArchetype.mainHandWeaponId))
        {
            Debug.Log($"[NPCConfigurationHandler] Weapon: {resolvedArchetype.mainHandWeaponId}");
        }
    }

    /// <summary>
    /// Set NPC name from archetype or override
    /// </summary>
    private void ConfigureName()
    {
        string finalName = overrideName;

        if (string.IsNullOrEmpty(finalName))
        {
            if (resolvedArchetype.useGenericName)
            {
                finalName = resolvedArchetype.genericName;
            }
            else
            {
                // TODO: Name generation from prefix/suffix
                finalName = resolvedArchetype.archetypeName;
            }
        }

        parentNPC.SetName(finalName);
    }

    // ========================================
    // Public API
    // ========================================

    /// <summary>
    /// Reconfigure NPC with new archetype settings
    /// Useful for dynamic NPC transformation
    /// </summary>
    public void Reconfigure(NPCFaction newFaction, NPCType newType, NPCImportance newImportance)
    {
        faction = newFaction;
        npcType = newType;
        importance = newImportance;

        ConfigureNPC();
    }

    /// <summary>
    /// Force configuration (even if configureOnStart is false)
    /// </summary>
    [ContextMenu("Configure Now")]
    public void ForceConfigureNow()
    {
        ConfigureNPC();
    }

    // ========================================
    // Save Data Structure
    // ========================================

    [System.Serializable]
    private class ConfigurationSaveData
    {
        public NPCFaction faction;
        public NPCType npcType;
        public NPCImportance importance;
        public string appliedArchetypeId;
        public string overrideName;
        public string overrideModelId;
    }
}