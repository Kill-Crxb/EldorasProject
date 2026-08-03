using System;
using System.Collections.Generic;
using UnityEngine;
using RPG.Factions;

/// <summary>
/// ModelModule — reads sockets from ModelSocketProvider by slot ID.
/// 
/// When an item is equipped:
/// 1. EquipmentSystem fires GameEvents.OnItemEquipped(slot, item)
/// 2. ModelModule looks up the socket using slot.slotId (via ModelSocketProvider)
/// 3. If found, instantiates equippedPrefab under that socket
/// 
/// ModelSocketProvider is the bridge — it holds the slot-to-socket mappings.
/// No socketName string matching needed; everything keyed by slot ID.
/// </summary>
public class ModelModule : MonoBehaviour, IBrainModule, ISaveable
{
    #region Inspector

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Model")]
    [SerializeField] private GameObject currentModel;
    [SerializeField] private string currentModelId;
    [SerializeField] private Animator modelAnimator;

    [Header("Database")]
    [SerializeField] private ModelDatabase modelDatabase;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private ModelSocketProvider socketProvider;
    private Dictionary<string, Transform> socketCache = new Dictionary<string, Transform>();

    #endregion

    #region Events

    public event Action<string> OnModelChanged;

    #endregion

    #region Properties

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public GameObject CurrentModel => currentModel;
    public string CurrentModelId => currentModelId;
    public Animator ModelAnimator => modelAnimator;

    #endregion

    #region IBrainModule Implementation

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        if (!isEnabled)
            return;

        if (currentModel == null)
            DetectExistingModel();

        if (currentModel != null && modelAnimator == null)
            modelAnimator = currentModel.GetComponentInChildren<Animator>();

        CacheSocketsFromProvider();
        GameEvents.OnItemEquipped += HandleItemEquipped;
    }

    public void UpdateModule()
    {
    }

    public void LateInitialize()
    {
    }

    #endregion

    #region Model Management

    public bool SwapModel(string modelId, bool preserveEquipment = true)
    {
        if (string.IsNullOrEmpty(modelId))
            return false;

        if (modelDatabase == null)
            return false;

        var modelVariant = modelDatabase.GetModel(modelId);
        if (modelVariant == null)
            return false;

        if (currentModel != null)
            Destroy(currentModel);

        currentModel = Instantiate(modelVariant.prefab, transform);
        currentModelId = modelId;

        modelAnimator = currentModel.GetComponentInChildren<Animator>();
        if (brain != null)
        {
            brain.RefreshAnimatorReference();
            brain.SetAnimatorDirect(modelAnimator);
        }

        CacheSocketsFromProvider();
        OnModelChanged?.Invoke(modelId);

        return true;
    }

    #endregion

    #region Socket Management

    /// <summary>
    /// Reads sockets from ModelSocketProvider on the current model.
    /// ModelSocketProvider has a list of slot assets mapped to bone transforms.
    /// We cache all of them for O(1) lookup during equipment changes.
    /// </summary>
    private void CacheSocketsFromProvider()
    {
        socketCache.Clear();
        socketProvider = null;

        if (currentModel == null)
            return;

        socketProvider = currentModel.GetComponent<ModelSocketProvider>();
        if (socketProvider == null)
        {
            Debug.LogWarning($"[ModelModule] Model '{currentModelId}' has no ModelSocketProvider. Add the component and map slots to sockets.");
            return;
        }

        socketCache = socketProvider.GetAllSockets();

        if (socketCache.Count == 0)
            Debug.LogWarning($"[ModelModule] Model '{currentModelId}' has no slot-socket mappings in ModelSocketProvider.");
    }

    /// <summary>
    /// Gets a socket transform by slot ID.
    /// Returns null if not found.
    /// </summary>
    public Transform GetSocket(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return null;

        if (socketCache.TryGetValue(slotId.ToLower(), out var socket))
            return socket;

        return null;
    }

    #endregion

    #region Equipment Visual Handling

    /// <summary>
    /// Called when EquipmentSystem fires GameEvents.OnItemEquipped.
    /// Uses slot.slotId to look up the socket in the cache.
    /// Spawns equippedPrefab under the socket.
    /// </summary>
    private void HandleItemEquipped(EquipmentSlotDefinition slot, ItemInstance item)
    {
        if (slot == null || currentModel == null)
            return;

        if (item == null)
        {
            ClearSocket(slot.slotId);
            return;
        }

        if (item.Definition == null || item.Definition.equippedPrefab == null)
            return;

        var socket = GetSocket(slot.slotId);
        if (socket == null)
        {
            // This is normal — not all slots need visuals (e.g., rings, storage)
            // Only log if this is a weapon or armor slot that should have a visual
            if (slot.isWeaponSlot || slot.socketName != "" || slot.slotId.Contains("armor") || slot.slotId.Contains("helmet"))
                Debug.LogWarning($"[ModelModule] No socket mapped for slot '{slot.slotId}' ({slot.displayName}). Add it to ModelSocketProvider.slotSockets.");
            return;
        }

        var visual = Instantiate(item.Definition.equippedPrefab, socket);
        visual.name = item.Definition.displayName;
    }

    /// <summary>
    /// Clears all children from a socket (unequip visual).
    /// </summary>
    private void ClearSocket(string slotId)
    {
        var socket = GetSocket(slotId);
        if (socket == null)
            return;

        foreach (Transform child in socket)
            Destroy(child.gameObject);
    }

    #endregion

    #region Model Detection

    private void DetectExistingModel()
    {
        var modelRoot = transform.Find("3D Model") ??
                       transform.Find("Model") ??
                       transform.Find("Visual");

        if (modelRoot != null)
            currentModel = modelRoot.gameObject;
    }

    #endregion

    #region ISaveable Implementation

    public string GetSaveId() => "model";

    public string GetSaveData()
    {
        var data = new ModelSaveData
        {
            currentModelId = currentModelId
        };

        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        var data = JsonUtility.FromJson<ModelSaveData>(json);
        if (!string.IsNullOrEmpty(data.currentModelId))
            SwapModel(data.currentModelId);
    }

    public int GetSaveVersion() => 1;

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        GameEvents.OnItemEquipped -= HandleItemEquipped;
    }

    #endregion
}

[System.Serializable]
public class ModelSaveData
{
    public string currentModelId;
}