// Example targetable component
using UnityEngine;

public class BasicTargetable : MonoBehaviour, ITargetable
{
    [Header("Targetable Settings")]
    [SerializeField] private bool canBeTargeted = true;
    [SerializeField] private Vector3 targetOffset = Vector3.up;
    [SerializeField] private GameObject targetIndicator;

    public bool CanBeTargeted => canBeTargeted && gameObject.activeInHierarchy;

    public Vector3 GetTargetPoint()
    {
        return transform.position + targetOffset;
    }

    public void OnTargeted()
    {
        if (targetIndicator != null)
            targetIndicator.SetActive(true);

        // Could trigger UI elements, sound effects, etc.
     
    }

    public void OnTargetLost()
    {
        if (targetIndicator != null)
            targetIndicator.SetActive(false);

    
    }

    public void SetTargetable(bool targetable)
    {
        canBeTargeted = targetable;
    }
}