using System.Linq;
using UnityEngine;

namespace CrabSystem
{
    public class ModelDatabase : ScriptableObject
    {
        [System.Serializable]
        public class ModelVariant
        {
            [Header("Identity")]
            public string modelId;
            public string displayName;

            [Tooltip("Offered in character creation.")]
            public bool playable;

            [Header("Model")]
            public GameObject prefab;
            public Sprite thumbnailSprite;

            [Header("Customization")]
            public bool allowColorCustomization = true;
            public Color[] defaultColors = new Color[4];

            public bool IsValid()
            {
                return !string.IsNullOrEmpty(modelId) &&
                       !string.IsNullOrEmpty(displayName) &&
                       prefab != null;
            }
        }

        [Header("Models")]
        [SerializeField] private ModelVariant[] models = new ModelVariant[0];

        [Header("Settings")]
        [SerializeField] private bool validateOnLoad = true;

        public ModelVariant[] AllModels => models;

        public ModelVariant GetModel(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return null;

            return models.FirstOrDefault(m => m.modelId.Equals(modelId, System.StringComparison.OrdinalIgnoreCase));
        }

        public ModelVariant GetModelByIndex(int index)
        {
            if (index < 0 || index >= models.Length)
                return null;

            return models[index];
        }

        public bool ValidateDatabase()
        {
            if (!validateOnLoad)
                return true;

            foreach (var model in models)
            {
                if (!model.IsValid())
                    return false;
            }

            return true;
        }
    }
}
