using UnityEngine;
using System;
using RPG.Factions;

/// <summary>
/// Object Context Module - gathers object-specific identity data
/// 
/// Responsibilities:
/// - Manage interaction state (interactable, locked, used)
/// - Handle interaction types (one-time, toggle, continuous)
/// - Lock/unlock logic with key requirements
/// - Set appropriate faction for objects
/// </summary>
public class ObjectContextModule : MonoBehaviour, IContextModule
{
    [Header("Object Configuration")]
    [SerializeField] private ObjectType objectType = ObjectType.Chest;
    [SerializeField] private InteractionType interactionType = InteractionType.OneTime;

    [Header("Interaction Settings")]
    [SerializeField] private bool isInteractable = true;
    [SerializeField] private bool isLocked = false;
    [SerializeField] private string requiredKeyId;
    [SerializeField] private float interactionRange = 2f;

    [Header("State Tracking")]
    [SerializeField] private bool hasBeenInteracted = false;
    [SerializeField] private bool currentState = false; // For toggles (on/off)

    [Header("Visual Feedback")]
    [SerializeField] private GameObject lockedIndicatorPrefab;
    [SerializeField] private GameObject interactableHighlightPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private IdentitySystem parent;
    private GameObject lockedIndicator;
    private GameObject highlightInstance;

    // Events
    public event Action<ControllerBrain> OnInteracted; // Actor who interacted
    public event Action<bool> OnStateChanged; // For toggles

    // Properties
    public bool IsInteractable => isInteractable && !isLocked;
    public bool IsLocked => isLocked;
    public bool HasBeenInteracted => hasBeenInteracted;
    public bool CurrentState => currentState;
    public ObjectType Type => objectType;
    public InteractionType InteractionMode => interactionType;

    #region IContextModule Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        // Set entity type to Prop
        if (parent.Identity != null)
        {
            parent.Identity.Type = EntityType.Prop;
            parent.Identity.Level = 0; // Objects typically have level 0
        }

        // Set faction based on object type
        SetFactionForObjectType();

        // Create visual indicators
        if (isLocked && lockedIndicatorPrefab != null)
        {
            CreateLockedIndicator();
        }

        if (debugMode)
        {
            Debug.Log($"[ObjectContextModule] Initialized {parent.GetEntityName()} " +
                     $"(Type: {objectType}, Interactable: {IsInteractable})");
        }
    }

    public void GatherContext()
    {
        // Track existence time (world lifetime tracking)
        // Handled by IdentityHandler.UpdateHandler()

        // Update visual indicators based on state
        UpdateVisualIndicators();
    }

    public string GetContextSaveData()
    {
        var saveData = new ObjectContextSaveData
        {
            objectType = objectType,
            isInteractable = isInteractable,
            isLocked = isLocked,
            hasBeenInteracted = hasBeenInteracted,
            currentState = currentState
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadContextData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<ObjectContextSaveData>(json);

        objectType = saveData.objectType;
        isInteractable = saveData.isInteractable;
        isLocked = saveData.isLocked;
        hasBeenInteracted = saveData.hasBeenInteracted;
        currentState = saveData.currentState;
    }

    #endregion

    #region Faction Configuration

    private void SetFactionForObjectType()
    {
        if (parent.Faction == null) return;

        FactionType faction = objectType switch
        {
            ObjectType.Trap => FactionType.Hostile,
            ObjectType.Chest when isLocked => FactionType.Neutral,
            ObjectType.Door when isLocked => FactionType.Neutral,
            _ => FactionType.Neutral
        };

        parent.Faction.SetFaction(faction);
    }

    #endregion

    #region Interaction Logic

    /// <summary>
    /// Attempt to interact with this object
    /// </summary>
    public bool TryInteract(ControllerBrain actor)
    {
        if (!isInteractable)
        {
            if (debugMode)
                Debug.Log($"[ObjectContextModule] {parent.GetEntityName()} is not interactable");
            return false;
        }

        if (isLocked)
        {
            if (HasRequiredKey(actor))
            {
                Unlock(actor);
            }
            else
            {
                if (debugMode)
                    Debug.Log($"[ObjectContextModule] {actor.name} lacks key: {requiredKeyId}");
                return false;
            }
        }

        return PerformInteraction(actor);
    }

    private bool PerformInteraction(ControllerBrain actor)
    {
        switch (interactionType)
        {
            case InteractionType.OneTime:
                if (hasBeenInteracted)
                {
                    if (debugMode)
                        Debug.Log($"[ObjectContextModule] {parent.GetEntityName()} already used");
                    return false;
                }
                hasBeenInteracted = true;
                isInteractable = false; // Disable after first use
                break;

            case InteractionType.Toggle:
                currentState = !currentState;
                OnStateChanged?.Invoke(currentState);
                break;

            case InteractionType.Continuous:
                // For things like campfires - no state change
                break;
        }

        OnInteracted?.Invoke(actor);

        if (debugMode)
        {
            Debug.Log($"[ObjectContextModule] {actor.name} interacted with {parent.GetEntityName()}");
        }

        return true;
    }

    #endregion

    #region Lock/Key System

    private bool HasRequiredKey(ControllerBrain actor)
    {
        if (string.IsNullOrEmpty(requiredKeyId)) return true;

        // Check actor's inventory for key
        var inventory = actor.GetProvider<IInventoryProvider>();
        if (inventory != null)
        {
            return inventory.HasItem(requiredKeyId);
        }

        return false;
    }

    public void Unlock(ControllerBrain actor)
    {
        if (!isLocked) return;

        isLocked = false;

        // Destroy locked indicator
        if (lockedIndicator != null)
        {
            Destroy(lockedIndicator);
        }

        if (debugMode)
        {
            Debug.Log($"[ObjectContextModule] {actor.name} unlocked {parent.GetEntityName()}");
        }
    }

    public void Lock()
    {
        isLocked = true;

        if (lockedIndicatorPrefab != null)
        {
            CreateLockedIndicator();
        }
    }

    #endregion

    #region Visual Indicators

    private void CreateLockedIndicator()
    {
        if (lockedIndicator != null) return;

        lockedIndicator = Instantiate(lockedIndicatorPrefab, parent.transform);
    }

    private void UpdateVisualIndicators()
    {
        // Show/hide interaction highlight based on player proximity
        // This would integrate with a proximity detection system
        // For now, just a placeholder
    }

    #endregion
}

#region Enums

public enum ObjectType
{
    Chest,      // Lootable container
    Door,       // Portal/barrier
    Lever,      // Switch/button
    Button,     // Pressure plate
    Trap,       // Harmful trigger
    Portal,     // Teleporter
    Campfire,   // Rest point
    Crafting,   // Crafting station
    Merchant    // Shop interface
}

public enum InteractionType
{
    OneTime,    // Can only be used once (chest)
    Toggle,     // On/off state (lever)
    Continuous  // Ongoing effect (campfire rest)
}

#endregion

#region Save Data Structure

[System.Serializable]
public class ObjectContextSaveData
{
    public ObjectType objectType;
    public bool isInteractable;
    public bool isLocked;
    public bool hasBeenInteracted;
    public bool currentState;
}

#endregion