using System.Collections.Generic;
using CrabSystem.Progression;
using UnityEngine;

// A body part that carries a power (Shonen_Traits.md §4a): Six Eyes, Mirror Eye and the like. The organ holds the
// power; a trait that grants one gives nothing else. Static authored data only; what a character has installed lives
// in OrganModule. Harvest rules (who can take it, how hard) are still to design.

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "Organ_", menuName = "CrabSystem/Traits/Organ")]
    public class OrganDefinition : ScriptableObject
    {
        public string organId;
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Tooltip("The body slot this fills, from the OrganCatalog's slot list.")]
        public string slot;

        [Tooltip("Applied while installed, under organ:{slot}:{index}.")]
        public List<Reward> rewards = new();
    }
}
