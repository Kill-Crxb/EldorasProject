using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Resource", menuName = "NinjaGame/Resources/Resource Definition")]
public class ResourceDefinition : ScriptableObject
{
    [Header("Identity")]

    [Tooltip("Stable internal ID (lowercase, unique, never change after shipping)")]
    public string resourceId = "health"; // e.g. "health", "mana", "barrier"

    [Tooltip("Semantic role: behaves like health (damage, death, UI priority)")]
    public bool isHealthLike;

    [Tooltip("Temporary resource (barrier, overshield, decay-based)")]
    public bool isTemporary;

    [Header("Display")]
    public string displayName = "Health";

    [TextArea(2, 4)]
    public string description = "Hit points";

    [Header("Stat Integration")]

    public string maxStatId = "character.max_health";
    public string currentStatId = "";

    [Header("Regeneration")]

    public float regenPerSecond = 0f;
    public float regenDelay = 0f;

    public bool regenOutOfCombatOnly = false;
    public float combatTimeout = 5f;

    [Header("Constraints")]

    public float minValue = 0f;
    public bool allowNegative = false;

    [Header("Visual / Audio")]

    public Color resourceColor = Color.red;
    public Sprite icon;

    public AudioClip depletedSound;
    public AudioClip restoredSound;

    [Header("Behavior")]

    [Tooltip("If true, reaching min value causes death")]
    public bool triggerDeathOnDepletion;

    [Tooltip("Reset to max on death (rage, focus, combo meters)")]
    public bool resetOnDeath;

    [Header("Tags (Effects & Queries)")]

    [Tooltip("Used by effects & abilities (e.g. LowLife, Shield, Energy)")]
    public List<string> tags = new();

    #region Validation

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            error = "resourceId is required";
            return false;
        }

        if (resourceId != resourceId.ToLowerInvariant())
        {
            error = "resourceId must be lowercase";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "displayName is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(maxStatId))
        {
            error = "maxStatId is required";
            return false;
        }

        if (!allowNegative && minValue < 0)
        {
            error = "minValue < 0 but allowNegative is false";
            return false;
        }

        if (regenPerSecond < 0 || regenDelay < 0)
        {
            error = "regen values cannot be negative";
            return false;
        }

        if (isHealthLike && !triggerDeathOnDepletion)
        {
            error = "Health-like resources should trigger death on depletion";
            return false;
        }

        error = "";
        return true;
    }

    #endregion
}
