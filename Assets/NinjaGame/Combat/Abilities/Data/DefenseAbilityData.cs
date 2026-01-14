using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Defines a defensive ability for dynamic resource-driven defense.
/// Supports any number of resources (Stamina, Mana, Chaos, Faith, etc.)
/// </summary>
[CreateAssetMenu(fileName = "New Defense Ability", menuName = "RPG/Defense Ability")]
public class DefenseAbilityData : ScriptableObject
{
    [Header("Basic Info")]
    public string defenseId;
    public string defenseName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Block Effectiveness")]
    [Range(0f, 1f)]
    public float blockDamageReduction = 0.7f;
    [Range(0f, 360f)]
    public float blockAngle = 120f;

    [Header("Parry Window (Sekiro-Style)")]
    public float parryWindowDuration = 0.2f;
    [Range(0f, 1f)]
    public float parryDamageReduction = 1.0f;
    public bool parryEnablesCounter = true;
    public float counterWindowDuration = 0.8f;

    [Header("Timing")]
    public float blockStartupTime = 0.05f;

    [Header("Animation Parameters")]
    public string blockAnimParam = "IsBlocking";
    public string parryAnimTrigger = "Parry";
    public string blockHitAnimTrigger = "BlockHit";

    [Header("Effects & Feedback")]
    public GameObject blockActivationVFX;
    public GameObject parrySuccessVFX;
    public GameObject blockImpactVFX;

    [Header("Weapon Requirements (Optional)")]
    public WeaponType[] requiredWeaponTypes;
    public bool allowsUnarmed = false;

    [Header("Dynamic Resource Costs")]
    [Tooltip("List of resources consumed while defending")]
    public List<ResourceEntry> resourceCosts = new List<ResourceEntry>();

    [System.Serializable]
    public class ResourceEntry
    {
        public ResourceDefinition resource;
        public float blockCost = 0f;      // Cost per blocked attack
        public float parryCost = 0f;      // Cost per parried attack
        public float blockDrain = 0f;     // Continuous drain per second
        public float parryRefund = 0f;    // Refund amount on successful parry
    }

    #region Public Methods

    /// <summary>
    /// Returns all resources required by this defense
    /// </summary>
    public IEnumerable<ResourceDefinition> GetAllRequiredResources()
    {
        foreach (var entry in resourceCosts)
            if (entry.resource != null)
                yield return entry.resource;
    }

    /// <summary>
    /// Get continuous drain per second for a resource
    /// </summary>
    public float GetResourceDrain(ResourceDefinition resource)
    {
        var entry = resourceCosts.Find(x => x.resource == resource);
        return entry != null ? entry.blockDrain : 0f;
    }

    /// <summary>
    /// Get cost for this resource when defending an incoming attack
    /// </summary>
    public float GetResourceCost(ResourceDefinition resource, bool isParry)
    {
        var entry = resourceCosts.Find(x => x.resource == resource);
        if (entry == null) return 0f;
        return isParry ? entry.parryCost : entry.blockCost;
    }

    /// <summary>
    /// Check if this resource is refunded on successful parry
    /// </summary>
    public bool RefundsResource(ResourceDefinition resource)
    {
        var entry = resourceCosts.Find(x => x.resource == resource);
        return entry != null && entry.parryRefund > 0f;
    }

    /// <summary>
    /// Amount refunded for this resource on successful parry
    /// </summary>
    public float GetResourceRefund(ResourceDefinition resource)
    {
        var entry = resourceCosts.Find(x => x.resource == resource);
        return entry != null ? entry.parryRefund : 0f;
    }

    /// <summary>
    /// Calculate damage reduction based on whether attack was parried
    /// </summary>
    public float GetDamageReduction(bool isInParryWindow)
    {
        return isInParryWindow ? parryDamageReduction : blockDamageReduction;
    }

    /// <summary>
    /// Check if this defense can be used with current weapon
    /// </summary>
    public bool IsCompatibleWithWeapon(WeaponType weaponType)
    {
        if (requiredWeaponTypes == null || requiredWeaponTypes.Length == 0)
            return true;

        foreach (var type in requiredWeaponTypes)
            if (type == weaponType)
                return true;

        return false;
    }

    #endregion
}
