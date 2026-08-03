using System.Linq;
using UnityEngine;

public class ModelDatabase : ScriptableObject
{
    #region Model Variant

    [System.Serializable]
    public class ModelVariant
    {
        [Header("Identity")]
        public string modelId;
        public string displayName;

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

    #endregion

    #region Inspector

    [Header("Models")]
    [SerializeField] private ModelVariant[] models = new ModelVariant[0];

    [Header("Settings")]
    [SerializeField] private bool validateOnLoad = true;

    #endregion

    #region Properties

    public ModelVariant[] AllModels => models;

    #endregion

    #region Lookup

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

    #endregion

    #region Validation

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

    #endregion
}