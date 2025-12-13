using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using RPG.Factions;

public class PlayerInfoModule : MonoBehaviour, IPlayerModule, ISaveable
{
    [Header("Module Settings")]
    [SerializeField] private bool isEnabled = true;

    [Header("Handler References")]
    [SerializeField] private PlayerFactionHandler factionHandler;
    [SerializeField] private IdentityHandler identityHandler;

    private ControllerBrain brain;
    private List<IPlayerInfoHandler> handlers = new List<IPlayerInfoHandler>();
    private bool isInitialized = false;

    public bool IsEnabled
    {
        get => isEnabled;
        set => isEnabled = value;
    }

    public ControllerBrain Brain => brain;
    public PlayerFactionHandler FactionHandler => factionHandler;
    public IdentityHandler IdentityHandler => identityHandler;
    public int HandlerCount => handlers.Count;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;

        DiscoverHandlers();

        foreach (var handler in handlers)
        {
            try
            {
                handler.Initialize(this);
            }
            catch
            {
            }
        }

        isInitialized = true;
    }

    public void UpdateModule()
    {
        if (!isEnabled || !isInitialized) return;

        foreach (var handler in handlers)
        {
            try
            {
                handler.UpdateHandler();
            }
            catch
            {
            }
        }
    }

    public string GetSaveId()
    {
        return "PlayerInfo";
    }

    public string GetSaveData()
    {
        var saveData = new PlayerInfoSaveData
        {
            version = GetSaveVersion(),
            handlerData = new Dictionary<string, string>()
        };

        foreach (var handler in handlers)
        {
            try
            {
                string handlerType = handler.GetType().Name;
                string handlerJson = handler.GetHandlerSaveData();
                saveData.handlerData[handlerType] = handlerJson;
            }
            catch
            {
            }
        }

        return JsonUtility.ToJson(saveData, true);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            var saveData = JsonUtility.FromJson<PlayerInfoSaveData>(json);

            foreach (var handler in handlers)
            {
                string handlerType = handler.GetType().Name;

                if (saveData.handlerData.TryGetValue(handlerType, out string handlerJson))
                {
                    try
                    {
                        handler.LoadHandlerData(handlerJson);
                    }
                    catch
                    {
                    }
                }
            }
        }
        catch
        {
        }
    }

    public int GetSaveVersion()
    {
        return 2;
    }

    public string GetCharacterName()
    {
        return identityHandler != null ? identityHandler.CharacterName : "Unknown";
    }

    public string GetCharacterId()
    {
        return identityHandler != null ? identityHandler.CharacterId : "";
    }

    public int GetCharacterLevel()
    {
        return identityHandler != null ? identityHandler.Level : 1;
    }

    public float GetTotalPlaytime()
    {
        return identityHandler != null ? identityHandler.TotalPlaytime : 0f;
    }

    public FactionType GetPlayerFaction()
    {
        return factionHandler != null ? factionHandler.GetFaction() : FactionType.Player;
    }

    public string GetPlayerFactionName()
    {
        return factionHandler != null ? factionHandler.GetFactionName() : "Player";
    }

    public void SetPlayerFaction(FactionType newFaction)
    {
        if (factionHandler != null)
        {
            factionHandler.SetFaction(newFaction);
        }
    }

    public FactionRelationship GetRelationshipWith(FactionType otherFaction)
    {
        return factionHandler != null
            ? factionHandler.GetRelationshipWith(otherFaction)
            : FactionRelationship.Neutral;
    }

    public bool IsHostileTo(FactionType otherFaction)
    {
        return factionHandler != null && factionHandler.IsHostileTo(otherFaction);
    }

    public bool IsFriendlyWith(FactionType otherFaction)
    {
        return factionHandler != null && factionHandler.IsFriendlyWith(otherFaction);
    }

    public bool ShouldNPCBeHostile(FactionType npcFaction)
    {
        return factionHandler != null && factionHandler.ShouldNPCBeHostile(npcFaction);
    }

    private void DiscoverHandlers()
    {
        handlers.Clear();

        // Only search for handlers if not already assigned in Inspector
        // This preserves manually assigned references!
        if (factionHandler == null)
        {
            factionHandler = GetComponentInChildren<PlayerFactionHandler>();
        }

        if (identityHandler == null)
        {
            identityHandler = GetComponentInChildren<IdentityHandler>();
        }

        // Build handler list from all IPlayerInfoHandler components in children
        var foundHandlers = GetComponentsInChildren<IPlayerInfoHandler>();

        foreach (var handler in foundHandlers)
        {
            handlers.Add(handler);
        }

        // Also add manually assigned handlers if they're not already in the list
        // This handles cases where handlers are assigned but not in children
        if (factionHandler != null && !handlers.Contains(factionHandler))
        {
            handlers.Add(factionHandler);
        }

        if (identityHandler != null && !handlers.Contains(identityHandler))
        {
            handlers.Add(identityHandler);
        }
    }

    public void ResetAllHandlers()
    {
        foreach (var handler in handlers)
        {
            try
            {
                handler.ResetHandler();
            }
            catch
            {
            }
        }
    }
}

[System.Serializable]
public class PlayerInfoSaveData
{
    public int version;
    public Dictionary<string, string> handlerData = new Dictionary<string, string>();
}