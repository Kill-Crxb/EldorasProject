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

    [Header("Regeneration")]

    [Tooltip("Pool this refills from. Stamina draws on Recovery, Health on Regeneration, " +
             "Mana on Recollection. Leave empty for a resource that refills from nothing — " +
             "those use regenPerSecond instead.")]
    public ResourceDefinition regenSource;

    [Tooltip("How much of THIS resource one point of the source pool is worth. " +
             "1 Recovery buying 10 Stamina means a full 20 Recovery is 200 Stamina banked.")]
    public float amountPerSourcePoint = 10f;

    [Tooltip("How long one source point takes to pay out. 10 seconds at 10 per point " +
             "is 1 per second, and it costs 0.1 of the source each second.")]
    public float secondsPerSourcePoint = 10f;

    [Tooltip("Flat refill per second, used only when there is no regenSource. This is how " +
             "the source pools themselves come back — leave at 0 for a pool that only " +
             "refills at rest points or from consumables.")]
    public float regenPerSecond = 0f;

    [Tooltip("Seconds of quiet required after spending or taking damage before this starts " +
             "refilling again. Draining a source pool to pay for regen does not count.")]
    public float regenDelay = 0f;

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

        if (regenSource == this)
        { error = "regenSource points at itself"; return false; }

        if (regenSource != null && amountPerSourcePoint <= 0f)
        { error = "regenSource is set but amountPerSourcePoint is not positive"; return false; }

        if (regenSource != null && secondsPerSourcePoint <= 0f)
        { error = "regenSource is set but secondsPerSourcePoint is not positive"; return false; }

        return true;
    }

    #endregion
}
