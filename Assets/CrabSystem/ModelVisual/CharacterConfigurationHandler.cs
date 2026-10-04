using UnityEngine;

public class CharacterConfigurationHandler : MonoBehaviour, IBrainModule
{
    #region Inspector

    [Header("Configuration")]
    [SerializeField] private bool autoDisableForPlayers = true;

    [Header("Level Scaling")]
    [SerializeField] private float statScalingPerLevel = 0.15f;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private bool isPlayerEntity;
    private bool nameplateSpawned;

    #endregion

    #region Properties

    public bool IsEnabled { get; set; } = true;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (autoDisableForPlayers && IsPlayerEntity())
            isPlayerEntity = true;

        GameEvents.OnCharacterConfigDataReady += ConfigureEntity;
    }

    public void UpdateModule()
    {
    }

    public void LateInitialize()
    {
    }

    // Subscribed in Initialize, so it must come off here. Without this every spawned entity leaks
    // a delegate, and the next broadcast after a despawn or a scene round-trip reaches destroyed
    // objects as a MissingReferenceException.
    void OnDestroy()
    {
        GameEvents.OnCharacterConfigDataReady -= ConfigureEntity;
    }

    #endregion

    #region Entity Detection

    private bool IsPlayerEntity()
    {
        if (brain.Identity != null && brain.Identity.Type == EntityType.Player)
            return true;

        if (gameObject.CompareTag("Player"))
            return true;

        if (brain.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null)
            return true;

        return false;
    }

    #endregion

    #region Public API

    public void ConfigureEntity(CharacterConfigData data)
    {
        if (data == null)
        {
            Debug.LogWarning($"[CharacterConfigurationHandler] Received null config data");
            return;
        }

        if (!IsEnabled)
        {
            Debug.LogWarning($"[CharacterConfigurationHandler] Handler is disabled");
            return;
        }

        // The event is global: every entity hears every config. Only the one it names applies it.
        // (The player used to accept any config, so NPC configs overwrote its faction and stats.)
        if (brain.Identity == null || data.characterId != brain.Identity.EntityId)
            return;

        if (brain.IsNPC)
            DisablePlayerOnlySystems();

        ConfigureIdentity(data);
        ConfigureFaction(data);
        ConfigureStats(data);
        ConfigureModel(data);

        if (brain.IsNPC)
            TrySpawnNameplate();
    }

    public void ForceEnable()
    {
        isPlayerEntity = false;
        IsEnabled = true;
    }

    #endregion

    #region Configuration

    private void DisablePlayerOnlySystems()
    {
        if (brain.Input != null)
        {
            brain.Input.SetMode(InputMode.AI);
            brain.Input.IsEnabled = false;
        }

        // Camera disable reconnects here after the camera rebuild.
    }

    private void ConfigureIdentity(CharacterConfigData data)
    {
        var identity = brain.Identity;
        if (identity == null)
            return;

        identity.DisplayName = data.displayName;
    }

    private void ConfigureFaction(CharacterConfigData data)
    {
        var factionSystem = brain.Faction;
        if (factionSystem == null)
            return;

        // Empty = archetype has no faction assigned. Don't wipe a faction
        // that was set directly on the prefab's FactionSystem.
        if (string.IsNullOrEmpty(data.factionId))
            return;

        factionSystem.CurrentFactionId = data.factionId;
    }

    private void ConfigureStats(CharacterConfigData data)
    {
        var statSystem = brain.Stats;
        if (statSystem == null)
        {
            Debug.LogWarning("[CharacterConfigurationHandler] No stat provider found");
            return;
        }

        int level = data.level > 0 ? data.level : 1;

        brain.RPG?.SetLevel(level);

        // Overrides are the authored seed for NPC archetypes. A saved character has none:
        // its stats came from stats.json before this runs, and must not be overwritten.
        if (data.baseStatOverrides == null) return;

        foreach (var overrideEntry in data.baseStatOverrides)
        {
            if (string.IsNullOrEmpty(overrideEntry.statId))
                continue;

            if (!statSystem.HasStat(overrideEntry.statId))
            {
                Debug.LogWarning($"[CharacterConfigurationHandler] Stat '{overrideEntry.statId}' not loaded on {brain.EntityName}");
                continue;
            }

            // A derived stat refuses SetValue, so the archetype feeds it as a contribution instead.
            if (statSystem.IsDerived(overrideEntry.statId))
            {
                statSystem.AddContribution(overrideEntry.statId, "archetype", overrideEntry.baseValue);
                continue;
            }

            statSystem.SetValue(overrideEntry.statId, overrideEntry.baseValue);
        }
    }

    private void ConfigureModel(CharacterConfigData data)
    {
        if (string.IsNullOrEmpty(data.modelId))
            return;

        var modelModule = brain.GetModule<ModelModule>();
        if (modelModule != null)
            modelModule.SwapModel(data.modelId);
    }

    private void TrySpawnNameplate()
    {
        if (nameplateSpawned)
            return;

        nameplateSpawned = true;
        NameplateManager.Instance?.SpawnNameplate(brain);
    }

    #endregion
}