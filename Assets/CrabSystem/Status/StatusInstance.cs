using UnityEngine;

/// <summary>
/// One application of one status on one bearer. Plain class, not serialized — statuses are
/// never saved, for the same reason contributions are never saved: a temporary number
/// written to disk outlives its source and nothing can detect the disagreement afterwards.
///
/// Owned by the BEARER's StatusSystem, which is the break with EffectManagerModule. That
/// module is held by whoever applied the effect, so its effects die with their applier and
/// nothing can ask an entity what is currently on it.
/// </summary>
public class StatusInstance
{
    /// <summary>The authored asset. Never written to.</summary>
    public readonly StatusDefinition Definition;

    /// <summary>
    /// Who applied it. Informational — attribution for reflected damage and kill credit.
    /// May be null, and may be a destroyed object, so check before dereferencing.
    /// </summary>
    public ControllerBrain Source;

    /// <summary>Always at least 1. Only ever above 1 under StatusStacking.Stack.</summary>
    public int Stacks;

    /// <summary>Seconds left. Meaningless when IsPermanent.</summary>
    public float Remaining;

    public StatusInstance(StatusDefinition definition, ControllerBrain source)
    {
        Definition = definition;
        Source = source;
        Stacks = 1;
        Remaining = definition.Seconds;
    }

    public string Id => Definition.id;

    /// <summary>A zero duration means it lasts until something removes it explicitly.</summary>
    public bool IsPermanent => Definition.Seconds <= 0f;

    /// <summary>0 to 1, for a radial sweep on the buff bar. Always 1 while permanent.</summary>
    public float Progress => IsPermanent ? 1f : Mathf.Clamp01(Remaining / Definition.Seconds);
}
