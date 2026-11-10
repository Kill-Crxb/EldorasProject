using System.Collections.Generic;
using UnityEngine;

// Every trait the game knows, so a saved id finds its definition.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "TraitCatalog", menuName = "CrabSystem/Traits/Catalog")]
    public class TraitCatalog : ScriptableObject
    {
        public List<TraitDefinition> traits = new();

        public TraitDefinition Find(string traitId)
        {
            if (string.IsNullOrEmpty(traitId)) return null;

            foreach (TraitDefinition trait in traits)
            {
                if (trait != null && trait.traitId == traitId) return trait;
            }
            return null;
        }
    }
}
