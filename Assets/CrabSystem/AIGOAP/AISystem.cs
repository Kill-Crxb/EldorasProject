using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UNIVERSAL AI System - works for ALL entity types that need AI
/// 
/// Coordinates AI handlers and decision systems to provide unified AI management.
/// Follows same pattern as IdentitySystem: Core handlers + Context module.
/// 
/// Architecture:
/// - Core Handlers (universal): PerceptionHandler, PathfindingHandler, CombatHandler
/// - Decision System: GOAPModule (GOAP-based goal planning)
/// - Context Module (optional): Provides entity-specific AI behavior
/// 
/// Configuration Flow:
/// NPCConfigurationHandler → AISystem.ConfigureFromArchetype() → Sets up all handlers
/// 
/// UPDATED: Legacy AI components (AIModule, AITargetDetection, AIStateUpdater) removed
/// All NPCs now use GOAP system exclusively.
/// </summary>
public class AISystem : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool debugMode = false;

    [Header("Decision System")]
    [Tooltip("GOAP Module - Goal-Oriented Action Planning")]
    [SerializeField] private GOAPModule goapModule;

    // REMOVED: Legacy AIModule - all NPCs now use GOAP
    // [SerializeField] private AIModule aiModule;

    [Header("Core Handlers - Universal")]
    [Tooltip("Perception: Vision, memory, target detection")]
    [SerializeField] private PerceptionModule perceptionHandler;

    [Tooltip("Pathfinding: NavMesh navigation wrapper")]
    [SerializeField] private PathfindingModule pathfindingHandler;

    [Tooltip("Combat: FSM-based combat behavior (added by archetype)")]
    [SerializeField] private MonoBehaviour combatHandler; // IAICombatBehavior

    [Tooltip("Fate System: Combo execution with RNG branches (optional)")]
    [SerializeField] private FateSystem fateSystem;

    // REMOVED: Legacy Components
    // [SerializeField] private AITargetDetection legacyTargetDetection;
    // [SerializeField] private AIStateUpdater legacyStateUpdater;

    // Cached references
    private ControllerBrain brain;
    private IAICombatBehavior cachedCombatBehavior;
    private List<IBrainModule> aiModules = new List<IBrainModule>();

    // Current archetype (set by ConfigureFromArchetype)
    private NPCArchetype appliedArchetype;

    #region Properties (Public API)

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;

    // Handler accessors
    public GOAPModule GOAP => goapModule;
    // REMOVED: public AIModule AI => aiModule;
    public PerceptionModule Perception => perceptionHandler;
    public PathfindingModule Pathfinding => pathfindingHandler;
    public IAICombatBehavior CombatBehavior => cachedCombatBehavior;
    public FateSystem Fate => fateSystem;

    // REMOVED: Legacy accessors
    // public AITargetDetection LegacyTargetDetection => legacyTargetDetection;
    // public AIStateUpdater LegacyStateUpdater => legacyStateUpdater;

    // System type detection
    public bool UsesGOAP => goapModule != null;
    // REMOVED: public bool UsesLegacyAI => aiModule != null && goapModule == null;
    public bool HasPerception => perceptionHandler != null; // UPDATED: removed legacy check
    public bool HasPathfinding => pathfindingHandler != null;
    public bool HasCombat => cachedCombatBehavior != null;

    // Current target (works with GOAP)
    public Transform CurrentTarget
    {
        get
        {
            // Try perception first (primary)
            if (perceptionHandler != null)
                return perceptionHandler.CurrentTarget;

            // REMOVED: Legacy fallback
            // if (aiModule != null)
            //     return aiModule.CurrentTarget;

            return null;
        }
    }

    #endregion

    #region Initialization

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        // Discover and cache all AI modules
        DiscoverAIModules();

        // Initialize all discovered modules
        foreach (var module in aiModules)
        {
            if (module != this) // Don't initialize self
            {
                module.Initialize(brain);

                if (debugMode)
                    Debug.Log($"[AISystem] Initialized {module.GetType().Name}");
            }
        }

        // Cache combat behavior interface
        if (combatHandler != null)
        {
            cachedCombatBehavior = combatHandler as IAICombatBehavior;
            if (cachedCombatBehavior == null)
            {
                Debug.LogWarning($"[AISystem] Combat handler {combatHandler.GetType().Name} does not implement IAICombatBehavior");
            }
        }

        string aiType = UsesGOAP ? "GOAP" : "None"; // UPDATED: removed legacy check
        Debug.Log($"[AISystem] Initialized on {brain.name} using {aiType}");

        LogConfiguration();
    }

    /// <summary>
    /// Discover all AI-related modules on this GameObject
    /// </summary>
    private void DiscoverAIModules()
    {
        // Clear existing
        aiModules.Clear();

        // Add decision system
        if (goapModule != null) aiModules.Add(goapModule);
        // REMOVED: if (aiModule != null) aiModules.Add(aiModule);

        // Add core handlers
        if (perceptionHandler != null) aiModules.Add(perceptionHandler);
        if (pathfindingHandler != null) aiModules.Add(pathfindingHandler);

        // Add optional systems
        if (fateSystem != null && fateSystem is IBrainModule fateModule)
        {
            aiModules.Add(fateModule);
        }

        // Add combat if it's a brain module
        if (combatHandler != null && combatHandler is IBrainModule combatModule)
        {
            aiModules.Add(combatModule);
        }

        // REMOVED: Legacy component discovery
        // if (legacyTargetDetection != null && legacyTargetDetection is IBrainModule legacyPerception)
        // {
        //     aiModules.Add(legacyPerception);
        // }
        // if (legacyStateUpdater != null && legacyStateUpdater is IBrainModule legacyState)
        // {
        //     aiModules.Add(legacyState);
        // }

        if (debugMode)
            Debug.Log($"[AISystem] Discovered {aiModules.Count} AI modules");
    }

    #endregion

    #region Configuration from Archetype

    /// <summary>
    /// Configure AI system from NPC archetype
    /// Called by NPCConfigurationHandler after archetype is set
    /// </summary>
    public void ConfigureFromArchetype(NPCArchetype archetype)
    {
        if (archetype == null)
        {
            Debug.LogWarning("[AISystem] ConfigureFromArchetype called with null archetype");
            return;
        }

        appliedArchetype = archetype;

        // Configure GOAP system
        if (archetype.UsesGOAP && goapModule != null)
        {
            ConfigureGOAP(archetype);
        }
        // REMOVED: Legacy AI configuration
        // else if (aiModule != null)
        // {
        //     ConfigureLegacyAI(archetype);
        // }

        // Configure perception
        if (perceptionHandler != null)
        {
            ConfigurePerception(archetype);
        }
        // REMOVED: Legacy perception fallback
        // else if (legacyTargetDetection != null)
        // {
        //     ConfigureLegacyPerception(archetype);
        // }

        // Pathfinding doesn't need archetype config (uses NavMesh)

        if (debugMode)
        {
            Debug.Log($"[AISystem] Configured from archetype: {archetype.archetypeName}");
            LogConfiguration();
        }
    }

    /// <summary>
    /// Configure GOAP system from archetype
    /// </summary>
    private void ConfigureGOAP(NPCArchetype archetype)
    {
        Debug.Log($"[AISystem] ConfigureGOAP called - goapModule: {goapModule != null}, archetype.HasGOAPGoals: {archetype.HasGOAPGoals}");

        if (goapModule == null || !archetype.HasGOAPGoals)
        {
            if (goapModule == null)
                Debug.LogWarning("[AISystem] Cannot configure GOAP - goapModule is null");
            if (!archetype.HasGOAPGoals)
                Debug.LogWarning($"[AISystem] Archetype {archetype.archetypeName} has no GOAP goals configured");
            return;
        }

        // GOAP goals are already assigned in the Inspector via goalPool
        // Archetype just enables/configures the system
        Debug.Log($"[AISystem] GOAP configured from archetype {archetype.archetypeName}");
    }

    // REMOVED: ConfigureLegacyAI method - no longer needed
    /*
    private void ConfigureLegacyAI(NPCArchetype archetype)
    {
        if (aiModule == null)
        {
            Debug.LogWarning("[AISystem] Cannot configure legacy AI - aiModule is null");
            return;
        }

        // Configure behavior tree (if using)
        aiModule.UseBehaviorTree = archetype.useBehaviorTree;

        Debug.Log($"[AISystem] Legacy AI configured from archetype {archetype.archetypeName}");
    }
    */

    /// <summary>
    /// Configure perception from archetype
    /// </summary>
    private void ConfigurePerception(NPCArchetype archetype)
    {
        if (perceptionHandler == null)
        {
            Debug.LogWarning("[AISystem] Cannot configure perception - perceptionHandler is null");
            return;
        }

        // Configure vision range
        perceptionHandler.SetVisionRange(archetype.visionRange);

        Debug.Log($"[AISystem] Perception configured - vision range: {archetype.visionRange}");
    }

    // REMOVED: ConfigureLegacyPerception method - no longer needed
    /*
    private void ConfigureLegacyPerception(NPCArchetype archetype)
    {
        if (legacyTargetDetection == null)
        {
            Debug.LogWarning("[AISystem] Cannot configure legacy perception - legacyTargetDetection is null");
            return;
        }

        legacyTargetDetection.SetDetectionRange(archetype.visionRange);

        Debug.Log($"[AISystem] Legacy perception configured - detection range: {archetype.visionRange}");
    }
    */

    #endregion

    #region Debug & Logging

    private void LogConfiguration()
    {
        if (!debugMode) return;

        Debug.Log("=== AISystem Configuration ===");
        Debug.Log($"Decision System: {(goapModule != null ? "GOAP" : "None")}"); // UPDATED
        Debug.Log($"Perception: {(perceptionHandler != null ? "PerceptionModule" : "None")}"); // UPDATED
        Debug.Log($"Pathfinding: {(pathfindingHandler != null ? "PathfindingModule" : "None")}");
        Debug.Log($"Combat: {(cachedCombatBehavior != null ? cachedCombatBehavior.GetType().Name : "None")}");
        Debug.Log($"Fate System: {(fateSystem != null ? "Enabled" : "Disabled")}");
        Debug.Log($"Applied Archetype: {(appliedArchetype != null ? appliedArchetype.archetypeName : "None")}");
        Debug.Log("==============================");
    }

    #endregion

    #region IBrainModule Implementation

    public void UpdateModule()
    {
        if (!isEnabled) return;

        foreach (var module in aiModules)
        {
            if (module != null && module.IsEnabled)
                module.UpdateModule();
        }
    }

    #endregion
}