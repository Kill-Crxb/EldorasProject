using System.Collections.Generic;
using UnityEngine;

// Every organ the game knows, so a saved id finds its definition, and the body slots a character has.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "OrganCatalog", menuName = "CrabSystem/Traits/Organ Catalog")]
    public class OrganCatalog : ScriptableObject
    {
        public List<string> slots = new();
        public List<OrganDefinition> organs = new();

        public OrganDefinition Find(string organId)
        {
            if (string.IsNullOrEmpty(organId)) return null;

            foreach (OrganDefinition organ in organs)
            {
                if (organ != null && organ.organId == organId) return organ;
            }
            return null;
        }

        public bool HasSlot(string slot) => !string.IsNullOrEmpty(slot) && slots.Contains(slot);
    }
}
