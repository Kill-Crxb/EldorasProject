using UnityEngine;

[CreateAssetMenu(fileName = "New Resource", menuName = "NinjaGame/Resources/Resource Definition")]
public class ResourceDefinition : ScriptableObject
{
    [Header("Identity")]

    [Tooltip("Stable internal ID (lowercase, unique, never change after shipping)")]
    public string resourceId = "health"; // e.g. "health", "mana", "barrier"

    [Tooltip("Temporary resource (barrier, overshield, decay-based)")]
    public bool isTemporary;

    [Header("Display")]
    public string displayName = "Health";

    [TextArea(2, 4)]
    public string description = "Hit points";

    [Header("Stat Integration")]

    public string maxStatId = "character.max_health";

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

    #region Validation

    public bool Validate(out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(resourceId))
        { error = "resourceId is required"; return false; }

        if (resourceId != resourceId.ToLowerInvariant())
        { error = "resourceId must be lowercase"; return false; }

        if (string.IsNullOrWhiteSpace(displayName))
        { error = "displayName is required"; return false; }

        if (!allowNegative && minValue < 0)
        { error = "minValue < 0 but allowNegative is false"; return false; }

        return true;
    }

    #endregion
}