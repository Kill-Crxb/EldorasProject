using UnityEngine;
using System.Collections.Generic;
using RPG.Factions;
using RPG.NPC.UI;

public class NPCModule : MonoBehaviour, IBrainModule
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("NPC Identity")]
    [SerializeField] private string npcId;
    [SerializeField] private string npcName;
    [SerializeField] private int npcLevel = 1;

    [Header("Configuration")]
    [SerializeField] private NPCArchetype appliedArchetype;

    [Header("Nameplate Settings")]
    [SerializeField] private bool enableNameplate = true;
    [SerializeField] private GameObject nameplatePrefab;
    [SerializeField] private Vector3 nameplateOffset = new Vector3(0, 2.5f, 0);

    //[Header("Network Settings")]
    //[SerializeField] private bool isNetworked = false;
    //[SerializeField] private bool isServerControlled = true;

    private NPCConfigurationHandler configurationHandler;
    private FactionAffiliationHandler factionHandler;
    private List<INPCHandler> handlers = new List<INPCHandler>();
    private ControllerBrain brain;
    private NPCNameplate nameplateInstance;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public string NPCId => npcId;
    public string NPCName => npcName;
    public int NPCLevel => npcLevel;
    public NPCArchetype AppliedArchetype => appliedArchetype;
    public ControllerBrain Brain => brain;

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;

        if (string.IsNullOrEmpty(npcId))
        {
            npcId = System.Guid.NewGuid().ToString();
        }

        DiscoverHandlers();

        foreach (var handler in handlers)
        {
            if (handler.IsEnabled)
            {
                handler.Initialize(this);
            }
        }

        if (enableNameplate)
        {
            CreateNameplate();
        }
    }

    public void UpdateModule()
    {
        if (!isEnabled) return;

        foreach (var handler in handlers)
        {
            if (handler.IsEnabled)
            {
                handler.UpdateHandler();
            }
        }
    }

    private void DiscoverHandlers()
    {
        handlers.Clear();

        var foundHandlers = GetComponentsInChildren<INPCHandler>();

        foreach (var handler in foundHandlers)
        {
            handlers.Add(handler);
        }

        configurationHandler = GetComponentInChildren<NPCConfigurationHandler>();
        factionHandler = GetComponentInChildren<FactionAffiliationHandler>();
    }

    public T GetHandler<T>() where T : class, INPCHandler
    {
        foreach (var handler in handlers)
        {
            if (handler is T typedHandler)
            {
                return typedHandler;
            }
        }
        return null;
    }

    public void SetArchetype(NPCArchetype archetype)
    {
        appliedArchetype = archetype;

        if (archetype.useGenericName && !string.IsNullOrEmpty(archetype.genericName))
        {
            npcName = archetype.genericName;
        }

        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateName(npcName);
        }
    }

    public void SetName(string name)
    {
        npcName = name;

        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateName(name);
        }
    }

    public void SetLevel(int level)
    {
        npcLevel = Mathf.Max(1, level);

        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateLevel(npcLevel);
        }
    }

    private void CreateNameplate()
    {
        if (nameplatePrefab == null)
        {
            return;
        }

        FactionType faction = factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;

        GameObject nameplateObj = Instantiate(nameplatePrefab, transform.position + nameplateOffset, Quaternion.identity);
        nameplateInstance = nameplateObj.GetComponent<NPCNameplate>();

        if (nameplateInstance != null)
        {
            nameplateInstance.Initialize(transform, npcName, npcLevel, faction);
        }
        else
        {
            Destroy(nameplateObj);
        }
    }

    public void UpdateNameplateHealth(float healthPercent)
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.UpdateHealth(healthPercent);
        }
    }

    public void SetNameplateVisible(bool visible)
    {
        if (nameplateInstance != null)
        {
            nameplateInstance.SetVisible(visible);
        }
    }

    public NPCNameplate GetNameplate()
    {
        return nameplateInstance;
    }

    public FactionType GetFaction()
    {
        return factionHandler != null ? factionHandler.AffiliatedFaction : FactionType.Neutral;
    }

    public FactionRelationship GetRelationshipToPlayer()
    {
        return factionHandler != null ? factionHandler.GetRelationshipToPlayer() : FactionRelationship.Neutral;
    }

    public bool IsHostileToPlayer()
    {
        return factionHandler != null && factionHandler.IsHostileToPlayer();
    }

    public bool IsFriendlyToPlayer()
    {
        return factionHandler != null && factionHandler.IsFriendlyToPlayer();
    }

    public bool CanInteractWithPlayer(PlayerInfoModule playerInfo)
    {
        if (factionHandler == null) return true;
        return factionHandler.CanInteractWithPlayer(playerInfo);
    }

    public bool IsHostileToPlayer(PlayerInfoModule playerInfo)
    {
        if (factionHandler == null) return false;
        return factionHandler.IsHostileToPlayer(playerInfo);
    }

    public bool IsAllyOf(NPCModule otherNPC)
    {
        if (factionHandler == null || otherNPC.factionHandler == null)
            return false;

        return factionHandler.IsAllyOf(otherNPC);
    }

    public bool IsEnemyOf(NPCModule otherNPC)
    {
        if (factionHandler == null || otherNPC.factionHandler == null)
            return false;

        return factionHandler.IsEnemyOf(otherNPC);
    }

    private void OnDestroy()
    {
        if (nameplateInstance != null)
        {
            Destroy(nameplateInstance.gameObject);
        }
    }
}