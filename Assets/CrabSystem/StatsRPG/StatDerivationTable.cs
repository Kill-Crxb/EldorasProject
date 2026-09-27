using System.Collections.Generic;
using NinjaGame.Progression;
using UnityEngine;

namespace NinjaGame.Stats
{
    /// <summary>
    /// What each core stat gives you as it rises. One entry per core stat, holding the
    /// thresholds it passes and what each one grants.
    ///
    /// Cumulative: every threshold reached keeps paying, so a character at Spirit 14 holds
    /// everything Spirit 5, 7, 10 and 14 gave them.
    ///
    /// Rewards are assets, so a breakpoint can grant a stat bonus, a per-point rate, a
    /// talent point or extra dice — and the same reward can be reused by a talent node.
    /// This is balance data, not code.
    /// </summary>
    [CreateAssetMenu(fileName = "New Derivation Table", menuName = "NinjaGame/Stats/Stat Derivation Table")]
    public class StatDerivationTable : ScriptableObject
    {
        [SerializeField] private List<StatDerivation> derivations = new();

        public IReadOnlyList<StatDerivation> Derivations => derivations;
    }

    [System.Serializable]
    public class StatDerivation
    {
        [Tooltip("Core stat that drives these thresholds, e.g. 'core.spirit'.")]
        public string sourceStatId;

        [Tooltip("Thresholds this stat passes. Order does not matter.")]
        public List<StatBreakpoint> breakpoints = new();
    }

    [System.Serializable]
    public class StatBreakpoint
    {
        [Tooltip("Source value at which these rewards start applying.")]
        public float threshold;

        [Tooltip("Granted on reaching the threshold, and taken back if it is ever lost.")]
        public List<Reward> rewards = new();
    }
}
