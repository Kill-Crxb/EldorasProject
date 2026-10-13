using System.Collections.Generic;
using UnityEngine;

namespace CrabSystem
{
    // One damage type (physical, fire…). Abilities, statuses and projectiles name it by id; DamageCalculationConfig
    // lists the ones the game knows, so adding a type is a new asset, not a code change.
    [CreateAssetMenu(fileName = "DamageType", menuName = "CrabSystem/Combat/Damage Type")]
    public class DamageTypeDefinition : ScriptableObject
    {
        [Tooltip("Stable id that assets store. Never rename one in use.")]
        public string id;
        public string displayName;
        [TextArea(2, 3)] public string description;
        public Color color = Color.white;

        [Tooltip("The armour shield soaks this type before health (Combat_Framework §6.1).")]
        public bool absorbedByShield;

        [Header("Attacker")]
        [Tooltip("Each stat is rolled as a die and added to the hit (Might 6 rolls 1d6), scaled by its multiplier.")]
        [IdRef(IdKind.Stat)] public List<string> attackerStatIds = new();
        public List<float> attackerStatMultipliers = new();

        [Header("Defender")]
        [Tooltip("Not read yet: every type is mitigated by Avoidance and armour (Stat_Resolution §4).")]
        [IdRef(IdKind.Stat)] public List<string> defenderStatIds = new();
        public List<float> defenderStatMultipliers = new();
    }

    // Code defaults only; every other type is data.
    public static class DamageTypeIds
    {
        public const string Physical = "physical";
    }
}
