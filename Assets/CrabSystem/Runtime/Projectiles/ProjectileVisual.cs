using UnityEngine;

/// <summary>
/// The presentation tier — the Visual child of a projectile. Same reasoning as m_Root on
/// ControllerBrain: everything that needs its own transform lives here, and no gameplay
/// logic does.
///
/// Populated at runtime from ProjectileData.visualPrefab, which is what lets one archetype
/// prefab serve Fireball, Iceball and Poison Bolt.
///
/// The model is REBUILT ONLY WHEN THE PREFAB CHANGES. A pooled shuriken fired repeatedly
/// keeps the same model instance and just re-tints — otherwise every shot would pay an
/// Instantiate and a Destroy, which is the cost the pool exists to avoid.
///
/// THREE WAYS TO BE COLOURED, because a projectile can be made of three things:
///
///   Renderer        MaterialPropertyBlock, never renderer.material — the latter instances a
///                   material per object and leaks under pooling.
///   ParticleSystem  main.startColor. A particle shader multiplies by vertex colour, so a
///                   MaterialPropertyBlock on a ParticleSystemRenderer usually does nothing
///                   at all. THIS is where a spell VFX's colour actually lives.
///   Light           light.color, straight.
///
/// The particle path is not a special case for one asset. Every spell effect worth shipping
/// is particles, so one orb prefab serving six schools depends on it entirely.
/// </summary>
public class ProjectileVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Where the runtime model is parented. Leave empty to use this transform.")]
    [SerializeField] private Transform modelParent;

    private GameObject spawnedModel;
    private GameObject spawnedFrom;
    private MaterialPropertyBlock propertyBlock;

    private Renderer[] renderers;
    private ParticleSystem[] particles;
    private Light[] lights;

    // The prefab's authored colours, captured once per rebuild. Re-tinting a pooled instance
    // has to start from these every time — tinting the already-tinted result compounds, and a
    // fireball fired six times would drift to black.
    private Color[] particleBaseColors;
    private Color[] lightBaseColors;

    private void Awake()
    {
        if (modelParent == null)
            modelParent = transform;
    }

    /// <summary>
    /// Called by ProjectileBrain at launch.
    ///
    /// The MODEL comes from the shared asset — it is what makes this projectile a fireball
    /// rather than a shuriken, and it is rebuilt only when it actually changes. The TINT comes
    /// from the runtime copy, because a composed spell picks its element at cast time. The
    /// asset's tint seeds the runtime in FromData, so an ordinary projectile is unaffected.
    /// </summary>
    public void Setup(ProjectileData data, ProjectileRuntime runtime)
    {
        if (data == null) return;

        if (data.visualPrefab != spawnedFrom)
            Rebuild(data.visualPrefab);

        // Spin accumulates on this transform during flight, so a recycled instance starts
        // rotated unless it is reset.
        transform.localRotation = Quaternion.identity;

        ApplyTint(runtime.tint, data.tintProperty);
    }

    private void Rebuild(GameObject prefab)
    {
        if (spawnedModel != null)
            Destroy(spawnedModel);

        spawnedModel = null;
        renderers = null;
        particles = null;
        lights = null;
        particleBaseColors = null;
        lightBaseColors = null;
        spawnedFrom = prefab;

        if (prefab == null) return;

        spawnedModel = Instantiate(prefab, modelParent);
        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;

        renderers = spawnedModel.GetComponentsInChildren<Renderer>(true);
        particles = spawnedModel.GetComponentsInChildren<ParticleSystem>(true);
        lights = spawnedModel.GetComponentsInChildren<Light>(true);

        CaptureBaseColors();
    }

    private void CaptureBaseColors()
    {
        particleBaseColors = VfxTint.CaptureParticleColors(particles);
        lightBaseColors = VfxTint.CaptureLightColors(lights);
    }

    /// <summary>
    /// The colour work itself lives in VfxTint, shared with the spell system's hand-sign
    /// bursts — the rules for where a colour hides in a Unity effect are the same wherever
    /// the effect is, and having two copies of them would mean one of them going stale.
    ///
    /// The CACHED form: base colours were captured at rebuild, so re-tinting a pooled
    /// projectile always starts from the prefab's colours rather than from the last tint.
    /// Tinting a tint compounds, and a fireball fired six times would drift to black.
    /// </summary>
    public void ApplyTint(Color color, string property)
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        VfxTint.TintRenderers(renderers, propertyBlock, color, property);
        VfxTint.TintParticles(particles, particleBaseColors, color);
        VfxTint.TintLights(lights, lightBaseColors, color);
    }
}
