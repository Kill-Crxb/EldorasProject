using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

//==========================================
// SAVE SYSTEM INTERFACES
//==========================================

public interface ISaveable
{
    string GetSaveId(); // Unique identifier for this component's save data
    string GetSaveData(); // JSON serialized data
    void LoadSaveData(string json); // Apply loaded data
    int GetSaveVersion(); // For future compatibility
}

//==========================================
// SUPPORTING DATA STRUCTURES
//==========================================

[System.Serializable]
public class SaveMetadata
{
    public string playerName;
    public int slotNumber;
    public DateTime saveTime;
    public string gameVersion;
    public int moduleCount;
    public float totalPlayTime;
}

[System.Serializable]
public class ConsolidatedSaveData
{
    public SaveMetadata metadata;
    public Dictionary<string, string> moduleData = new Dictionary<string, string>();
}

//==========================================
// SAVE SYSTEM MODULE (IPlayerModule)
//==========================================

public class SaveSystemModule : MonoBehaviour, IPlayerModule
{
    [Header("Save Configuration")]
    [SerializeField] private bool autoSaveEnabled = true;
    [SerializeField] private float autoSaveInterval = 300f; // 5 minutes
    [SerializeField] private int maxSaveSlots = 10;
    [SerializeField] private bool useModularFiles = true; // Individual JSON files per module

    [Header("Debug")]
    [SerializeField] private bool debugSaving = false;

    // Module system integration
    private ControllerBrain brain;
    private float autoSaveTimer;
    private string playerSaveDirectory;
    private string worldSaveDirectory;

    // Discovered saveable modules
    private List<ISaveable> saveableModules = new List<ISaveable>();

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
        SetupSaveDirectories();
        DiscoverSaveableModules();
        autoSaveTimer = autoSaveInterval;

        if (debugSaving)
        {
            Debug.Log($"SaveSystemModule initialized with {saveableModules.Count} saveable modules");
        }
    }

    public void UpdateModule()
    {
        if (!IsEnabled) return;

        if (autoSaveEnabled)
        {
            autoSaveTimer -= Time.deltaTime;
            if (autoSaveTimer <= 0)
            {
                AutoSave();
                autoSaveTimer = autoSaveInterval;
            }
        }
    }

    private void SetupSaveDirectories()
    {
        string baseDir = Application.persistentDataPath;
        playerSaveDirectory = Path.Combine(baseDir, "PlayerSaves");
        worldSaveDirectory = Path.Combine(baseDir, "WorldSaves");

        Directory.CreateDirectory(playerSaveDirectory);
        Directory.CreateDirectory(worldSaveDirectory);
    }

    private void DiscoverSaveableModules()
    {
        saveableModules.Clear();

        // Find all ISaveable components under the brain
        var saveables = brain.GetComponentsInChildren<ISaveable>();
        saveableModules.AddRange(saveables);

        // Also check modules directly accessible through Brain
        if (brain.Controller is ISaveable controllerSaveable)
            saveableModules.Add(controllerSaveable);

        if (brain.RPGCoreStats is ISaveable coreStatsSaveable)
            saveableModules.Add(coreStatsSaveable);

        if (brain.RPGSecondaryStats is ISaveable secondaryStatsSaveable)
            saveableModules.Add(secondaryStatsSaveable);

        if (brain.RPGResources is ISaveable resourcesSaveable)
            saveableModules.Add(resourcesSaveable);

        // NEW: Check for unified PlayerItemsModule (replaces old inventory systems)
        var playerItemsModule = brain.GetModule<PlayerItemsModule>();
        if (playerItemsModule is ISaveable playerItemsSaveable)
        {
            saveableModules.Add(playerItemsSaveable);
            if (debugSaving)
                Debug.Log("Found PlayerItemsModule with save support");
        }

        // Remove duplicates
        saveableModules = saveableModules.Distinct().ToList();

        if (debugSaving)
        {
            foreach (var saveable in saveableModules)
            {
                Debug.Log($"Discovered saveable module: {saveable.GetSaveId()}");
            }
        }
    }

    //==========================================
    // PLAYER DATA SAVE/LOAD
    //==========================================

    public void SavePlayerData(int slotNumber)
    {
        try
        {
            if (useModularFiles)
            {
                SavePlayerDataModular(slotNumber);
            }
            else
            {
                SavePlayerDataMonolithic(slotNumber);
            }

            if (debugSaving)
                Debug.Log($"Player data saved to slot {slotNumber}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save player data: {e.Message}");
        }
    }

    public bool LoadPlayerData(int slotNumber)
    {
        try
        {
            bool success;
            if (useModularFiles)
            {
                success = LoadPlayerDataModular(slotNumber);
            }
            else
            {
                success = LoadPlayerDataMonolithic(slotNumber);
            }

            if (success && debugSaving)
                Debug.Log($"Player data loaded from slot {slotNumber}");

            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load player data: {e.Message}");
            return false;
        }
    }

    private void SavePlayerDataModular(int slotNumber)
    {
        string slotDirectory = Path.Combine(playerSaveDirectory, $"Slot_{slotNumber}");
        Directory.CreateDirectory(slotDirectory);

        // Save metadata
        var metadata = new SaveMetadata
        {
            playerName = "Player", // TODO: Get from player system
            slotNumber = slotNumber,
            saveTime = DateTime.Now,
            gameVersion = Application.version,
            moduleCount = saveableModules.Count
        };

        string metadataPath = Path.Combine(slotDirectory, "metadata.json");
        File.WriteAllText(metadataPath, JsonUtility.ToJson(metadata, true));

        // Save each module to its own file
        foreach (var saveable in saveableModules)
        {
            if (saveable == null) continue;

            string moduleData = saveable.GetSaveData();
            if (!string.IsNullOrEmpty(moduleData))
            {
                string fileName = $"{saveable.GetSaveId()}.json";
                string filePath = Path.Combine(slotDirectory, fileName);
                File.WriteAllText(filePath, moduleData);
            }
        }
    }

    private bool LoadPlayerDataModular(int slotNumber)
    {
        string slotDirectory = Path.Combine(playerSaveDirectory, $"Slot_{slotNumber}");

        if (!Directory.Exists(slotDirectory))
        {
            Debug.LogWarning($"No save data found for slot {slotNumber}");
            return false;
        }

        // Load metadata
        string metadataPath = Path.Combine(slotDirectory, "metadata.json");
        if (File.Exists(metadataPath))
        {
            string metadataJson = File.ReadAllText(metadataPath);
            var metadata = JsonUtility.FromJson<SaveMetadata>(metadataJson);

            if (debugSaving)
            {
                Debug.Log($"Loading save from {metadata.saveTime}, version {metadata.gameVersion}");
            }
        }

        // UPDATED: Load order for streamlined architecture
        // Core stats first, then unified player items (which handles both equipment and inventory), then resources
        var loadOrder = new string[]
        {
            "RPGCoreStats",          // Foundation stats
            "PlayerItems",           // NEW: Unified player items (equipment + inventory)
            "RPGResources",          // Resource maximums (depends on equipment stats)
        };

        bool anyLoaded = false;

        // Load in specific order first
        foreach (string saveId in loadOrder)
        {
            var saveable = saveableModules.FirstOrDefault(s => s?.GetSaveId() == saveId);
            if (saveable != null)
            {
                string filePath = Path.Combine(slotDirectory, $"{saveId}.json");
                if (File.Exists(filePath))
                {
                    string moduleData = File.ReadAllText(filePath);
                    saveable.LoadSaveData(moduleData);
                    anyLoaded = true;

                    if (debugSaving)
                        Debug.Log($"Loaded {saveId} from save file");
                }
            }
        }

        // Load any remaining modules not in the ordered list
        foreach (var saveable in saveableModules)
        {
            if (saveable == null) continue;

            string saveId = saveable.GetSaveId();
            if (System.Array.IndexOf(loadOrder, saveId) >= 0) continue; // Already loaded

            string filePath = Path.Combine(slotDirectory, $"{saveId}.json");
            if (File.Exists(filePath))
            {
                string moduleData = File.ReadAllText(filePath);
                saveable.LoadSaveData(moduleData);
                anyLoaded = true;

                if (debugSaving)
                    Debug.Log($"Loaded additional module: {saveId}");
            }
            else if (debugSaving)
            {
                Debug.LogWarning($"No save data found for module: {saveId}");
            }
        }

        return anyLoaded;
    }

    private void SavePlayerDataMonolithic(int slotNumber)
    {
        var consolidatedData = new ConsolidatedSaveData();

        // Collect all module data
        foreach (var saveable in saveableModules)
        {
            if (saveable == null) continue;

            string moduleData = saveable.GetSaveData();
            if (!string.IsNullOrEmpty(moduleData))
            {
                consolidatedData.moduleData[saveable.GetSaveId()] = moduleData;
            }
        }

        // Add metadata
        consolidatedData.metadata = new SaveMetadata
        {
            playerName = "Player",
            slotNumber = slotNumber,
            saveTime = DateTime.Now,
            gameVersion = Application.version,
            moduleCount = consolidatedData.moduleData.Count
        };

        string json = JsonUtility.ToJson(consolidatedData, true);
        string filePath = Path.Combine(playerSaveDirectory, $"Player_Slot_{slotNumber}.json");
        File.WriteAllText(filePath, json);
    }

    private bool LoadPlayerDataMonolithic(int slotNumber)
    {
        string filePath = Path.Combine(playerSaveDirectory, $"Player_Slot_{slotNumber}.json");

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"No save file found for slot {slotNumber}");
            return false;
        }

        string json = File.ReadAllText(filePath);
        var consolidatedData = JsonUtility.FromJson<ConsolidatedSaveData>(json);

        if (consolidatedData?.moduleData == null)
        {
            Debug.LogError("Invalid save data format");
            return false;
        }

        // UPDATED: Apply data in dependency order for streamlined architecture
        var loadOrder = new string[]
        {
            "RPGCoreStats",          // Foundation stats
            "PlayerItems",           // NEW: Unified player items
            "RPGResources",          // Resource maximums
        };

        bool anyLoaded = false;

        // Load in specific order first
        foreach (string saveId in loadOrder)
        {
            var saveable = saveableModules.FirstOrDefault(s => s?.GetSaveId() == saveId);
            if (saveable != null && consolidatedData.moduleData.ContainsKey(saveId))
            {
                saveable.LoadSaveData(consolidatedData.moduleData[saveId]);
                anyLoaded = true;

                if (debugSaving)
                    Debug.Log($"Loaded {saveId} from monolithic save");
            }
        }

        // Load any remaining modules
        foreach (var saveable in saveableModules)
        {
            if (saveable == null) continue;

            string saveId = saveable.GetSaveId();
            if (System.Array.IndexOf(loadOrder, saveId) >= 0) continue; // Already loaded

            if (consolidatedData.moduleData.ContainsKey(saveId))
            {
                saveable.LoadSaveData(consolidatedData.moduleData[saveId]);
                anyLoaded = true;

                if (debugSaving)
                    Debug.Log($"Loaded additional module: {saveId}");
            }
        }

        return anyLoaded;
    }

    //==========================================
    // UTILITY METHODS
    //==========================================

    private void AutoSave()
    {
        if (debugSaving)
            Debug.Log("Auto-saving player data...");

        SavePlayerData(0); // Auto-save to slot 0
    }

    public bool DoesSaveExist(int slotNumber)
    {
        if (useModularFiles)
        {
            string slotDirectory = Path.Combine(playerSaveDirectory, $"Slot_{slotNumber}");
            return Directory.Exists(slotDirectory) && File.Exists(Path.Combine(slotDirectory, "metadata.json"));
        }
        else
        {
            string filePath = Path.Combine(playerSaveDirectory, $"Player_Slot_{slotNumber}.json");
            return File.Exists(filePath);
        }
    }

    public SaveMetadata GetSaveMetadata(int slotNumber)
    {
        try
        {
            if (useModularFiles)
            {
                string metadataPath = Path.Combine(playerSaveDirectory, $"Slot_{slotNumber}", "metadata.json");
                if (File.Exists(metadataPath))
                {
                    string json = File.ReadAllText(metadataPath);
                    return JsonUtility.FromJson<SaveMetadata>(json);
                }
            }
            else
            {
                string filePath = Path.Combine(playerSaveDirectory, $"Player_Slot_{slotNumber}.json");
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var consolidatedData = JsonUtility.FromJson<ConsolidatedSaveData>(json);
                    return consolidatedData?.metadata;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to read save metadata: {e.Message}");
        }

        return null;
    }

    //==========================================
    // CLEANUP UTILITY METHODS
    //==========================================

    [ContextMenu("Clear All Save Data")]
    public void ClearAllSaveData()
    {
        try
        {
            if (Directory.Exists(playerSaveDirectory))
            {
                Directory.Delete(playerSaveDirectory, true);
                Directory.CreateDirectory(playerSaveDirectory);
                Debug.Log("All save data cleared");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to clear save data: {e.Message}");
        }
    }

    [ContextMenu("Show Save Directory")]
    public void ShowSaveDirectory()
    {
        if (Directory.Exists(playerSaveDirectory))
        {
            Debug.Log($"Save directory: {playerSaveDirectory}");

#if UNITY_EDITOR_WIN
            System.Diagnostics.Process.Start("explorer.exe", playerSaveDirectory.Replace('/', '\\'));
#elif UNITY_EDITOR_OSX
            System.Diagnostics.Process.Start("open", playerSaveDirectory);
#endif
        }
    }

    //==========================================
    // PUBLIC API
    //==========================================

    [ContextMenu("Save Game (Slot 1)")]
    public void QuickSave() => SavePlayerData(1);

    [ContextMenu("Load Game (Slot 1)")]
    public void QuickLoad() => LoadPlayerData(1);

    [ContextMenu("Refresh Saveable Modules")]
    public void RefreshSaveableModules() => DiscoverSaveableModules();

    [ContextMenu("Debug Print Saveable Modules")]
    public void DebugPrintSaveableModules()
    {
        Debug.Log("=== SAVEABLE MODULES DEBUG ===");
        foreach (var saveable in saveableModules)
        {
            if (saveable != null)
            {
                Debug.Log($"Module: {saveable.GetSaveId()} (Version: {saveable.GetSaveVersion()})");
            }
        }
        Debug.Log($"Total modules: {saveableModules.Count}");
    }

    public void RegisterSaveableModule(ISaveable saveable)
    {
        if (!saveableModules.Contains(saveable))
        {
            saveableModules.Add(saveable);
        }
    }

    public void UnregisterSaveableModule(ISaveable saveable)
    {
        saveableModules.Remove(saveable);
    }
}