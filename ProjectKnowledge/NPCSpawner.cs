using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    #region Inspector

    [Header("Spawn Configuration")]
    [SerializeField] private NPCSpawnConfig spawnConfig;

    [SerializeField] private Transform spawnPoint;

    [Header("Spawn Settings")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private int spawnCount = 1;
    [SerializeField] private float spawnDelay = 0.5f;

    [Header("Level Override")]
    [SerializeField] private int spawnLevel = -1;

    [Header("Advanced")]
    [SerializeField] private float spawnRadius = 0f;
    [SerializeField] private bool parentToSpawner = false;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    #endregion

    #region Private Fields

    private int currentSpawnCount = 0;

    #endregion

    #region Lifecycle

    private void Start()
    {
        if (spawnOnStart)
            SpawnNPCs();
    }

    #endregion

    #region Public API

    public void SpawnNPCs()
    {
        if (spawnCount == 1)
        {
            SpawnSingleNPC();
        }
        else
        {
            StartCoroutine(SpawnMultipleNPCs());
        }
    }

    public GameObject SpawnSingleNPC()
    {
        if (!ValidateSpawnConfig())
            return null;

        var config = spawnConfig.useRandomVariant
            ? spawnConfig.GetRandomVariant()
            : spawnConfig;

        if (config == null)
            return null;

        Vector3 position = GetSpawnPosition();
        Quaternion rotation = GetSpawnRotation();

        var spawnedNPC = Instantiate(config.prefab, position, rotation);

        if (parentToSpawner)
            spawnedNPC.transform.SetParent(transform);

        if (!ConfigureSpawnedNPC(spawnedNPC, config))
        {
            Destroy(spawnedNPC);
            return null;
        }

        currentSpawnCount++;

        if (debugMode)
        {
            string levelInfo = spawnLevel > 0 ? $" at level {spawnLevel}" : $" (base level {config.archetype.baseLevel})";
            Debug.Log($"[NPCSpawner] Spawned {config.displayName}{levelInfo} at {position} (Total: {currentSpawnCount})");
        }

        return spawnedNPC;
    }

    public void ResetSpawnCount()
    {
        currentSpawnCount = 0;
    }

    [ContextMenu("Spawn NPC Now")]
    public void SpawnNow()
    {
        SpawnSingleNPC();
    }

    #endregion

    #region Spawning

    private System.Collections.IEnumerator SpawnMultipleNPCs()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnSingleNPC();

            if (i < spawnCount - 1 && spawnDelay > 0)
                yield return new WaitForSeconds(spawnDelay);
        }
    }

    private bool ConfigureSpawnedNPC(GameObject spawnedNPC, NPCSpawnConfig config)
    {
        var brain = spawnedNPC.GetComponent<ControllerBrain>();
        if (brain == null)
            brain = spawnedNPC.GetComponentInChildren<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError($"[NPCSpawner] No ControllerBrain found on '{config.prefab.name}' or its children");
            return false;
        }

        int effectiveLevel = spawnLevel > 0 ? spawnLevel : config.spawnLevel;

        var configData = new CharacterConfigData(
            displayName: config.archetype.archetypeName,
            factionId: config.archetype.factionId,
            modelId: config.archetype.modelPool?.Count > 0 ? config.archetype.modelPool[0] : null,
            baseStatOverrides: config.archetype.baseStatOverrides,
            level: effectiveLevel
        );

        configData.characterId = spawnedNPC.name;

        if (brain.IsInitialized)
        {
            GameEvents.CharacterConfigDataReady(configData);

            if (debugMode)
                Debug.Log($"[NPCSpawner] Configured {spawnedNPC.name} with archetype '{config.archetype.archetypeName}' (immediate)");
        }
        else
        {
            brain.OnInitialized += (b) =>
            {
                GameEvents.CharacterConfigDataReady(configData);

                if (debugMode)
                    Debug.Log($"[NPCSpawner] Configured {spawnedNPC.name} with archetype '{config.archetype.archetypeName}' (deferred)");
            };
        }

        return true;
    }

    private Vector3 GetSpawnPosition()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        Vector3 basePosition = spawnTransform.position;

        if (spawnRadius > 0f)
        {
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        return basePosition;
    }

    private Quaternion GetSpawnRotation()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        return spawnTransform.rotation;
    }

    private bool ValidateSpawnConfig()
    {
        if (spawnConfig == null)
        {
            Debug.LogError("[NPCSpawner] Spawn Config not assigned");
            return false;
        }

        if (!spawnConfig.IsValid())
        {
            Debug.LogError($"[NPCSpawner] Spawn Config '{spawnConfig.name}' is invalid");
            return false;
        }

        return true;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (spawnPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);

        if (spawnRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPoint.position, spawnRadius);
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(spawnPoint.position, spawnPoint.position + spawnPoint.forward * 2f);
    }

    private void OnDrawGizmos()
    {
        if (spawnPoint == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);

        if (spawnRadius > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPoint.position, spawnRadius);
        }
    }

    #endregion
}