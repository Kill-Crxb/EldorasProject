using UnityEngine;
using System.Collections.Generic;

namespace CrabSystem
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private List<Transform> spawnPoints = new List<Transform>();

        private SaveManager saveManager;
        private ControllerBrain spawnedPlayerBrain;

        private void Awake()
        {
            saveManager = ManagerBrain.Instance?.GetManager<SaveManager>();
            if (saveManager == null)
            {
                Debug.LogError("[PlayerSpawner] SaveManager not found in Awake");
                return;
            }

            GameEvents.OnGameSceneReady += HandleGameSceneReady;
        }

        private void OnDestroy()
        {
            GameEvents.OnGameSceneReady -= HandleGameSceneReady;
        }

        private void HandleGameSceneReady()
        {
            if (!saveManager.HasActiveCharacter)
            {
                Debug.LogWarning("[PlayerSpawner] No active character to spawn");
                return;
            }

            SpawnPlayer();
        }

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

            spawnedPlayerBrain = spawnedObject.GetComponent<ControllerBrain>();

            if (spawnedPlayerBrain == null)
            {
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

            if (!spawnedPlayerBrain.enabled)
            {
                Debug.LogWarning("[PlayerSpawner] ControllerBrain is disabled!");
                spawnedPlayerBrain.enabled = true;
            }

            saveManager.SetPlayerBrain(spawnedPlayerBrain);
        }

        public void AddSpawnPoint(Transform spawnPoint)
        {
            if (spawnPoint != null && !spawnPoints.Contains(spawnPoint))
                spawnPoints.Add(spawnPoint);
        }
    }
}
