using UnityEngine;

/// <summary>
/// Interface for entity-specific context modules.
/// Context modules gather data specific to their entity type (Player, NPC, Object)
/// and configure the core identity handlers accordingly.
/// 
/// Only ONE context module should be present per IdentitySystem.
/// </summary>
public interface IContextModule
{
    /// <summary>
    /// Initialize context module with reference to parent IdentitySystem.
    /// This is called AFTER all handlers have been initialized.
    /// Use this to configure handlers based on entity type (apply archetypes, set defaults, etc.)
    /// </summary>
    /// <param name="parent">The IdentitySystem managing this context</param>
    void Initialize(IdentitySystem parent);

    /// <summary>
    /// Called every frame by IdentitySystem.UpdateModule().
    /// Use this to gather fresh data and update handlers.
    /// Example: Sync player level from StatAllocationSystem, update NPC nameplate health
    /// </summary>
    void GatherContext();

    /// <summary>
    /// Serialize context-specific data to JSON for saving.
    /// Return null or "{}" if no context-specific data needs saving.
    /// </summary>
    string GetContextSaveData();

    /// <summary>
    /// Deserialize context-specific data from JSON when loading.
    /// </summary>
    void LoadContextData(string json);
}