using System;
using System.Collections.Generic;

[Serializable]
public class QuestSaveData
{
    public List<QuestState> active = new List<QuestState>();
    public List<string> completed = new List<string>();
    public List<string> failed = new List<string>();
    public List<FlagEntry> flags = new List<FlagEntry>();

    // "questId:stageIndex" for every stage that has paid out. Stat contributions aren't
    // saved by StatSystem, so these rewards are re-applied on load under the same keys.
    public List<string> paidStages = new List<string>();
}

[Serializable]
public class QuestState
{
    public string questId;
    public int stageIndex;
    public int[] counts;            // one per objective in the current stage, same order
    public float secondsRemaining;  // 0 = untimed. Ticks only while the character is in the world.
}

[Serializable]
public class FlagEntry
{
    public string id;
    public int value;
}
