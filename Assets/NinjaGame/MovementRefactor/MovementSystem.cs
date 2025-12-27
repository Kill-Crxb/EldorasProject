using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Universal Movement System - Works for all entities
/// 
/// Architecture:
/// - ONE LocomotionHandler (defines game's movement style - interchangeable)
/// - ONE active ControlSource (defines who controls - switchable at runtime)
/// - Universal handlers (animation, constraints - same for all)
/// 
/// This system enables:
/// - Player and NPC using same movement code
/// - Swapping locomotion style between games (RPG → MilSim)
/// - Runtime control switching (possession, pets, cutscenes)
/// - Zero code duplication
/// </summary>
public class MovementSystem : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Core Handlers")]
    [SerializeField] private LocomotionHandler locomotionHandler;

    [Header("Optional Modules")]
    [Tooltip("Feet detection module for grounded state (optional - will auto-discover)")]
    [SerializeField] private FeetDetectionModule feetDetection;

    [Header("Control Sources")]
    [SerializeField] private List<MonoBehaviour> controlSourceComponents;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    // References
    private ControllerBrain brain;
    private IMovementControlSource activeControlSource;
    private List<IMovementControlSource> availableControlSources;

    // ========================================
    // Properties
    // ========================================

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public LocomotionHandler Locomotion => locomotionHandler;
    public IMovementControlSource ActiveControlSource => activeControlSource;
    public ControllerBrain Brain => brain;

    /// <summary>
    /// Grounded state from FeetDetectionModule (if available) or Brain fallback
    /// </summary>
    public bool IsGrounded => feetDetection?.IsGrounded ?? brain?.IsGrounded ?? false;

    // Locomotion state passthrough
    public bool IsMoving => locomotionHandler?.IsMoving ?? false;
    public Vector3 Velocity => locomotionHandler?.Velocity ?? Vector3.zero;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Auto-discover feet detection if not assigned
        if (feetDetection == null)
        {
            feetDetection = GetComponent<FeetDetectionModule>();

            if (feetDetection == null)
            {
                Debug.LogWarning("[MovementSystem] No FeetDetectionModule found - will use Brain.IsGrounded fallback");
            }
        }

        // Validate locomotion handler
        if (locomotionHandler == null)
        {
            locomotionHandler = GetComponentInChildren<LocomotionHandler>();

            if (locomotionHandler == null)
            {
                Debug.LogError("[MovementSystem] No LocomotionHandler found!");
                isEnabled = false;
                return;
            }
        }

        // Initialize locomotion handler
        locomotionHandler.Initialize(this);

        // Setup control sources
        SetupControlSources();

        // Activate default control source
        ActivateDefaultControlSource();

        if (showDebugInfo)
        {
            Debug.Log($"[MovementSystem] Initialized on {brain.name}");
            Debug.Log($"  Feet Detection: {(feetDetection != null ? "Active" : "Using Brain fallback")}");
            Debug.Log($"  Locomotion: {locomotionHandler.GetType().Name}");
            Debug.Log($"  Active Control: {activeControlSource?.SourceName ?? "NONE"}");
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled || locomotionHandler == null) return;

        // Update active control source
        activeControlSource?.UpdateSource();

        // Get movement input from control source
        MovementInput input = activeControlSource?.GetMovementInput() ?? MovementInput.Zero;

        // Execute movement via locomotion handler
        locomotionHandler.ExecuteMovement(input);
    }

    // ========================================
    // Control Source Management
    // ========================================

    private void SetupControlSources()
    {
        availableControlSources = new List<IMovementControlSource>();

        // Find all control sources in children
        var sources = GetComponentsInChildren<IMovementControlSource>();
        availableControlSources.AddRange(sources);

        // Also check serialized list (for manual assignment)
        if (controlSourceComponents != null)
        {
            foreach (var component in controlSourceComponents)
            {
                if (component is IMovementControlSource source && !availableControlSources.Contains(source))
                {
                    availableControlSources.Add(source);
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log($"[MovementSystem] Found {availableControlSources.Count} control sources:");
            foreach (var source in availableControlSources)
            {
                Debug.Log($"  - {source.SourceName}");
            }
        }
    }

    private void ActivateDefaultControlSource()
    {
        // Find first enabled control source
        foreach (var source in availableControlSources)
        {
            var monoBehaviour = source as MonoBehaviour;
            if (monoBehaviour != null && monoBehaviour.enabled)
            {
                SetControlSource(source);
                return;
            }
        }

        Debug.LogWarning($"[MovementSystem] No enabled control source found on {brain.name}");
    }

    /// <summary>
    /// Switch to a different control source at runtime
    /// This enables possession, pets, cutscenes, etc.
    /// </summary>
    public void SetControlSource(IMovementControlSource newSource)
    {
        if (newSource == activeControlSource) return;

        // Deactivate current source
        if (activeControlSource != null)
        {
            activeControlSource.OnDeactivated();

            if (showDebugInfo)
                Debug.Log($"[MovementSystem] Deactivated: {activeControlSource.SourceName}");
        }

        // Activate new source
        activeControlSource = newSource;

        if (activeControlSource != null)
        {
            activeControlSource.OnActivated();

            if (showDebugInfo)
                Debug.Log($"[MovementSystem] Activated: {activeControlSource.SourceName}");
        }
    }

    /// <summary>
    /// Get control source by type
    /// Useful for possession system: SetControlSource(GetControlSource<AdminControlSource>())
    /// </summary>
    public T GetControlSource<T>() where T : class, IMovementControlSource
    {
        return availableControlSources.OfType<T>().FirstOrDefault();
    }

    // ========================================
    // Debug Info
    // ========================================

    private void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 300, 300, 200));
        GUILayout.Label("=== MOVEMENT SYSTEM ===");
        GUILayout.Label($"Handler: {locomotionHandler?.GetType().Name ?? "NONE"}");
        GUILayout.Label($"Control: {activeControlSource?.SourceName ?? "NONE"}");
        GUILayout.Label($"Grounded: {IsGrounded}");
        GUILayout.Label($"Moving: {IsMoving}");
        GUILayout.Label($"Velocity: {Velocity.magnitude:F2}");
        GUILayout.EndArea();
    }
}