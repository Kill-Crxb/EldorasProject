using UnityEngine;
using System.Collections.Generic;
using RPG.Factions;
using RPG.NPC.UI;

/// <summary>
/// Central coordinator for NPC identity and behavior.
/// Parallel to PlayerInfoModule but for NPCs.
/// Lives under Component_Brain, same as other IBrainModule components.
/// 
/// PHASE 2 ENHANCEMENTS:
/// - Integrated nameplate system
/// - Faction-aware API for other systems
/// - Health tracking for nameplate updates
/// </summary>
public class NPCModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;
    [SerializeField] private bool showDebugInfo = false;

    [Header("NPC Identity")]
    [SerializeField] private string npcId;              // Unique instance ID
    [SerializeField] private string npcName;            // Display name
    [SerializeField] private int npcLevel = 1;          // NPC level (for nameplate)

    [Header("Configuration")]
    [SerializeField] private NPCArchetype appliedArchetype;

    [Header("Nameplate Settings (Phase 2)")]
    [SerializeField] private bool enableNameplate = true;
    [SerializeField] private GameObject nameplatePrefab;
    [SerializeField] private Vector3 nameplateOffset = new Vector3(0, 2.5f, 0);

    [Header("Network Settings (Future)")]
    [SerializeField] private bool isNetworked = false;
    [SerializeField] private bool isServerControlled = true;

    // Sub-handlers
    private NPCConfigurationHandler configurationHandler;
    private FactionAffiliationHandler factionHandler;
    private List<INPCHandler> handlers = new List<INPCHandler>();

    // Parent brain reference
    private ControllerBrain brain;

    // Nameplate (Phase 2)
    private NPCNameplate nameplateInstance;

    // Health tracking (for nameplate)
    private NPCDamageable damageable;

    // Properties
    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public string NPCId => npcId;
    public string NPCName => npcName;
    public int NPCLevel => npcLevel;
    public NPCArchetype AppliedArchetype => appliedArchetype;
    public ControllerBrain Brain => brain;

    // ========================================
    // IBrainModule Implementation
    // ========================================

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        // Generate unique ID if not set
        if (string.IsNullOrEmpty(npcId))
        {
            npcId = System.Guid.NewGuid().ToString();
        }

        // Auto-discover handlers (same pattern as MeleeModule)
        DiscoverHandlers();

        // Initialize all handlers
        foreach (var handler in handlers)
        {
            if (handler.IsEnabled)
            {
                handler.Initialize(this);
            }
        }

        // Initialize nameplate (Phase 2)
        if (enableNameplate)
        {
            CreateNameplate();
        }

        // Find damageable for health tracking
        damageable = GetComponentInParent<NPCDamageable>();
        if (damageable != null)
        {
            damageable.OnDamageTaken += HandleDamageTaken;
            damageable.OnDeath += HandleDeath;
        }

        Debug.Log($"[NPCModule] Initialized NPC: {npcName} ({npcId})");
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        // Update all active handlers
        foreach (var handler in handlers)
        {
            if (handler.IsEnabled)
            {
                handler.UpdateHandler();
            }
        }
    }

    // ========================================
    // Handler Discovery & Management
    // ========================================

    private void DiscoverHandlers()
    {
        handlers.Clear();

        // Find all INPCHandler components in children
        var foundHandlers = GetComponentsInChildren<INPCHandler>();

        foreach (var handler in foundHandlers)
        {
            handlers.Add(handler);

            if (showDebugInfo)
            {
                Debug.Log($"[NPCModule] Discovered handler: {handler.GetType().Name}");
            }
        }

        // Store specific handler references for quick access
        configurationHandler = GetComponentInChildren<NPCConfigurationHandler>();
        factionHandler = GetComponentInChildren<FactionAffiliationHandler>();
    }

    /// <summary>
    /// Get specific handler by type (like GetModule in ControllerBrain)
    /// </summary>
    public T GetHandler<T>() where T : class, INPCHandler
    {
        foreach (var handler in handlers)
        {
            if (handler is T typedHandler)
            {
                return typedHandler;
            }
        }
        return null;
    }

    // ========================================
    // Configuration API
    // ========================================

    /// <summary>
    /// Apply archetype configuration to this NPC
    /// Called by NPCConfigurationHandler
    /// </summary>
    public void SetArchetype(NPCArchetype archetype)
    {
        appliedArchetype = archetype;

        if (archetype.useGenericName && !string.IsNullOrEmpty(archetype.genericName))
        {
            npcName = archetype.genericName;
        }

        // Update nameplate if it exists
        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateName(npcName);
        }
    }

    /// <summary>
    /// Set NPC name (for named NPCs, not generic)
    /// </summary>
    public void SetName(string name)
    {
        npcName = name;

        // Update nameplate
        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateName(name);
        }
    }

    /// <summary>
    /// Set NPC level
    /// </summary>
    public void SetLevel(int level)
    {
        npcLevel = Mathf.Max(1, level);

        // Update nameplate
        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateLevel(npcLevel);
        }
    }

    // ========================================
    // Nameplate System (Phase 2)
    // ========================================

    /// <summary>
    /// Create and initialize the nameplate
    /// </summary>
    private void CreateNameplate()
    {
        if (nameplatePrefab == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"[NPCModule] {npcName}: Nameplate prefab not assigned. Skipping nameplate creation.");
            }
            return;
        }

        // Get faction for color coding
        FactionType faction = factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;

        // Instantiate nameplate
        GameObject nameplateObj = Instantiate(nameplatePrefab, transform.position + nameplateOffset, Quaternion.identity);
        nameplateInstance = nameplateObj.GetComponent<NPCNameplate>();

        if (nameplateInstance != null)
        {
            nameplateInstance.Initialize(transform, npcName, npcLevel, faction);

            if (showDebugInfo)
            {
                Debug.Log($"[NPCModule] Nameplate created for {npcName}");
            }
        }
        else
        {
            Debug.LogError($"[NPCModule] {npcName}: Nameplate prefab missing NPCNameplate component!");
            Destroy(nameplateObj);
        }
    }

    /// <summary>
    /// Update nameplate health display
    /// Called automatically when NPC takes damage
    /// </summary>
    private void HandleDamageTaken(float damage)
    {
        if (nameplateInstance != null && damageable != null)
        {
            float healthPercent = damageable.CurrentHealth / damageable.MaxHealth;
            nameplateInstance.UpdateHealth(healthPercent);
        }
    }

    /// <summary>
    /// Handle death - hide nameplate
    /// </summary>
    private void HandleDeath()
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.SetVisible(false);
        }
    }

    /// <summary>
    /// Manually update nameplate health (for custom health systems)
    /// </summary>
    public void UpdateNameplateHealth(float healthPercent)
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateHealth(healthPercent);
        }
    }

    /// <summary>
    /// Show or hide nameplate
    /// </summary>
    public void SetNameplateVisible(bool visible)
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.SetVisible(visible);
        }
    }

    /// <summary>
    /// Get the nameplate instance
    /// </summary>
    public NPCNameplate GetNameplate()
    {
        return nameplateInstance;
    }

    // ========================================
    // Faction API (Phase 2)
    // ========================================

    /// <summary>
    /// Get this NPC's faction
    /// </summary>
    public FactionType GetFaction()
    {
        return factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;
    }

    /// <summary>
    /// Get relationship to player
    /// </summary>
    public FactionRelationship GetRelationshipToPlayer()
    {
        return factionHandler != null ? factionHandler.GetRelationshipToPlayer() : FactionRelationship.Neutral;
    }

    /// <summary>
    /// Check if hostile to player
    /// </summary>
    public bool IsHostileToPlayer()
    {
        return factionHandler != null && factionHandler.IsHostileToPlayer();
    }

    /// <summary>
    /// Check if friendly to player
    /// </summary>
    public bool IsFriendlyToPlayer()
    {
        return factionHandler != null && factionHandler.IsFriendlyToPlayer();
    }

    // ========================================
    // Interaction API (for other systems)
    // ========================================

    /// <summary>
    /// Check if this NPC can interact with a player
    /// Uses faction system
    /// </summary>
    public bool CanInteractWithPlayer(PlayerInfoModule playerInfo)
    {
        if (factionHandler == null) return true;
        return factionHandler.CanInteractWithPlayer(playerInfo);
    }

    /// <summary>
    /// Check if this NPC is hostile to a player
    /// </summary>
    public bool IsHostileToPlayer(PlayerInfoModule playerInfo)
    {
        if (factionHandler == null) return false;
        return factionHandler.IsHostileToPlayer(playerInfo);
    }

    /// <summary>
    /// Check if this NPC is allied with another NPC
    /// </summary>
    public bool IsAllyOf(NPCModule otherNPC)
    {
        if (factionHandler == null || otherNPC.factionHandler == null)
            return false;

        return factionHandler.IsAllyOf(otherNPC);
    }

    /// <summary>
    /// Check if this NPC is enemy with another NPC
    /// </summary>
    public bool IsEnemyOf(NPCModule otherNPC)
    {
        if (factionHandler == null || otherNPC.factionHandler == null)
            return false;

        return factionHandler.IsEnemyOf(otherNPC);
    }

    // ========================================
    // Debug & Utility
    // ========================================

    [ContextMenu("Debug: Print NPC Info")]
    private void DebugPrintInfo()
    {
        Debug.Log($"=== NPC Info ===");
        Debug.Log($"ID: {npcId}");
        Debug.Log($"Name: {npcName}");
        Debug.Log($"Level: {npcLevel}");
        Debug.Log($"Faction: {GetFaction()}");
        Debug.Log($"Relationship to Player: {GetRelationshipToPlayer()}");
        Debug.Log($"Hostile to Player: {IsHostileToPlayer()}");
        Debug.Log($"Archetype: {appliedArchetype?.archetypeName ?? "None"}");
        Debug.Log($"Handlers: {handlers.Count}");
        Debug.Log($"Nameplate: {(nameplateInstance != null ? "Active" : "None")}");
    }

    [ContextMenu("Debug: List All Handlers")]
    private void DebugListHandlers()
    {
        Debug.Log($"=== NPC Handlers ({handlers.Count}) ===");
        foreach (var handler in handlers)
        {
            string status = handler.IsEnabled ? "ENABLED" : "DISABLED";
            Debug.Log($"- {handler.GetType().Name} [{status}]");
        }
    }

    [ContextMenu("Debug: Simulate Damage (50%)")]
    private void DebugSimulateDamage()
    {
        UpdateNameplateHealth(0.5f);
        Debug.Log($"[NPCModule] {npcName} health displayed at 50%");
    }

    [ContextMenu("Debug: Toggle Nameplate")]
    private void DebugToggleNameplate()
    {
        if (nameplateInstance != null)
        {
            bool currentState = nameplateInstance.GetComponent<Canvas>().enabled;
            SetNameplateVisible(!currentState);
            Debug.Log($"[NPCModule] Nameplate {(!currentState ? "shown" : "hidden")}");
        }
    }

    // ========================================
    // Cleanup
    // ========================================

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (damageable != null)
        {
            damageable.OnDamageTaken -= HandleDamageTaken;
            damageable.OnDeath -= HandleDeath;
        }

        // Clean up nameplate
        if (nameplateInstance != null)
        {
            Destroy(nameplateInstance.gameObject);
        }
    }
}