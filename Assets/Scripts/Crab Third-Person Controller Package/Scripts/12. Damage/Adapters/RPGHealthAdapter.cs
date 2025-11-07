using UnityEngine;

/// <summary>
/// Adapter that connects DamageIn to RPGResources.
/// Searches for RPGResources from the Brain level (not from local position).
/// </summary>
public class RPGHealthAdapter : MonoBehaviour, IHealthProvider
{
    [Header("Manual Reference (Optional)")]
    [SerializeField] private RPGResources resources;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ControllerBrain brain;
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        // Find the Brain (should be parent of Component_Damage)
        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError($"[RPGHealthAdapter] No ControllerBrain found in parent hierarchy!");
            return;
        }

        // If no manual reference, search from Brain level
        if (resources == null)
        {
            // Search DOWN from Brain to find RPGResources
            resources = brain.GetComponentInChildren<RPGResources>();
        }

        if (resources != null)
        {
            if (debugLogs)
            {
                Debug.Log($"[RPGHealthAdapter] ✓ Found RPGResources: {resources.name}");
            }
        }
        else
        {
            Debug.LogWarning($"[RPGHealthAdapter] ✗ Could not find RPGResources! " +
                           "DamageIn will not function properly.");
        }

        isInitialized = true;
    }

    // === IHealthProvider Implementation ===

    public float GetCurrentHealth()
    {
        if (!ValidateResources())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGHealthAdapter] No resources available, returning 0");
            return 0f;
        }

        return resources.CurrentHealth;
    }

    public float GetMaxHealth()
    {
        if (!ValidateResources())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGHealthAdapter] No resources available, returning 100");
            return 100f;
        }

        return resources.MaxHealth;
    }

    public float GetHealthPercentage()
    {
        if (!ValidateResources())
        {
            return 1f;
        }

        return resources.HealthPercentage;
    }

    public bool IsAlive()
    {
        if (!ValidateResources())
        {
            return true; // Assume alive if no resources
        }

        return resources.CurrentHealth > 0f;
    }

    public void ApplyDamage(float damage)
    {
        if (!ValidateResources())
        {
            Debug.LogError("[RPGHealthAdapter] Cannot apply damage - no resources!");
            return;
        }

        resources.ModifyHealth(-damage);

        if (debugLogs)
        {
            Debug.Log($"[RPGHealthAdapter] Applied {damage:F1} damage. " +
                     $"Health: {resources.CurrentHealth:F1}/{resources.MaxHealth:F1}");
        }
    }

    public void ApplyHealing(float healing)
    {
        if (!ValidateResources())
        {
            Debug.LogError("[RPGHealthAdapter] Cannot apply healing - no resources!");
            return;
        }

        resources.ModifyHealth(healing);

        if (debugLogs)
        {
            Debug.Log($"[RPGHealthAdapter] Applied {healing:F1} healing. " +
                     $"Health: {resources.CurrentHealth:F1}/{resources.MaxHealth:F1}");
        }
    }

    public void SetHealth(float value)
    {
        if (!ValidateResources())
        {
            Debug.LogError("[RPGHealthAdapter] Cannot set health - no resources!");
            return;
        }

        // Calculate the difference and apply as healing/damage
        float currentHealth = resources.CurrentHealth;
        float difference = value - currentHealth;

        if (difference != 0)
        {
            resources.ModifyHealth(difference);

            if (debugLogs)
            {
                Debug.Log($"[RPGHealthAdapter] Set health to {value:F1}. " +
                         $"Health: {resources.CurrentHealth:F1}/{resources.MaxHealth:F1}");
            }
        }
    }

    public void SetHealthToMax()
    {
        if (!ValidateResources())
        {
            Debug.LogError("[RPGHealthAdapter] Cannot set health to max - no resources!");
            return;
        }

        resources.SetHealthToMax();

        if (debugLogs)
        {
            Debug.Log($"[RPGHealthAdapter] Health set to max: {resources.MaxHealth:F1}");
        }
    }

    private bool ValidateResources()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        return resources != null;
    }

    // === Inspector Helpers ===

    [ContextMenu("Debug: Show Current Health")]
    private void DebugShowHealth()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGHealthAdapter] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGHealthAdapter] Current Health ===\n" +
                  $"Current: {GetCurrentHealth():F1}\n" +
                  $"Max: {GetMaxHealth():F1}\n" +
                  $"Percentage: {GetHealthPercentage():P0}\n" +
                  $"Alive: {IsAlive()}");
    }

    [ContextMenu("Debug: Show Component Paths")]
    private void DebugShowPaths()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGHealthAdapter] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGHealthAdapter] Component Paths ===\n" +
                  $"This Adapter: {GetFullPath(transform)}\n" +
                  $"Brain: {(brain != null ? GetFullPath(brain.transform) : "NOT FOUND")}\n" +
                  $"RPGResources: {(resources != null ? GetFullPath(resources.transform) : "NOT FOUND")}");
    }

    [ContextMenu("Test: Apply 10 Damage")]
    private void TestApplyDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGHealthAdapter] Test only works in Play Mode!");
            return;
        }

        ApplyDamage(10f);
    }

    [ContextMenu("Test: Apply 25 Healing")]
    private void TestApplyHealing()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGHealthAdapter] Test only works in Play Mode!");
            return;
        }

        ApplyHealing(25f);
    }

    [ContextMenu("Test: Set Health To Max")]
    private void TestSetHealthToMax()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGHealthAdapter] Test only works in Play Mode!");
            return;
        }

        SetHealthToMax();
    }

    private string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}