
using UnityEngine;

/// <summary>
/// Interface for AI combat behavior modules
/// Allows different combat styles (melee, ranged, magic) to be swapped
/// Step 1.9: Added ExecuteAttack() for AIModule combat state implementation
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
    bool ShouldExitCombat(Transform target);

    // State transitions
    void OnCombatEnter(Transform target);
    void OnCombatExit();

    // ⭐ STEP 1.9: Execute attack on cooldown timer
    void ExecuteAttack();
}