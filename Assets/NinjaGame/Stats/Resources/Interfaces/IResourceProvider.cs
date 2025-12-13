using System;

public enum ResourceType { Health, Mana, Stamina, Energy, Rage, Focus }

public interface IResourceProvider
{
    float GetResource(ResourceType type);
    float GetMaxResource(ResourceType type);
    float GetResourcePercentage(ResourceType type);

    bool HasResource(ResourceType type, float amount);
    bool ConsumeResource(ResourceType type, float amount);
    void RestoreResource(ResourceType type, float amount);
    void SetResourceToMax(ResourceType type);

    event Action<ResourceType, float> OnResourceChanged;
}