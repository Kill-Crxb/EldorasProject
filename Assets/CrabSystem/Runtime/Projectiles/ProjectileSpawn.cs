using System;
using UnityEngine;

/// <summary>
/// WHERE a projectile comes into the world. Inline block on ProjectileData, alongside
/// ProjectileAim and ProjectileMovement.
///
/// Three origins, covering the cheap cases:
///
///   CastAnchor   — a VFXSystem anchor on the brain. The default, and the safe one: anchors
///                  live on the brain rather than the model, so they survive the runtime
///                  model swaps that Model_System does.
///   ModelSocket  — a named socket on the CURRENT model's rig, via ModelModule.GetNamedSocket.
///                  This is the "from the hand" case. Costs nothing because the socket system
///                  already exists and already re-resolves on model swap.
///   AboveTarget  — spawns overhead and points down. The "rains from the sky" case.
///
/// ==== WHERE THE LINE IS ====
/// These three are cheap because each is a single position lookup. Anything that spawns MORE
/// THAN ONE projectile, or needs its own timing and parameters — a meteor shower, a volley, a
/// spread, a delayed rain, a spawn-at-cursor AoE — should NOT become a fourth enum value. It
/// wants its own spawner data object driving repeated launches, because it is a different
/// shape of thing: one cast producing many projectiles over time, rather than one projectile
/// starting somewhere unusual.
/// </summary>

public enum SpawnOrigin
{
    /// <summary>A VFXSystem anchor on the brain. Survives model swaps.</summary>
    CastAnchor,

    /// <summary>A named socket on the current model's rig — a hand, a shoulder, a staff tip.</summary>
    ModelSocket,

    /// <summary>Overhead, pointing down. Falls back to a point ahead of the caster when untargeted.</summary>
    AboveTarget,

    /// <summary>
    /// The caster's own feet. THIS IS WHAT MAKES A BURST — a stationary, big-radius, short-lived
    /// projectile centred on you, which is an eruption without needing an eruption system.
    ///
    /// The entity root rather than an anchor: an anchor sits at chest height and a ground slam
    /// that detonates at your sternum reads wrong.
    /// </summary>
    Self,
}

[Serializable]
public class ProjectileSpawn
{
    [NonSerialized] private bool warnedMissingSocket;

    // =========================================================================
    // Origin
    // =========================================================================

    [Header("Origin")]
    [Tooltip("Where the projectile appears.")]
    public SpawnOrigin origin = SpawnOrigin.CastAnchor;

    [Tooltip("Which VFXSystem anchor to use in CastAnchor mode. CastOrigin is chest height, " +
             "forward of the body.")]
    public VFXAnchor anchor = VFXAnchor.CastOrigin;

    [Tooltip("Named socket id in ModelSocket mode — matches an entry in the model's " +
             "ModelSocketProvider named sockets list (e.g. 'hand_r'). Falls back to the anchor " +
             "if the model has no such socket.")]
    public string socketId = "hand_r";

    // =========================================================================
    // Above Target
    // =========================================================================

    [Header("Self")]
    [Tooltip("How far above the caster's feet a Self-origin projectile appears. Small — enough " +
             "that a burst sits on the ground instead of inside it.")]
    public float selfHeight = 0.5f;

    [Header("Above Target")]
    [Tooltip("How far above the target the projectile appears.")]
    public float skyHeight = 12f;

    [Tooltip("Used when nothing was targeted: the strike lands this far ahead of the caster " +
             "along the aim direction.")]
    public float untargetedDistance = 8f;

    // =========================================================================
    // Queries
    // =========================================================================

    /// <summary>
    /// True when this origin dictates the launch direction itself, overriding ProjectileAim.
    /// A projectile spawned in the sky must point down; nothing else about aiming applies.
    /// </summary>
    public bool OverridesDirection => origin == SpawnOrigin.AboveTarget;

    // =========================================================================
    // Resolution
    // =========================================================================

    /// <summary>
    /// The anchor position on the brain. Always available, always sensible — the launcher
    /// uses it as the origin for the aim probe even when the projectile itself spawns
    /// somewhere else, because a probe should come from the caster's chest, not the sky.
    /// </summary>
    public Vector3 ResolveAnchor(ControllerBrain source, VFXSystem vfx)
    {
        if (vfx != null) return vfx.GetAnchorPosition(anchor);
        if (source == null) return Vector3.zero;

        Transform root = source.EntityRoot != null ? source.EntityRoot : source.transform;

        return root.position + Vector3.up * 1.2f + root.forward * 0.8f;
    }

    /// <summary>
    /// The actual spawn position. Called after the target is known, because AboveTarget
    /// depends on it.
    /// </summary>
    public Vector3 ResolveOrigin(ControllerBrain source, Vector3 anchorOrigin, Vector3 baseDirection, Transform target)
    {
        if (origin == SpawnOrigin.ModelSocket)
            return ResolveSocket(source, anchorOrigin);

        if (origin == SpawnOrigin.AboveTarget)
            return StrikePoint(anchorOrigin, baseDirection, target) + Vector3.up * skyHeight;

        if (origin == SpawnOrigin.Self)
            return SelfOrigin(source, anchorOrigin);

        return anchorOrigin;
    }

    /// <summary>
    /// The caster's feet, lifted by selfHeight so a burst sits on the ground rather than
    /// half-buried in it. Falls back to the anchor when there is no brain to stand on.
    /// </summary>
    private Vector3 SelfOrigin(ControllerBrain source, Vector3 fallback)
    {
        if (source == null) return fallback;

        Transform root = source.EntityRoot != null ? source.EntityRoot : source.transform;

        return root.position + Vector3.up * selfHeight;
    }

    /// <summary>
    /// Launch direction for origins that dictate it. Only AboveTarget does — straight down at
    /// the strike point.
    /// </summary>
    public Vector3 ResolveDirection(Vector3 spawnOrigin, Vector3 baseDirection, Transform target, Vector3 anchorOrigin)
    {
        if (origin != SpawnOrigin.AboveTarget) return baseDirection;

        Vector3 down = StrikePoint(anchorOrigin, baseDirection, target) - spawnOrigin;

        return down.sqrMagnitude > 0.0001f ? down.normalized : Vector3.down;
    }

    // =========================================================================
    // Internals
    // =========================================================================

    private Vector3 ResolveSocket(ControllerBrain source, Vector3 fallback)
    {
        if (source == null) return fallback;

        // ModelModule re-resolves sockets against whatever model is currently loaded, so this
        // keeps working across runtime model swaps. That is why it is a socket id and not a
        // Transform reference.
        Transform socket = source.Model != null ? source.Model.GetNamedSocket(socketId) : null;

        if (socket != null) return socket.position;

        if (warnedMissingSocket) return fallback;
        warnedMissingSocket = true;

        Debug.LogWarning($"[ProjectileSpawn] Socket '{socketId}' not found on {source.name}'s model — " +
                         $"falling back to the {anchor} anchor. Add it to the model's ModelSocketProvider.");

        return fallback;
    }

    /// <summary>Where the strike lands. The origin sits above it and points at it.</summary>
    private Vector3 StrikePoint(Vector3 anchorOrigin, Vector3 baseDirection, Transform target)
    {
        if (target != null) return target.position;

        return anchorOrigin + baseDirection * untargetedDistance;
    }
}
