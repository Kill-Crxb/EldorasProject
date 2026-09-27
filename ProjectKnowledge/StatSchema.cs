using System.Collections.Generic;
using UnityEngine;

namespace NinjaGame.Stats
{
    /// <summary>
    /// A named set of stat definitions. Entities load one or more schemas by asset name.
    /// A stat is a plain number with an authored range — no formulas, no derivation.
    /// </summary>
    [CreateAssetMenu(fileName = "New Stat Schema", menuName = "NinjaGame/Stats/Stat Schema")]
    public class StatSchema : ScriptableObject
    {
        [Header("Persistence")]
        [Tooltip("These stats are produced by other things — core stats, gear, talents — " +
                 "rather than owned by the character. Derived stats are never written to the " +
                 "save file and always start at their default, so the sources stay the only " +
                 "truth and a stale save can never disagree with them.")]
        [SerializeField] private bool derived;

        [Header("Definitions")]
        [SerializeField] private List<StatEntry> entries = new();

        public bool Derived => derived;

        public IReadOnlyList<StatEntry> Entries => entries;

        public StatEntry Find(string id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].id == id) return entries[i];
            }

            return null;
        }
    }

    /// <summary>
    /// One stat. Range is authored per entry so no limits are baked into code.
    /// </summary>
    [System.Serializable]
    public class StatEntry
    {
        [Tooltip("Stable unique id, e.g. 'core.body'. Never change after shipping.")]
        public string id;

        [Tooltip("Name shown in UI.")]
        public string displayName;

        [Tooltip("Abbreviated label for compact UI, e.g. 'BDY'. Falls back to displayName.")]
        public string shortName;

        [TextArea(1, 3)]
        public string description;

        [Tooltip("Value assigned when an entity first loads this schema.")]
        public float defaultValue;

        [Tooltip("Lowest value this stat may hold.")]
        public float minValue;

        [Tooltip("Highest value this stat may hold.")]
        public float maxValue = 99f;

        public string ShortLabel => string.IsNullOrEmpty(shortName) ? displayName : shortName;

        public float Clamp(float value) => Mathf.Clamp(value, minValue, maxValue);

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                error = "id is required";
                return false;
            }

            if (maxValue < minValue)
            {
                error = $"'{id}' has maxValue below minValue";
                return false;
            }

            if (defaultValue < minValue || defaultValue > maxValue)
            {
                error = $"'{id}' has defaultValue outside its range";
                return false;
            }

            error = "";
            return true;
        }
    }
}
