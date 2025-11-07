using UnityEngine;

/// <summary>
/// Interface for AI combat behavior modules
/// Allows different combat styles (melee, ranged, magic) to be swapped
/// 
/// FIXED VERSION: Complete interface with all required methods for AIModule integration
/// </summary>
public interface IAICombatBehavior
{
    bool IsEnabled { get; set; }

    // Combat range properties
    float AttackRange { get; }
    float ExitCombatRange { get; }

    // Initialization
    void Initialize(AIModule aiModule, ControllerBrain brain);

    // Combat loop
    void UpdateCombat(Transform target);

    // State checks
    bool CanEnterCombat();
    bool CanAttack();                           // ← ADDED: Required by AIStateUpdater and AIDebugVisualizer
    bool ShouldExitCombat(Transform target);

    // State transitions
    void OnCombatEnter(Transform target);
    void OnCombatExit();

    // Attack execution
    void ExecuteAttack();                       // ← Parameterless version for interface
    void ExecuteAttack(Transform target);       // ← Target-aware version (preferred)
}