using UnityEngine;

/// <summary>
/// Universal Character Configuration Handler
///
/// INTELLIGENT BEHAVIOR:
/// - Checks entity type on initialization
/// - Auto-disables for Players (they load via SaveManager + GameEvents)
/// - Stays active for NPCs (they use archetypes)
///
/// This enables UNIVERSAL PREFABS:
/// - Same prefab for Players and NPCs
/// - ConfigHandler adapts automatically
/// - No manual component removal needed
/// </summary>
public class CharacterConfigurationHandler : MonoBehaviour, IBrainModule
{
    [Header("Configuration")]
    [SerializeField] private bool debugMode = false;

    [Header("Auto-Disable Settings")]
    [Tooltip("If true, this component disables itself for Player entities")]
    [SerializeField] private bool autoDisableForPlayers = true;

    [Header("Level Scaling")]
    [Tooltip("How much stats increase per level (0.15 = 15% per level)")]
    [SerializeField] private float statScalingPerLevel = 0.15f;

    private ControllerBrain brain;
    private IEntityConfig currentConfig;
    private bool isPlayerEntity = false;

    public bool IsEnabled { get; set; } = true;

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (autoDisableForPlayers && IsPlayerEntityType())
        {
            isPlayerEntity = true;
            IsEnabled = false;

            if (debugMode)
                Debug.Log($"[CharacterConfig] Player entity detected — auto-disabling on {brain.name}");

            return;
        }

        if (debugMode)
            Debug.Log($"[CharacterConfig] Initialized on {brain.name} (Type: {GetEntityTypeName()})");
    }

    public void UpdateModule() { }

    #endregion

    #region Smart Entity Detection

    private bool IsPlayerEntityType()
    {
        if (brain.Identity?.Identity != null && brain.Identity.Identity.Type == EntityType.Player)
            return true;

        if (gameObject.CompareTag("Player"))
            return true;

        if (brain.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null)
            return true;

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
    /// Configure entity from any NPC data source.
    /// Auto-skips if this is a player entity.
    /// </summary>
    public void Configure(IEntityConfig config)
    {
        if (isPlayerEntity)
        {
            if (debugMode)
                Debug.LogWarning("[CharacterConfig] Skipping Configure() — Player entities load via SaveManager.");
            return;
        }

        if (!IsEnabled)
        {
            if (debugMode)
                Debug.LogWarning("[CharacterConfig] Configure() called but handler is disabled.");
            return;
        }

        currentConfig = config;

        switch (config)
        {
            case NPCConfig npcConfig:
                ConfigureNPC(npcConfig);
                break;

            default:
                Debug.LogError($"[CharacterConfig] Unknown config type: {config.GetType()}");
                break;
        }
    }

    /// <summary>Manual override — force enable for special cases.</summary>
    public void ForceEnable()
    {
        isPlayerEntity = false;
        IsEnabled = true;

        if (debugMode)
            Debug.Log($"[CharacterConfig] Force-enabled on {brain.name}");
    }

    #endregion

    #region NPC Configuration

    private void ConfigureNPC(NPCConfig config)
    {
        var archetype = config.archetype;

        if (archetype == null)
        {
            Debug.LogError("[CharacterConfig] NPCConfig has null archetype!");
            return;
        }

        if (debugMode)
            Debug.Log($"[CharacterConfig] Configuring NPC from archetype: {archetype.archetypeName}");

        if (brain.Identity?.Identity != null)
            brain.Identity.Identity.Type = EntityType.NPC;

        ConfigureIdentity(archetype, config.overrideLevel);
        ConfigureFaction(archetype);
        ConfigureStats(archetype, config.overrideLevel);

        if (debugMode)
            Debug.Log($"[CharacterConfig] Applied archetype '{archetype.archetypeName}' to {brain.name}");
    }

    private void ConfigureIdentity(NPCArchetype archetype, int levelOverride)
    {
        var identity = brain.Identity?.Identity;
        if (identity == null)
        {
            Debug.LogWarning("[CharacterConfig] IdentityHandler not found!");
            return;
        }

        identity.DisplayName = archetype.useGenericName ? archetype.genericName : GenerateNPCName(archetype);
        identity.Level = levelOverride > 0 ? levelOverride : 1;
    }

    private void ConfigureFaction(NPCArchetype archetype)
    {
        var faction = brain.Identity?.Faction;
        if (faction == null) return;

        faction.SetFaction(ConvertToFactionType(archetype.faction));
    }

    private void ConfigureStats(NPCArchetype archetype, int levelOverride)
    {
        var statSystem = brain.Stats;
        if (statSystem == null)
        {
            Debug.LogWarning("[CharacterConfig] StatSystem not found!");
            return;
        }

        int level = levelOverride > 0 ? levelOverride : 1;
        float multiplier = 1f + (level - 1) * statScalingPerLevel;

        brain.RPG?.SetLevel(level);

        string ns = statSystem.HasStat("character.mind") ? "character"
                  : statSystem.HasStat("core.mind") ? "core"
                  : "character";

        var stats = archetype.baseStats;
        statSystem.SetBaseValue($"{ns}.mind", stats.mind * multiplier);
        statSystem.SetBaseValue($"{ns}.body", stats.body * multiplier);
        statSystem.SetBaseValue($"{ns}.spirit", stats.spirit * multiplier);
        statSystem.SetBaseValue($"{ns}.resilience", stats.resilience * multiplier);
        statSystem.SetBaseValue($"{ns}.endurance", stats.endurance * multiplier);
        statSystem.SetBaseValue($"{ns}.insight", stats.insight * multiplier);
    }

    #endregion

    #region Helpers

    private string GenerateNPCName(NPCArchetype archetype) => archetype.genericName;

    private RPG.Factions.FactionType ConvertToFactionType(NPCFaction npcFaction)
    {
        return npcFaction switch
        {
            NPCFaction.Wildlife => RPG.Factions.FactionType.Wildlife,
            NPCFaction.Beasts => RPG.Factions.FactionType.Monsters,
            NPCFaction.Humans => RPG.Factions.FactionType.Humans,
            NPCFaction.Elves => RPG.Factions.FactionType.Elves,
            NPCFaction.Dwarves => RPG.Factions.FactionType.Dwarves,
            NPCFaction.Undead => RPG.Factions.FactionType.Undead,
            NPCFaction.Warlocks => RPG.Factions.FactionType.Warlocks,
            NPCFaction.Demons => RPG.Factions.FactionType.Hostile,
            NPCFaction.Neutral => RPG.Factions.FactionType.Neutral,
            NPCFaction.Player => RPG.Factions.FactionType.Player,
            _ => RPG.Factions.FactionType.Neutral
        };
    }

    #endregion

    #region Debug

    [ContextMenu("Check Entity Type")]
    private void DebugCheckEntityType()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[CharacterConfig] Must be in Play Mode!"); return; }
        if (brain == null) { Debug.LogError("[CharacterConfig] Brain not initialized!"); return; }

        Debug.Log($"=== CHARACTER CONFIG DEBUG ===\n" +
                  $"GameObject: {gameObject.name}\n" +
                  $"Is Player Entity: {isPlayerEntity}\n" +
                  $"IsEnabled: {IsEnabled}\n" +
                  $"Entity Type: {GetEntityTypeName()}");
    }

    #endregion
}