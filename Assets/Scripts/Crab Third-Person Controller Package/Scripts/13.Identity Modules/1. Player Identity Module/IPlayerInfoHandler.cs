using UnityEngine;

/// <summary>
/// Interface for all PlayerInfo sub-handlers.
/// Follows the same pattern as IMeleeSubModule (used by AttackModule, ComboModule, etc.)
/// </summary>
public interface IPlayerInfoHandler
{
    /// <summary>
    /// Initialize the handler with a reference to the parent PlayerInfoModule
    /// </summary>
    void Initialize(PlayerInfoModule parent);

    /// <summary>
    /// Called every frame if the handler needs to update
    /// </summary>
    void UpdateHandler();

    /// <summary>
    /// Serialize handler data to JSON string for saving
    /// </summary>
    string GetHandlerSaveData();

    /// <summary>
    /// Deserialize handler data from JSON string when loading
    /// </summary>
    void LoadHandlerData(string json);

    /// <summary>
    /// Reset handler to default/initial state
    /// </summary>
    void ResetHandler();
}