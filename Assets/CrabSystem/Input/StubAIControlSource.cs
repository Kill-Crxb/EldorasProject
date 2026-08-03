using UnityEngine;

/// <summary>
/// Stub AI control source for NPCs that don't have pathfinding or AI behavior yet.
/// Returns zero input every frame — NPC stands still but doesn't spam warnings.
/// 
/// ARCHITECTURE:
/// - Owned by InputSystem as a child component
/// - InputSystem delegates to this when currentMode == InputMode.AI
/// - Satisfies IMovementControlSource interface
/// - Replace with real AIControlSource or PathfindingControlSource when AI is implemented
/// </summary>
public class StubAIControlSource : MonoBehaviour, IMovementControlSource
{
    public MovementInput GetMovementInput() => MovementInput.Zero;

    public void OnActivated()
    {
    }

    public void OnDeactivated()
    {
    }

    public void UpdateSource()
    {
    }

    public bool IsActive => isActiveAndEnabled;

    public string SourceName => "StubAIControlSource (no movement)";
}
