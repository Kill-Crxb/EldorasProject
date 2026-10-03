using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Checks every QuestDefinition in the project for authoring mistakes the runtime would
/// only find mid-quest: missing or duplicate ids, stages pointing nowhere, self-loops.
/// </summary>
public static class QuestValidator
{
    [MenuItem("NinjaGame/Quests/Validate Quest Definitions")]
    public static void ValidateAll()
    {
        var seenIds = new Dictionary<string, QuestDefinition>();
        int problems = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest == null) continue;

            foreach (string problem in Check(quest, seenIds))
            {
                Debug.LogError($"[QuestValidator] {quest.name}: {problem}", quest);
                problems++;
            }
        }

        Debug.Log($"[QuestValidator] {seenIds.Count} quests checked, {problems} problems.");
    }

    private static IEnumerable<string> Check(QuestDefinition quest, Dictionary<string, QuestDefinition> seenIds)
    {
        if (string.IsNullOrEmpty(quest.questId))
            yield return "no questId";
        else if (seenIds.TryGetValue(quest.questId, out var other))
            yield return $"questId '{quest.questId}' also used by {other.name}";
        else
            seenIds[quest.questId] = quest;

        if (quest.stages.Count == 0)
            yield return "no stages";

        var stageIndexes = new HashSet<int>();

        foreach (var stage in quest.stages)
        {
            if (!stageIndexes.Add(stage.stageIndex))
                yield return $"stage index {stage.stageIndex} used twice";
        }

        foreach (var stage in quest.stages)
        {
            string at = $"stage {stage.stageIndex}";

            if (stage.nextStage >= 0 && !stageIndexes.Contains(stage.nextStage))
                yield return $"{at} nextStage {stage.nextStage} doesn't exist";

            if (stage.nextStage == stage.stageIndex)
                yield return $"{at} points at itself";

            int exits = 0;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                string where = $"{at} objective {i}";

                if (string.IsNullOrEmpty(objective.targetId))
                    yield return $"{where} has no targetId";

                if (objective.requiredCount < 1)
                    yield return $"{where} requiredCount is below 1";

                if (objective.nextStage < 0) continue;

                exits++;

                if (!stageIndexes.Contains(objective.nextStage))
                    yield return $"{where} nextStage {objective.nextStage} doesn't exist";

                if (objective.nextStage == stage.stageIndex)
                    yield return $"{where} points back at its own stage";
            }

            if (exits > 2)
                yield return $"{at} has {exits} branch exits — the design allows two, write a second quest";
        }
    }
}
