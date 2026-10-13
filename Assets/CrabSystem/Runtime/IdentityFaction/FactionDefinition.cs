using UnityEngine;

namespace RPG.Factions
{
    [CreateAssetMenu(menuName = "RPG/Factions/Faction Definition")]
    public class FactionDefinition : ScriptableObject
    {
        [SerializeField] private string factionId;
        [SerializeField] private string displayName;
        [SerializeField] private Color factionColor = Color.white;
        [SerializeField] private Texture2D factionIcon;

        public string FactionId => factionId;
        public string DisplayName => displayName;
        public Color FactionColor => factionColor;
        public Texture2D FactionIcon => factionIcon;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(factionId))
                factionId = "faction_" + name.ToLower().Replace(" ", "_");
        }
    }
}