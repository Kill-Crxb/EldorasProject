using UnityEngine;

/// <summary>
/// Resource Definition - ScriptableObject defining a resource type
/// 
/// Purpose:
/// - Data-driven resource configuration
/// - Maps resource type to stat ID
/// - Defines regeneration rules
/// - Configurable per game/mode
/// 
/// Usage:
/// Create via Assets > Create > NinjaGame > Resources > Resource Definition
/// 
/// Example:
/// Health Resource:
///   resourceType: Health
///   statId: "character.max_health"
///   regenPerSecond: 0
///   regenDelay: 5
///   color: Red
/// 
/// Architecture:
/// ResourceManager (holds collection) → ResourceSystem (uses definitions)
/// 
/// Created: January 2026
/// </summary>
[CreateAssetMenu(fileName = "New Resource", menuName = "NinjaGame/Resources/Resource Definition")]
public class ResourceDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Type of resource this definition represents")]
    public ResourceType resourceType = ResourceType.Health;

    [Tooltip("Display name for UI")]
    public string displayName = "Health";

    [Tooltip("Short description")]
    [TextArea(2, 4)]
    public string description = "Hit points";

    [Header("Stat Integration")]
    [Tooltip("Stat ID for maximum value (e.g., 'character.max_health')")]
    public string maxStatId = "character.max_health";

    [Tooltip("Optional stat ID for current value (if stored in stats)")]
    public string currentStatId = ""; // Usually empty - ResourceSystem tracks current

    [Header("Regeneration")]
    [Tooltip("Regeneration per second (0 = no regen)")]
    public float regenPerSecond = 0f;

    [Tooltip("Delay before regen starts after consumption (seconds)")]
    public float regenDelay = 0f;

    [Tooltip("Can regenerate out of combat?")]
    public bool regenOutOfCombatOnly = false;

    [Tooltip("Combat timeout (seconds after last damage/ability to exit combat)")]
    public float combatTimeout = 5f;

    [Header("Constraints")]
    [Tooltip("Minimum value (usually 0)")]
    public float minValue = 0f;

    [Tooltip("Can go below zero? (for systems like debt/corruption)")]
    public bool allowNegative = false;

    [Header("Visual/Audio")]
    [Tooltip("Color for UI bars and effects")]
    public Color resourceColor = Color.red;

    [Tooltip("Icon for UI")]
    public Sprite icon;

    [Tooltip("Sound when resource depleted")]
    public AudioClip depletedSound;

    [Tooltip("Sound when resource restored")]
    public AudioClip restoredSound;

    [Header("Special Behavior")]
    [Tooltip("Trigger death when depleted? (typically only Health)")]
    public bool triggerDeathOnDepletion = false;

    [Tooltip("Auto-restore to max on death? (for rage/focus systems)")]
    public bool resetOnDeath = false;

    #region Validation

    /// <summary>
    /// Validate configuration
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            errorMessage = "Display name is required";
            return false;
        }

        if (string.IsNullOrEmpty(maxStatId))
        {
            errorMessage = "Max stat ID is required";
            return false;
        }

        if (regenPerSecond < 0)
        {
            errorMessage = "Regen per second cannot be negative";
            return false;
        }

        if (regenDelay < 0)
        {
            errorMessage = "Regen delay cannot be negative";
            return false;
        }

        errorMessage = "";
        return true;
    }

    [ContextMenu("Validate Configuration")]
    private void ValidateConfiguration()
    {
        if (Validate(out string error))
        {
            Debug.Log($"[ResourceDefinition] {displayName} configuration is valid");
        }
        else
        {
            Debug.LogError($"[ResourceDefinition] {displayName} validation failed: {error}");
        }
    }

    [ContextMenu("Print Summary")]
    private void PrintSummary()
    {
        Debug.Log($"=== {displayName} ===");
        Debug.Log($"Type: {resourceType}");
        Debug.Log($"Max Stat: {maxStatId}");
        Debug.Log($"Regen: {regenPerSecond}/sec (delay: {regenDelay}s)");
        Debug.Log($"Min: {minValue}, Allow Negative: {allowNegative}");
        Debug.Log($"Death on Depletion: {triggerDeathOnDepletion}");
    }

    #endregion
}