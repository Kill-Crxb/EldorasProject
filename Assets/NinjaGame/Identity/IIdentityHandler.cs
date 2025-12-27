using UnityEngine;

/// <summary>
/// Universal interface for all identity handler components.
/// Replaces IPlayerInfoHandler and INPCHandler with a single unified interface.
/// 
/// Handlers manage specific aspects of entity identity (name, faction, model, etc.)
/// and are coordinated by the IdentitySystem module.
/// </summary>
public interface IIdentityHandler
{
    /// <summary>
    /// Is this handler currently active?
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Initialize handler with reference to parent IdentitySystem.
    /// Called once during IdentitySystem.Initialize().
    /// </summary>
    /// <param name="parent">The IdentitySystem managing this handler</param>
    void Initialize(IdentitySystem parent);

    /// <summary>
    /// Called every frame by IdentitySystem.UpdateModule().
    /// Use for per-frame logic (time tracking, state monitoring, etc.)
    /// </summary>
    void UpdateHandler();

    /// <summary>
    /// Serialize handler data to JSON string for saving.
    /// Return "{}" if handler has no data to save.
    /// </summary>
    string GetHandlerSaveData();

    /// <summary>
    /// Deserialize handler data from JSON string when loading.
    /// </summary>
    void LoadHandlerData(string json);

    /// <summary>
    /// Reset handler to default/initial state.
    /// </summary>
    void ResetHandler();
}