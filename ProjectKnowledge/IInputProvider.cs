using UnityEngine;

public interface IInputProvider
{
    Vector2 MoveInput { get; }
    Vector2 LookInput { get; }
    bool JumpPressed { get; }
    bool JumpHeld { get; }
    bool SprintHeld { get; }
    bool DashPressed { get; }
    bool LightAttackPressed { get; }
    bool HeavyAttackPressed { get; }
    bool BlockHeld { get; }
    bool ParryPressed { get; }

    // Ability Quickslots (Q, Z, X, C, V)
    bool AbilityQPressed { get; }
    bool AbilityZPressed { get; }
    bool AbilityXPressed { get; }
    bool AbilityCPressed { get; }
    bool AbilityVPressed { get; }
}