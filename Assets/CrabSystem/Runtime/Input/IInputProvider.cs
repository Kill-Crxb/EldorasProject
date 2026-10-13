using UnityEngine;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpPressed { get; }
    bool JumpHeld { get; }
    bool DashPressed { get; }
    bool LightAttackPressed { get; }
    bool HeavyAttackPressed { get; }
    bool BlockHeld { get; }
    bool ParryPressed { get; }

    /// <summary>Sheathe/draw toggle — flips CombatStanceModule between armed and unarmed.</summary>
    bool ToggleStancePressed { get; }

    // Ability Quickslots (Q, Z, X, C, V)
    bool AbilityQPressed { get; }
    bool AbilityZPressed { get; }
    bool AbilityXPressed { get; }
    bool AbilityCPressed { get; }
    bool AbilityVPressed { get; }
}