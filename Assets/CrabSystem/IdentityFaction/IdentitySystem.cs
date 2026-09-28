using UnityEngine;
using RPG.Factions;

public class IdentitySystem : MonoBehaviour, IBrainModule, ISaveable
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Identity Data")]
    [SerializeField] private string displayName = "Entity";
    [SerializeField] private EntityType type = EntityType.Entity;
    [SerializeField] private int level = 1;

    private ControllerBrain brain;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;

    public string DisplayName { get => displayName; set => displayName = value; }
    public EntityType Type { get => type; set => type = value; }
    public int Level { get => level; set => level = value; }

    // Entity unique identifier
    private string entityId = System.Guid.NewGuid().ToString();
    public string EntityId { get => entityId; set => entityId = value; }

    public bool IsPlayer => type == EntityType.Player;
    public bool IsNPC => type == EntityType.NPC || type == EntityType.Enemy || type == EntityType.Neutral;

    public EntityType GetEntityType() => type;

    /// <summary>This entity's faction asset, via the brain's FactionSystem. Null if unaffiliated.</summary>
    public FactionDefinition GetFaction() => brain?.Faction?.CurrentFaction;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
    }

    public void UpdateModule()
    {
    }

    public void LateInitialize()
    {
    }

    #region ISaveable

    public string GetSaveId() => "identity";

    public string GetSaveData()
    {
        var data = new IdentitySaveData
        {
            entityId = entityId,
            displayName = displayName,
            type = type.ToString(),
            level = level
        };
        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            var data = JsonUtility.FromJson<IdentitySaveData>(json);
            if (data != null)
            {
                if (!string.IsNullOrEmpty(data.entityId))
                    entityId = data.entityId;
                displayName = data.displayName;
                level = data.level;
                if (System.Enum.TryParse<EntityType>(data.type, out var parsedType))
                    type = parsedType;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[IdentitySystem] Couldn't read identity save — keeping defaults: {e.Message}");
        }
    }

    public int GetSaveVersion() => 1;

    #endregion

    [System.Serializable]
    private class IdentitySaveData
    {
        public string entityId;
        public string displayName;
        public string type;
        public int level;
    }
}