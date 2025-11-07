using UnityEngine;

/// <summary>
/// Adapter that connects DamageIn to ActiveDefenseModule.
/// Searches for IDefenseCapability from the Brain level (not from local position).
/// </summary>
public class RPGDefenseAdapter : MonoBehaviour, IDefenseProvider
{
    [Header("Manual References (Optional)")]
    [SerializeField] private MonoBehaviour defenseCapabilityModule;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private ControllerBrain brain;
    private IDefenseCapability defenseCapability;
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
            Debug.LogError($"[RPGDefenseAdapter] No ControllerBrain found in parent hierarchy!");
            return;
        }

        // If no manual reference, search from Brain level
        if (defenseCapabilityModule == null)
        {
            // Search DOWN from Brain to find IDefenseCapability (ActiveDefenseModule)
            defenseCapabilityModule = brain.GetComponentInChildren<IDefenseCapability>() as MonoBehaviour;
        }

        if (defenseCapabilityModule != null && defenseCapabilityModule is IDefenseCapability capability)
        {
            defenseCapability = capability;

            if (debugLogs)
            {
                Debug.Log($"[RPGDefenseAdapter] ✓ Found IDefenseCapability: {defenseCapabilityModule.name}");
            }
        }
        else
        {
            if (debugLogs)
            {
                Debug.LogWarning($"[RPGDefenseAdapter] ✗ Could not find IDefenseCapability! " +
                               "Defense processing will be disabled.");
            }
        }

        isInitialized = true;
    }

    // === IDefenseProvider Implementation ===

    public float ProcessIncomingDamage(float damage, Vector3 attackDirection)
    {
        if (!ValidateDefense())
        {
            if (debugLogs)
                Debug.LogWarning("[RPGDefenseAdapter] No defense capability, damage unmodified");
            return damage;
        }

        float multiplier = defenseCapability.GetDefensiveMultiplier(attackDirection);
        float processedDamage = damage * multiplier;

        if (debugLogs)
        {
            Debug.Log($"[RPGDefenseAdapter] Damage: {damage:F1} × {multiplier:F2} = {processedDamage:F1}");
        }

        return processedDamage;
    }

    public bool IsBlocking()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.IsBlocking;
    }

    public bool IsParrying()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.IsParrying;
    }

    public bool CanDefend()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.CanDefend;
    }

    private bool ValidateDefense()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        return defenseCapability != null;
    }

    // === Inspector Helpers ===

    [ContextMenu("Debug: Show Defense State")]
    private void DebugShowDefenseState()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGDefenseAdapter] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGDefenseAdapter] Defense State ===\n" +
                  $"Can Defend: {CanDefend()}\n" +
                  $"Is Blocking: {IsBlocking()}\n" +
                  $"Is Parrying: {IsParrying()}");
    }

    [ContextMenu("Debug: Show Component Paths")]
    private void DebugShowPaths()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGDefenseAdapter] Debug only works in Play Mode!");
            return;
        }

        Debug.Log($"=== [RPGDefenseAdapter] Component Paths ===\n" +
                  $"This Adapter: {GetFullPath(transform)}\n" +
                  $"Brain: {(brain != null ? GetFullPath(brain.transform) : "NOT FOUND")}\n" +
                  $"Defense Module: {(defenseCapabilityModule != null ? GetFullPath(defenseCapabilityModule.transform) : "NOT FOUND")}");
    }

    [ContextMenu("Test: Process Sample Damage")]
    private void TestProcessDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[RPGDefenseAdapter] Test only works in Play Mode!");
            return;
        }

        float testDamage = 50f;
        Vector3 testDirection = transform.forward;

        float result = ProcessIncomingDamage(testDamage, testDirection);

        Debug.Log($"[RPGDefenseAdapter] Test: {testDamage:F1} damage → {result:F1} after defense");
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