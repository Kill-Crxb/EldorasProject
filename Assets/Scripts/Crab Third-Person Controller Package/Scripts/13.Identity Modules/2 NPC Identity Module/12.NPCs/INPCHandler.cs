/// <summary>
/// Interface for NPC handler sub-modules.
/// Follows same pattern as IMeleeSubModule for consistency.
/// </summary>
public interface INPCHandler
{
    /// <summary>
    /// Enable/disable this handler
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Initialize handler with parent NPC module
    /// </summary>
    void Initialize(NPCModule parent);

    /// <summary>
    /// Update handler (called by NPCModule)
    /// </summary>
    void UpdateHandler();

    /// <summary>
    /// Serialize handler data for saving (future multiplayer sync)
    /// </summary>
    string GetHandlerSaveData();

    /// <summary>
    /// Deserialize handler data from save
    /// </summary>
    void LoadHandlerData(string json);

    /// <summary>
    /// Reset handler to default state
    /// </summary>
    void ResetHandler();
}