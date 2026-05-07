using UnityEngine;

/// <summary>
/// VFXSystem — brain module that owns all named effect anchor transforms
/// and handles spawning visual effects on behalf of other systems.
///
/// Responsibilities:
///   - Expose named anchor transforms (Overhead, CastOrigin, HitSparks, Aura, Back)
///   - Spawn and auto-destroy effect prefabs at anchors or arbitrary world positions
///   - Provide the correct world position for damage numbers
///
/// NOT responsible for:
///   - Pooling (plain Instantiate/Destroy for now — pool is a later upgrade)
///   - Audio (SFXSystem will handle that)
///   - Animation events (AbilitySystem owns those)
///
/// Setup:
///   1. Add VFXSystem component to the ControllerBrain GameObject.
///   2. Create a child GameObject named "Anchors" under the brain.
///   3. Create child transforms inside Anchors: Overhead, CastOrigin, HitSparks, Aura, Back.
///   4. Assign each transform to the matching field in the inspector.
///
/// Usage:
///   brain.GetModule&lt;VFXSystem&gt;().SpawnEffect(prefab, VFXAnchor.CastOrigin);
///   brain.GetModule&lt;VFXSystem&gt;().GetAnchor(VFXAnchor.Overhead).position;
/// </summary>
public class VFXSystem : MonoBehaviour, IBrainModule
{
    [Header("Anchors")]
    [Tooltip("Above head — damage numbers, status icons")]
    [SerializeField] private Transform overhead;

    [Tooltip("Hands / weapon origin — cast effects, spell particles")]
    [SerializeField] private Transform castOrigin;

    [Tooltip("Centre of mass — hit impact sparks, reaction effects")]
    [SerializeField] private Transform hitSparks;

    [Tooltip("Around the body — persistent aura effects, buffs, debuffs")]
    [SerializeField] private Transform aura;

    [Tooltip("Behind the entity — cape effects, back-mounted equipment VFX")]
    [SerializeField] private Transform back;

    [Header("Debug")]
    [SerializeField] private bool debugVFX = false;

    private ControllerBrain brain;

    public bool IsEnabled { get; set; } = true;

    // =========================================================================
    // IBrainModule
    // =========================================================================

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (debugVFX)
            Debug.Log($"[VFXSystem] Initialized on {brain.name}");
    }

    public void UpdateModule() { }

    // =========================================================================
    // Anchor Access
    // =========================================================================

    /// <summary>
    /// Returns the Transform for the given anchor. Falls back to the brain root
    /// if the anchor is not assigned, so callers always get a valid position.
    /// </summary>
    public Transform GetAnchor(VFXAnchor anchor)
    {
        Transform result = anchor switch
        {
            VFXAnchor.Overhead   => overhead,
            VFXAnchor.CastOrigin => castOrigin,
            VFXAnchor.HitSparks  => hitSparks,
            VFXAnchor.Aura       => aura,
            VFXAnchor.Back       => back,
            _                    => null,
        };

        if (result == null)
        {
            if (debugVFX)
                Debug.LogWarning($"[VFXSystem] Anchor '{anchor}' not assigned on {brain?.name} — falling back to root");
            return transform;
        }

        return result;
    }

    /// <summary>
    /// Returns the world position of the given anchor.
    /// Safe to call even if the anchor transform is not assigned.
    /// </summary>
    public Vector3 GetAnchorPosition(VFXAnchor anchor) => GetAnchor(anchor).position;

    // =========================================================================
    // Effect Spawning
    // =========================================================================

    /// <summary>
    /// Spawn a prefab at the given anchor. Auto-destroys when its ParticleSystem
    /// finishes, or after a fallback lifetime if it has no ParticleSystem.
    /// Returns the spawned instance (null if prefab is null).
    /// </summary>
    public GameObject SpawnEffect(GameObject prefab, VFXAnchor anchor)
    {
        if (prefab == null) return null;
        return SpawnEffectAt(prefab, GetAnchor(anchor).position);
    }

    /// <summary>
    /// Spawn a prefab at an arbitrary world position.
    /// Use this for hit effects that land on a target, not the caster.
    /// </summary>
    public GameObject SpawnEffectAt(GameObject prefab, Vector3 worldPos)
    {
        if (prefab == null) return null;

        GameObject instance = Instantiate(prefab, worldPos, Quaternion.identity);

        float destroyDelay = 3f; // fallback lifetime

        var particles = instance.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Play();
            destroyDelay = particles.main.duration + particles.main.startLifetime.constantMax;
        }

        Destroy(instance, destroyDelay);

        if (debugVFX)
            Debug.Log($"[VFXSystem] Spawned '{prefab.name}' at {worldPos} (destroy in {destroyDelay:F1}s)");

        return instance;
    }
}
