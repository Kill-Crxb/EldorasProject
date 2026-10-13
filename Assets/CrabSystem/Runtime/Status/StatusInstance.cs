using UnityEngine;

// One application of one status on one bearer. Plain class, not serialized — statuses are
// never saved, for the same reason contributions are never saved: a temporary number
// written to disk outlives its source and nothing can detect the disagreement afterwards.
//
// Owned by the BEARER's StatusSystem.

namespace CrabSystem
{
    public class StatusInstance
    {
        // The authored asset. Never written to.
        public readonly StatusDefinition Definition;

        // Who applied it. Informational — attribution for reflected damage and kill credit.
        // May be null, and may be a destroyed object, so check before dereferencing.
        public ControllerBrain Source;

        // Always at least 1. Only ever above 1 under StatusStacking.Stack.
        public int Stacks;

        // Seconds left. Meaningless when IsPermanent.
        public float Remaining;

        // Seconds to the next tick, for a status that ticks (StatusDefinition.tickSeconds).
        public float NextTick;

        public StatusInstance(StatusDefinition definition, ControllerBrain source)
        {
            Definition = definition;
            Source = source;
            Stacks = 1;
            Remaining = definition.Seconds;
            NextTick = definition.tickSeconds;
        }

        public string Id => Definition.id;

        // A zero duration means it lasts until something removes it explicitly.
        public bool IsPermanent => Definition.Seconds <= 0f;

        // 0 to 1, for a radial sweep on the buff bar. Always 1 while permanent.
        public float Progress => IsPermanent ? 1f : Mathf.Clamp01(Remaining / Definition.Seconds);
    }
}
