using System;
using System.Collections.Generic;
using UnityEngine;
using RPG.Factions;

/// <summary>
/// ModelModule — reads sockets from ModelSocketProvider by slot ID.
/// 
/// When an item is equipped:
/// 1. This entity's EquipmentSystem fires OnEquipmentVisual(slot, item)
/// 2. ModelModule looks up the socket using slot.slotId (via ModelSocketProvider)
/// 3. If found, instantiates equippedPrefab under that socket
/// 
/// ModelSocketProvider is the bridge — it holds the slot-to-socket mappings.
/// No socketName string matching needed; everything keyed by slot ID.
/// </summary>
public class ModelModule : MonoBehaviour, IBrainModule, ISaveable
{
    public int InitOrder => 20;

    #region Inspector

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Model")]
    [SerializeField] private GameObject currentModel;
    [SerializeField] private string currentModelId;
    [SerializeField] private Animator modelAnimator;

    [Header("Database")]
    [SerializeField] private ModelDatabase modelDatabase;

    [Header("Sockets")]
    [Tooltip("Cancel the socket bone's inherited scale so equipment ends up the size it was " +
             "authored at. Rigs commonly arrive at scale 100 from an FBX imported without unit " +
             "conversion and every bone inherits it, so parenting a sword to such a hand makes " +
             "the sword a hundred times too big.\n\n" +
             "Untick only for equipment that should genuinely grow with the creature carrying it.")]
    [SerializeField] private bool normaliseEquipmentScale = true;

    #endregion

    #region Private Fields

    private ControllerBrain brain;
    private EquipmentSystem equipment;
    private ModelSocketProvider socketProvider;
    private Dictionary<string, Transform> socketCache = new Dictionary<string, Transform>();

    // The visual each slot spawned, so it can be found and cleared after something has moved it to
    // another socket (a sheath, or a clip's socket events). Clearing the socket would miss it.
    private readonly Dictionary<string, GameObject> equippedVisuals = new Dictionary<string, GameObject>();

    // Which option each creation group shows. Owned here and saved in model.json.
    private List<AppearanceChoice> appearance = new List<AppearanceChoice>();

    // Slot → the appearance group its item drives (clothes in the model's mesh). Equipment owns these.
    private readonly Dictionary<string, string> equippedLooks = new Dictionary<string, string>();

    #endregion

    #region Events

    public event Action<string> OnModelChanged;

    #endregion

    #region Properties

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public GameObject CurrentModel => currentModel;
    public string CurrentModelId => currentModelId;
    public Animator ModelAnimator => modelAnimator;
    public const string SaveId = "model";

    // 2: appearance choices. A version 1 file has none and loads every group at its first option.
    public const int SaveVersion = 2;

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
    }

    public void UpdateModule()
    {
    }

    public void LateInitialize()
    {
        equipment = brain?.GetModule<EquipmentSystem>();
        if (equipment != null)
            equipment.OnEquipmentVisual += HandleItemEquipped;

        brain.OnLoaded += HandleLoaded;

        BindParts();
    }

    // A model loaded from the save (model.json loads before equipment.json) puts back whatever was
    // equipped at that moment: the prefab's inspector loadout, not the save's. The equipment load is
    // quiet and its broadcast only covers occupied slots, so once the character has loaded, drop any
    // visual whose slot ended up empty.
    private void HandleLoaded()
    {
        brain.OnLoaded -= HandleLoaded;
        if (equipment == null)
            return;

        var emptySlots = new List<string>();
        foreach (var pair in equippedVisuals)
            if (equipment.GetEquippedItem(pair.Key) == null)
                emptySlots.Add(pair.Key);

        foreach (var pair in equippedLooks)
            if (equipment.GetEquippedItem(pair.Key) == null && !emptySlots.Contains(pair.Key))
                emptySlots.Add(pair.Key);

        foreach (string slotId in emptySlots)
            ClearSocket(slotId);
    }

    #endregion

    #region Model Management

    public bool SwapModel(string modelId, bool preserveEquipment = true)
    {
        if (string.IsNullOrEmpty(modelId))
            return false;

        if (modelDatabase == null)
            return false;

        // Already wearing it: a second load path naming the same model is a no-op.
        if (currentModel != null && modelId == currentModelId)
            return true;

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
        if (preserveEquipment)
            ReattachEquipment();
        ApplyAppearance();
        BindParts();
        OnModelChanged?.Invoke(modelId);

        return true;
    }

    // Parts on the model that need this character's modules (the socket relay) get them here, on
    // every new model. A model with no brain (the creation preview) is never bound.
    private void BindParts()
    {
        if (currentModel == null || brain == null)
            return;

        foreach (var part in currentModel.GetComponentsInChildren<IModelPart>(true))
            part.Bind(brain, this);
    }

    private void ApplyAppearance()
    {
        if (currentModel == null)
            return;

        var parts = currentModel.GetComponent<ModelAppearance>();
        if (parts != null)
            parts.Apply(appearance);
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

    /// <summary>
    /// Gets a non-slot socket (sheath point, prop mount) by its free-form id.
    /// Returns null if the model has no such socket.
    /// </summary>
    public Transform GetNamedSocket(string socketId)
    {
        if (socketProvider == null)
            return null;

        return socketProvider.GetNamedSocket(socketId);
    }

    public Transform GetEquippedVisual(string slotId)
    {
        if (string.IsNullOrEmpty(slotId))
            return null;

        if (!equippedVisuals.TryGetValue(slotId, out var visual) || visual == null)
            return null;

        return visual.transform;
    }

    /// <summary>
    /// Reparents everything under one socket to another, zeroing the local transform so the
    /// item sits on the new bone rather than keeping its old offset. Used by the stance
    /// toggle to move a weapon between the hand and its sheath.
    ///
    /// World scale is carried across deliberately. SetParent(to, false) keeps localScale, so
    /// two bones scaled differently — a hand and a spine on the same rig often are — would
    /// resize the weapon every time it was drawn or sheathed. Capturing lossyScale and
    /// restoring it means the sword is the same sword on the hip as in the hand.
    /// </summary>
    public static void MoveSocketContents(Transform from, Transform to)
    {
        if (from == null || to == null) return;

        for (int i = from.childCount - 1; i >= 0; i--)
            PlaceInSocket(from.GetChild(i), to);
    }

    // One item onto one socket, by the same rules: local zero, world scale kept.
    public static void PlaceInSocket(Transform item, Transform socket)
    {
        if (item == null || socket == null) return;

        Vector3 worldScale = item.lossyScale;

        item.SetParent(socket, false);
        item.localPosition = Vector3.zero;
        item.localRotation = Quaternion.identity;

        SocketScale.Normalise(item, worldScale);
    }

    #endregion

    #region Equipment Visual Handling

    /// <summary>
    /// Called when this entity's EquipmentSystem fires OnEquipmentVisual.
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

        if (item.Definition == null)
            return;

        ClearSocket(slot.slotId);
        ShowLook(slot.slotId, item.Definition);

        if (item.Definition.equippedPrefab == null)
            return;

        // A model with no socket map can't hold anything, and CacheSocketsFromProvider already said so.
        if (socketProvider == null)
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
        equippedVisuals[slot.slotId] = visual;

        // Instantiate-with-parent keeps the prefab's LOCAL scale, so the final size is
        // socket.lossyScale x prefab.localScale. On a rig that imported at 100 that is a
        // hundred-times-too-big sword, and it is not the prefab's fault — see SocketScale.
        if (normaliseEquipmentScale)
            SocketScale.Normalise(visual.transform, item.Definition.equippedPrefab.transform.localScale);
    }

    /// <summary>
    /// A new model has empty sockets. Put back whatever is equipped — an NPC's natural weapon is
    /// equipped at Initialize, before its config has loaded any model to hang it on.
    /// </summary>
    private void ReattachEquipment()
    {
        // A model with no socket map can't hold anything; asking would only warn per slot.
        if (equipment == null || socketProvider == null) return;

        foreach (var pair in equipment.GetAllEquippedItems())
            HandleItemEquipped(equipment.GetSlotDefinition(pair.Key), pair.Value);
    }

    // An item can be part of the model's own mesh (an outfit): it names an appearance group and option
    // instead of, or as well as, a prefab. A model without that group ignores it.
    private void ShowLook(string slotId, ItemDefinition definition)
    {
        if (string.IsNullOrEmpty(definition.appearanceGroup))
            return;

        var parts = currentModel.GetComponent<ModelAppearance>();
        if (parts != null && parts.Show(definition.appearanceGroup, definition.appearanceOption))
            equippedLooks[slotId] = definition.appearanceGroup;
    }

    private void ClearLook(string slotId)
    {
        if (!equippedLooks.TryGetValue(slotId, out var groupId))
            return;

        equippedLooks.Remove(slotId);

        var parts = currentModel != null ? currentModel.GetComponent<ModelAppearance>() : null;
        if (parts != null)
            parts.ShowDefault(groupId);
    }

    private void ClearSocket(string slotId)
    {
        ClearLook(slotId);

        if (!equippedVisuals.TryGetValue(slotId, out var visual))
            return;

        equippedVisuals.Remove(slotId);
        if (visual != null)
            Destroy(visual);
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

    public string GetSaveId() => SaveId;

    public string GetSaveData()
    {
        var data = new ModelSaveData
        {
            version = GetSaveVersion(),
            currentModelId = currentModelId,
            appearance = appearance
        };

        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        var data = JsonUtility.FromJson<ModelSaveData>(json);
        appearance = data.appearance ?? new List<AppearanceChoice>();

        if (!string.IsNullOrEmpty(data.currentModelId))
            SwapModel(data.currentModelId);

        // SwapModel returns early when the model is already on; the look still has to land.
        ApplyAppearance();
    }

    public int GetSaveVersion() => SaveVersion;
    public int LoadOrder => 20;

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (equipment != null)
            equipment.OnEquipmentVisual -= HandleItemEquipped;

        if (brain != null)
            brain.OnLoaded -= HandleLoaded;
    }

    #endregion
}

[System.Serializable]
public class ModelSaveData
{
    public int version;
    public string currentModelId;
    public List<AppearanceChoice> appearance = new List<AppearanceChoice>();
}