using UnityEngine;

public class FeetDetectionModule : MonoBehaviour, IPlayerModule
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask groundLayers = 256; // Layer 8 (Ground) = 2^8 = 256
    [SerializeField] private LayerMask wallLayers = 0;     // Set if you want wall detection
    [SerializeField] private LayerMask ceilingLayers = 0;  // Set if you want ceiling detection

    private ControllerBrain brain;

    public bool IsEnabled { get; set; } = true;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        if (brain == null)
        {
            Debug.LogError("FeetDetectionModule: Brain reference is null!");
            return;
        }

        // Validate setup
        var triggerCollider = GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            Debug.LogError($"{gameObject.name}: Missing BoxCollider component!");
            return;
        }

        if (!triggerCollider.isTrigger)
        {
            Debug.LogError($"{gameObject.name}: BoxCollider must be set as Trigger!");
            return;
        }
    }

    public void UpdateModule() { }

    #region Collision Detection

    void OnTriggerEnter(Collider other)
    {
        if (brain == null) return;

        FeetContactType contactType = GetContactTypeByLayer(other);

        if (contactType != FeetContactType.Unknown)
        {
            brain.NotifyFeetEnter(other, contactType);
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (brain == null) return;

        FeetContactType contactType = GetContactTypeByLayer(other);

        if (contactType != FeetContactType.Unknown)
        {
            brain.NotifyFeetStay(other, contactType);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (brain == null) return;

        FeetContactType contactType = GetContactTypeByLayer(other);

        if (contactType != FeetContactType.Unknown)
        {
            brain.NotifyFeetExit(other, contactType);
        }
    }

    #endregion

    #region Layer-Based Classification

    private FeetContactType GetContactTypeByLayer(Collider other)
    {
        int objectLayer = other.gameObject.layer;

        // Check ground layers first (most common)
        if (IsLayerInMask(objectLayer, groundLayers))
            return FeetContactType.Ground;

        // Check wall layers
        if (wallLayers.value != 0 && IsLayerInMask(objectLayer, wallLayers))
            return FeetContactType.Wall;

        // Check ceiling layers  
        if (ceilingLayers.value != 0 && IsLayerInMask(objectLayer, ceilingLayers))
            return FeetContactType.Ceiling;

        // Not a recognized layer
        return FeetContactType.Unknown;
    }

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask) != 0;
    }

    #endregion
}