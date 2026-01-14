using UnityEngine;
using System.Collections;

/// <summary>
/// Initializes all NaturalWeaponHitbox components on an NPC entity.
/// 
/// Add this to Component_NPC (or any child of the entity with ControllerBrain).
/// It will automatically find and initialize all natural weapon hitboxes with
/// the required DamageModule and HealthProvider references.
/// 
/// FIXED: Delays initialization to ensure health provider is ready.
/// </summary>
public class NaturalWeaponInitializer : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool debugInitialization = true;

    private ControllerBrain brain;

    void Start()
    {
        // Delay initialization to next frame to ensure all handlers are initialized
        StartCoroutine(DelayedInitialize());
    }

    private IEnumerator DelayedInitialize()
    {
        // Wait one frame for NPCHealthHandler and other handlers to initialize
        yield return null;

        brain = GetComponentInParent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError("[NaturalWeaponInitializer] No ControllerBrain found in parent hierarchy!");
            yield break;
        }

        InitializeNaturalWeapons();
    }

    /// <summary>
    /// Finds all NaturalWeaponHitbox components and initializes them.
    /// Can also be called manually from NPCModule or elsewhere.
    /// </summary>
    public void InitializeNaturalWeapons()
    {
        // Get required modules
        var damageModule = brain.GetModule<DamageSystem>();
        var healthProvider = brain.GetModuleImplementing<IHealthProvider>();

        if (damageModule == null)
        {
            Debug.LogError($"[NaturalWeaponInitializer] DamageModule not found on {brain.name}!");
            return;
        }

        if (healthProvider == null)
        {
            Debug.LogError($"[NaturalWeaponInitializer] IHealthProvider not found on {brain.name}!");
            return;
        }

        // Find all hitboxes in entire entity hierarchy (includeInactive = true to find disabled colliders)
        // IMPORTANT: We search from the brain's root to find hitboxes in the model
        var hitboxes = brain.GetComponentsInChildren<NaturalWeaponHitbox>(true);

        if (hitboxes.Length == 0)
        {
            if (debugInitialization)
            {
                Debug.LogWarning($"[NaturalWeaponInitializer] No NaturalWeaponHitbox components found on {brain.name}");
            }
            return;
        }

        // Initialize each hitbox
        int successCount = 0;
        foreach (var hitbox in hitboxes)
        {
            hitbox.Initialize(damageModule, healthProvider);
            successCount++;

            if (debugInitialization)
            {
                Debug.Log($"[NaturalWeaponInitializer] ✓ Initialized {hitbox.gameObject.name}");
            }
        }

        if (debugInitialization)
        {
            Debug.Log($"[NaturalWeaponInitializer] Successfully initialized {successCount}/{hitboxes.Length} natural weapon hitboxes");
        }
    }

    /// <summary>
    /// Manual initialization trigger from editor or other scripts
    /// </summary>
    [ContextMenu("Initialize Natural Weapons Now")]
    public void ForceInitialize()
    {
        if (brain == null)
        {
            brain = GetComponentInParent<ControllerBrain>();
        }

        if (brain != null)
        {
            InitializeNaturalWeapons();
        }
        else
        {
            Debug.LogError("[NaturalWeaponInitializer] Cannot initialize - no ControllerBrain found!");
        }
    }
}