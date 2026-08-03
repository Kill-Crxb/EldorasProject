using UnityEngine;

public class PersistentNPCConfigurator : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string entityId;
    [SerializeField] private string displayNameOverride;

    [Header("Source")]
    [SerializeField] private NPCArchetype archetype;

    [Header("Level")]
    [SerializeField] private int level = 1;

    [Header("References")]
    [SerializeField] private ControllerBrain brain;

    private void Start()
    {
        if (brain == null)
            brain = GetComponentInParent<ControllerBrain>();

        if (brain == null)
        {
            Debug.LogError($"[PersistentNPCConfigurator] No ControllerBrain found for '{name}'");
            return;
        }

        if (!ValidateConfig())
            return;

        if (brain.IsInitialized)
            Configure();
        else
            brain.OnInitialized += _ => Configure();
    }

    private bool ValidateConfig()
    {
        if (string.IsNullOrEmpty(entityId))
        {
            Debug.LogError($"[PersistentNPCConfigurator] '{name}' has no entityId assigned");
            return false;
        }

        if (archetype == null)
        {
            Debug.LogError($"[PersistentNPCConfigurator] '{name}' has no archetype assigned");
            return false;
        }

        return true;
    }

    private void Configure()
    {
        if (brain.Identity != null)
            brain.Identity.EntityId = entityId;

        string resolvedName = !string.IsNullOrEmpty(displayNameOverride)
            ? displayNameOverride
            : archetype.archetypeName;

        int effectiveLevel = level > 0 ? level : archetype.baseLevel;

        string modelId = archetype.modelPool != null && archetype.modelPool.Count > 0
            ? archetype.modelPool[0]
            : null;

        var configData = new CharacterConfigData(
            displayName: resolvedName,
            factionId: archetype.factionId,
            modelId: modelId,
            baseStatOverrides: archetype.baseStatOverrides,
            level: effectiveLevel
        );

        configData.characterId = entityId;
        configData.isPersistent = true;

        GameEvents.CharacterConfigDataReady(configData);
    }
}
