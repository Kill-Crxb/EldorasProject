/// <summary>
/// Interface for modules managed by a ControllerBrain.
/// Used by both player characters, NPCs, and any entity with a Brain.
/// Modules are auto-discovered and initialized by the Brain.
/// 
/// Migration Note: This replaces IPlayerModule with a universal interface.
/// IPlayerModule will inherit from IBrainModule for backward compatibility.
/// </summary>
public interface IBrainModule
{
    /// <summary>Is this module currently active?</summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Called once when the Brain initializes.
    /// Use this to cache references to other modules.
    /// </summary>
    /// <param name="brain">The Brain managing this module</param>
    void Initialize(ControllerBrain brain);

    /// <summary>
    /// Called every frame by the Brain.
    /// Use this for per-frame logic.
    /// </summary>
    void UpdateModule();
}