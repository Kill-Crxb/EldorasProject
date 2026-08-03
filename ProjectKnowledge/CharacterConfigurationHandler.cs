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

        bool isPlayerConfig = !brain.IsNPC;
        bool isNpcConfig = !string.IsNullOrEmpty(data.characterId) && brain.Identity != null && data.characterId == brain.Identity.EntityId;

        if (!isPlayerConfig && !isNpcConfig)
        {
            Debug.Log($"[CharacterConfigurationHandler] Config not for this entity, skipping");
            return;
        }

        Debug.Log($"[CharacterConfigurationHandler] Configuring entity: name='{data.displayName}', faction='{data.factionId}'");

        if (brain.IsNPC)
            DisablePlayerOnlySystems();

        ConfigureIdentity(data);
        ConfigureFaction(data);
        ConfigureStats(data);
        ConfigureModel(data);
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

        var cameraCoordinator = brain.GetModule<CameraCoordinator>();
        if (cameraCoordinator != null)
            cameraCoordinator.IsEnabled = false;
    }

    private void ConfigureIdentity(CharacterConfigData data)
    {
        var identity = brain.Identity;
        if (identity == null)
            return;

        if (brain.IsNPC)
            identity.Type = EntityType.NPC;

        identity.DisplayName = data.displayName;
        identity.Level = data.level;
    }

    private void ConfigureFaction(CharacterConfigData data)
    {
        var factionSystem = brain.Faction;
        if (factionSystem == null)
            return;

        factionSystem.CurrentFactionId = data.factionId;
    }

    private void ConfigureStats(CharacterConfigData data)
    {
        var statSystem = brain.Stats;
        if (statSystem == null)
        {
            Debug.LogWarning("[CharacterConfigurationHandler] StatSystem not found");
            return;
        }

        int level = data.level > 0 ? data.level : 1;

        brain.RPG?.SetLevel(level);

        if (data.baseStatOverrides == null || data.baseStatOverrides.Length == 0)
        {
            Debug.LogWarning("[CharacterConfigurationHandler] No stat overrides in config");
            return;
        }

        Debug.Log($"[CharacterConfigurationHandler] Applying {data.baseStatOverrides.Length} stat overrides");

        foreach (var overrideEntry in data.baseStatOverrides)
        {
            if (string.IsNullOrEmpty(overrideEntry.statId))
                continue;

            var stat = statSystem.GetStat(overrideEntry.statId);
            if (stat == null)
            {
                Debug.LogWarning($"[CharacterConfigurationHandler] Stat '{overrideEntry.statId}' not found in engine");
                continue;
            }

            if (!string.IsNullOrEmpty(stat.formula))
            {
                Debug.Log($"[CharacterConfigurationHandler] Skipping '{overrideEntry.statId}' (has formula)");
                continue;
            }

            Debug.Log($"[CharacterConfigurationHandler] Setting {overrideEntry.statId} = {overrideEntry.baseValue}");
            statSystem.SetBaseValue(overrideEntry.statId, overrideEntry.baseValue);
        }

        Debug.Log("[CharacterConfigurationHandler] Stat configuration complete");
    }

    private void ConfigureModel(CharacterConfigData data)
    {
        if (string.IsNullOrEmpty(data.modelId))
            return;

        var modelModule = brain.GetModule<ModelModule>();
        if (modelModule != null)
            modelModule.SwapModel(data.modelId);
    }

    #endregion
}