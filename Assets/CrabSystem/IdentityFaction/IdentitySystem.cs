using UnityEngine;
using RPG.Factions;

public class IdentitySystem : MonoBehaviour, IBrainModule, ISaveable
{
    public int InitOrder => 0;

    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Identity Data")]
    [SerializeField] private string displayName = "Entity";

    private ControllerBrain brain;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;

    public string DisplayName { get => displayName; set => displayName = value; }
    // The brain owns entity type and RPGSystem owns level; Identity only forwards them, so neither
    // can disagree with its owner or be saved twice (Audit 1 E1, Audit 4 S2).
    public EntityType Type => brain != null ? brain.EntityType : EntityType.Entity;
    public int Level => brain != null && brain.RPG != null ? brain.RPG.CurrentLevel : 1;

    // Entity unique identifier
    private string entityId = System.Guid.NewGuid().ToString();
    public string EntityId { get => entityId; set => entityId = value; }

    public bool IsPlayer => Type == EntityType.Player;
    public bool IsNPC => Type == EntityType.NPC || Type == EntityType.Enemy || Type == EntityType.Neutral;

    public EntityType GetEntityType() => Type;

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
            displayName = displayName
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
    }
}