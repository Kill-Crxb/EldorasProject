using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registry of every quest. Definitions come from the inspector array and from any
/// Resources/QuestDatabase folder; saves, providers and zones resolve quests by id here.
/// Same shape as ItemManager. Lives on Manager_Brain.
/// </summary>
public class QuestManager : MonoBehaviour, IGameManager
{
    private const string DatabaseFolder = "QuestDatabase";

    private static QuestManager instance;

    [Tooltip("Optional. Anything in a Resources/QuestDatabase folder is loaded as well.")]
    [SerializeField] private QuestDefinition[] questDefinitions;

    private readonly Dictionary<string, QuestDefinition> lookup = new Dictionary<string, QuestDefinition>();

    #region IGameManager

    public string ManagerName => "Quest Manager";
    public int InitializationPriority => 20; // After Items (5)
    public bool IsEnabled => enabled;
    public bool IsInitialized { get; private set; }

    public void Initialize()
    {
        if (IsInitialized) return;

        instance = this;
        lookup.Clear();

        int count = Register(questDefinitions);
        count += Register(Resources.LoadAll<QuestDefinition>(DatabaseFolder));

        if (count == 0)
            Debug.LogWarning($"[QuestManager] No quests loaded. Assign them in the inspector or drop them in a Resources/{DatabaseFolder} folder.");

        IsInitialized = true;
    }

    public void LateInitialize() { }

    public void Shutdown() { }

    public ValidationResult Validate()
    {
        var result = ValidationResult.Success();
        if (!IsInitialized) return result;

        if (lookup.Count == 0)
            result.Warnings.Add($"No quests loaded — inspector array is empty and Resources/{DatabaseFolder} found nothing");
        else
            result.Info.Add($"{lookup.Count} quests registered");

        return result;
    }

    #endregion

    // An asset listed in both the array and the folder is skipped quietly the second time.
    private int Register(QuestDefinition[] definitions)
    {
        if (definitions == null) return 0;

        int count = 0;

        foreach (var definition in definitions)
        {
            if (definition == null) continue;

            if (string.IsNullOrEmpty(definition.questId))
            {
                Debug.LogWarning($"[QuestManager] '{definition.name}' has no questId, skipping.");
                continue;
            }

            if (lookup.TryGetValue(definition.questId, out var existing))
            {
                if (existing != definition)
                    Debug.LogError($"[QuestManager] Duplicate questId '{definition.questId}' on '{definition.name}' and '{existing.name}'.");
                continue;
            }

            lookup[definition.questId] = definition;
            count++;
        }

        return count;
    }

    /// <summary>Returns null (with a warning) for an unknown id or before initialisation.</summary>
    public static QuestDefinition GetDefinition(string questId)
    {
        if (instance == null || !instance.IsInitialized)
        {
            Debug.LogError("[QuestManager] Not initialised. ManagerBrain should initialise QuestManager.");
            return null;
        }

        if (string.IsNullOrEmpty(questId)) return null;

        if (instance.lookup.TryGetValue(questId, out var definition)) return definition;

        Debug.LogWarning($"[QuestManager] Quest not found: {questId}");
        return null;
    }

    public static IEnumerable<QuestDefinition> All =>
        instance != null ? instance.lookup.Values : (IEnumerable<QuestDefinition>)System.Array.Empty<QuestDefinition>();
}
