using UnityEngine;

namespace RPG.Factions
{
    /// <summary>
    /// Per-entity faction module. Holds a FactionDefinition asset reference —
    /// the single runtime representation of "what faction am I".
    ///
    /// Consumers ask stance questions here (IsHostileTo / GetStanceTo) and the
    /// module delegates to FactionManager's relationship matrix.
    ///
    /// Strings appear only at the persistence boundary: CurrentFactionId
    /// (used by saves and CharacterConfigData) resolves through
    /// FactionManager.Resolve on set.
    /// </summary>
    public class FactionSystem : MonoBehaviour, IBrainModule, ISaveable
    {
        public int InitOrder => 10;

        #region Inspector

        [Header("Module Settings")]
        [SerializeField] private bool isEnabled = true;

        [Header("Faction Data")]
        [Tooltip("This entity's faction. Drag a FactionDefinition asset here for scene-placed entities; spawned NPCs get it from their archetype.")]
        [SerializeField] private FactionDefinition currentFaction;

        #endregion

        #region Private Fields

        private ControllerBrain brain;

        #endregion

        #region Properties

        public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

        public FactionDefinition CurrentFaction
        {
            get => currentFaction;
            set => currentFaction = value;
        }

        /// <summary>
        /// String id view of the faction — persistence boundary only.
        /// Setting resolves the id to its asset via FactionManager.
        ///
        /// An id that does NOT resolve leaves the current faction alone. This used to assign
        /// the null straight through, so one stale or misspelled id anywhere upstream did not
        /// merely fail to set a faction — it WIPED the one configured on the prefab, silently
        /// turning that entity factionless. Everything downstream then reads as non-friendly,
        /// because ProjectileAim.IsFriendly and WeaponHitbox both treat "no faction" as "not an
        /// ally", so heals and buffs quietly stop finding anyone.
        ///
        /// LoadSaveData below already guarded against exactly this for save data. The setter is
        /// the same boundary and needed the same rule.
        /// </summary>
        public string CurrentFactionId
        {
            get => currentFaction != null ? currentFaction.FactionId : "";
            set
            {
                var resolved = FactionManager.Resolve(value);
                if (resolved != null) currentFaction = resolved;
            }
        }

        public string CurrentFactionName
            => currentFaction != null ? currentFaction.DisplayName : "Unaffiliated";

        public Color CurrentFactionColor
            => currentFaction != null ? currentFaction.FactionColor : Color.white;

        #endregion

        #region IBrainModule Implementation

        public void Initialize(ControllerBrain controllerBrain)
        {
            brain = controllerBrain;
        }

        public void UpdateModule()
        {
        }

        public void LateInitialize()
        {
        }

        #endregion

        #region Stance Queries

        public FactionRelationship GetStanceTo(FactionDefinition other)
            => FactionManager.GetStance(currentFaction, other);

        public FactionRelationship GetStanceTo(ControllerBrain other)
            => FactionManager.GetStance(currentFaction, other?.Faction?.CurrentFaction);

        public bool IsHostileTo(ControllerBrain other)
            => GetStanceTo(other) == FactionRelationship.Hostile;

        public bool IsFriendlyTo(ControllerBrain other)
            => GetStanceTo(other) == FactionRelationship.Friendly;

        /// <summary>Damage multiplier when this entity attacks the target. 1.0 by default.</summary>
        public float GetDamageMultiplierAgainst(ControllerBrain target)
            => FactionManager.GetDamageModifier(currentFaction, target?.Faction?.CurrentFaction);

        #endregion

        #region ISaveable Implementation

        public string GetSaveId() => "faction";

        public string GetSaveData()
        {
            var data = new FactionSaveData
            {
                characterId = brain.Identity.EntityId,
                currentFactionId = CurrentFactionId
            };

            return JsonUtility.ToJson(data);
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<FactionSaveData>(json);

            if (string.IsNullOrEmpty(data.currentFactionId))
                return;

            // Only overwrite if the saved id resolves — a stale id from an old
            // save (e.g. pre-rework "faction_neutral") keeps the inspector value
            // instead of wiping the faction to null. Resolve logs the warning.
            var resolved = FactionManager.Resolve(data.currentFactionId);
            if (resolved != null)
                currentFaction = resolved;
        }

        public int GetSaveVersion() => 1;

        #endregion
    }
}
