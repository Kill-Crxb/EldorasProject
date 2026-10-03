using System;
using System.Collections.Generic;
using UnityEngine;
using NinjaGame.Progression;

/// <summary>
/// A quest as authored. Never written at runtime: progress, counters and deadlines live in
/// QuestSystem's save. questId is the save key and must never change once shipped.
/// </summary>
[CreateAssetMenu(fileName = "Quest_", menuName = "NinjaGame/Quests/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    public string questId;
    public string displayName;
    [TextArea] public string description;
    public QuestType questType = QuestType.Side;
    public int uiPriority;
    public bool canAbandon = true;
    [Tooltip("Hidden quests stay out of the log until their first stage completes.")]
    public bool revealOnProgress;
    [Tooltip("Optional. Ties the quest to a ZoneDefinition.")]
    public string zoneId;

    [Tooltip("Must be open to accept the quest.")]
    public Gate requires = new Gate();

    [Tooltip("Stage indexes should leave gaps (10, 20, 30) so stages can be inserted later.")]
    public List<QuestStage> stages = new List<QuestStage>();

    public QuestStage GetStage(int stageIndex)
    {
        foreach (var stage in stages)
        {
            if (stage.stageIndex == stageIndex) return stage;
        }

        return null;
    }

    /// <summary>The stage after this one in list order, or null if it is the last.</summary>
    public QuestStage GetStageAfter(int stageIndex)
    {
        for (int i = 0; i < stages.Count - 1; i++)
        {
            if (stages[i].stageIndex == stageIndex) return stages[i + 1];
        }

        return null;
    }

    public QuestStage FirstStage => stages.Count > 0 ? stages[0] : null;
}

[Serializable]
public class QuestStage
{
    public int stageIndex = 10;
    public string stageName;
    [TextArea] public string description;

    [Tooltip("0 = untimed. The deadline itself is save data, never stored here.")]
    public float timeLimitSeconds;

    [Tooltip("Objective order is the save key for counters. Don't reorder a shipped stage.")]
    public List<QuestObjective> objectives = new List<QuestObjective>();

    public List<FlagChange> onEnter = new List<FlagChange>();
    [Tooltip("Applied if the quest fails or is abandoned while on this stage.")]
    public List<FlagChange> onFail = new List<FlagChange>();

    [Tooltip("Paid when this stage completes. Any stage may pay.")]
    public List<Reward> rewards = new List<Reward>();
    public List<ItemGrant> items = new List<ItemGrant>();

    [Tooltip("-1 = the next stage in the list, or complete the quest if this is the last.")]
    public int nextStage = -1;

    [Tooltip("Ends the quest when this stage completes. Needed on branch stages that aren't last in the list.")]
    public bool completesQuest;

    public bool HasRequiredObjectives()
    {
        foreach (var objective in objectives)
        {
            if (!objective.optional) return true;
        }

        return false;
    }
}

[Serializable]
public class QuestObjective
{
    public ObjectiveType type;
    [Tooltip("Kill: QuestTarget id. Find/Have/Submit: item id. Reach: location id. Interact: npc id. Flag: flag id.")]
    public string targetId;
    [Tooltip("For Flag objectives this is the value the flag must reach.")]
    public int requiredCount = 1;
    public SubmitMode submitMode;
    [TextArea] public string description;

    [Tooltip("Optional objectives never block the stage. A stage with only optional objectives completes when any one is done.")]
    public bool optional;

    [Tooltip("If this objective is what completes the stage, go here instead. -1 = use the stage's nextStage. Two exits per stage at most.")]
    public int nextStage = -1;
}

[Serializable]
public class ItemGrant
{
    public string itemId;
    public int count = 1;
}
