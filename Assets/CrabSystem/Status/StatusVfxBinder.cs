using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts a status's aura on the bearer and takes it away again. That is the whole job.
///
/// Separate from StatusSystem on purpose: statuses are stats and a clock, and nothing about
/// that should have to know what a ParticleSystem is. An entity with no VFXSystem, or a
/// status with no auraVfx, simply never involves this module.
///
/// WHY NOT VFXSystem.SpawnEffect:
/// That method spawns UNPARENTED and AUTO-DESTROYS when the particles finish — right for an
/// impact spark, wrong for an aura in both halves. An aura has to follow the character, and
/// it has to last exactly as long as the status rather than as long as its own particles.
/// So this instantiates against the Aura anchor itself and owns the lifetime.
/// </summary>
public class StatusVfxBinder : MonoBehaviour, IBrainModule
{
    [Header("Module Config")]
    [SerializeField] private bool isEnabled = true;

    [Tooltip("Let particles finish their last lifetime instead of snapping the aura off the " +
             "instant the status ends. Untick for an effect that should cut hard.")]
    [SerializeField] private bool fadeOutOnRemove = true;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private ControllerBrain brain;
    private StatusSystem statuses;
    private VFXSystem vfx;

    // Keyed by status id, matching StatusSystem's one-instance-per-id rule.
    private readonly Dictionary<string, GameObject> auras = new();

    #region IBrainModule

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        statuses = brain.GetModule<StatusSystem>();
        vfx = brain.GetModule<VFXSystem>();

        if (statuses == null)
        {
            Debug.LogWarning($"[StatusVfxBinder] No StatusSystem on {brain.EntityName} — no auras will play.");
            return;
        }

        statuses.OnStatusApplied += Spawn;
        statuses.OnStatusRemoved += Despawn;
    }

    public void UpdateModule() { }

    #endregion

    #region Spawn / despawn

    private void Spawn(StatusInstance instance)
    {
        if (!IsEnabled) return;

        GameObject prefab = instance.Definition.auraVfx;
        if (prefab == null) return;

        if (vfx == null)
        {
            Debug.LogWarning($"[StatusVfxBinder] '{instance.Id}' has an aura but {brain.EntityName} " +
                             "has no VFXSystem to anchor it to.");
            return;
        }

        // Re-applying a Refresh status fires Applied only on the first application, so an
        // existing aura here means something went wrong. Keep the one that is already playing.
        if (auras.ContainsKey(instance.Id)) return;

        // GetAnchor falls back to the brain root rather than returning null, so an entity whose
        // Aura transform was never wired still shows the effect somewhere sensible.
        Transform anchor = vfx.GetAnchor(VFXAnchor.Aura);

        // PARENTED — the aura follows the character. That is the whole difference from an
        // impact effect, which is spawned in world space at the point of contact and forgotten.
        auras[instance.Id] = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
    }

    private void Despawn(StatusInstance instance)
    {
        if (!auras.TryGetValue(instance.Id, out GameObject aura)) return;

        auras.Remove(instance.Id);
        if (aura == null) return;

        ParticleSystem particles = fadeOutOnRemove ? aura.GetComponentInChildren<ParticleSystem>() : null;

        if (particles == null)
        {
            Destroy(aura);
            return;
        }

        // Stop emitting and let the particles already in flight live out their lifetime.
        // Destroying outright leaves a visible pop on anything with a slow, drifting aura.
        particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(aura, particles.main.startLifetime.constantMax);
    }

    #endregion

    #region Teardown

    private void OnDestroy()
    {
        // StatusSystem is a concrete MonoBehaviour reference, so Unity's null-check overload
        // applies here and this is safe during scene teardown. The auras are children of an
        // anchor that is being destroyed with us, so there is nothing to clean up.
        if (statuses != null)
        {
            statuses.OnStatusApplied -= Spawn;
            statuses.OnStatusRemoved -= Despawn;
        }

        auras.Clear();
    }

    #endregion
}
