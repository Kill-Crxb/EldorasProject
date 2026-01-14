using System;
using System.Collections.Generic;

public interface IResourceProvider
{
    float GetResource(ResourceDefinition def);
    float GetMaxResource(ResourceDefinition def);
    float GetResourcePercentage(ResourceDefinition def);

    bool HasResource(ResourceDefinition def, float amount);
    bool ConsumeResource(ResourceDefinition def, float amount);
    void RestoreResource(ResourceDefinition def, float amount);
    void SetResourceToMax(ResourceDefinition def);

    IReadOnlyDictionary<ResourceDefinition, float> GetAllResources();

    event Action<ResourceDefinition, float> OnResourceChanged;
}
