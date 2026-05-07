using UnityEngine;

/// <summary>
/// AbilityDefinition - Validation Partial
/// Editor validation and configuration checking
/// </summary>
public partial class AbilityDefinition
{
    // ========================================
    // VALIDATION
    // ========================================

    /// <summary>
    /// Validate ability configuration
    /// </summary>
    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrEmpty(abilityId))
        {
            errorMessage = "Ability ID required";
            return false;
        }

        if (string.IsNullOrEmpty(abilityName))
        {
            errorMessage = "Ability Name required";
            return false;
        }

        if (resourceCosts != null)
        {
            foreach (var cost in resourceCosts)
            {
                if (cost.cost < 0)
                {
                    errorMessage = "Resource cost cannot be negative";
                    return false;
                }
            }
        }

        if (cooldown < 0)
        {
            errorMessage = "Cooldown cannot be negative";
            return false;
        }

        if (castTime < 0)
        {
            errorMessage = "Cast time cannot be negative";
            return false;
        }

        if (maxDuration < 0)
        {
            errorMessage = "Max duration cannot be negative";
            return false;
        }

        errorMessage = "";
        return true;
    }

    [ContextMenu("Validate Configuration")]
    private void ValidateConfiguration()
    {
        if (Validate(out string error))
            Debug.Log($"[AbilityDefinition] {abilityName} is valid");
        else
            Debug.LogError($"[AbilityDefinition] {abilityName} validation failed: {error}");
    }
}
