using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrabThirdPerson.Character
{
    /// <summary>
    /// ModelModule - manages the entity's visible model, sockets, and equipped visual items.
    ///
    /// Equipment Visual Spawning:
    /// Subscribes to GameEvents.OnItemEquipped. When an item is equipped, reads
    /// slot.socketName and item.Definition.equippedPrefab, then calls EquipVisualItem
    /// to instantiate the prefab on the correct socket. On unequip (null item) calls
    /// ClearSocket. No direct reference to EquipmentSystem required.
    ///
    /// Prefab Transform:
    /// EquipVisualItem preserves the prefab's baked local position, rotation, and scale
    /// so weapon socket alignment authored on the prefab root is respected.
    /// </summary>
    public class ModelModule : MonoBehaviour, IPlayerModule, ISaveable
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

        private Dictionary<string, Transform> socketCache = new Dictionary<string, Transform>();
        private ControllerBrain brain;
        private bool isFullyInitialized = false;

        public event Action<ModelDatabase.ModelVariant> OnModelChanged;
        public event Action<string> OnModelChangeRequested;

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
                return;

            if (currentModel == null)
                DetectExistingModel();

            CacheStandardSockets();

            if (currentModel != null && modelAnimator == null)
                modelAnimator = currentModel.GetComponentInChildren<Animator>();

            GameEvents.OnItemEquipped += HandleItemEquipped;

            isFullyInitialized = true;
        }

        public void UpdateModule()
        {
            if (!isEnabled || !isFullyInitialized) return;

#if UNITY_EDITOR
            if (showDebugInfo)
                ValidateModelState();
#endif
        }

        #endregion

        #region Unity Callbacks

        private void OnDestroy()
        {
            GameEvents.OnItemEquipped -= HandleItemEquipped;
        }

        private void OnValidate()
        {
            if (currentModel != null && modelAnimator == null)
                modelAnimator = currentModel.GetComponentInChildren<Animator>();
        }

        #endregion

        #region Equipment Visual Handling

        /// <summary>
        /// Responds to GameEvents.OnItemEquipped.
        /// Spawns equippedPrefab on the slot's socket, or clears the socket on unequip.
        /// Slots with no socketName are silently skipped (rings, amulets, etc.).
        /// </summary>
        private void HandleItemEquipped(EquipmentSlotDefinition slot, ItemInstance item)
        {
            if (!isFullyInitialized) return;
            if (slot == null) return;
            if (string.IsNullOrEmpty(slot.socketName)) return;

            if (item == null)
            {
                ClearSocket(slot.socketName);

                if (showDebugInfo)
                    Debug.Log($"[ModelModule] Cleared socket '{slot.socketName}' (unequip)");

                return;
            }

            var prefab = item.Definition?.equippedPrefab;
            if (prefab == null)
            {
                if (showDebugInfo)
                    Debug.Log($"[ModelModule] '{item.Definition?.displayName}' has no equippedPrefab — skipping visual spawn");
                return;
            }

            bool spawned = EquipVisualItem(slot.socketName, prefab);

            if (showDebugInfo)
                Debug.Log($"[ModelModule] EquipVisualItem '{slot.socketName}' → '{prefab.name}': {(spawned ? "OK" : "socket not found")}");
        }

        #endregion

        #region Model Management

        public bool SwapModel(string newModelId, bool fromNetwork = false)
        {
            if (!isEnabled || modelDatabase == null)
            {
                Debug.LogWarning($"[ModelModule] Cannot swap model — module disabled or no database assigned");
                return false;
            }

            var newVariant = modelDatabase.GetModelById(newModelId);
            if (newVariant == null)
            {
                Debug.LogWarning($"[ModelModule] Model with ID '{newModelId}' not found in database");
                return false;
            }

            var currentEquipment = ExtractCurrentEquipment();

            if (currentModel != null)
            {
                if (Application.isPlaying)
                    Destroy(currentModel);
                else
                    DestroyImmediate(currentModel);
            }

            currentModel = Instantiate(newVariant.modelPrefab, transform);
            currentModel.name = newVariant.modelPrefab.name + " (Runtime)";
            currentModelId = newModelId;

            CacheStandardSockets();

            modelAnimator = currentModel.GetComponentInChildren<Animator>();
            brain.RefreshAnimatorReference();

            ReapplyEquipment(currentEquipment);

            OnModelChanged?.Invoke(newVariant);

            if (!fromNetwork && syncModelChanges)
                OnModelChangeRequested?.Invoke(newModelId);

            return true;
        }

        public bool SetRandomModelForFaction(FactionType faction, RaceType race = RaceType.Any)
        {
            if (modelDatabase == null) return false;

            var randomModel = modelDatabase.GetRandomModel(faction, race);
            if (randomModel != null)
                return SwapModel(randomModel.modelId);

            Debug.LogWarning($"[ModelModule] No models found for faction: {faction}, race: {race}");
            return false;
        }

        private void DetectExistingModel()
        {
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

        public Transform GetSocket(string socketName)
        {
            if (socketCache.TryGetValue(socketName.ToLower(), out Transform socket))
                return socket;

            if (showDebugInfo)
                Debug.LogWarning($"[ModelModule] Socket '{socketName}' not found in cache");
            return null;
        }

        public Transform GetWeaponSocket() => GetSocket("weapon");

        public string[] GetAvailableSocketNames()
        {
            var names = new string[socketCache.Count];
            socketCache.Keys.CopyTo(names, 0);
            return names;
        }

        private void CacheStandardSockets()
        {
            socketCache.Clear();

            if (currentModel == null)
            {
                Debug.LogWarning("[ModelModule] CacheStandardSockets called with no model");
                return;
            }

            var provider = currentModel.GetComponent<ModelSocketProvider>();
            if (provider != null)
            {
                foreach (var kvp in provider.GetAllSockets())
                    socketCache[kvp.Key] = kvp.Value;

                if (showDebugInfo)
                    Debug.Log($"[ModelModule] Cached {socketCache.Count} sockets from ModelSocketProvider");
            }
            else
            {
                Debug.LogWarning($"[ModelModule] No ModelSocketProvider found on '{currentModel.name}'. Add ModelSocketProvider to the model root prefab and assign socket references.");
            }
        }

        private void RegisterSocket(string socketName, Transform socket)
        {
            if (socket == null) return;
            socketCache[socketName.ToLower()] = socket;
        }

        public void RegisterSocket(string socketName, Transform socket, bool overwrite = false)
        {
            if (socket == null || string.IsNullOrEmpty(socketName)) return;
            if (!overwrite && socketCache.ContainsKey(socketName.ToLower())) return;
            socketCache[socketName.ToLower()] = socket;
        }

        #endregion

        #region Equipment Management

        /// <summary>
        /// Instantiates itemPrefab parented to the named socket.
        /// Preserves the prefab's baked local transform so socket alignment
        /// authored on the prefab root is respected.
        /// </summary>
        public bool EquipVisualItem(string socketName, GameObject itemPrefab)
        {
            var socket = GetSocket(socketName);
            if (socket == null || itemPrefab == null)
                return false;

            ClearSocket(socketName);

            var equipped = Instantiate(itemPrefab, socket);
            equipped.transform.localPosition = itemPrefab.transform.localPosition;
            equipped.transform.localRotation = itemPrefab.transform.localRotation;
            equipped.transform.localScale = itemPrefab.transform.localScale;

            return true;
        }

        public void ClearSocket(string socketName)
        {
            var socket = GetSocket(socketName);
            if (socket == null) return;

            for (int i = socket.childCount - 1; i >= 0; i--)
            {
                var child = socket.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private Dictionary<string, GameObject[]> ExtractCurrentEquipment()
        {
            var equipment = new Dictionary<string, GameObject[]>();

            foreach (var kvp in socketCache)
            {
                var socket = kvp.Value;
                if (socket == null) continue;

                var items = new GameObject[socket.childCount];
                for (int i = 0; i < socket.childCount; i++)
                    items[i] = socket.GetChild(i).gameObject;

                if (items.Length > 0)
                    equipment[kvp.Key] = items;
            }

            return equipment;
        }

        private void ReapplyEquipment(Dictionary<string, GameObject[]> equipment)
        {
            foreach (var kvp in equipment)
            {
                var socket = GetSocket(kvp.Key);
                if (socket == null) continue;

                foreach (var item in kvp.Value)
                {
                    if (item == null) continue;
                    item.transform.SetParent(socket);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                    item.transform.localScale = Vector3.one;
                }
            }
        }

        #endregion

        #region ISaveable

        public string GetSaveId() => "model";
        public int GetSaveVersion() => 1;

        public string GetSaveData()
        {
            return JsonUtility.ToJson(new ModelSaveData { modelId = currentModelId });
        }

        public void LoadSaveData(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonUtility.FromJson<ModelSaveData>(json);
            if (data == null || string.IsNullOrEmpty(data.modelId)) return;

            SwapModel(data.modelId);
        }

        [System.Serializable]
        private class ModelSaveData
        {
            public string modelId;
        }

        #endregion

        #region Multiplayer Support

        public PlayerSelectionData GetCurrentModelData()
        {
            return new PlayerSelectionData
            {
                selectedModelId = currentModelId,
                customColors = GetCurrentColors(),
                equipmentChoices = GetCurrentEquipment()
            };
        }

        public void ApplyModelData(PlayerSelectionData data)
        {
            if (!string.IsNullOrEmpty(data.selectedModelId))
                SwapModel(data.selectedModelId, fromNetwork: true);

            if (data.customColors != null)
                ApplyColorCustomization(data.customColors);
        }

        private void ApplyColorCustomization(Color[] colors)
        {
            // TODO: Implement color customization system
        }

        private Color[] GetCurrentColors()
        {
            return new Color[0];
        }

        private Dictionary<string, string> GetCurrentEquipment()
        {
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
                Debug.LogWarning("[ModelModule] No sockets cached — other modules may not function correctly");
        }

        private void OnDrawGizmosSelected()
        {
            if (!showDebugInfo || socketCache == null) return;

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