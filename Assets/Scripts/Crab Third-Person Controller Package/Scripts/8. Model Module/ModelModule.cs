using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrabThirdPerson.Character
{
    public class ModelModule : MonoBehaviour, IPlayerModule
    {
        [Header("Module Settings")]
        [SerializeField] private bool isEnabled = true;
        [SerializeField] private bool showDebugInfo = false;

        [Header("Current Model")]
        [SerializeField] private GameObject currentModel;
        [SerializeField] private string currentModelId;
        [SerializeField] private Animator modelAnimator;

        [Header("Model Database")]
        [SerializeField] private ModelDatabase modelDatabase;

        [Header("Network Settings")]
        [SerializeField] private bool syncModelChanges = true;

        // Cached socket references
        private Dictionary<string, Transform> socketCache = new Dictionary<string, Transform>();
        private ControllerBrain brain;
        private bool isFullyInitialized = false;

        // Events
        public event Action<ModelDatabase.ModelVariant> OnModelChanged;
        public event Action<string> OnModelChangeRequested; // For network sync
       // public event Action<ModelCustomization> OnCustomizationChanged;

        // Properties
        public bool IsEnabled
        {
            get => isEnabled;
            set => isEnabled = value;
        }
        public GameObject CurrentModel => currentModel;
        public string CurrentModelId => currentModelId;
        public Animator ModelAnimator => modelAnimator;
        public bool IsFullyInitialized => isFullyInitialized;

        #region IPlayerModule Implementation

        public void Initialize(ControllerBrain brain)
        {
            this.brain = brain;

            if (!isEnabled)
            {
                return;
            }

            // If no model is assigned, try to find existing model in hierarchy
            if (currentModel == null)
                DetectExistingModel();

            // Apply default socket configuration if available
            CacheStandardSockets();

            // Cache animator reference
            if (currentModel != null && modelAnimator == null)
                modelAnimator = currentModel.GetComponentInChildren<Animator>();

            isFullyInitialized = true;
        }

        public void UpdateModule()
        {
            if (!isEnabled || !isFullyInitialized) return;

            // Model module is primarily event-driven and doesn't need constant updates
            // But we can perform validation checks here if needed

#if UNITY_EDITOR
            if (showDebugInfo)
                ValidateModelState();
#endif
        }

        #endregion

        #region Model Management

        /// <summary>
        /// Swaps the current model with a new one from the database
        /// </summary>
        public bool SwapModel(string newModelId, bool fromNetwork = false)
        {
            if (!isEnabled || modelDatabase == null)
            {
                Debug.LogWarning($"[ModelModule] Cannot swap model - module disabled or no database assigned");
                return false;
            }

            var newVariant = modelDatabase.GetModelById(newModelId);
            if (newVariant == null)
            {
                Debug.LogWarning($"[ModelModule] Model with ID '{newModelId}' not found in database");
                return false;
            }

            // Store current equipment before swapping
            var currentEquipment = ExtractCurrentEquipment();

            // Destroy old model
            if (currentModel != null)
            {
                if (Application.isPlaying)
                    Destroy(currentModel);
                else
                    DestroyImmediate(currentModel);
            }

            // Instantiate new model as child of this component
            currentModel = Instantiate(newVariant.modelPrefab, transform);
            currentModel.name = newVariant.modelPrefab.name + " (Runtime)";
            currentModelId = newModelId;

            // Cache new sockets
            CacheStandardSockets();

            // Update animator reference
            modelAnimator = currentModel.GetComponentInChildren<Animator>();

            // Re-apply equipment
            ReapplyEquipment(currentEquipment);

            // Notify other modules of the model change
            OnModelChanged?.Invoke(newVariant);

            // Network synchronization
            if (!fromNetwork && syncModelChanges)
                OnModelChangeRequested?.Invoke(newModelId);

            return true;
        }

        /// <summary>
        /// Sets a random model based on faction and race
        /// </summary>
        public bool SetRandomModelForFaction(FactionType faction, RaceType race = RaceType.Any)
        {
            if (modelDatabase == null) return false;

            var randomModel = modelDatabase.GetRandomModel(faction, race);
            if (randomModel != null)
                return SwapModel(randomModel.modelId);

            Debug.LogWarning($"[ModelModule] No models found for faction: {faction}, race: {race}");
            return false;
        }

        /// <summary>
        /// Detects existing model in the hierarchy (for backward compatibility)
        /// </summary>
        private void DetectExistingModel()
        {
            // Look for existing model with animator
            var existingAnimator = GetComponentInChildren<Animator>();
            if (existingAnimator != null)
            {
                currentModel = existingAnimator.gameObject;
                modelAnimator = existingAnimator;
                currentModelId = "existing_model";
            }
        }

        #endregion

        #region Socket Management

        /// <summary>
        /// Gets a socket transform by name
        /// </summary>
        public Transform GetSocket(string socketName)
        {
            if (socketCache.TryGetValue(socketName.ToLower(), out Transform socket))
                return socket;

            Debug.LogWarning($"[ModelModule] Socket '{socketName}' not found in cache");
            return null;
        }

        /// <summary>
        /// Gets the weapon socket (convenience method)
        /// </summary>
        public Transform GetWeaponSocket() => GetSocket("weapon");

        /// <summary>
        /// Gets all available socket names
        /// </summary>
        public string[] GetAvailableSocketNames()
        {
            var names = new string[socketCache.Count];
            socketCache.Keys.CopyTo(names, 0);
            return names;
        }

        /// <summary>
        /// Caches standard socket references (fallback method)
        /// </summary>
        private void CacheStandardSockets()
        {
            socketCache.Clear();

            if (currentModel == null) return;

            // Standard socket paths
            CacheSocket("weapon", "Armature/Hand/WeaponSocket");
            CacheSocket("shield", "Armature/Hand_L/ShieldSocket");
            CacheSocket("helmet", "Armature/Head/HelmetSocket");
            CacheSocket("chest", "Armature/Spine/ChestSocket");
            CacheSocket("boots", "Armature/Foot/BootsSocket");
            CacheSocket("feeteffects", "Armature/Foot/FeetEffects");
            CacheSocket("backeffects", "Armature/Spine/BackEffects");
        }

        /// <summary>
        /// Caches a single socket by path
        /// </summary>
        private void CacheSocket(string socketName, string socketPath)
        {
            if (string.IsNullOrEmpty(socketPath)) return;

            var socket = FindChildByPath(socketPath);
            if (socket != null)
            {
                socketCache[socketName.ToLower()] = socket;
            }
            else if (showDebugInfo)
            {
                Debug.LogWarning($"[ModelModule] Socket '{socketName}' not found at path: {socketPath}");
            }
        }

        /// <summary>
        /// Finds a child transform by hierarchical path
        /// </summary>
        private Transform FindChildByPath(string path)
        {
            if (currentModel == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('/');
            Transform current = currentModel.transform;

            foreach (var part in parts)
            {
                current = current.Find(part);
                if (current == null)
                    break;
            }

            return current;
        }

        #endregion

        #region Equipment Management

        /// <summary>
        /// Equips a visual item to a specific socket
        /// </summary>
        public bool EquipVisualItem(string socketName, GameObject itemPrefab)
        {
            var socket = GetSocket(socketName);
            if (socket == null || itemPrefab == null)
                return false;

            // Clear existing equipment in socket
            ClearSocket(socketName);

            // Instantiate new equipment
            var equipment = Instantiate(itemPrefab, socket);
            equipment.transform.localPosition = Vector3.zero;
            equipment.transform.localRotation = Quaternion.identity;
            equipment.transform.localScale = Vector3.one;

            return true;
        }

        /// <summary>
        /// Clears all items from a socket
        /// </summary>
        public void ClearSocket(string socketName)
        {
            var socket = GetSocket(socketName);
            if (socket == null) return;

            // Destroy all children in the socket
            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                var child = socket.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        /// <summary>
        /// Extracts current equipment before model swap
        /// </summary>
        private Dictionary<string, GameObject[]> ExtractCurrentEquipment()
        {
            var equipment = new Dictionary<string, GameObject[]>();

            foreach (var kvp in socketCache)
            {
                var socket = kvp.Value;
                if (socket == null) continue;

                var items = new GameObject[socket.childCount];
                for (int i = 0; i < socket.childCount; i++)
                {
                    items[i] = socket.GetChild(i).gameObject;
                }

                if (items.Length > 0)
                    equipment[kvp.Key] = items;
            }

            return equipment;
        }

        /// <summary>
        /// Re-applies equipment after model swap
        /// </summary>
        private void ReapplyEquipment(Dictionary<string, GameObject[]> equipment)
        {
            foreach (var kvp in equipment)
            {
                var socketName = kvp.Key;
                var items = kvp.Value;
                var socket = GetSocket(socketName);

                if (socket != null)
                {
                    foreach (var item in items)
                    {
                        if (item != null)
                        {
                            item.transform.SetParent(socket);
                            item.transform.localPosition = Vector3.zero;
                            item.transform.localRotation = Quaternion.identity;
                            item.transform.localScale = Vector3.one;
                        }
                    }
                }
            }
        }

        #endregion

        #region Multiplayer Support

        /// <summary>
        /// Gets current model data for network synchronization
        /// </summary>
        public PlayerSelectionData GetCurrentModelData()
        {
            return new PlayerSelectionData
            {
                selectedModelId = currentModelId,
                customColors = GetCurrentColors(),
                equipmentChoices = GetCurrentEquipment()
            };
        }

        /// <summary>
        /// Applies model data from network
        /// </summary>
        public void ApplyModelData(PlayerSelectionData data)
        {
            if (!string.IsNullOrEmpty(data.selectedModelId))
                SwapModel(data.selectedModelId, fromNetwork: true);

            // Apply customizations
            if (data.customColors != null)
                ApplyColorCustomization(data.customColors);

            // Apply equipment
            if (data.equipmentChoices != null)
            {
                foreach (var equipment in data.equipmentChoices)
                {
                    // This would need equipment database lookup
                    // EquipVisualItem(equipment.Key, equipment.Value);
                }
            }
        }

        /// <summary>
        /// Placeholder for color customization
        /// </summary>
        private void ApplyColorCustomization(Color[] colors)
        {
            // TODO: Implement color customization system
        }

        /// <summary>
        /// Placeholder for getting current colors
        /// </summary>
        private Color[] GetCurrentColors()
        {
            // TODO: Implement current color extraction
            return new Color[0];
        }

        /// <summary>
        /// Placeholder for getting current equipment
        /// </summary>
        private Dictionary<string, string> GetCurrentEquipment()
        {
            // TODO: Implement equipment ID extraction
            return new Dictionary<string, string>();
        }

        #endregion

        #region Debug and Validation

#if UNITY_EDITOR
        private void ValidateModelState()
        {
            if (currentModel == null)
            {
                Debug.LogWarning("[ModelModule] No current model assigned");
                return;
            }

            if (modelAnimator == null)
                Debug.LogWarning("[ModelModule] No animator found on current model");

            if (socketCache.Count == 0)
                Debug.LogWarning("[ModelModule] No sockets cached - other modules may not function correctly");
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo || socketCache == null) return;

            // Draw socket positions
            Gizmos.color = Color.yellow;
            foreach (var kvp in socketCache)
            {
                if (kvp.Value != null)
                {
                    Gizmos.DrawWireSphere(kvp.Value.position, 0.05f);
                    UnityEditor.Handles.Label(kvp.Value.position, kvp.Key);
                }
            }
        }
#endif

        #endregion

        #region Unity Callbacks

        private void OnValidate()
        {
            // Ensure we have references in the editor
            if (currentModel != null && modelAnimator == null)
                modelAnimator = currentModel.GetComponentInChildren<Animator>();
        }

        #endregion
    }

    #region Supporting Data Structures

    [System.Serializable]
    public class PlayerSelectionData
    {
        public string playerName;
        public string selectedModelId;
        public FactionType faction;
        public Color[] customColors;
        public Dictionary<string, string> equipmentChoices;

        public PlayerSelectionData()
        {
            customColors = new Color[0];
            equipmentChoices = new Dictionary<string, string>();
        }
    }

    [System.Serializable]
    public class ModelCustomization
    {
        public MaterialChange[] materialChanges;
        public BoneScale[] boneScales;
    }

    [System.Serializable]
    public class MaterialChange
    {
        public string rendererPath;
        public Material material;
    }

    [System.Serializable]
    public class BoneScale
    {
        public string bonePath;
        public Vector3 scale;
    }

    // Enums (you may need to define these based on your game design)
    public enum FactionType
    {
        None,
        Alliance,
        Horde,
        Neutral
    }

    public enum RaceType
    {
        Any,
        Human,
        Elf,
        Dwarf,
        Orc,
        Undead
    }

    #endregion
}