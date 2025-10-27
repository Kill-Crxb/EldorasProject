using UnityEngine;

public class RPGDefenseAdapter : MonoBehaviour, IDefenseProvider
{
    [Header("Manual References (Optional)")]
    [SerializeField] private MonoBehaviour defenseCapabilityModule;

    private ControllerBrain brain;
    private IDefenseCapability defenseCapability;
    private bool isInitialized = false;

    void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        brain = GetComponentInParent<ControllerBrain>();
        if (brain == null)
        {
            Debug.LogError($"[RPGDefenseAdapter] No ControllerBrain found in parent!");
            return;
        }

        if (defenseCapabilityModule == null)
        {
            defenseCapabilityModule = GetComponentInChildren<IDefenseCapability>() as MonoBehaviour;
        }

        if (defenseCapabilityModule != null && defenseCapabilityModule is IDefenseCapability capability)
        {
            defenseCapability = capability;
        }

        isInitialized = true;
    }

    public float ProcessIncomingDamage(float damage, Vector3 attackDirection)
    {
        if (!ValidateDefense())
        {
            return damage;
        }

        float multiplier = defenseCapability.GetDefensiveMultiplier(attackDirection);
        return damage * multiplier;
    }

    public bool IsBlocking()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.IsBlocking;
    }

    public bool IsParrying()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.IsParrying;
    }

    public bool CanDefend()
    {
        if (!ValidateDefense()) return false;
        return defenseCapability.CanDefend;
    }

    private bool ValidateDefense()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (defenseCapability == null)
        {
            return false;
        }

        return true;
    }
}