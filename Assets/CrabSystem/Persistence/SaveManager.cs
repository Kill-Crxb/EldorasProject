using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Save Manager — Phase 3 of the Persistence System
///
/// Responsibilities:
/// - Holds the active characterId for the session
/// - Orchestrates Load / Save across all ISaveable modules on the player brain
/// - Enforces dependency load order: stats → inventory → equipment
/// - Responds to GameEvents (OnCharacterSelected, OnSaveRequested, OnApplicationExiting)
/// - Writes metadata.json at save time (mirrors level so CharacterSelect needs no stats load)
///
/// Architecture:
/// - IGameManager child of ManagerBrain (auto-discovered via GetComponentsInChildren)
/// - Priority 25 — initialises after AccountManager (priority 20)
/// - ISaveProvider retrieved from AccountManager during LateInitialize
/// - Player brain reference set via SetPlayerBrain() — called by the player entity on scene load
/// </summary>
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
    [SerializeField] private float autoSaveInterval = 300f; // 5 minutes

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    #endregion

    #region State

    /// <summary>
    /// The character folder ID currently loaded (e.g. "Crxb/Hanzo_1740220800").
    /// Empty string when no character is active.
    /// </summary>
    public string ActiveCharacterId { get; private set; } = string.Empty;

    public bool HasActiveCharacter => !string.IsNullOrEmpty(ActiveCharacterId);

    private ISaveProvider provider;
    private ControllerBrain playerBrain;
    private float autoSaveTimer;
    private bool pendingLoad = false;

    // Load order: stats first (base values), then inventory (items), then equipment (re-applies item stat modifiers)
    // stats first (base values), inventory (items), equipment (re-applies item stat modifiers), resources last (max is stat-driven)
    private static readonly string[] LoadOrder = { "stats", "inventory", "equipment", "resources" };

    #endregion

    #region IGameManager Lifecycle

    public void Initialize()
    {
        if (IsInitialized) return;

        autoSaveTimer = autoSaveInterval;
        IsInitialized = true;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Initialized");
    }

    public void LateInitialize()
    {
        // Retrieve provider from AccountManager (guaranteed initialised before us)
        var accountManager = ManagerBrain.Instance.GetManager<AccountManager>();
        if (accountManager == null)
        {
            Debug.LogError($"[{ManagerName}] AccountManager not found! Save system will not function.");
            return;
        }

        provider = accountManager.SaveProvider;

        // Subscribe to game events
        GameEvents.OnCharacterSelected += HandleCharacterSelected;
        GameEvents.OnGameSceneReady += HandleGameSceneReady;
        GameEvents.OnSaveRequested += HandleSaveRequested;
        GameEvents.OnTargetedSaveRequested += HandleTargetedSaveRequested;
        GameEvents.OnApplicationExiting += HandleApplicationExiting;
        Application.quitting += HandleApplicationQuitting;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] LateInitialize — provider: {provider?.GetType().Name ?? "null"}");
    }

    public void Shutdown()
    {
        GameEvents.OnCharacterSelected -= HandleCharacterSelected;
        GameEvents.OnGameSceneReady -= HandleGameSceneReady;
        GameEvents.OnSaveRequested -= HandleSaveRequested;
        GameEvents.OnTargetedSaveRequested -= HandleTargetedSaveRequested;
        GameEvents.OnApplicationExiting -= HandleApplicationExiting;
        Application.quitting -= HandleApplicationQuitting;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Shutdown");
    }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();

        if (provider == null)
            result.Warnings.Add("ISaveProvider is null — AccountManager may not have initialised yet");

        return result;
    }

    #endregion

    #region IUpdatableManager

    public void UpdateManager()
    {
        if (!autoSaveEnabled) return;
        if (!HasActiveCharacter) return;
        if (playerBrain == null) return;

        autoSaveTimer -= Time.deltaTime;
        if (autoSaveTimer > 0f) return;

        autoSaveTimer = autoSaveInterval;
        _ = SaveAll();
    }

    #endregion

    #region Player Brain Registration

    /// <summary>
    /// Called by the player entity (e.g. from ControllerBrain.Start or a dedicated
    /// PlayerRegistrationModule) once the game scene is ready.
    /// SaveManager cannot discover the player brain itself — the brain calls in.
    /// </summary>
    public ControllerBrain PlayerBrain => playerBrain;

    public void SetPlayerBrain(ControllerBrain brain)
    {
        if (playerBrain != null)
        {
            var oldInventory = playerBrain.GetModule<InventorySystem>();
            if (oldInventory != null)
                oldInventory.OnInventoryChanged -= HandleInventoryChanged;

            var oldEquipment = playerBrain.GetModule<EquipmentSystem>();
            if (oldEquipment != null)
                oldEquipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        playerBrain = brain;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Player brain registered: {brain.name}");

        if (pendingLoad && HasActiveCharacter)
        {
            if (debugLogging)
                Debug.Log($"[{ManagerName}] Executing deferred load for {ActiveCharacterId}");

            _ = LoadCharacter(ActiveCharacterId);
        }
        else
        {
            // Brain registered without a pending load — still wire up inventory save events
            SubscribeToSaveableEvents();
        }
    }

    private void SubscribeToSaveableEvents()
    {
        var inventory = playerBrain?.GetModule<InventorySystem>();
        if (inventory != null)
            inventory.OnInventoryChanged += HandleInventoryChanged;

        var equipment = playerBrain?.GetModule<EquipmentSystem>();
        if (equipment != null)
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
    }

    #endregion

    #region Character Operations

    /// <summary>
    /// Create a new character folder and write blank default state for all ISaveable modules.
    /// Returns the new characterId (e.g. "Crxb/Hanzo_1740220800").
    /// </summary>
    public async Task<string> CreateCharacter(string characterName, string accountName = null)
    {
        if (string.IsNullOrWhiteSpace(characterName)) return null;
        if (provider == null) return null;

        if (string.IsNullOrWhiteSpace(accountName))
        {
            var accountManager = ManagerBrain.Instance?.GetManager<AccountManager>();
            accountName = accountManager?.ActiveAccountName;
        }

        if (string.IsNullOrWhiteSpace(accountName)) return null;

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string characterId = $"{accountName}/{characterName}_{timestamp}";

        var metadata = new CharacterMetadata
        {
            characterName = characterName,
            accountName = accountName,
            characterId = characterId,
            creationTimestamp = timestamp,
            lastSaved = timestamp,
            totalPlayTime = 0f,
            gameVersion = Application.version,
            level = 1
        };

        bool success = await provider.Save(characterId, "metadata", JsonUtility.ToJson(metadata, prettyPrint: true));

        if (!success)
        {
            Debug.LogError($"[{ManagerName}] Failed to create character '{characterName}'");
            return null;
        }

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Created character: {characterId}");

        return characterId;
    }

    /// <summary>
    /// Create a new character from the creation panel's data object.
    /// Writes metadata + stats seed atomically so the character loads correctly
    /// on first login without requiring a live player brain.
    /// </summary>
    public async Task<string> CreateCharacter(CharacterCreationData data)
    {
        string characterId = await CreateCharacter(data.characterName, data.accountName);
        if (string.IsNullOrEmpty(characterId)) return null;

        var statValues = new System.Collections.Generic.Dictionary<string, float>
        {
            { "character.mind",       data.mind       },
            { "character.body",       data.body       },
            { "character.spirit",     data.spirit     },
            { "character.resilience", data.resilience },
            { "character.endurance",  data.endurance  },
            { "character.insight",    data.insight    },
        };

        var sb = new System.Text.StringBuilder("{\"statValues\":{");
        bool first = true;
        foreach (var kvp in statValues)
        {
            if (!first) sb.Append(',');
            sb.Append($"\"{kvp.Key}\":{kvp.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            first = false;
        }
        sb.Append("}}");

        await provider.Save(characterId, "stats", sb.ToString());

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Wrote stat seed for {characterId}");

        return characterId;
    }

    /// <summary>
    /// Return metadata for all available characters (open testing policy: all accounts).
    /// Used by the character select screen.
    /// </summary>
    public async Task<List<CharacterMetadata>> GetAllCharacters()
    {
        var result = new List<CharacterMetadata>();

        if (provider == null) return result;

        string[] characterIds = await provider.GetCharacters(null);

        foreach (string characterId in characterIds)
        {
            string json = await provider.Load(characterId, "metadata");
            if (string.IsNullOrEmpty(json)) continue;

            var metadata = JsonUtility.FromJson<CharacterMetadata>(json);
            if (metadata != null)
                result.Add(metadata);
        }

        if (debugLogging)
            Debug.Log($"[{ManagerName}] GetAllCharacters — found {result.Count}");

        return result;
    }

    #endregion

    #region Load

    /// <summary>
    /// Load all ISaveable modules for the given character in dependency order.
    /// Fires GameEvents.OnLoadCompleted when done.
    /// </summary>
    public async Task LoadCharacter(string characterId)
    {
        if (string.IsNullOrEmpty(characterId)) return;
        if (provider == null) return;

        ActiveCharacterId = characterId;
        autoSaveTimer = autoSaveInterval;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Loading character: {characterId}");

        if (playerBrain == null)
        {
            if (debugLogging)
                Debug.Log($"[{ManagerName}] Player brain not yet registered — load deferred until brain registers.");

            pendingLoad = true;
            return;
        }

        pendingLoad = false;
        await LoadModulesInOrder(characterId);

        SubscribeToSaveableEvents();

        GameEvents.LoadCompleted();

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Load complete for {characterId}");
    }

    private async Task LoadModulesInOrder(string characterId)
    {
        // Build a lookup of all ISaveable modules on the player brain
        var saveables = BuildSaveableLookup();

        // Load in enforced dependency order first
        foreach (string saveId in LoadOrder)
        {
            if (!saveables.TryGetValue(saveId, out ISaveable module)) continue;

            string json = await provider.Load(characterId, saveId);
            if (string.IsNullOrEmpty(json))
            {
                if (debugLogging)
                    Debug.Log($"[{ManagerName}] No save file for '{saveId}' — using defaults");
                continue;
            }

            try
            {
                module.LoadSaveData(json);

                if (debugLogging)
                    Debug.Log($"[{ManagerName}] Loaded '{saveId}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{ManagerName}] Failed to load '{saveId}': {e.Message}");
            }
        }

        // Load any remaining modules not in the explicit order
        foreach (var kvp in saveables)
        {
            if (System.Array.IndexOf(LoadOrder, kvp.Key) >= 0) continue;

            string json = await provider.Load(characterId, kvp.Key);
            if (string.IsNullOrEmpty(json)) continue;

            try
            {
                kvp.Value.LoadSaveData(json);

                if (debugLogging)
                    Debug.Log($"[{ManagerName}] Loaded '{kvp.Key}'");
            }
            catch (Exception e)
            {
                Debug.LogError($"[{ManagerName}] Failed to load '{kvp.Key}': {e.Message}");
            }
        }
    }

    #endregion

    #region Save

    /// <summary>
    /// Save all ISaveable modules for the active character.
    /// Also updates metadata.json (mirrors level for CharacterSelect display).
    /// </summary>
    public async Task SaveAll()
    {
        if (!HasActiveCharacter) return;
        if (provider == null) return;
        if (playerBrain == null) return;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] SaveAll for {ActiveCharacterId}");

        var saveables = BuildSaveableLookup();

        foreach (var kvp in saveables)
        {
            await SaveModule(kvp.Key, kvp.Value);
        }

        await WriteMetadata();
    }

    /// <summary>
    /// Save a single ISaveable module by its saveId key.
    /// Used for targeted saves (e.g. equipment changed, stats allocated).
    /// </summary>
    public async Task SaveFile(string saveId, string json)
    {
        if (!HasActiveCharacter) return;
        if (provider == null) return;
        if (string.IsNullOrEmpty(saveId)) return;
        if (string.IsNullOrEmpty(json)) return;

        bool success = await provider.Save(ActiveCharacterId, saveId, json);

        if (debugLogging)
            Debug.Log($"[{ManagerName}] SaveFile '{saveId}': {(success ? "OK" : "FAILED")}");
    }

    private async Task SaveModule(string saveId, ISaveable module)
    {
        string json;

        try
        {
            json = module.GetSaveData();
        }
        catch (Exception e)
        {
            Debug.LogError($"[{ManagerName}] GetSaveData failed for '{saveId}': {e.Message}");
            return;
        }

        if (string.IsNullOrEmpty(json)) return;

        bool success = await provider.Save(ActiveCharacterId, saveId, json);

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Saved '{saveId}': {(success ? "OK" : "FAILED")}");
    }

    private async Task WriteMetadata()
    {
        // Read existing metadata to preserve creation timestamp and play time
        string existingJson = await provider.Load(ActiveCharacterId, "metadata");

        CharacterMetadata metadata = string.IsNullOrEmpty(existingJson)
            ? new CharacterMetadata()
            : JsonUtility.FromJson<CharacterMetadata>(existingJson);

        metadata.lastSaved = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        metadata.gameVersion = Application.version;

        // Mirror level from RPGSystem if present (for CharacterSelect display)
        var rpgSystem = playerBrain?.GetModule<RPGSystem>();
        if (rpgSystem != null)
            metadata.level = rpgSystem.CurrentLevel;

        await provider.Save(ActiveCharacterId, "metadata", JsonUtility.ToJson(metadata, prettyPrint: true));
    }

    #endregion

    #region GameEvent Handlers

    private void HandleCharacterSelected(string characterId)
    {
        ActiveCharacterId = characterId;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Character selected: {characterId}");

        // Actual load is triggered once the game scene fires OnGameSceneReady
    }

    private void HandleGameSceneReady()
    {
        if (!HasActiveCharacter) return;

        if (debugLogging)
            Debug.Log($"[{ManagerName}] Game scene ready — loading character {ActiveCharacterId}");

        _ = LoadCharacter(ActiveCharacterId);
    }

    private void HandleSaveRequested()
    {
        _ = SaveAll();
    }

    private void HandleTargetedSaveRequested(string saveId)
    {
        if (!HasActiveCharacter) return;
        if (playerBrain == null) return;

        var saveables = BuildSaveableLookup();
        if (!saveables.TryGetValue(saveId, out ISaveable module)) return;

        _ = SaveModule(saveId, module);
    }

    private void HandleApplicationExiting()
    {
        _ = SaveAll();
    }

    private void HandleApplicationQuitting()
    {
        _ = SaveAll();
    }

    private void HandleInventoryChanged()
    {
        if (!HasActiveCharacter || playerBrain == null) return;

        var inventory = playerBrain.GetModule<InventorySystem>();
        if (inventory == null) return;

        _ = SaveModule("inventory", inventory);
    }

    private void HandleEquipmentChanged(EquipmentSlotDefinition slot, ItemInstance item)
    {
        if (!HasActiveCharacter || playerBrain == null) return;

        var equipment = playerBrain.GetModule<EquipmentSystem>();
        if (equipment == null) return;

        _ = SaveModule("equipment", equipment);
    }

    #endregion

    #region Helpers

    private Dictionary<string, ISaveable> BuildSaveableLookup()
    {
        var lookup = new Dictionary<string, ISaveable>();

        if (playerBrain == null) return lookup;

        var saveables = playerBrain.GetComponentsInChildren<ISaveable>();
        foreach (var saveable in saveables)
        {
            if (saveable == null) continue;

            string id = saveable.GetSaveId();
            if (string.IsNullOrEmpty(id)) continue;

            // First registration wins (no duplicates)
            if (!lookup.ContainsKey(id))
                lookup[id] = saveable;
        }

        return lookup;
    }

    #endregion

    #region Debug

    [ContextMenu("Debug: Print State")]
    private void DebugPrintState()
    {
        Debug.Log($"=== SAVE MANAGER ===\n" +
                  $"ActiveCharacterId: {ActiveCharacterId}\n" +
                  $"HasActiveCharacter: {HasActiveCharacter}\n" +
                  $"PlayerBrain: {(playerBrain != null ? playerBrain.name : "null")}\n" +
                  $"Provider: {provider?.GetType().Name ?? "null"}");
    }

    [ContextMenu("Debug: Force SaveAll")]
    private void DebugForceSaveAll()
    {
        if (!Application.isPlaying) return;
        _ = SaveAll();
    }

    #endregion
}

// ── Data Structures ───────────────────────────────────────────────────────────

/// <summary>
/// Persisted character header — stored in metadata.json.
/// Read by CharacterSelectScreen without loading inventory/stats.
/// </summary>
[Serializable]
public class CharacterMetadata
{
    public string characterId;
    public string characterName;
    public string accountName;
    public long creationTimestamp;
    public long lastSaved;
    public float totalPlayTime;
    public string gameVersion;
    public int level;           // Mirrored from RPGSystem at save time
}