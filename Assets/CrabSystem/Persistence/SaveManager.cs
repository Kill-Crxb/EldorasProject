using System;
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

    #endregion

    #region State

    public string ActiveCharacterId { get; private set; } = string.Empty;
    public bool HasActiveCharacter => !string.IsNullOrEmpty(ActiveCharacterId);
    public ControllerBrain PlayerBrain => playerBrain;
    public ISaveProvider GetProvider() => provider;

    private ISaveProvider provider;
    private ControllerBrain playerBrain;
    private float autoSaveTimer;
    private bool pendingLoad = false;

    private static readonly string[] LoadOrder = { "stats", "model", "inputProfile", "inventory", "equipment", "hotbar", "resources", "dialogue" };

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
        if (!autoSaveEnabled || !HasActiveCharacter || playerBrain == null) return;

        autoSaveTimer -= Time.deltaTime;
        if (autoSaveTimer <= 0)
        {
            autoSaveTimer = autoSaveInterval;
            _ = SaveAll();
        }
    }

    #endregion

    #region Player Brain Registration

    public void SetPlayerBrain(ControllerBrain brain)
    {
        if (playerBrain != null)
            UnsubscribeFromSaveableEvents();

        playerBrain = brain;

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

    private void HandleInventoryChanged()
    {
        _ = SaveFile("inventory", playerBrain.Inventory.GetSaveData());
    }

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        var equipment = playerBrain.GetModule<EquipmentSystem>();
        if (equipment is ISaveable saveable)
            _ = SaveFile(saveable.GetSaveId(), saveable.GetSaveData());
    }

    #endregion

    #region Load

    public async Task LoadCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId) || provider == null) return;

        ActiveCharacterId = characterId;
        autoSaveTimer = autoSaveInterval;

        if (playerBrain == null)
        {
            pendingLoad = true;
            return;
        }

        pendingLoad = false;

        try
        {
            await LoadModulesInOrder(characterId);
            SubscribeToSaveableEvents();
            await FirePlayerConfigAfterLoad(characterId);
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
            GameEvents.CharacterConfigDataReady(configData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{ManagerName}] Error loading config: {ex.Message}");
        }
    }

    private async Task LoadModulesInOrder(string characterId)
    {
        var saveables = BuildSaveableLookup();

        foreach (string saveId in LoadOrder)
        {
            if (!saveables.TryGetValue(saveId, out ISaveable module)) continue;
            await LoadModuleData(characterId, saveId, module);
        }

        foreach (var kvp in saveables)
        {
            if (System.Array.IndexOf(LoadOrder, kvp.Key) >= 0) continue;
            await LoadModuleData(characterId, kvp.Key, kvp.Value);
        }
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

        var saveables = BuildSaveableLookup();
        foreach (var kvp in saveables)
        {
            try
            {
                await SaveModule(kvp.Key, kvp.Value);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{ManagerName}] Failed to save '{kvp.Key}': {e.Message}");
            }
        }
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

    private async Task SaveModule(string saveId, ISaveable module)
    {
        if (module == null) return;
        string json = module.GetSaveData();
        await SaveFile(saveId, json);
    }

    #endregion

    #region Helpers

    private void SubscribeToSaveableEvents()
    {
        if (playerBrain == null) return;

        var inventory = playerBrain.GetModule<InventorySystem>();
        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;

        var equipment = playerBrain.GetModule<EquipmentSystem>();
        if (equipment != null)
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    private void UnsubscribeFromSaveableEvents()
    {
        if (playerBrain == null) return;

        var inventory = playerBrain.GetModule<InventorySystem>();
        if (inventory != null)
            inventory.OnInventoryChanged -= HandleInventoryChanged;

        var equipment = playerBrain.GetModule<EquipmentSystem>();
        if (equipment != null)
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    private Dictionary<string, ISaveable> BuildSaveableLookup()
    {
        var lookup = new Dictionary<string, ISaveable>();

        if (playerBrain == null)
            return lookup;

        var saveables = playerBrain.GetComponents<ISaveable>();
        var children = playerBrain.GetComponentsInChildren<ISaveable>();

        foreach (var saveable in saveables)
            lookup[saveable.GetSaveId()] = saveable;

        foreach (var saveable in children)
        {
            if (!lookup.ContainsKey(saveable.GetSaveId()))
                lookup[saveable.GetSaveId()] = saveable;
        }

        return lookup;
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
        string characterId = $"{accountName}/{characterName}_{timestamp}";

        var metadata = new CharacterMetadata
        {
            characterName = characterName,
            accountName = accountName,
            characterId = characterId,
            level = 1
        };

        bool success = await provider.Save(characterId, "metadata", JsonUtility.ToJson(metadata, prettyPrint: true));

        if (!success)
        {
            Debug.LogError($"[{ManagerName}] Failed to create character '{characterName}'");
            return null;
        }

        return characterId;
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