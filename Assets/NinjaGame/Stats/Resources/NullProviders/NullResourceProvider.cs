using System;
using UnityEngine;

public class NullResourceProvider : MonoBehaviour, IResourceProvider
{
    public float GetResource(ResourceType type) => 100f;
    public float GetMaxResource(ResourceType type) => 100f;
    public float GetResourcePercentage(ResourceType type) => 100f;

    public bool HasResource(ResourceType type, float amount) => true;
    public bool ConsumeResource(ResourceType type, float amount) => true;
    public void RestoreResource(ResourceType type, float amount) { }
    public void SetResourceToMax(ResourceType type) { }

    public event Action<ResourceType, float> OnResourceChanged;
}