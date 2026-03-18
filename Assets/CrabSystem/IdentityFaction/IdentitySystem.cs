using UnityEngine;
using System.Collections.Generic;
using RPG.Factions;

public class IdentitySystem : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Handlers")]
    [SerializeField] private UniversalIdentityHandler identityHandler;
    [SerializeField] private UniversalFactionHandler factionHandler;
    [SerializeField] private MonoBehaviour modelHandler;

    private ControllerBrain brain;
    private List<IIdentityHandler> handlers = new List<IIdentityHandler>();

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }
    public ControllerBrain Brain => brain;

    public UniversalIdentityHandler Identity => identityHandler;
    public UniversalFactionHandler Faction => factionHandler;
    public MonoBehaviour Model => modelHandler;

    public bool IsPlayer => GetEntityType() == EntityType.Player;
    public bool IsNPC => GetEntityType() == EntityType.NPC || GetEntityType() == EntityType.Enemy || GetEntityType() == EntityType.Neutral;
    public bool IsObject => GetEntityType() == EntityType.Prop;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        DiscoverHandlers();

        foreach (var handler in handlers)
        {
            if (handler != null && handler.IsEnabled)
                handler.Initialize(this);
        }

        if (IsNPC)
            NameplateManager.Instance?.SpawnNameplate(brain);
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;
        foreach (var handler in handlers)
        {
            if (handler != null && handler.IsEnabled)
                handler.UpdateHandler();
        }
    }

    private void DiscoverHandlers()
    {
        handlers.Clear();

        if (identityHandler != null) handlers.Add(identityHandler);
        if (factionHandler != null) handlers.Add(factionHandler);

        if (modelHandler is IBrainModule modelBrain)
            modelBrain.Initialize(brain);

        foreach (var handler in GetComponentsInChildren<IIdentityHandler>())
        {
            if (!handlers.Contains(handler))
                handlers.Add(handler);
        }
    }

    // ── Identity Queries ──────────────────────────────────────────────────

    public string GetEntityName() => identityHandler?.DisplayName ?? "Unknown";
    public new string GetEntityId() => identityHandler?.EntityId ?? "";
    public int GetLevel() => identityHandler?.Level ?? 0;
    public EntityType GetEntityType() => identityHandler?.Type ?? EntityType.Entity;
    public float GetExistenceTime() => identityHandler?.ExistenceTime ?? 0f;

    // ── Faction Queries ───────────────────────────────────────────────────

    public FactionType GetFaction() => factionHandler != null ? factionHandler.GetFaction() : FactionType.None;
    public bool IsHostileTo(FactionType other) => factionHandler?.IsHostileTo(other) ?? false;
    public bool IsFriendlyWith(FactionType other) => factionHandler?.IsFriendlyWith(other) ?? false;
    public FactionRelationship GetRelationshipWith(FactionType other) =>
        factionHandler?.GetRelationshipWith(other) ?? FactionRelationship.Neutral;

    // ── Model Queries ─────────────────────────────────────────────────────

    public GameObject GetCurrentModel()
    {
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm) return mm.CurrentModel;
        return null;
    }

    public string GetModelId()
    {
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm) return mm.CurrentModelId ?? "";
        return "";
    }

    public bool SwapModel(string modelId)
    {
        if (modelHandler is CrabThirdPerson.Character.ModelModule mm) return mm.SwapModel(modelId);
        return false;
    }

    // ── Debug ─────────────────────────────────────────────────────────────

    [ContextMenu("Debug: Print Identity Info")]
    private void DebugPrintInfo()
    {
        Debug.Log($"[IdentitySystem] Name={GetEntityName()} | ID={GetEntityId()} | Type={GetEntityType()} | Level={GetLevel()} | Faction={GetFaction()}");
    }
}