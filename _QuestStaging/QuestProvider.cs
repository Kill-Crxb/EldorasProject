using UnityEngine;

/// <summary>
/// Sits on an NPC. Interact() counts as talking to this NPC and then offers the first
/// quest the actor can take. No offer UI yet: the quest is accepted straight away.
///
/// Not wired to input. Call Interact() from the interaction handler once AR7 has traced
/// the live InteractionSystem, or from a debug key while testing.
/// </summary>
public class QuestProvider : MonoBehaviour
{
    [Tooltip("Used by Interact objectives.")]
    [SerializeField] private string npcId;
    [SerializeField] private QuestDefinition[] offers;

    public string NpcId => npcId;

    public void Interact(ControllerBrain actor)
    {
        if (actor == null) return;

        GameEvents.RaiseGameplayEvent(new GameplayEvent(ObjectiveType.Interact, actor, npcId));

        var quests = actor.GetModule<QuestSystem>();
        if (quests == null) return;

        foreach (var definition in offers)
        {
            if (!quests.CanAccept(definition)) continue;

            quests.Accept(definition.questId);
            return;
        }
    }
}
