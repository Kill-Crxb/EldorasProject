using System.Collections.Generic;
using UnityEngine;

namespace CrabSystem.Stats
{
    // The damage rules DamageManager hands to every DamageSystem: which types exist and the defence every target
    // starts with.
    [CreateAssetMenu(fileName = "Damage Calculation Config", menuName = "CrabSystem/Stats/Damage Calculation Config")]
    public class DamageCalculationConfig : ScriptableObject
    {
        public List<DamageTypeDefinition> damageTypes = new();

        [Tooltip("Used for an id no listed type has.")]
        public DamageTypeDefinition defaultType;

        [Tooltip("Defence before Avoidance and armour are added: the number a d20 + accuracy must reach (Stat_Resolution §4).")]
        public float baseDefense = 5f;

        public DamageTypeDefinition Find(string id)
        {
            foreach (var type in damageTypes)
                if (type != null && type.id == id) return type;
            return defaultType;
        }
    }
}
