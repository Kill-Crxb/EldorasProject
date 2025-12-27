using UnityEngine;
using System;

/// <summary>
/// State Machine Module - manages 4 parallel state layers with permission enforcement.
/// Companion component to ControllerBrain.
/// 
/// Architecture:
/// - Brain State: What am I focusing on? (high-level context)
/// - Upper Body State: What are my arms doing? (actions)
/// - Lower Body State: What are my legs doing? (movement)
/// - Posture State: What's my body position? (foundation)
/// 
/// Universal Design:
/// - Works for any game genre
/// - You define state enums per game
/// - Permission rules are customizable
/// </summary>
public partial class StateMachineModule : MonoBehaviour, IStateProvider

{
    [Header("Starting States")]
    [Tooltip("Initial brain state on spawn")]
    [SerializeField] private BrainState startingBrainState = BrainState.Idle;

    [Tooltip("Initial upper body state on spawn")]
    [SerializeField] private UpperBodyState startingUpperBodyState = UpperBodyState.Idle;

    [Tooltip("Initial lower body state on spawn")]
    [SerializeField] private LowerBodyState startingLowerBodyState = LowerBodyState.Idle;

    [Tooltip("Initial posture state on spawn")]
    [SerializeField] private PostureState startingPostureState = PostureState.Standing;

    [Header("Permission System")]
    [Tooltip("Permission matrix that defines what states can coexist")]
    [SerializeField] private StatePermissionMatrix permissions;

    [Header("Debug")]
    [SerializeField] private bool debugStateMachine = false;
    [SerializeField] private bool debugTransitions = true;
    [SerializeField] private bool showDebugUI = true;

    // The 4 state machines
    private StateMachine<BrainState> brainState;
    private StateMachine<UpperBodyState> upperBodyState;
    private StateMachine<LowerBodyState> lowerBodyState;
    private StateMachine<PostureState> postureState;

    // Brain reference
    private ControllerBrain brain;

    // Initialization flag
    public bool IsInitialized { get; private set; }

    // Events - modules can listen to these
    public event Action<BrainState, BrainState> OnBrainStateChanged;
    public event Action<UpperBodyState, UpperBodyState> OnUpperBodyStateChanged;
    public event Action<LowerBodyState, LowerBodyState> OnLowerBodyStateChanged;
    public event Action<PostureState, PostureState> OnPostureStateChanged;

    // ===== INITIALIZATION =====

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Create permission matrix if not assigned
        if (permissions == null)
        {
            permissions = ScriptableObject.CreateInstance<StatePermissionMatrix>();
            Debug.LogWarning($"[StateMachineModule] No permission matrix assigned, using default");
        }

        // Initialize state machines
        brainState = new StateMachine<BrainState>(startingBrainState);
        upperBodyState = new StateMachine<UpperBodyState>(startingUpperBodyState);
        lowerBodyState = new StateMachine<LowerBodyState>(startingLowerBodyState);
        postureState = new StateMachine<PostureState>(startingPostureState);

        // Subscribe to state change events for forwarding
        brainState.OnStateChanged += (old, newState) => OnBrainStateChanged?.Invoke(old, newState);
        upperBodyState.OnStateChanged += (old, newState) => OnUpperBodyStateChanged?.Invoke(old, newState);
        lowerBodyState.OnStateChanged += (old, newState) => OnLowerBodyStateChanged?.Invoke(old, newState);
        postureState.OnStateChanged += (old, newState) => OnPostureStateChanged?.Invoke(old, newState);

        IsInitialized = true;

        if (debugStateMachine)
        {
            Debug.Log($"[StateMachineModule] Initialized on {brain.name}");
            Debug.Log($"  Brain: {brainState.Current}");
            Debug.Log($"  UpperBody: {upperBodyState.Current}");
            Debug.Log($"  LowerBody: {lowerBodyState.Current}");
            Debug.Log($"  Posture: {postureState.Current}");
        }
    }

    // ===== STATE QUERIES =====

    public BrainState GetBrainState() => brainState.Current;
    public UpperBodyState GetUpperBodyState() => upperBodyState.Current;
    public LowerBodyState GetLowerBodyState() => lowerBodyState.Current;
    public PostureState GetPostureState() => postureState.Current;

    public BrainState GetPreviousBrainState() => brainState.Previous;
    public UpperBodyState GetPreviousUpperBodyState() => upperBodyState.Previous;
    public LowerBodyState GetPreviousLowerBodyState() => lowerBodyState.Previous;
    public PostureState GetPreviousPostureState() => postureState.Previous;

    public float GetTimeInBrainState() => brainState.TimeInState;
    public float GetTimeInUpperBodyState() => upperBodyState.TimeInState;
    public float GetTimeInLowerBodyState() => lowerBodyState.TimeInState;
    public float GetTimeInPostureState() => postureState.TimeInState;

    public bool IsInBrainState(BrainState state) => brainState.IsInState(state);
    public bool IsInUpperBodyState(UpperBodyState state) => upperBodyState.IsInState(state);
    public bool IsInLowerBodyState(LowerBodyState state) => lowerBodyState.IsInState(state);
    public bool IsInPostureState(PostureState state) => postureState.IsInState(state);

    // ===== CONVENIENCE PROPERTIES =====

    /// <summary>
    /// Is entity currently grounded? (derived from posture)
    /// </summary>
    public bool IsGrounded
    {
        get
        {
            PostureState current = postureState.Current;
            return current == PostureState.Standing ||
                   current == PostureState.Crouching ||
                   current == PostureState.Prone ||
                   current == PostureState.Sitting ||
                   current == PostureState.Kneeling;
        }
    }

    /// <summary>
    /// Is entity airborne? (derived from posture and lower body)
    /// </summary>
    public bool IsAirborne
    {
        get
        {
            return lowerBodyState.IsInAnyState(
                LowerBodyState.Jumping,
                LowerBodyState.Falling,
                LowerBodyState.DoubleJump
            );
        }
    }

    // ===== PERMISSION CHECKS =====

    /// <summary>
    /// Can perform a specific upper body action right now?
    /// </summary>
    public bool CanPerformUpperBodyAction(UpperBodyState desiredAction)
    {
        // Check posture allows it
        if (!permissions.CanPerformAction_PostureCheck(postureState.Current, desiredAction))
            return false;

        // Check lower body allows it
        if (!permissions.CanPerformAction_MovementCheck(lowerBodyState.Current, desiredAction))
            return false;

        return true;
    }

    /// <summary>
    /// Can perform a specific lower body movement right now?
    /// </summary>
    public bool CanPerformMovement(LowerBodyState desiredMovement)
    {
        // Check posture allows it
        if (!permissions.CanPerformMovement(postureState.Current, desiredMovement))
            return false;

        // Check upper body allows it
        if (!permissions.CanPerformMovement_ActionCheck(upperBodyState.Current, desiredMovement))
            return false;

        return true;
    }

    /// <summary>
    /// Can change posture right now?
    /// </summary>
    public bool CanChangePosture(PostureState desiredPosture)
    {
        return permissions.CanChangePosture(
            upperBodyState.Current,
            lowerBodyState.Current,
            desiredPosture
        );
    }

    // ===== STATE TRANSITIONS (WITH PERMISSION CHECKS) =====

    /// <summary>
    /// Try to transition brain state (always allowed)
    /// </summary>
    public bool TryTransitionBrain(BrainState newState)
    {
        brainState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] Brain: {brainState.Previous} → {newState}");

        return true;
    }

    /// <summary>
    /// Try to transition upper body state (subject to permissions)
    /// </summary>
    public bool TryTransitionUpperBody(UpperBodyState newState)
    {
        // Check if already in this state
        if (upperBodyState.IsInState(newState))
            return true;

        // Check permissions
        if (!CanPerformUpperBodyAction(newState))
        {
            if (debugTransitions)
                Debug.Log($"[StateMachine] UpperBody transition to {newState} BLOCKED by permissions");
            return false;
        }

        upperBodyState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] UpperBody: {upperBodyState.Previous} → {newState}");

        return true;
    }

    /// <summary>
    /// Try to transition lower body state (subject to permissions)
    /// </summary>
    public bool TryTransitionLowerBody(LowerBodyState newState)
    {
        // Check if already in this state
        if (lowerBodyState.IsInState(newState))
            return true;

        // Check permissions
        if (!CanPerformMovement(newState))
        {
            if (debugTransitions)
                Debug.Log($"[StateMachine] LowerBody transition to {newState} BLOCKED by permissions");
            return false;
        }

        lowerBodyState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] LowerBody: {lowerBodyState.Previous} → {newState}");

        return true;
    }

    /// <summary>
    /// Try to transition posture state (subject to permissions)
    /// </summary>
    public bool TryTransitionPosture(PostureState newState)
    {
        // Check if already in this state
        if (postureState.IsInState(newState))
            return true;

        // Check permissions
        if (!CanChangePosture(newState))
        {
            if (debugTransitions)
                Debug.Log($"[StateMachine] Posture transition to {newState} BLOCKED by permissions");
            return false;
        }

        postureState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] Posture: {postureState.Previous} → {newState}");

        return true;
    }

    // ===== FORCED TRANSITIONS (BYPASS PERMISSIONS) =====
    // Use these for animation events, external forces, or special cases

    public void ForceTransitionBrain(BrainState newState)
    {
        brainState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] Brain FORCED: {brainState.Previous} → {newState}");
    }

    public void ForceTransitionUpperBody(UpperBodyState newState)
    {
        upperBodyState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] UpperBody FORCED: {upperBodyState.Previous} → {newState}");
    }

    public void ForceTransitionLowerBody(LowerBodyState newState)
    {
        lowerBodyState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] LowerBody FORCED: {lowerBodyState.Previous} → {newState}");
    }

    public void ForceTransitionPosture(PostureState newState)
    {
        postureState.TransitionTo(newState);

        if (debugTransitions)
            Debug.Log($"[StateMachine] Posture FORCED: {postureState.Previous} → {newState}");
    }

    // ===== DEBUG VISUALIZATION =====

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (!showDebugUI || !IsInitialized) return;
        
        GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        
        GUIStyle stateStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = Color.white }
        };
        
        GUILayout.BeginArea(new Rect(10, 10, 450, 250));
        
        // Background
        GUI.Box(new Rect(0, 0, 450, 250), "");
        
        GUILayout.Space(5);
        GUILayout.Label("=== Body State Machine ===", headerStyle);
        GUILayout.Space(5);
        
        // State layers with color coding
        GUI.color = new Color(0.5f, 1f, 1f); // Cyan
        GUILayout.Label($"Brain (Focus):      {brainState.Current}", stateStyle);
        
        GUI.color = Color.yellow;
        GUILayout.Label($"UpperBody (Arms):   {upperBodyState.Current}", stateStyle);
        
        GUI.color = Color.green;
        GUILayout.Label($"LowerBody (Legs):   {lowerBodyState.Current}", stateStyle);
        
        GUI.color = Color.magenta;
        GUILayout.Label($"Posture (Spine):    {postureState.Current}", stateStyle);
        
        GUI.color = Color.white;
        GUILayout.Space(10);
        
        GUILayout.Label("=== Permissions ===", headerStyle);
        GUILayout.Label($"Can Attack: {CanPerformUpperBodyAction(UpperBodyState.MeleeWindUp)}", stateStyle);
        GUILayout.Label($"Can Block: {CanPerformUpperBodyAction(UpperBodyState.Blocking)}", stateStyle);
        GUILayout.Label($"Can Move: {CanPerformMovement(LowerBodyState.Walking)}", stateStyle);
        GUILayout.Label($"Can Dash: {CanPerformMovement(LowerBodyState.Dashing)}", stateStyle);
        GUILayout.Label($"Can Crouch: {CanChangePosture(PostureState.Crouching)}", stateStyle);
        
        GUILayout.Space(5);
        GUILayout.Label($"IsGrounded: {IsGrounded}", stateStyle);
        GUILayout.Label($"IsAirborne: {IsAirborne}", stateStyle);
        
        GUILayout.EndArea();
        
        GUI.color = Color.white;
    }
    
    private void OnDrawGizmos()
    {
        if (!debugStateMachine || !Application.isPlaying || !IsInitialized) return;
        
        // Draw state label above entity
        Vector3 labelPosition = transform.position + Vector3.up * 2.5f;
        
        string stateText = $"B:{brainState.Current}\n" +
                          $"U:{upperBodyState.Current}\n" +
                          $"L:{lowerBodyState.Current}\n" +
                          $"P:{postureState.Current}";
        
        UnityEditor.Handles.Label(labelPosition, stateText, new GUIStyle()
        {
            normal = new GUIStyleState() { textColor = Color.yellow },
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        });
    }
#endif
}