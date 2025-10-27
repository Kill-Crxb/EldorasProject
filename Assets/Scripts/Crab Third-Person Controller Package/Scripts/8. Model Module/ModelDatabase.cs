using System.Linq;
using UnityEngine;

namespace CrabThirdPerson.Character
{
    [CreateAssetMenu(fileName = "ModelDatabase", menuName = "Character/Model Database")]
    public class ModelDatabase : ScriptableObject
    {
        [System.Serializable]
        public class ModelVariant
        {
            [Header("Basic Info")]
            public string modelId;
            public string displayName;
            [TextArea(2, 4)]
            public string description;

            [Header("Model Data")]
            public GameObject modelPrefab;
            public Sprite thumbnailSprite;

            [Header("Classification")]
            public FactionType faction = FactionType.Alliance;
            public RaceType race = RaceType.Human;
            public ClassType characterClass = ClassType.Soldier;

            [Header("Configuration")]
            // TODO: Add socket configuration support later
            // public ModelSocketConfig socketConfig;

            [Header("Customization")]
            public bool allowColorCustomization = true;
            public Color[] defaultColors = new Color[4];

            // Validation
            public bool IsValid()
            {
                return !string.IsNullOrEmpty(modelId) &&
                       !string.IsNullOrEmpty(displayName) &&
                       modelPrefab != null;
            }
        }

        [Header("Available Models")]
        [SerializeField] private ModelVariant[] models = new ModelVariant[0];

        [Header("Default Settings")]
        // TODO: Add socket configuration support later
        // [SerializeField] private ModelSocketConfig defaultSocketConfig;
        [SerializeField] private bool validateOnLoad = true;

        public ModelVariant[] AllModels => models;

        #region Model Lookup Methods

        /// <summary>
        /// Gets a model by its ID
        /// </summary>
        public ModelVariant GetModelById(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return null;

            return models.FirstOrDefault(m => m.modelId.Equals(modelId, System.StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets all models for a specific faction
        /// </summary>
        public ModelVariant[] GetModelsForFaction(FactionType faction)
        {
            return models.Where(m => m.faction == faction && m.IsValid()).ToArray();
        }

        /// <summary>
        /// Gets all models for a specific race
        /// </summary>
        public ModelVariant[] GetModelsForRace(RaceType race)
        {
            return models.Where(m => m.race == race && m.IsValid()).ToArray();
        }

        /// <summary>
        /// Gets all models for a specific class
        /// </summary>
        public ModelVariant[] GetModelsForClass(ClassType characterClass)
        {
            return models.Where(m => m.characterClass == characterClass && m.IsValid()).ToArray();
        }

        /// <summary>
        /// Gets models matching multiple criteria
        /// </summary>
        public ModelVariant[] GetModels(FactionType faction = FactionType.None,
                                       RaceType race = RaceType.Any,
                                       ClassType characterClass = ClassType.None)
        {
            var query = models.Where(m => m.IsValid());

            if (faction != FactionType.None)
                query = query.Where(m => m.faction == faction);

            if (race != RaceType.Any)
                query = query.Where(m => m.race == race);

            if (characterClass != ClassType.None)
                query = query.Where(m => m.characterClass == characterClass);

            return query.ToArray();
        }

        /// <summary>
        /// Gets a random model from the database
        /// </summary>
        public ModelVariant GetRandomModel()
        {
            var validModels = models.Where(m => m.IsValid()).ToArray();
            if (validModels.Length == 0)
                return null;

            return validModels[Random.Range(0, validModels.Length)];
        }

        /// <summary>
        /// Gets a random model matching criteria
        /// </summary>
        public ModelVariant GetRandomModel(FactionType faction, RaceType race = RaceType.Any)
        {
            var candidates = GetModels(faction, race);
            if (candidates.Length == 0)
                return null;

            return candidates[Random.Range(0, candidates.Length)];
        }

        #endregion

        #region Database Management

        /// <summary>
        /// Adds a new model to the database (Editor only)
        /// </summary>
        public void AddModel(ModelVariant newModel)
        {
#if UNITY_EDITOR
            if (newModel == null || !newModel.IsValid())
            {
                Debug.LogWarning("[ModelDatabase] Cannot add invalid model");
                return;
            }

            if (GetModelById(newModel.modelId) != null)
            {
                Debug.LogWarning($"[ModelDatabase] Model with ID '{newModel.modelId}' already exists");
                return;
            }

            var modelList = models.ToList();
            modelList.Add(newModel);
            models = modelList.ToArray();

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ModelDatabase] Added model: {newModel.displayName} ({newModel.modelId})");
#endif
        }

        /// <summary>
        /// Removes a model from the database (Editor only)
        /// </summary>
        public bool RemoveModel(string modelId)
        {
#if UNITY_EDITOR
            var modelToRemove = GetModelById(modelId);
            if (modelToRemove == null)
                return false;

            var modelList = models.ToList();
            modelList.Remove(modelToRemove);
            models = modelList.ToArray();

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[ModelDatabase] Removed model: {modelToRemove.displayName} ({modelId})");
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Validates all models in the database
        /// </summary>
        public void ValidateDatabase()
        {
            int validCount = 0;
            int invalidCount = 0;

            foreach (var model in models)
            {
                if (model.IsValid())
                {
                    validCount++;
                }
                else
                {
                    invalidCount++;
                    Debug.LogWarning($"[ModelDatabase] Invalid model found: {model.displayName} ({model.modelId})");

                    if (string.IsNullOrEmpty(model.modelId))
                        Debug.LogWarning("  - Missing modelId");
                    if (string.IsNullOrEmpty(model.displayName))
                        Debug.LogWarning("  - Missing displayName");
                    if (model.modelPrefab == null)
                        Debug.LogWarning("  - Missing modelPrefab");
                }
            }

            //Debug.Log($"[ModelDatabase] Validation complete: {validCount} valid, {invalidCount} invalid models");
        }

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            if (validateOnLoad)
                ValidateDatabase();
        }

        private void OnValidate()
        {
            // Ensure all models have unique IDs
            var duplicateIds = models.GroupBy(m => m.modelId)
                                   .Where(g => g.Count() > 1)
                                   .Select(g => g.Key);

            foreach (var duplicateId in duplicateIds)
            {
                Debug.LogWarning($"[ModelDatabase] Duplicate model ID found: {duplicateId}");
            }
        }

        #endregion

        #region Editor Helpers

#if UNITY_EDITOR
        /// <summary>
        /// Auto-populates database from prefabs in a folder (Editor only)
        /// </summary>
        [ContextMenu("Auto-Populate From Folder")]
        public void AutoPopulateFromFolder()
        {
            string folderPath = UnityEditor.EditorUtility.OpenFolderPanel("Select Model Prefab Folder", "Assets", "");
            if (string.IsNullOrEmpty(folderPath))
                return;

            // Convert absolute path to relative path
            if (folderPath.StartsWith(Application.dataPath))
            {
                folderPath = "Assets" + folderPath.Substring(Application.dataPath.Length);
            }

            var prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
            int addedCount = 0;

            foreach (var guid in prefabGuids)
            {
                var prefabPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab != null && prefab.GetComponentInChildren<Animator>() != null)
                {
                    var fileName = System.IO.Path.GetFileNameWithoutExtension(prefabPath);

                    // Skip if model already exists
                    if (GetModelById(fileName) != null)
                        continue;

                    var newModel = new ModelVariant
                    {
                        modelId = fileName,
                        displayName = fileName.Replace("_", " "),
                        description = $"Auto-generated from {prefabPath}",
                        modelPrefab = prefab
                        // TODO: Add socket config when implemented
                        // socketConfig = defaultSocketConfig
                    };

                    AddModel(newModel);
                    addedCount++;
                }
            }

            Debug.Log($"[ModelDatabase] Auto-populated {addedCount} models from {folderPath}");
        }
#endif

        #endregion
    }

    // Additional enum for character classes
    public enum ClassType
    {
        None,
        Soldier,
        Mage,
        Rogue,
        Cleric,
        Warrior,
        Archer
    }
}