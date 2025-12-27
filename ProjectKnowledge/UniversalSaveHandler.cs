using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UNIVERSAL Save Handler - optional component for entity persistence
/// Collects save data from all identity handlers and manages save/load.
/// 
/// Not all entities need this (e.g., temporary NPCs don't persist)
/// </summary>
public class UniversalSaveHandler : MonoBehaviour, IIdentityHandler
{
    [Header("Handler Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Persistence Settings")]
    [Tooltip("Should this entity be saved between sessions?")]
    [SerializeField] private bool isPersistent = true;

    [Tooltip("Save immediately when data changes? (expensive)")]
    [SerializeField] private bool saveOnChange = false;

    [Header("Debug")]
    [SerializeField] private bool debugSaving = false;

    // Internal state
    private IdentitySystem parent;

    #region Properties

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    /// <summary>
    /// Should this entity persist between sessions?
    /// </summary>
    public bool IsPersistent => isPersistent;

    #endregion

    #region IIdentityHandler Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        if (debugSaving)
        {
            Debug.Log($"[SaveHandler] Initialized for {parent.GetEntityName()} (Persistent: {isPersistent})");
        }
    }

    public void UpdateHandler()
    {
        // No per-frame updates needed
    }

    public string GetHandlerSaveData()
    {
        // SaveHandler doesn't save itself (it coordinates other handlers)
        return "{}";
    }

    public void LoadHandlerData(string json)
    {
        // SaveHandler doesn't load itself
    }

    public void ResetHandler()
    {
        // Nothing to reset
    }

    #endregion

    #region Save/Load Coordination

    /// <summary>
    /// Get unique save ID for this entity
    /// </summary>
    public string GetSaveId()
    {
        return parent.Identity?.EntityId ?? "unknown";
    }

    /// <summary>
    /// Collect save data from all identity handlers
    /// </summary>
    public string GetCompleteSaveData()
    {
        var saveData = new EntitySaveData
        {
            version = 1,
            handlerData = new Dictionary<string, string>()
        };

        // Collect save data from all identity handlers
        if (parent.Identity != null)
            saveData.handlerData["Identity"] = parent.Identity.GetHandlerSaveData();

        if (parent.Faction != null)
            saveData.handlerData["Faction"] = parent.Faction.GetHandlerSaveData();

        // Model handler is optional and might be different types
        if (parent.Model != null && parent.Model is IIdentityHandler modelHandler)
            saveData.handlerData["Model"] = modelHandler.GetHandlerSaveData();

        // Let context module contribute data
        if (parent.Context != null)
        {
            string contextData = parent.Context.GetContextSaveData();
            if (!string.IsNullOrEmpty(contextData))
            {
                saveData.handlerData["Context"] = contextData;
            }
        }

        string json = JsonUtility.ToJson(saveData, true);

        if (debugSaving)
        {
            Debug.Log($"[SaveHandler] Saved data for {parent.GetEntityName()}:\n{json}");
        }

        return json;
    }

    /// <summary>
    /// Distribute loaded data to all identity handlers
    /// </summary>
    public void LoadCompleteSaveData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var saveData = JsonUtility.FromJson<EntitySaveData>(json);

            // Distribute data to handlers
            if (saveData.handlerData.TryGetValue("Identity", out string identityJson))
                parent.Identity?.LoadHandlerData(identityJson);

            if (saveData.handlerData.TryGetValue("Faction", out string factionJson))
                parent.Faction?.LoadHandlerData(factionJson);

            // Model handler is optional and might be different types
            if (saveData.handlerData.TryGetValue("Model", out string modelJson))
            {
                if (parent.Model is IIdentityHandler modelHandler)
                    modelHandler.LoadHandlerData(modelJson);
            }

            if (saveData.handlerData.TryGetValue("Context", out string contextJson))
                parent.Context?.LoadContextData(contextJson);

            if (debugSaving)
            {
                Debug.Log($"[SaveHandler] Loaded data for {parent.GetEntityName()}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveHandler] Failed to load data: {e.Message}");
        }
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class EntitySaveData
{
    public int version;
    public Dictionary<string, string> handlerData = new Dictionary<string, string>();
}

#endregion