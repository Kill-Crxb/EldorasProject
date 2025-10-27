// Core Animation State Module - Updated for Player/NPC compatibility
using UnityEngine;

public enum AnimationState
{
    Free,                   // Can move and act freely
    Locked,                 // Cannot move or act (will be used by future packages)
    Restricted              // Can move but limited actions (will be used by future packages)
}

[System.Serializable]
public class AnimationStateModule : MonoBehaviour, IPlayerModule
{
    [Header("Animation State Settings")]
    [SerializeField] private bool debugState = false;

    // Use interface instead of specific controller type
    private IMovementState movementState;
    private ControllerBrain brain;
    private AnimationState currentState = AnimationState.Free;

    public AnimationState CurrentState => currentState;

    // IPlayerModule implementation
    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Get movement module via interface (works for both Player and NPC)
        movementState = brain.GetModuleImplementing<IMovementState>();

        if (movementState == null)
        {
            Debug.LogWarning("[AnimationStateModule] No movement module found!");
        }
        else if (debugState)
        {
            Debug.Log($"[AnimationStateModule] Found movement module: {movementState.GetType().Name}");
        }
    }

    public void UpdateModule()
    {
        UpdateState();
    }

    void Awake()
    {
        // Only auto-find if not using Brain architecture
        var parentBrain = GetComponentInParent<ControllerBrain>();
        if (parentBrain == null)
        {
            // No Brain found, try to find movement state manually
            movementState = GetComponentInParent<IMovementState>();
            if (movementState == null)
            {
                movementState = FindFirstObjectByType<ThirdPersonController>();
            }
        }
        // If Brain is found, Initialize() will handle getting the movement state
    }

    void Start()
    {
        if (movementState == null)
        {
            Debug.LogWarning("[AnimationStateModule] No movement module found!");
        }
    }

    public void UpdateState()
    {
        // For core package, we mostly stay in Free state
        // Future packages (Combat, etc.) can override this logic

        AnimationState previousState = currentState;

        // Core logic - mostly free movement
        currentState = AnimationState.Free;

        // Future packages will add their own state checks here via events or direct access
    }

    // Public API for other modules to change state
    public void SetState(AnimationState newState)
    {
        currentState = newState;
    }

    public void SetStateFree()
    {
        SetState(AnimationState.Free);
    }

    public void SetStateLocked()
    {
        SetState(AnimationState.Locked);
    }

    public void SetStateRestricted()
    {
        SetState(AnimationState.Restricted);
    }

    // State queries for controller
    public bool CanMove()
    {
        return currentState != AnimationState.Locked;
    }

    public bool CanAct()
    {
        return currentState == AnimationState.Free;
    }

    public void DrawGizmos()
    {
        if (movementState == null) return;

        // Show animation state as colored cube above character
        Vector3 position;

        // Get position from movement state
        if (movementState is MonoBehaviour mb)
        {
            position = mb.transform.position + Vector3.up * 2.5f;
        }
        else
        {
            return;
        }

        switch (currentState)
        {
            case AnimationState.Locked:
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(position, Vector3.one * 0.5f);
                break;
            case AnimationState.Restricted:
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(position, Vector3.one * 0.3f);
                break;
            case AnimationState.Free:
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(position, Vector3.one * 0.2f);
                break;
        }
    }
}