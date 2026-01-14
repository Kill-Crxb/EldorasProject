using UnityEngine;
using RPG.Factions;
using RPG.NPC.UI;

/// <summary>
/// NPC Context Module - gathers NPC-specific identity data
/// 
/// Responsibilities:
/// - Apply archetype data to identity handlers
/// - Create and update nameplate
/// - Track NPC lifetime
/// - Handle NPC-specific configuration
/// 
/// Note: NPCConfigurationHandler still exists and does the heavy lifting for archetype application.
/// This context module focuses on identity-specific concerns (nameplate, basic identity).
/// </summary>
public class NPCContextModule : MonoBehaviour, IContextModule
{
    [Header("NPC Configuration")]
    [SerializeField] private NPCArchetype archetype;
    [SerializeField] private bool applyArchetypeOnInit = true;

    [Header("Nameplate Settings")]
    [SerializeField] private bool enableNameplate = true;
    [SerializeField] private GameObject nameplatePrefab;
    [SerializeField] private Vector3 nameplateOffset = new Vector3(0, 2.5f, 0);

    [Header("Overrides (Optional)")]
    [Tooltip("Override archetype name (leave empty to use archetype name)")]
    [SerializeField] private string overrideName;
    [Tooltip("Override archetype level (-1 = use archetype)")]
    [SerializeField] private int overrideLevel = -1;

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;

    // References
    private IdentitySystem parent;
    private NPCNameplate nameplateInstance;

    // Properties
    public NPCArchetype Archetype => archetype;
    public NPCNameplate Nameplate => nameplateInstance;

    #region IContextModule Implementation

    public void Initialize(IdentitySystem parentSystem)
    {
        parent = parentSystem;

        // Set entity type to NPC
        if (parent.Identity != null)
        {
            parent.Identity.Type = EntityType.NPC;
        }

        // Apply basic archetype data to identity handlers
        if (applyArchetypeOnInit && archetype != null)
        {
            ApplyBasicArchetypeData();
        }

        // Create nameplate
        if (enableNameplate && nameplatePrefab != null)
        {
            CreateNameplate();
        }

        if (debugMode)
        {
            Debug.Log($"[NPCContextModule] Initialized {parent.GetEntityName()} " +
                     $"(Archetype: {archetype?.archetypeName ?? "None"})");
        }
    }

    public void GatherContext()
    {
        // Update lifetime tracking (handled by IdentityHandler)

        // Update nameplate with current health
        UpdateNameplateHealth();
    }

    public string GetContextSaveData()
    {
        var saveData = new NPCContextSaveData
        {
            archetypeId = archetype?.archetypeId,
            nameplateEnabled = enableNameplate
        };
        return JsonUtility.ToJson(saveData);
    }

    public void LoadContextData(string json)
    {
        if (string.IsNullOrEmpty(json)) return;

        var saveData = JsonUtility.FromJson<NPCContextSaveData>(json);

        enableNameplate = saveData.nameplateEnabled;

        // Reload archetype if needed (requires archetype database lookup)
        // For now, assume archetype is assigned in inspector
    }

    #endregion

    #region Archetype Application (Basic Identity Only)

    /// <summary>
    /// Apply basic identity data from archetype
    /// Note: NPCConfigurationHandler still handles the full archetype application
    /// (stats, abilities, combat behavior, etc.). This just sets name/level/faction.
    /// </summary>
    private void ApplyBasicArchetypeData()
    {
        if (archetype == null)
        {
            Debug.LogWarning("[NPCContextModule] No archetype assigned!");
            return;
        }

        // Apply name
        string finalName = !string.IsNullOrEmpty(overrideName) ? overrideName
                         : archetype.useGenericName ? archetype.genericName
                         : archetype.archetypeName;

        if (parent.Identity != null)
        {
            parent.Identity.DisplayName = finalName;
        }

        // Apply level
        int finalLevel = overrideLevel >= 0 ? overrideLevel
                       : CalculateLevelFromArchetype();

        if (parent.Identity != null)
        {
            parent.Identity.Level = finalLevel;
        }

        // Apply faction (convert NPCFaction enum to FactionType)
        if (parent.Faction != null)
        {
            FactionType factionType = ConvertToFactionType(archetype.faction);
            parent.Faction.SetFaction(factionType);
        }

        if (debugMode)
        {
            Debug.Log($"[NPCContextModule] Applied basic archetype data: {finalName} (Lvl {finalLevel})");
        }
    }

    private int CalculateLevelFromArchetype()
    {
        // Example: Base level from importance tier
        return archetype.importance switch
        {
            NPCImportance.Civilian => 0,
            NPCImportance.Soldier => 1,
            NPCImportance.Elite => 3,
            NPCImportance.Hero => 5,
            NPCImportance.Boss => 10,
            _ => 1
        };
    }

    private FactionType ConvertToFactionType(NPCFaction npcFaction)
    {
        // Map NPCFaction enum values to FactionType enum values
        return npcFaction switch
        {
            NPCFaction.None => FactionType.None,
            NPCFaction.Humans => FactionType.Humans,
            NPCFaction.Elves => FactionType.Elves,
            NPCFaction.Dwarves => FactionType.Dwarves,
            NPCFaction.Undead => FactionType.Undead,
            NPCFaction.Warlocks => FactionType.Warlocks,
            NPCFaction.Demons => FactionType.Hostile,
            NPCFaction.Wildlife => FactionType.Wildlife,
            NPCFaction.Beasts => FactionType.Monsters,
            NPCFaction.Neutral => FactionType.Neutral,
            NPCFaction.Player => FactionType.Player,
            _ => FactionType.Neutral
        };
    }

    #endregion

    #region Nameplate Management

    private void CreateNameplate()
    {
        if (nameplatePrefab == null)
        {
            Debug.LogWarning("[NPCContextModule] No nameplate prefab assigned!");
            return;
        }

        FactionType faction = parent.Faction?.GetFaction() ?? FactionType.Neutral;

        GameObject nameplateObj = Instantiate(nameplatePrefab,
            parent.transform.position + nameplateOffset,
            Quaternion.identity);

        nameplateInstance = nameplateObj.GetComponent<NPCNameplate>();

        if (nameplateInstance != null)
        {
            nameplateInstance.Initialize(parent.transform,
                parent.GetEntityName(),
                parent.GetLevel(),
                faction);

            if (debugMode)
            {
                Debug.Log($"[NPCContextModule] Created nameplate for {parent.GetEntityName()}");
            }
        }
        else
        {
            Debug.LogError("[NPCContextModule] Nameplate prefab missing NPCNameplate component!");
            Destroy(nameplateObj);
        }
    }

    private void UpdateNameplateHealth()
    {
        if (nameplateInstance == null) return;

        // Get health from Brain
        var health = parent.Brain?.GetProvider<IHealthProvider>();
        if (health != null)
        {
            float maxHealth = health.GetMaxHealth();
            if (maxHealth > 0)
            {
                float percent = health.GetCurrentHealth() / maxHealth;
                nameplateInstance.UpdateHealth(percent);
            }
        }
    }

    /// <summary>
    /// Manually update nameplate visibility
    /// </summary>
    public void SetNameplateVisible(bool visible)
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.SetVisible(visible);
        }
    }

    #endregion

    #region NPC-Specific Helpers

    /// <summary>
    /// Update NPC name (and nameplate)
    /// </summary>
    public void SetName(string newName)
    {
        if (parent.Identity != null)
        {
            parent.Identity.DisplayName = newName;
        }

        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateName(newName);
        }
    }

    /// <summary>
    /// Update NPC level (and nameplate)
    /// </summary>
    public void SetLevel(int newLevel)
    {
        if (parent.Identity != null)
        {
            parent.Identity.Level = newLevel;
        }

        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateLevel(newLevel);
        }
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        if (nameplateInstance != null)
        {
            Destroy(nameplateInstance.gameObject);
        }
    }

    #endregion
}

#region Save Data Structure

[System.Serializable]
public class NPCContextSaveData
{
    public string archetypeId;
    public bool nameplateEnabled;
}

#endregion