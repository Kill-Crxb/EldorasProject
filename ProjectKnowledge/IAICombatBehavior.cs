using UnityEngine;

/// <summary>
/// Interface for AI combat behavior modules
/// Allows different combat styles (melee, ranged, magic) to be swapped
/// 
/// UPDATED: Removed AIModule dependency - combat behaviors now work with GOAP system
/// AIModule was legacy, all NPCs now use GOAP for decision-making
/// </summary>
public interface IAICombatBehavior
{
    bool IsEnabled { get; set; }

    // Combat range properties
    float AttackRange { get; }
    float ExitCombatRange { get; }

    // Initialization (UPDATED: Removed AIModule parameter)
    void Initialize(ControllerBrain brain);

    // Combat loop
    void UpdateCombat(Transform target);

    // State checks
    bool CanEnterCombat();
    bool CanAttack();
    bool ShouldExitCombat(Transform target);

    // State transitions
    void OnCombatEnter(Transform target);
    void OnCombatExit();

    // Attack execution
    void ExecuteAttack();                       // Parameterless version for interface
    void ExecuteAttack(Transform target);       // Target-aware version (preferred)
}