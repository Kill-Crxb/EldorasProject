using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RPG.Factions;

public class SaveManager : MonoBehaviour, IGameManager, IManagerDependency, IUpdatableManager
{
    #region IGameManager

    public string ManagerName => "Save Manager";
    public int InitializationPriority => 25;
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    #endregion

    #region IManagerDependency

    public IEnumerable<Type> DependsOn => new[] { typeof(AccountManager) };

    #endregion

    #region Inspector

    [Header("Auto-Save")]
    [SerializeField] private bool autoSaveEnabled = true;
    [SerializeField] private float autoSaveInterval = 300f;

    [Tooltip("Seconds between writes of saveables that marked themselves dirty (an item picked up, a slot " +
             "equipped). A burst of changes inside one window is written once.")]
    [SerializeField] private float dirtyFlushInterval = 5f;

    #endregion

    #region State

    public string ActiveCharacterId { get; private set; } = string.Empty;
    public bool HasActiveCharacter => !string.IsNullOrEmpty(ActiveCharacterId);
    public ControllerBrain PlayerBrain => playerBrain;
    public ISaveProvider GetProvider() => provider;

    private ISaveProvider provider;
    private ControllerBrain playerBrain;
    private float autoSaveTimer;
    private float dirtyFlushTimer;

    // The player's saveables, found once per brain and sorted by LoadOrder (B6, Audit 2 S2–S4).
    private readonly List<ISaveable> saveables = new List<ISaveable>();
    private readonly HashSet<ISaveable> dirty = new HashSet<ISaveable>();
    private bool pendingLoad = false;

    // Only a character that finished loading is saved on shutdown. Saving a half-loaded one would
    // write its defaults over the real files.
    private bool characterLoaded;

    // Character select's index for the loaded character, refreshed from the owning modules on every save.
    private CharacterMetadata activeMetadata;


    #endregion

    #region IGameManager Lifecycle

    public void Initialize()
    {
        if (IsInitialized) return;
        autoSaveTimer = autoSaveInterval;
        IsInitialized = true;
    }

    public void LateInitialize()
    {
        var accountManager = ManagerBrain.Instance.GetManager<AccountManager>();
        if (accountManager == null)
        {
            Debug.LogError($"[{ManagerName}] AccountManager not found!");
            return;
        }

        provider = accountManager.SaveProvider;

        GameEvents.OnCharacterSelected += HandleCharacterSelected;
        GameEvents.OnGameSceneReady += HandleGameSceneReady;
        GameEvents.OnSaveRequested += HandleSaveRequested;
    }

    public void Shutdown()
    {
        SaveAllNow();
        UnsubscribeFromSaveables();

        GameEvents.OnCharacterSelected -= HandleCharacterSelected;
        GameEvents.OnGameSceneReady -= HandleGameSceneReady;
        GameEvents.OnSaveRequested -= HandleSaveRequested;
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();
        if (provider == null)
        {
            result.IsFatal = true;
            result.Errors.Add("SaveProvider not initialized");
        }
        return result;
    }

    #endregion

    #region Update

    public void UpdateManager()
    {
        if (!HasActiveCharacter || playerBrain == null) return;

        FlushDirty();

        if (!autoSaveEnabled) return;

        autoSaveTimer -= Time.deltaTime;
        if (autoSaveTimer > 0) return;

        autoSaveTimer = autoSaveInterval;
        _ = SaveAll();
    }

    private void FlushDirty()
    {
        if (dirty.Count == 0) return;

        dirtyFlushTimer -= Time.deltaTime;
        if (dirtyFlushTimer > 0) return;

        dirtyFlushTimer = dirtyFlushInterval;
        foreach (var saveable in dirty)
            _ = SaveModule(saveable);
        dirty.Clear();
    }

    private void HandleDirty(ISaveable saveable)
    {
        if (dirty.Count == 0) dirtyFlushTimer = dirtyFlushInterval;
        dirty.Add(saveable);
    }

    #endregion

    #region Player Brain Registration

    public void SetPlayerBrain(ControllerBrain brain)
    {
        UnsubscribeFromSaveables();

        playerBrain = brain;
        CacheSaveables();

        if (pendingLoad && HasActiveCharacter)
            _ = LoadCharacter(ActiveCharacterId);
    }

    #endregion

    #region Event Handlers

    private void HandleCharacterSelected(string characterId)
    {
        ActiveCharacterId = characterId;
        _ = LoadCharacter(characterId);
    }

    private void HandleGameSceneReady()
    {
        pendingLoad = true;
        if (playerBrain != null && HasActiveCharacter)
            _ = LoadCharacter(ActiveCharacterId);
    }

    private void HandleSaveRequested() => _ = SaveAll();

    #endregion

    #region Load

    public async Task LoadCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || provider == null) return;

        ActiveCharacterId = characterId;
        autoSaveTimer = autoSaveInterval;
        characterLoaded = false;
        activeMetadata = null;

        if (playerBrain == null)
        {
            pendingLoad = true;
            return;
        }

        pendingLoad = false;
        UnsubscribeFromSaveables();

        try
        {
            activeMetadata = await LoadMetadata(characterId);
            await LoadModulesInOrder(characterId);
            SubscribeToSaveables();
            await FirePlayerConfigAfterLoad(characterId);
            characterLoaded = true;
            playerBrain.MarkLoaded();
            GameEvents.LoadCompleted();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{ManagerName}] Failed to load character {characterId}: {ex.Message}");
        }
    }

    private async Task FirePlayerConfigAfterLoad(string characterId)
    {
        try
        {
            // Config is the creation seed. Once the character has been saved, each module's own file
            // is the truth, and re-applying the seed would reset level, XP, name and model (B21).
            string identityJson = await provider.Load(characterId, "identity");
            if (!string.IsNullOrEmpty(identityJson)) return;

            string configJson = await provider.Load(characterId, "config");

            if (string.IsNullOrEmpty(configJson))
            {
                Debug.LogWarning($"[{ManagerName}] No config file found for character {characterId}");
                return;
            }

            var configData = JsonUtility.FromJson<CharacterConfigData>(configJson);
            if (configData == null)
            {
                Debug.LogWarning($"[{ManagerName}] Failed to parse config JSON for {characterId}");
                return;
            }

            configData.characterId = playerBrain.Identity?.EntityId ?? characterId;
            CharacterConfigurationHandler.Apply(playerBrain, configData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{ManagerName}] Error loading config: {ex.Message}");
        }
    }

    private async Task LoadModulesInOrder(string characterId)
    {
        foreach (var module in saveables)
            await LoadModuleData(characterId, module.GetSaveId(), module);
    }

    private async Task LoadModuleData(string characterId, string saveId, ISaveable module)
    {
        string json = await provider.Load(characterId, saveId);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            module.LoadSaveData(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[{ManagerName}] Failed to load '{saveId}': {e.Message}");
        }
    }

    #region Save

    public async Task SaveAll()
    {
        if (string.IsNullOrEmpty(ActiveCharacterId) || playerBrain == null) return;

        dirty.Clear();
        foreach (var module in saveables)
            await SaveModule(module);

        await SaveFile("metadata", BuildMetadataJson());
    }

    // Quit and leaving play mode (B25). Synchronous, because an async write can be cut off by the exit.
    private void SaveAllNow()
    {
        if (!characterLoaded || playerBrain == null || provider == null) return;

        dirty.Clear();
        foreach (var module in saveables)
            provider.SaveNow(ActiveCharacterId, module.GetSaveId(), module.GetSaveData());

        provider.SaveNow(ActiveCharacterId, "metadata", BuildMetadataJson());
    }

    private async Task<CharacterMetadata> LoadMetadata(string characterId)
    {
        string json = await provider.Load(characterId, "metadata");
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<CharacterMetadata>(json);
    }

    // Metadata owns nothing: each field is copied from the module that owns the fact (Audit 2 P2).
    private string BuildMetadataJson()
    {
        if (activeMetadata == null || playerBrain == null) return null;

        if (playerBrain.Identity != null) activeMetadata.characterName = playerBrain.Identity.DisplayName;
        if (playerBrain.RPG != null) activeMetadata.level = playerBrain.RPG.CurrentLevel;

        var model = playerBrain.GetModule<ModelModule>();
        if (model != null) activeMetadata.modelId = model.CurrentModelId;

        activeMetadata.lastPlayedTime = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return JsonUtility.ToJson(activeMetadata, prettyPrint: true);
    }

    public async Task SaveFile(string saveId, string json)
    {
        if (string.IsNullOrEmpty(ActiveCharacterId) || provider == null) return;

        try
        {
            await provider.Save(ActiveCharacterId, saveId, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[{ManagerName}] Failed to save '{saveId}': {e.Message}");
        }
    }

    private async Task SaveModule(ISaveable module)
    {
        string saveId = module.GetSaveId();
        try
        {
            await SaveFile(saveId, module.GetSaveData());
        }
        catch (Exception e)
        {
            Debug.LogError($"[{ManagerName}] Failed to save '{saveId}': {e}");
        }
    }

    #endregion

    #region Helpers

    // Once per player brain, not per save. A second saveable with the same id is a prefab mistake;
    // the first found keeps the file.
    private void CacheSaveables()
    {
        saveables.Clear();
        dirty.Clear();
        if (playerBrain == null) return;

        var seen = new HashSet<string>();
        var found = new List<ISaveable>();
        foreach (var saveable in playerBrain.GetComponentsInChildren<ISaveable>())
        {
            if (seen.Add(saveable.GetSaveId())) found.Add(saveable);
            else Debug.LogError($"[{ManagerName}] Two saveables use the id '{saveable.GetSaveId()}' on {playerBrain.EntityName}; keeping the first.", playerBrain);
        }

        // Stable, so saveables sharing an order keep their hierarchy order, as before.
        saveables.AddRange(found.OrderBy(s => s.LoadOrder));
    }

    // After the load, so restoring a character doesn't mark everything dirty.
    private void SubscribeToSaveables()
    {
        foreach (var saveable in saveables)
            saveable.Dirty += HandleDirty;
    }

    private void UnsubscribeFromSaveables()
    {
        foreach (var saveable in saveables)
            saveable.Dirty -= HandleDirty;
    }

    #endregion

    #region Character Management

    public async Task<string> CreateCharacter(string characterName, string accountName = null)
    {
        if (string.IsNullOrWhiteSpace(characterName) || provider == null) return null;

        if (string.IsNullOrWhiteSpace(accountName))
        {
            var accountManager = ManagerBrain.Instance?.GetManager<AccountManager>();
            accountName = accountManager?.ActiveAccountName;
        }

        if (string.IsNullOrWhiteSpace(accountName)) return null;

        long timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string characterId = $"{accountName}/{FolderSafe(characterName)}_{timestamp}";

        var metadata = new CharacterMetadata
        {
            characterName = characterName,
            accountName = accountName,
            characterId = characterId,
            level = 1,
            creationTime = timestamp
        };

        bool success = await provider.Save(characterId, "metadata", JsonUtility.ToJson(metadata, prettyPrint: true));

        if (!success)
        {
            Debug.LogError($"[{ManagerName}] Failed to create character '{characterName}'");
            return null;
        }

        return characterId;
    }

    // The character id doubles as a folder path, so the name part keeps only letters, digits,
    // spaces, '-' and '_'. A name like "../x" would otherwise write outside the saves folder
    // (Audit 2 L3). The display name keeps whatever the player typed.
    private static string FolderSafe(string name)
    {
        var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' ? c : '_').ToArray();
        string safe = new string(chars).Trim();
        return safe.Length > 0 ? safe : "character";
    }

    public async Task<string> CreateCharacter(CharacterCreationData data)
    {
        string characterId = await CreateCharacter(data.characterName, data.accountName);

        if (string.IsNullOrEmpty(characterId))
            return characterId;

        // The rolled statline, or a flat baseline for callers that only pass a name.
        var seedStats = data.baseStats != null && data.baseStats.Length > 0
            ? data.baseStats
            : DefaultBaseStats();

        // config.json carries identity and appearance only. Stats live in stats.json,
        // which StatSystem owns as an ISaveable — one writer, one reader, no drift.
        var configData = new CharacterConfigData
        {
            characterId = characterId,
            displayName = data.characterName,
            // EMPTY ON PURPOSE. There is no "the player faction" — the player belongs to one of
            // the world's real factions like anyone else, and which one is a property of the
            // character, not of the save layer.
            //
            // CharacterConfigurationHandler.ConfigureFaction skips an empty id specifically so
            // it does not wipe the faction configured on the prefab, so leaving this blank means
            // the character keeps whatever Base_PC is set to. When faction becomes a creation
            // choice, it gets written here from CharacterCreationData instead.
            factionId = "",
            level = 1,
            modelId = data.modelId
        };

        try
        {
            await provider.Save(characterId, "config", JsonUtility.ToJson(configData));

            var statSaveData = new StatSeedData();
            foreach (var stat in seedStats)
                statSaveData.stats.Add(new StatSeedPair { id = stat.statId, value = stat.baseValue });

            await provider.Save(characterId, "stats", JsonUtility.ToJson(statSaveData));

            // Write an explicit empty equipment file. Without this, a fresh character
            // has no "equipment.json" on disk, so LoadModuleData's `if (string.IsNullOrEmpty(json)) return;`
            // guard skips calling EquipmentSystem.LoadSaveData() entirely on first login.
            // That leaves whatever items happen to be serialized in EquipmentSystem's
            // inspector-visible "equippedItems" debug list (e.g. a sword equipped once
            // in the editor) as the character's equipment, instead of an empty loadout.
            await provider.Save(characterId, "equipment", "{\"version\":1,\"slots\":[]}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{ManagerName}] Failed to save character config/stats: {ex.Message}");
        }

        return characterId;
    }

    private static StatBaseOverride[] DefaultBaseStats()
    {
        return new StatBaseOverride[]
        {
            new StatBaseOverride { statId = "core.mind", baseValue = 3 },
            new StatBaseOverride { statId = "core.body", baseValue = 3 },
            new StatBaseOverride { statId = "core.spirit", baseValue = 3 },
            new StatBaseOverride { statId = "core.resilience", baseValue = 3 },
            new StatBaseOverride { statId = "core.endurance", baseValue = 3 },
            new StatBaseOverride { statId = "core.insight", baseValue = 3 },
        };
    }

    // Mirrors StatSystem's save shape. JsonUtility cannot serialise a Dictionary,
    // so both sides use a list of pairs.
    [System.Serializable]
    private class StatSeedData
    {
        public int version = 2;
        public System.Collections.Generic.List<StatSeedPair> stats = new();
    }

    [System.Serializable]
    private class StatSeedPair
    {
        public string id;
        public float value;
    }

    public async Task<List<CharacterMetadata>> GetAllCharacters()
    {
        var result = new List<CharacterMetadata>();

        if (provider == null)
            return result;

        try
        {
            string[] characterIds = await provider.GetCharacters(null);

            foreach (string characterId in characterIds)
            {
                string json = await provider.Load(characterId, "metadata");
                if (string.IsNullOrEmpty(json))
                    continue;

                var metadata = JsonUtility.FromJson<CharacterMetadata>(json);
                if (metadata != null)
                    result.Add(metadata);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{ManagerName}] Failed to get characters: {e.Message}");
        }

        return result;
    }

    #endregion
}
#endregion
[System.Serializable]
public class CharacterMetadata
{
    public string characterId;
    public string characterName;
    public int level;
    public string accountName;
    public long creationTime;
    public long lastPlayedTime;
    public string modelId;
}