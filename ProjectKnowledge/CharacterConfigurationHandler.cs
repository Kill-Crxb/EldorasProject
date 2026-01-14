using UnityEngine;

/// <summary>
/// Universal Character Configuration Handler (Simplified for Current Codebase)
/// 
/// INTELLIGENT BEHAVIOR:
/// - Checks entity type on initialization
/// - Auto-disables for Players (they use StatSchemas + SaveSystem)
/// - Stays active for NPCs (they use archetypes)
/// 
/// This enables UNIVERSAL PREFABS:
/// - Same prefab for Players and NPCs
/// - ConfigHandler adapts automatically
/// - No manual component removal needed
/// 
/// Phase 1.7b: Universal entity initialization with smart detection
/// SIMPLIFIED: Only uses features that exist in current codebase
/// </summary>
public class CharacterConfigurationHandler : MonoBehaviour, IBrainModule
{
    [Header("Configuration")]
    [SerializeField] private bool debugMode = false;

    [Header("Auto-Disable Settings")]
    [SerializeField] private bool autoDisableForPlayers = true;
    [Tooltip("If true, this component disables itself for Player entities")]

    [Header("Level Scaling")]
    [SerializeField] private float statScalingPerLevel = 0.15f;
    [Tooltip("How much stats increase per level (0.15 = 15% per level)")]

    private ControllerBrain brain;
    private IEntityConfig currentConfig;
    private bool isPlayerEntity = false;

    public bool IsEnabled { get; set; } = true;

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // SMART CHECK: Detect entity type
        if (autoDisableForPlayers && IsPlayerEntityType())
        {
            isPlayerEntity = true;
            IsEnabled = false;

            if (debugMode)
                Debug.Log($"[CharacterConfig] Player entity detected - auto-disabling ConfigHandler on {brain.name}");

            return;  // Exit early - don't configure players!
        }

        if (debugMode)
            Debug.Log($"[CharacterConfig] Initialized on {brain.name} (Entity Type: {GetEntityTypeName()})");
    }

    public void UpdateModule()
    {
        // No per-frame updates needed
    }

    #endregion

    #region Smart Entity Detection

    /// <summary>
    /// Check if this entity is a Player type
    /// </summary>
    private bool IsPlayerEntityType()
    {
        // Method 1: Check IdentitySystem entity type
        if (brain.Identity?.Identity != null)
        {
            var entityType = brain.Identity.Identity.Type;

            if (entityType == EntityType.Player)
            {
                if (debugMode)
                    Debug.Log($"[CharacterConfig] Entity type is Player - ConfigHandler not needed");
                return true;
            }
        }

        // Method 2: Check GameObject tag (fallback)
        if (gameObject.CompareTag("Player"))
        {
            if (debugMode)
                Debug.Log($"[CharacterConfig] GameObject tagged as Player - ConfigHandler not needed");
            return true;
        }

        // Method 3: Check for player-specific modules (fallback)
        if (brain.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null)
        {
            if (debugMode)
                Debug.Log($"[CharacterConfig] Has PlayerInput component - assuming Player entity");
            return true;
        }

        return false;
    }

    private string GetEntityTypeName()
    {
        if (brain.Identity?.Identity != null)
            return brain.Identity.Identity.Type.ToString();
        return "Unknown";
    }

    #endregion

    #region Public API

    /// <summary>
    /// Configure entity from any data source
    /// Auto-skips if this is a player entity
    /// </summary>
    public void Configure(IEntityConfig config)
    {
        // SMART GUARD: Don't configure players!
        if (isPlayerEntity)
        {
            if (debugMode)
                Debug.LogWarning($"[CharacterConfig] Skipping Configure() - this is a Player entity. Players use SaveSystem, not ConfigHandler.");
            return;
        }

        if (!IsEnabled)
        {
            if (debugMode)
                Debug.LogWarning($"[CharacterConfig] Configure() called but handler is disabled!");
            return;
        }

        currentConfig = config;

        switch (config)
        {
            case PlayerConfig playerConfig:
                // This shouldn't happen due to smart detection, but handle it gracefully
                if (debugMode)
                    Debug.LogWarning($"[CharacterConfig] PlayerConfig passed but auto-disable should have caught this. Configuring anyway.");
                ConfigurePlayer(playerConfig);
                break;

            case NPCConfig npcConfig:
                ConfigureNPC(npcConfig);
                break;

            default:
                Debug.LogError($"[CharacterConfig] Unknown config type: {config.GetType()}");
                break;
        }
    }

    /// <summary>
    /// Manual override - force enable for special cases
    /// </summary>
    public void ForceEnable()
    {
        isPlayerEntity = false;
        IsEnabled = true;

        if (debugMode)
            Debug.Log($"[CharacterConfig] Force-enabled on {brain.name}");
    }

    #endregion

    #region Entity-Specific Configuration

    private void ConfigurePlayer(PlayerConfig config)
    {
        if (debugMode)
            Debug.Log($"[CharacterConfig] Configuring player from save slot {config.saveSlot}");

        // Set entity type
        if (brain.Identity?.Identity != null)
        {
            brain.Identity.Identity.Type = EntityType.Player;
            brain.Identity.Identity.DisplayName = config.playerName ?? "Player";
        }

        // Delegate to SaveSystem
        var saveSystem = brain.GetModule<SaveSystemModule>();
        if (saveSystem != null)
        {
            saveSystem.LoadPlayerData(config.saveSlot);
        }
        else
        {
            Debug.LogError("[CharacterConfig] SaveSystemModule not found for player configuration!");
        }
    }

    private void ConfigureNPC(NPCConfig config)
    {
        if (debugMode)
            Debug.Log($"[CharacterConfig] Configuring NPC from archetype: {config.archetype.archetypeName}");

        // Set entity type
        if (brain.Identity?.Identity != null)
        {
            brain.Identity.Identity.Type = EntityType.NPC;
        }

        // Get archetype
        var archetype = config.archetype;
        if (archetype == null)
        {
            Debug.LogError("[CharacterConfig] NPCConfig has null archetype!");
            return;
        }

        // Get level override (or use archetype base level)
        int levelOverride = config.overrideLevel;

        // Configure all subsystems
        ConfigureIdentity(archetype, levelOverride);
        ConfigureFaction(archetype);
        ConfigureStats(archetype, levelOverride);

        if (debugMode)
            Debug.Log($"[CharacterConfig] Applied archetype '{archetype.archetypeName}' to {brain.name}");
    }

    #endregion

    #region NPC Configuration Subsystems

    private void ConfigureIdentity(NPCArchetype archetype, int levelOverride)
    {
        var identity = brain.Identity?.Identity;
        if (identity == null)
        {
            Debug.LogWarning("[CharacterConfig] IdentityHandler not found!");
            return;
        }

        // Set name
        if (archetype.useGenericName)
            identity.DisplayName = archetype.genericName;
        else
            identity.DisplayName = GenerateNPCName(archetype);

        // Set level - use override if provided, otherwise default to level 1
        // (NPCArchetype doesn't have baseLevel field in current codebase)
        int level = levelOverride > 0 ? levelOverride : 1;
        identity.Level = level;
    }

    private void ConfigureFaction(NPCArchetype archetype)
    {
        var faction = brain.Identity?.Faction;
        if (faction == null)
        {
            if (debugMode)
                Debug.LogWarning("[CharacterConfig] FactionHandler not found");
            return;
        }

        // Convert NPCFaction to RPG.Factions.FactionType
        faction.SetFaction(ConvertToFactionType(archetype.faction));
    }

    private void ConfigureStats(NPCArchetype archetype, int levelOverride)
    {
        var statSystem = brain.Stats;
        var rpgSystem = brain.RPG;

        if (statSystem == null)
        {
            Debug.LogWarning("[CharacterConfig] StatSystem not found!");
            return;
        }

        // Determine level
        int level = levelOverride > 0 ? levelOverride : 1;

        // Set RPGSystem level if present (for dynamic scaling)
        if (rpgSystem != null)
        {
            rpgSystem.SetLevel(level);
        }

        // Calculate level multiplier
        float levelMultiplier = CalculateLevelMultiplier(level);

        // Auto-detect core stat namespace (works with "character" or "core")
        string coreNamespace = DetectCoreStatNamespace(statSystem);

        // Apply scaled base stats
        var stats = archetype.baseStats;
        statSystem.SetBaseValue($"{coreNamespace}.mind", stats.mind * levelMultiplier);
        statSystem.SetBaseValue($"{coreNamespace}.body", stats.body * levelMultiplier);
        statSystem.SetBaseValue($"{coreNamespace}.spirit", stats.spirit * levelMultiplier);
        statSystem.SetBaseValue($"{coreNamespace}.resilience", stats.resilience * levelMultiplier);
        statSystem.SetBaseValue($"{coreNamespace}.endurance", stats.endurance * levelMultiplier);
        statSystem.SetBaseValue($"{coreNamespace}.insight", stats.insight * levelMultiplier);

        // NOTE: healthMultiplier and damageMultiplier exist in StatAllocation
        // but StatSystem doesn't have AddModifier() method yet
        // These will be applied when that system is implemented
        // For now, they're stored in archetype but not applied

        if (debugMode && (stats.healthMultiplier != 1f || stats.damageMultiplier != 1f))
        {
            Debug.LogWarning($"[CharacterConfig] Archetype has health/damage multipliers but StatSystem.AddModifier() not yet implemented. Multipliers ignored for now.");
        }
    }

    #endregion

    #region Helper Methods

    private float CalculateLevelMultiplier(int level)
    {
        // Simple linear scaling: 1.0 at level 1, increases by statScalingPerLevel per level
        // Level 1 = 1.0x, Level 2 = 1.15x, Level 3 = 1.3x, etc.
        return 1.0f + (level - 1) * statScalingPerLevel;
    }

    private string DetectCoreStatNamespace(StatSystem statSystem)
    {
        // Try "character" first (standard)
        if (statSystem.HasStat("character.mind"))
            return "character";

        // Try "core" (alternative)
        if (statSystem.HasStat("core.mind"))
            return "core";

        // Default to character
        if (debugMode)
            Debug.LogWarning("[CharacterConfig] Could not detect core stat namespace, defaulting to 'character'");
        return "character";
    }

    private string GenerateNPCName(NPCArchetype archetype)
    {
        // TODO: Implement name generation from prefix/suffix lists
        return archetype.genericName;
    }

    private RPG.Factions.FactionType ConvertToFactionType(NPCFaction npcFaction)
    {
        // Map NPCFaction enum to RPG.Factions.FactionType enum
        return npcFaction switch
        {
            // Wildlife
            NPCFaction.Wildlife => RPG.Factions.FactionType.Wildlife,
            NPCFaction.Beasts => RPG.Factions.FactionType.Monsters,

            // Civilized Factions
            NPCFaction.Humans => RPG.Factions.FactionType.Humans,
            NPCFaction.Elves => RPG.Factions.FactionType.Elves,
            NPCFaction.Dwarves => RPG.Factions.FactionType.Dwarves,

            // Hostile Factions
            NPCFaction.Undead => RPG.Factions.FactionType.Undead,
            NPCFaction.Warlocks => RPG.Factions.FactionType.Warlocks,
            NPCFaction.Demons => RPG.Factions.FactionType.Hostile,

            // Special
            NPCFaction.Neutral => RPG.Factions.FactionType.Neutral,
            NPCFaction.Player => RPG.Factions.FactionType.Player,

            // Default
            _ => RPG.Factions.FactionType.Neutral
        };
    }

    #endregion

    #region Inspector Helpers

    [ContextMenu("Check Entity Type")]
    private void DebugCheckEntityType()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CharacterConfig] Must be in Play Mode!");
            return;
        }

        if (brain == null)
        {
            Debug.LogError("[CharacterConfig] Brain not initialized!");
            return;
        }

        Debug.Log($"=== CHARACTER CONFIG DEBUG ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Is Player Entity: {isPlayerEntity}");
        Debug.Log($"IsEnabled: {IsEnabled}");
        Debug.Log($"Entity Type: {GetEntityTypeName()}");
        Debug.Log($"Auto-Disable For Players: {autoDisableForPlayers}");

        if (brain.Identity?.Identity != null)
        {
            Debug.Log($"Identity Type: {brain.Identity.Identity.Type}");
            Debug.Log($"Display Name: {brain.Identity.Identity.DisplayName}");
        }
    }

    #endregion
}