using UnityEngine;

/// <summary>
/// Spawns NPCs using the archetype system.
/// Attach to any GameObject to create a spawn point.
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("Spawn Configuration")]
    [Tooltip("The Enemy_Universal prefab to spawn")]
    [SerializeField] private GameObject enemyPrefab;

    [Tooltip("The archetype to apply to spawned enemies")]
    [SerializeField] private NPCArchetype npcArchetype;

    [Tooltip("Where to spawn the enemy (leave null to use this object's position)")]
    [SerializeField] private Transform spawnPoint;

    [Header("Spawn Settings")]
    [Tooltip("Spawn immediately on Start()")]
    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("Number of enemies to spawn")]
    [SerializeField] private int spawnCount = 1;

    [Tooltip("Delay between each spawn (in seconds)")]
    [SerializeField] private float spawnDelay = 0.5f;

    [Header("Advanced Settings")]
    [Tooltip("Random offset range for spawn position (0 = exact position)")]
    [SerializeField] private float spawnRadius = 0f;

    [Tooltip("Parent spawned enemies to this object")]
    [SerializeField] private bool parentToSpawner = false;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    private int currentSpawnCount = 0;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnEnemies();
        }
    }

    /// <summary>
    /// Spawn all configured enemies
    /// </summary>
    public void SpawnEnemies()
    {
        if (spawnCount == 1)
        {
            SpawnSingleEnemy();
        }
        else
        {
            StartCoroutine(SpawnMultipleEnemies());
        }
    }

    /// <summary>
    /// Spawn a single enemy immediately
    /// </summary>
    public GameObject SpawnSingleEnemy()
    {
        if (!ValidateSpawnSettings())
            return null;

        Vector3 position = GetSpawnPosition();
        Quaternion rotation = GetSpawnRotation();

        GameObject spawnedEnemy = Instantiate(enemyPrefab, position, rotation);

        if (parentToSpawner)
        {
            spawnedEnemy.transform.SetParent(transform);
        }

        // Apply archetype via NPCConfigurationHandler
        ApplyArchetype(spawnedEnemy);

        currentSpawnCount++;

        if (debugMode)
        {
            Debug.Log($"[NPCSpawner] Spawned {npcArchetype.archetypeName} at {position} (Total: {currentSpawnCount})");
        }

        return spawnedEnemy;
    }

    /// <summary>
    /// Spawn multiple enemies with delay
    /// </summary>
    private System.Collections.IEnumerator SpawnMultipleEnemies()
    {
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnSingleEnemy();

            if (i < spawnCount - 1 && spawnDelay > 0)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }
    }

    /// <summary>
    /// Apply archetype to spawned enemy
    /// </summary>
    private void ApplyArchetype(GameObject spawnedEnemy)
    {
        // Find the NPCConfigurationHandler on the spawned enemy
        var configHandler = spawnedEnemy.GetComponentInChildren<NPCConfigurationHandler>();

        if (configHandler == null)
        {
            Debug.LogError($"[NPCSpawner] No NPCConfigurationHandler found on spawned enemy! Make sure Enemy_Universal prefab has NPCConfigurationHandler component.");
            return;
        }

        // Set the archetype
        configHandler.SetCustomArchetype(npcArchetype);

        if (debugMode)
        {
            Debug.Log($"[NPCSpawner] Applied archetype: {npcArchetype.archetypeName}");
        }
    }

    /// <summary>
    /// Get spawn position with optional random offset
    /// </summary>
    private Vector3 GetSpawnPosition()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        Vector3 basePosition = spawnTransform.position;

        if (spawnRadius > 0f)
        {
            // Random position within radius
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            basePosition += new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        return basePosition;
    }

    /// <summary>
    /// Get spawn rotation (uses spawn point or spawner's rotation)
    /// </summary>
    private Quaternion GetSpawnRotation()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;
        return spawnTransform.rotation;
    }

    /// <summary>
    /// Validate spawn settings
    /// </summary>
    private bool ValidateSpawnSettings()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("[NPCSpawner] Enemy Prefab is not assigned!");
            return false;
        }

        if (npcArchetype == null)
        {
            Debug.LogError("[NPCSpawner] NPC Archetype is not assigned!");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Manual spawn trigger (for external scripts or events)
    /// </summary>
    [ContextMenu("Spawn Enemy Now")]
    public void SpawnNow()
    {
        SpawnSingleEnemy();
    }

    /// <summary>
    /// Reset spawn count (useful for respawning)
    /// </summary>
    public void ResetSpawnCount()
    {
        currentSpawnCount = 0;
    }

    // Gizmos for visualizing spawn point
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

    private void OnDrawGizmosSelected()
    {
        Transform spawnTransform = spawnPoint != null ? spawnPoint : transform;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(spawnTransform.position, 0.3f);
        Gizmos.DrawLine(spawnTransform.position, spawnTransform.position + spawnTransform.forward * 2f);
    }
}