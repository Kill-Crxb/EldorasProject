using UnityEngine;

/// <summary>
/// The AI's hands on the controls. Goals write intent here (steer, face, stop); MovementSystem
/// reads it every frame exactly as it reads the player's InputSystem.
///
/// Also holds the few pieces of per-entity AI memory a goal needs between frames. Goals are
/// ScriptableObjects shared by every NPC that uses them, so nothing per-entity can live on a goal.
///
/// Setup: put it on a child of the brain. At LateInitialize it makes itself the active movement
/// control source, replacing InputSystem's AI mode (and its StubAIControlSource).
/// </summary>
public class AIControlSource : MonoBehaviour, IBrainModule, IMovementControlSource
{
    [SerializeField] private bool isEnabled = true;

    private ControllerBrain brain;
    private Vector2 moveDirection;
    private Vector2 lookDirection;

    // Per-entity memory for goals
    public float LastAttackTime { get; set; } = -999f;
    public float GuardUntil { get; set; } = -999f;
    public bool ReadThisSwing { get; set; }
    public bool DeflectReadThisSwing { get; set; }
    public int StringPressesLeft { get; set; }

    // The AI's block key. AbilitySystem keeps a guard up only while this is held.
    public bool GuardHeld { get; set; }

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public bool IsActive => isEnabled && isActiveAndEnabled;
    public string SourceName => "AIControlSource";

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void LateInitialize()
    {
        if (brain.Movement == null)
        {
            Debug.LogWarning($"[AIControlSource] {brain.EntityName} has no MovementSystem");
            return;
        }
        brain.Movement.SetControlSource(this);
    }

    public void UpdateModule() { }

    // Intent — called by goals

    /// <summary>
    /// Move this way. Strength 0–1 is the analog stick: the locomotion handler scales its speed
    /// request by it, the same as a half-pushed gamepad stick.
    /// </summary>
    public void Steer(Vector3 worldDirection, float strength = 1f)
    {
        moveDirection = Flatten(worldDirection) * Mathf.Clamp01(strength);
    }

    public void Face(Vector3 worldDirection)
    {
        lookDirection = Flatten(worldDirection);
    }

    public void Stop()
    {
        moveDirection = Vector2.zero;
    }

    public void Release()
    {
        moveDirection = Vector2.zero;
        lookDirection = Vector2.zero;
        GuardHeld = false;
    }

    // IMovementControlSource

    public MovementInput GetMovementInput()
    {
        MovementInput input = MovementInput.Zero;
        input.MoveDirection = moveDirection;
        input.LookDirection = lookDirection;
        return input;
    }

    public void OnActivated() { }
    public void OnDeactivated() => Release();
    public void UpdateSource() { }

    private static Vector2 Flatten(Vector3 v)
    {
        Vector2 flat = new Vector2(v.x, v.z);
        return flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector2.zero;
    }
}
