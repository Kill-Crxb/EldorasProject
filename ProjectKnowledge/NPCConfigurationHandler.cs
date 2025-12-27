using CrabThirdPerson.Character;
using UnityEngine;
using RPG.Factions;
using System;

public class NPCConfigurationHandler : MonoBehaviour, INPCHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Archetype Configuration")]
    [SerializeField] private NPCFaction faction = NPCFaction.Wildlife;
    [SerializeField] private NPCType npcType = NPCType.Beast;
    [SerializeField] private NPCImportance importance = NPCImportance.Soldier;

    [Header("Archetype Override")]
    [SerializeField] private NPCArchetype customArchetype;

    [Header("Optional Overrides")]
    [SerializeField] private string overrideName;
    [SerializeField] private string overrideModelId;

    [Header("Configuration Timing")]
    [SerializeField] private bool configureOnStart = true;
    [SerializeField] private bool clearAbilitiesBeforeConfiguring = true;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private NPCModule parentNPC;
    private ControllerBrain brain;
    private NPCArchetype resolvedArchetype;
    private bool hasConfigured = false;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public NPCArchetype ResolvedArchetype => resolvedArchetype;

    /// <summary>
    /// Set a custom archetype externally (e.g., from a spawner)
    /// Call this BEFORE the entity initializes, or call ReconfigureNPC() after
    /// </summary>
    public void SetCustomArchetype(NPCArchetype archetype)
    {
        customArchetype = archetype;

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Custom archetype set to: {archetype.archetypeName}");

        // Trigger configuration with new archetype
        hasConfigured = false;
        ConfigureNPC();
    }

    /// <summary>
    /// Reconfigure the NPC with current archetype (useful for runtime changes)
    /// </summary>
    public void ReconfigureNPC()
    {
        hasConfigured = false;
        ConfigureNPC();
    }

    public void Initialize(NPCModule parent)
    {
        parentNPC = parent;
        brain = parent.Brain;

        if (configureOnStart)
        {
            ConfigureNPC();
        }
    }

    public void UpdateHandler()
    {
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

        ConfigureNPC();
    }

    public void ResetHandler()
    {
        overrideName = null;
        overrideModelId = null;
        ConfigureNPC();
    }

    public void ConfigureNPC()
    {
        if (!isEnabled || hasConfigured) return;

        resolvedArchetype = ResolveArchetype();

        if (resolvedArchetype == null)
        {
            if (debugMode)
                Debug.LogWarning($"[NPCConfigurationHandler] Could not resolve archetype for {gameObject.name}");
            return;
        }

        hasConfigured = true;

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Configuring {gameObject.name} as {resolvedArchetype.archetypeName}");

        parentNPC.SetArchetype(resolvedArchetype);

        ConfigureFaction();
        ConfigureStats();
        ConfigureAbilities();
        ConfigureAbilityLoadout();
        ConfigureCombatBehavior();
        ConfigureAI(); // NEW: Configure GOAP/AI system from archetype
        // ConfigureBehaviorTree(); // REMOVED: BT system deprecated, FSM+GOAP coming in Phase 1
        ConfigureModel();
        ConfigureEquipment();
        ConfigureName();

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Configuration complete for {resolvedArchetype.archetypeName}");
    }

    private NPCArchetype ResolveArchetype()
    {
        if (customArchetype != null)
        {
            return customArchetype;
        }

        string archetypeId = $"archetype_{faction}_{npcType}_{importance}".ToLower();
        NPCArchetype archetype = Resources.Load<NPCArchetype>($"NPCArchetypes/{archetypeId}");

        if (archetype == null)
        {
            if (debugMode)
                Debug.LogWarning($"[NPCConfigurationHandler] Archetype '{archetypeId}' not found, creating default");
            archetype = CreateDefaultArchetype();
        }

        return archetype;
    }

    private NPCArchetype CreateDefaultArchetype()
    {
        var archetype = ScriptableObject.CreateInstance<NPCArchetype>();
        archetype.archetypeId = $"default_{faction}_{npcType}_{importance}";
        archetype.archetypeName = $"{faction} {npcType}";
        archetype.faction = faction;
        archetype.npcType = npcType;
        archetype.importance = importance;

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

    private void ConfigureFaction()
    {
        // Get FactionHandler via IdentitySystem's public property
        var identitySystem = brain.GetComponentInChildren<IdentitySystem>();
        if (identitySystem == null)
        {
            Debug.LogError("[NPCConfigurationHandler] IdentitySystem not found!");
            return;
        }

        UniversalFactionHandler factionHandler = identitySystem.Faction;
        if (factionHandler == null)
        {
            Debug.LogError("[NPCConfigurationHandler] FactionAffiliationHandler not found on IdentitySystem!");
            return;
        }

        RPG.Factions.FactionType factionType = ConvertNPCFactionToFactionType(resolvedArchetype.faction);
        factionHandler.SetFaction(factionType);

        if (debugMode)
        {
            Debug.Log($"[NPCConfigurationHandler] Set faction to {factionType}");
            Debug.Log($"[NPCConfigurationHandler] Verification - faction is now: {factionHandler.AffiliatedFaction}");
        }
    }

    private RPG.Factions.FactionType ConvertNPCFactionToFactionType(NPCFaction npcFaction)
    {
        string factionString = npcFaction.ToString().ToLower();

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

        return RPG.Factions.FactionType.Neutral;
    }

    private void ConfigureStats()
    {
        var coreStats = brain.GetModule<RPGCoreStats>();
        if (coreStats == null)
        {
            if (debugMode)
                Debug.LogWarning("[NPCConfigurationHandler] RPGCoreStats not found");
            return;
        }

        var stats = resolvedArchetype.baseStats;

        coreStats.SetStatBaseValue("Mind", stats.mind);
        coreStats.SetStatBaseValue("Body", stats.body);
        coreStats.SetStatBaseValue("Spirit", stats.spirit);
        coreStats.SetStatBaseValue("Resilience", stats.resilience);
        coreStats.SetStatBaseValue("Endurance", stats.endurance);
        coreStats.SetStatBaseValue("Insight", stats.insight);

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

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Configured stats: Body={stats.body}, Endurance={stats.endurance}");
    }

    private void ConfigureAbilities()
    {
        if (resolvedArchetype.abilities == null || resolvedArchetype.abilities.Count == 0)
        {
            if (debugMode)
                Debug.Log("[NPCConfigurationHandler] No abilities to configure");
            return;
        }

        var abilityModule = brain.GetModule<AbilityModule>();
        if (abilityModule == null)
        {
            if (debugMode)
                Debug.LogWarning("[NPCConfigurationHandler] AbilityModule not found");
            return;
        }


        // Optionally clear existing abilities
        if (clearAbilitiesBeforeConfiguring)
        {
            var existingAbilities = abilityModule.GetAllAbilities();
            foreach (var existing in existingAbilities)
            {
                abilityModule.RemoveAbility(existing.abilityId);
            }

            if (debugMode && existingAbilities.Count > 0)
                Debug.Log($"[NPCConfigurationHandler] Cleared {existingAbilities.Count} existing abilities");
        }

        // Add abilities from archetype
        foreach (var ability in resolvedArchetype.abilities)
        {
            if (ability != null)
            {
                abilityModule.AddAbility(ability);
                if (debugMode)
                    Debug.Log($"[NPCConfigurationHandler] Registered ability: {ability.abilityName}");
            }
        }

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Configured {resolvedArchetype.abilities.Count} abilities");
    }
    private void ConfigureBehaviorTree()
    {
        // DEPRECATED: Behavior Tree system has been removed
        // FSM + GOAP system will be implemented in phases
        // This method is kept as a stub to avoid breaking existing calls

        if (debugMode)
            Debug.Log("[NPCConfigurationHandler] ConfigureBehaviorTree - DEPRECATED (BT system removed)");
    }
    private void ConfigureAbilityLoadout()
    {
        // Check if archetype has loadout configuration
        if (resolvedArchetype.loadoutConfig == null || !resolvedArchetype.loadoutConfig.HasAnySlots())
        {
            if (debugMode)
                Debug.Log("[NPCConfigurationHandler] No ability loadout to configure (creature/natural weapons)");
            return;
        }

        // Get the loadout module
        var loadoutModule = brain.GetModule<AbilityLoadoutModule>();
        if (loadoutModule == null)
        {
            Debug.LogWarning("[NPCConfigurationHandler] AbilityLoadoutModule not found - humanoid NPCs need this!");
            return;
        }

        // Configure each slot from archetype
        var configuredSlots = resolvedArchetype.loadoutConfig.GetConfiguredSlots();
        int slotsConfigured = 0;

        foreach (var (key, slotData) in configuredSlots)
        {
            if (slotData != null)
            {
                if (key.ToUpper() == "BASICATTACK")
                {
                    loadoutModule.SetBasicAttackSlot(slotData);
                }
                else
                {
                    loadoutModule.AssignSlot(key, slotData);
                }
                slotsConfigured++;

                if (debugMode)
                {
                    string slotInfo = slotData.IsCombo
                        ? $"combo ({slotData.ChainLength} abilities)"
                        : "single ability";
                    Debug.Log($"[NPCConfigurationHandler] Configured {key} slot: {slotData.slotName} ({slotInfo})");
                }
            }
        }

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Configured {slotsConfigured} ability slots for loadout system");
    }
    // Replace ConfigureCombatBehavior method in NPCConfigurationHandler.cs with this version:

    // UPDATED ConfigureCombatBehavior with enhanced debugging
    // Replace the method in NPCConfigurationHandler.cs

    // Replace ConfigureCombatBehavior in NPCConfigurationHandler.cs

    // UPDATED: Works with AISystem instead of AIModule
    // Dynamically creates combat behaviors at runtime from archetype
    private void ConfigureCombatBehavior()
    {
        if (string.IsNullOrEmpty(resolvedArchetype.combatBehaviorClassName))
        {
            if (debugMode)
                Debug.Log("[NPCConfigurationHandler] No combat behavior specified");
            return;
        }

        var aiSystem = brain.GetModule<AISystem>();
        if (aiSystem == null)
        {
            if (debugMode)
                Debug.LogWarning("[NPCConfigurationHandler] AISystem not found");
            return;
        }

        // Check if behavior already exists
        var existingBehavior = brain.GetComponentInChildren<IAICombatBehavior>();
        if (existingBehavior != null)
        {
            if (debugMode)
                Debug.Log($"[NPCConfigurationHandler] Combat behavior already exists: {existingBehavior.GetType().Name}");
            return;
        }

        // Try to create the combat behavior by type name
        System.Type behaviorType = System.Type.GetType(resolvedArchetype.combatBehaviorClassName);
        if (behaviorType == null)
        {
            // Try with Assembly-CSharp prefix
            behaviorType = System.Type.GetType($"{resolvedArchetype.combatBehaviorClassName}, Assembly-CSharp");
        }

        if (behaviorType == null)
        {
            if (debugMode)
                Debug.LogError($"[NPCConfigurationHandler] Could not find type: {resolvedArchetype.combatBehaviorClassName}");
            return;
        }

        // Add combat behavior to AISystem's GameObject
        Transform aiTransform = aiSystem.transform;
        var behavior = aiTransform.gameObject.AddComponent(behaviorType) as IAICombatBehavior;

        if (behavior != null)
        {
            if (debugMode)
                Debug.Log($"[NPCConfigurationHandler] Created combat behavior: {behaviorType.Name}");

            // AISystem will auto-discover the combat behavior
            // No manual wiring needed - it uses GetModuleImplementing<IAICombatBehavior>()

            // Initialize the combat behavior through IBrainModule interface
            if (behavior is IBrainModule brainModule)
            {
                brainModule.Initialize(brain);

                if (debugMode)
                    Debug.Log($"[NPCConfigurationHandler] Initialized combat behavior: {behaviorType.Name}");
            }
        }
        else
        {
            if (debugMode)
                Debug.LogError($"[NPCConfigurationHandler] Failed to create combat behavior: {resolvedArchetype.combatBehaviorClassName}");
        }
    }

    private void ConfigureAI()
    {
        // Get AISystem from brain
        var aiSystem = brain.GetComponentInChildren<AISystem>();
        if (aiSystem == null)
        {
            if (debugMode)
                Debug.LogWarning("[NPCConfigurationHandler] AISystem not found - skipping AI configuration");
            return;
        }

        // Configure AI system from archetype (loads GOAP goals, perception settings, etc)
        aiSystem.ConfigureFromArchetype(resolvedArchetype);

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] AI system configured from archetype: {resolvedArchetype.archetypeName}");
    }

    private void ConfigureModel()
    {
        var modelModule = brain.GetModule<ModelModule>();
        if (modelModule == null)
        {
            if (debugMode)
                Debug.LogWarning("[NPCConfigurationHandler] ModelModule not found");
            return;
        }

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] ModelModule found, isEnabled: {modelModule.IsEnabled}");

        string modelId = overrideModelId;

        if (string.IsNullOrEmpty(modelId))
        {
            if (resolvedArchetype.modelPool != null && resolvedArchetype.modelPool.Count > 0)
            {
                if (resolvedArchetype.randomizeModel)
                {
                    int randomIndex = UnityEngine.Random.Range(0, resolvedArchetype.modelPool.Count);
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
            bool success = modelModule.SwapModel(modelId);
            if (debugMode)
            {
                if (success)
                    Debug.Log($"[NPCConfigurationHandler] Set model to: {modelId}");
                else
                    Debug.LogError($"[NPCConfigurationHandler] FAILED to set model to: {modelId}");
            }
        }
    }

    private void ConfigureEquipment()
    {
        // TODO: Implement equipment configuration when equipment system is ready
    }

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
                finalName = resolvedArchetype.archetypeName;
            }
        }

        parentNPC.SetName(finalName);

        if (debugMode)
            Debug.Log($"[NPCConfigurationHandler] Set name to: {finalName}");
    }

    public void Reconfigure(NPCFaction newFaction, NPCType newType, NPCImportance newImportance)
    {
        faction = newFaction;
        npcType = newType;
        importance = newImportance;

        ConfigureNPC();
    }

    [ContextMenu("Configure Now")]
    public void ForceConfigureNow()
    {
        if (brain == null)
            brain = GetComponentInParent<ControllerBrain>();
        if (parentNPC == null)
            parentNPC = brain?.GetModule<NPCModule>();

        ConfigureNPC();
    }

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