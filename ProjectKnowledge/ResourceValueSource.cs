using UnityEngine;

/// <summary>
/// Value source that queries a ResourceDefinition through IResourceProvider.
/// Type-safe: Can only reference ResourceDefinition assets.
/// 
/// Use Cases:
/// - Health percentage for "IsWounded" condition
/// - Mana percentage for "LowMana" condition
/// - Stamina current for "CanDodge" condition
/// 
/// Properties:
/// - Current: Raw current value (75 health)
/// - Max: Maximum capacity (100 health)
/// - Percentage: Ratio 0-1 (0.75 = 75%)
/// 
/// Example Configuration:
/// Health_Percentage.asset:
///   resource: Health.asset (ResourceDefinition)
///   property: Percentage
///   
/// Usage in Condition:
/// BlackboardCondition "IsWounded":
///   valueSource: Health_Percentage.asset
///   comparison: LessThan
///   threshold: 0.25
///   outputFactKey: "IsWounded"
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
[CreateAssetMenu(fileName = "ResourceValue", menuName = "NinjaGame/Blackboard/Value Sources/Resource")]
public class ResourceValueSource : ValueSourceDefinition
{
    [Header("Resource Query")]
    [Tooltip("Resource to query (drag ResourceDefinition asset)")]
    [SerializeField] private ResourceDefinition resource;

    [Tooltip("Which property to read")]
    [SerializeField] private ResourceValueProperty property = ResourceValueProperty.Percentage;

    public override float GetValue(ControllerBrain brain)
    {
        // Guard clauses (flat logic)
        if (brain == null) return 0f;
        if (resource == null) return 0f;

        var resources = brain.Resources;
        if (resources == null) return 0f;

        // Query appropriate property
        switch (property)
        {
            case ResourceValueProperty.Current:
                return resources.GetResource(resource);

            case ResourceValueProperty.Max:
                return resources.GetMaxResource(resource);

            case ResourceValueProperty.Percentage:
                return resources.GetResourcePercentage(resource);

            default:
                return 0f;
        }
    }

    public override string GetDisplayName()
    {
        if (resource == null) return "None";
        return $"{resource.displayName} ({property})";
    }

    public override bool Validate(out string error)
    {
        if (resource == null)
        {
            error = "Resource not assigned";
            return false;
        }

        error = null;
        return true;
    }
}

/// <summary>
/// Which resource property to query
/// </summary>
public enum ResourceValueProperty
{
    Current,    // Raw current value (75 health)
    Max,        // Maximum capacity (100 health)
    Percentage  // Ratio 0-1 (0.75 = 75%)
}
