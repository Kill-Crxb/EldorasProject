using UnityEngine;
using System.Collections.Generic;

public class PlayerSpawner : MonoBehaviour
{
    #region Inspector

    [Header("Spawn Configuration")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region Private Fields

    private SaveManager saveManager;
    private ControllerBrain spawnedPlayerBrain;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        Debug.Log("[PlayerSpawner] Awake");

        saveManager = ManagerBrain.Instance?.GetManager<SaveManager>();
        if (saveManager == null)
        {
            Debug.LogError("[PlayerSpawner] SaveManager not found in Awake");
            return;
        }

        Debug.Log("[PlayerSpawner] SaveManager found, subscribing to OnGameSceneReady in Awake");
        GameEvents.OnGameSceneReady += HandleGameSceneReady;
    }

    private void OnDestroy()
    {
        GameEvents.OnGameSceneReady -= HandleGameSceneReady;
    }

    #endregion

    #region Event Handlers

    private void HandleGameSceneReady()
    {
        Debug.Log("[PlayerSpawner] OnGameSceneReady fired");

        if (!saveManager.HasActiveCharacter)
        {
            Debug.LogWarning("[PlayerSpawner] No active character to spawn");
            return;
        }

        Debug.Log($"[PlayerSpawner] Active character: {saveManager.ActiveCharacterId}");
        SpawnPlayer();
    }

    #endregion

    #region Spawning

    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] Player prefab not assigned");
            return;
        }

        Transform spawnPoint = null;
        if (spawnPoints.Count > 0)
        {
            spawnPoint = spawnPoints[0];
        }

        var spawnedObject = spawnPoint != null
            ? Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation)
            : Instantiate(playerPrefab);

        Debug.Log($"[PlayerSpawner] Spawned object: {spawnedObject.name}, active: {spawnedObject.activeInHierarchy}");

        spawnedPlayerBrain = spawnedObject.GetComponent<ControllerBrain>();

        if (spawnedPlayerBrain == null)
        {
            Debug.Log("[PlayerSpawner] Not on root, searching children...");
            spawnedPlayerBrain = spawnedObject.GetComponentInChildren<ControllerBrain>();
        }

        if (spawnedPlayerBrain == null)
        {
            var rootComponents = spawnedObject.GetComponents<Component>();
            Debug.LogError($"[PlayerSpawner] ControllerBrain not found. Root components: {string.Join(", ", System.Array.ConvertAll(rootComponents, c => c?.GetType().Name ?? "null"))}");

            var allComponents = spawnedObject.GetComponentsInChildren<Component>();
            Debug.LogError($"[PlayerSpawner] All components in hierarchy: {string.Join(", ", System.Array.ConvertAll(allComponents, c => c?.GetType().Name ?? "null"))}");

            return;
        }

        Debug.Log($"[PlayerSpawner] Found ControllerBrain at {spawnedPlayerBrain.gameObject.name}, enabled: {spawnedPlayerBrain.enabled}");

        if (!spawnedPlayerBrain.enabled)
        {
            Debug.LogWarning("[PlayerSpawner] ControllerBrain is disabled!");
            spawnedPlayerBrain.enabled = true;
        }

        saveManager.SetPlayerBrain(spawnedPlayerBrain);

        ApplyPlayerConfigDirectly();
    }

    #endregion

    #region Configuration

    private async void ApplyPlayerConfigDirectly()
    {
        await System.Threading.Tasks.Task.Delay(100);

        if (saveManager == null || string.IsNullOrEmpty(saveManager.ActiveCharacterId))
        {
            Debug.LogWarning("[PlayerSpawner] No active character to config");
            return;
        }

        try
        {
            var provider = saveManager.GetProvider();
            if (provider == null)
            {
                Debug.LogError("[PlayerSpawner] SaveProvider not available");
                return;
            }

            string configJson = await provider.Load(saveManager.ActiveCharacterId, "config");
            if (string.IsNullOrEmpty(configJson))
            {
                Debug.LogWarning("[PlayerSpawner] No config file found");
                return;
            }

            var configData = JsonUtility.FromJson<CharacterConfigData>(configJson);
            if (configData != null)
            {
                configData.characterId = spawnedPlayerBrain.Identity?.EntityId ?? saveManager.ActiveCharacterId;
                GameEvents.CharacterConfigDataReady(configData);

                Debug.Log($"[PlayerSpawner] Applied config: {configData.displayName}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerSpawner] Failed to apply config: {ex.Message}");
        }
    }

    #endregion

    #region Public API

    public void AddSpawnPoint(Transform spawnPoint)
    {
        if (spawnPoint != null && !spawnPoints.Contains(spawnPoint))
            spawnPoints.Add(spawnPoint);
    }

    #endregion
}