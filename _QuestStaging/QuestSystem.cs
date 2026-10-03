using System;
using System.Collections.Generic;
using UnityEngine;
using NinjaGame.Progression;

/// <summary>
/// One character's quests: what's active, objective counters, deadlines, and the
/// character's own flags (quest. and player.). Saves as quests.json.
///
/// Progress is immediate — counters, stage changes and rewards land the moment they
/// happen (Quest_System Q1). Kills come from this character's own DamageSystem.OnKill,
/// pickups from its own InventorySystem, everything else from GameEvents.OnGameplayEvent
/// where the actor is this character.
/// </summary>
public class QuestSystem : MonoBehaviour, IBrainModule, ISaveable, IFlagSource
{
    [SerializeField] private bool isEnabled = true;

    /// <summary>
    /// Shared zone. / world. flags. Null until WorldStateSystem exists, so they read 0 and
    /// can't be written from here — world flags are server-owned.
    /// </summary>
    public static IFlagSource WorldFlags;

    public bool IsEnabled { get => isEnabled; set => isEnabled = value; }

    private ControllerBrain brain;
    private DamageSystem damage;
    private InventorySystem inventory;

    private readonly Dictionary<string, QuestState> active = new Dictionary<string, QuestState>();
    private readonly HashSet<string> completed = new HashSet<string>();
    private readonly HashSet<string> failed = new HashSet<string>();
    private readonly Dictionary<string, int> flags = new Dictionary<string, int>();
    private readonly HashSet<string> paidStages = new HashSet<string>();

    public IEnumerable<QuestState> Active => active.Values;
    public bool IsActive(string questId) => active.ContainsKey(questId);
    public bool IsCompleted(string questId) => completed.Contains(questId);
    public bool HasFailed(string questId) => failed.Contains(questId);

    public QuestState GetState(string questId)
    {
        active.TryGetValue(questId, out var state);
        return state;
    }

    #region IBrainModule

    public void Initialize(ControllerBrain brain)
    {
        this.brain = brain;
    }

    public void LateInitialize()
    {
        damage = brain.GetModule<DamageSystem>();
        inventory = brain.GetModule<InventorySystem>();

        if (damage != null) damage.OnKill += OnKill;

        if (inventory != null)
        {
            inventory.OnItemAdded += OnItemAdded;
            inventory.OnInventoryChanged += RefreshAll;
        }

        GameEvents.OnGameplayEvent += OnGameplayEvent;
    }

    public void UpdateModule()
    {
        TickTimers();
    }

    private void OnDestroy()
    {
        if (damage != null) damage.OnKill -= OnKill;

        if (inventory != null)
        {
            inventory.OnItemAdded -= OnItemAdded;
            inventory.OnInventoryChanged -= RefreshAll;
        }

        GameEvents.OnGameplayEvent -= OnGameplayEvent;
    }

    #endregion

    #region Accept / Abandon / Fail

    public bool CanAccept(QuestDefinition definition)
    {
        if (definition == null || definition.FirstStage == null) return false;
        if (active.ContainsKey(definition.questId)) return false;
        if (completed.Contains(definition.questId)) return false;

        return definition.requires.IsOpen(this, WorldFlags);
    }

    public bool Accept(string questId)
    {
        var definition = QuestManager.GetDefinition(questId);
        if (!CanAccept(definition)) return false;

        failed.Remove(questId); // a failed quest can be taken again

        var state = new QuestState { questId = questId };
        active[questId] = state;

        GameEvents.QuestAccepted(brain, questId);
        EnterStage(state, definition, definition.FirstStage);
        return true;
    }

    public bool Abandon(string questId)
    {
        if (!active.TryGetValue(questId, out var state)) return false;
        if (!TryGetStage(state, out var definition, out var stage)) return false;
        if (!definition.canAbandon) return false;

        Drop(state, stage);
        GameEvents.QuestAbandoned(brain, questId);
        return true;
    }

    public void Fail(string questId)
    {
        if (!active.TryGetValue(questId, out var state)) return;
        if (!TryGetStage(state, out _, out var stage)) return;

        Drop(state, stage);
        failed.Add(questId);
        GameEvents.QuestFailed(brain, questId);
    }

    // Removed before the fail flags apply, so the flag refresh can't advance it.
    private void Drop(QuestState state, QuestStage stage)
    {
        active.Remove(state.questId);
        ApplyFlags(stage.onFail);
    }

    #endregion

    #region Stages

    private void EnterStage(QuestState state, QuestDefinition definition, QuestStage stage)
    {
        state.stageIndex = stage.stageIndex;
        state.counts = new int[stage.objectives.Count];
        state.secondsRemaining = stage.timeLimitSeconds;

        GameEvents.QuestStageChanged(brain, state.questId, stage.stageIndex);

        ApplyFlags(stage.onEnter);
        RefreshStateObjectives(state, stage);
        TryCompleteStage(state, definition, stage);
    }

    private void TryCompleteStage(QuestState state, QuestDefinition definition, QuestStage stage)
    {
        // A flag or item change further up the stack may already have moved this quest on.
        if (!active.TryGetValue(state.questId, out var current) || current != state) return;
        if (state.stageIndex != stage.stageIndex) return;
        if (!IsStageDone(state, stage)) return;

        var next = ResolveNextStage(state, definition, stage);

        PayStage(state.questId, stage);

        if (next == null)
        {
            active.Remove(state.questId);
            completed.Add(state.questId);
            GameEvents.QuestCompleted(brain, state.questId);
            return;
        }

        EnterStage(state, definition, next);
    }

    private static bool IsStageDone(QuestState state, QuestStage stage)
    {
        if (stage.objectives.Count == 0) return true;

        bool anyRequired = false;
        bool anyOptionalDone = false;

        for (int i = 0; i < stage.objectives.Count; i++)
        {
            var objective = stage.objectives[i];
            bool done = state.counts[i] >= objective.requiredCount;

            if (objective.optional)
            {
                anyOptionalDone |= done;
                continue;
            }

            if (!done) return false;
            anyRequired = true;
        }

        return anyRequired || anyOptionalDone;
    }

    // Null means the quest is finished. Resolved before paying so an item grant that
    // cascades into other quests can't change which branch this stage took.
    private static QuestStage ResolveNextStage(QuestState state, QuestDefinition definition, QuestStage stage)
    {
        int target = stage.nextStage;

        for (int i = 0; i < stage.objectives.Count; i++)
        {
            var objective = stage.objectives[i];
            if (objective.nextStage < 0) continue;
            if (state.counts[i] < objective.requiredCount) continue;

            target = objective.nextStage;
            break;
        }

        if (target < 0)
            return stage.completesQuest ? null : definition.GetStageAfter(stage.stageIndex);

        var next = definition.GetStage(target);
        if (next == null)
            Debug.LogError($"[QuestSystem] '{definition.questId}' stage {stage.stageIndex} points at missing stage {target}. Completing the quest instead.");

        return next;
    }

    private bool TryGetStage(QuestState state, out QuestDefinition definition, out QuestStage stage)
    {
        definition = QuestManager.GetDefinition(state.questId);
        stage = definition != null ? definition.GetStage(state.stageIndex) : null;
        return stage != null;
    }

    #endregion

    #region Objectives

    private void OnKill(ControllerBrain victim)
    {
        string targetId = QuestTarget.IdOf(victim);
        if (string.IsNullOrEmpty(targetId)) return;

        CountEvent(ObjectiveType.Kill, targetId, 1);
    }

    private void OnItemAdded(ItemInstance item)
    {
        CountEvent(ObjectiveType.Find, item.definitionId, Mathf.Max(1, item.stackCount));
    }

    private void OnGameplayEvent(GameplayEvent gameplayEvent)
    {
        if (gameplayEvent.Actor != brain) return;

        CountEvent(gameplayEvent.Type, gameplayEvent.TargetId, gameplayEvent.Amount);
    }

    private void CountEvent(ObjectiveType type, string targetId, int amount)
    {
        if (string.IsNullOrEmpty(targetId)) return;

        foreach (var state in Snapshot())
        {
            if (!TryGetStage(state, out var definition, out var stage)) continue;

            bool changed = false;

            for (int i = 0; i < stage.objectives.Count; i++)
            {
                var objective = stage.objectives[i];
                if (objective.type != type || objective.targetId != targetId) continue;
                if (state.counts[i] >= objective.requiredCount) continue;

                state.counts[i] = Mathf.Min(objective.requiredCount, state.counts[i] + amount);
                changed = true;
            }

            if (!changed) continue;

            GameEvents.QuestProgress(brain, state.questId);
            TryCompleteStage(state, definition, stage);
        }
    }

    /// <summary>Re-reads Have and Flag objectives on every active quest.</summary>
    private void RefreshAll()
    {
        foreach (var state in Snapshot())
        {
            if (!active.ContainsKey(state.questId)) continue;
            if (!TryGetStage(state, out var definition, out var stage)) continue;

            if (RefreshStateObjectives(state, stage))
                GameEvents.QuestProgress(brain, state.questId);

            TryCompleteStage(state, definition, stage);
        }
    }

    private bool RefreshStateObjectives(QuestState state, QuestStage stage)
    {
        bool changed = false;

        for (int i = 0; i < stage.objectives.Count; i++)
        {
            var objective = stage.objectives[i];
            int value;

            if (objective.type == ObjectiveType.Have) value = CountItems(objective.targetId);
            else if (objective.type == ObjectiveType.Flag) value = Flags.Read(objective.targetId, this, WorldFlags);
            else continue;

            value = Mathf.Clamp(value, 0, objective.requiredCount);
            if (state.counts[i] == value) continue;

            state.counts[i] = value;
            changed = true;
        }

        return changed;
    }

    private int CountItems(string itemId)
    {
        if (inventory == null || string.IsNullOrEmpty(itemId)) return 0;

        int total = 0;

        foreach (var item in inventory.GetAllItems())
        {
            if (item.definitionId == itemId) total += Mathf.Max(1, item.stackCount);
        }

        return total;
    }

    // Completing a stage can grant items or set flags that complete other quests,
    // which edits the active dictionary mid-loop. Every cascading loop walks a copy.
    private List<QuestState> Snapshot() => new List<QuestState>(active.Values);

    #endregion

    #region Timers

    private void TickTimers()
    {
        List<string> expired = null;

        foreach (var state in active.Values)
        {
            if (state.secondsRemaining <= 0f) continue;

            state.secondsRemaining -= Time.deltaTime;
            if (state.secondsRemaining > 0f) continue;

            if (expired == null) expired = new List<string>();
            expired.Add(state.questId);
        }

        if (expired == null) return;

        foreach (string questId in expired) Fail(questId);
    }

    #endregion

    #region Rewards

    private static string StageKey(string questId, int stageIndex) => $"{questId}:{stageIndex}";

    private void PayStage(string questId, QuestStage stage)
    {
        string key = StageKey(questId, stage.stageIndex);
        if (!paidStages.Add(key)) return;

        ApplyRewards(key, stage);
        GrantItems(stage.items);
    }

    // Keyed so a re-apply on load replaces rather than stacks. Never removed.
    private void ApplyRewards(string stageKey, QuestStage stage)
    {
        var context = new RewardContext(brain, "quest:" + stageKey);

        foreach (var reward in stage.rewards)
        {
            if (reward != null) reward.Apply(context);
        }
    }

    private void GrantItems(List<ItemGrant> grants)
    {
        foreach (var grant in grants)
        {
            var item = ItemManager.CreateItem(grant.itemId);
            if (item == null) continue;

            item.stackCount = Mathf.Max(1, grant.count);

            if (inventory == null || !inventory.AddItem(item))
                Debug.LogWarning($"[QuestSystem] No room for quest item '{grant.itemId}' x{grant.count} on {brain.name} — it was not granted.");
        }
    }

    #endregion

    #region Flags

    public int GetFlag(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return 0;

        flags.TryGetValue(flagId, out int value);
        return value;
    }

    /// <summary>Sets a quest. or player. flag. World flags are refused — the server owns them.</summary>
    public void SetFlag(string flagId, int value)
    {
        if (string.IsNullOrEmpty(flagId)) return;

        if (Flags.IsWorldScoped(flagId))
        {
            Debug.LogWarning($"[QuestSystem] '{flagId}' is a world flag. Only WorldStateSystem may write it.");
            return;
        }

        if (GetFlag(flagId) == value) return;

        flags[flagId] = value;
        RefreshAll();
    }

    private void ApplyFlags(List<FlagChange> changes)
    {
        foreach (var change in changes) SetFlag(change.flagId, change.value);
    }

    #endregion

    #region ISaveable

    public string GetSaveId() => "quests";

    public int GetSaveVersion() => 1;

    public string GetSaveData()
    {
        var data = new QuestSaveData
        {
            active = new List<QuestState>(active.Values),
            completed = new List<string>(completed),
            failed = new List<string>(failed),
            paidStages = new List<string>(paidStages)
        };

        foreach (var pair in flags)
            data.flags.Add(new FlagEntry { id = pair.Key, value = pair.Value });

        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        active.Clear();
        completed.Clear();
        failed.Clear();
        flags.Clear();
        paidStages.Clear();

        if (string.IsNullOrEmpty(json)) return;

        var data = JsonUtility.FromJson<QuestSaveData>(json);
        if (data == null) return;

        foreach (var state in data.active) RestoreState(state);
        foreach (var entry in data.flags) flags[entry.id] = entry.value;

        completed.UnionWith(data.completed);
        failed.UnionWith(data.failed);
        paidStages.UnionWith(data.paidStages);

        ReapplyPaidRewards();
        RefreshAll();
    }

    private void RestoreState(QuestState state)
    {
        if (!TryGetStage(state, out _, out var stage))
        {
            Debug.LogWarning($"[QuestSystem] Dropping saved quest '{state.questId}' stage {state.stageIndex} — no longer defined.");
            return;
        }

        // A stage that gained or lost objectives since the save keeps what lines up.
        if (state.counts == null || state.counts.Length != stage.objectives.Count)
            Array.Resize(ref state.counts, stage.objectives.Count);

        active[state.questId] = state;
    }

    // StatSystem doesn't save contributions, so quest rewards are re-applied every load.
    private void ReapplyPaidRewards()
    {
        foreach (string key in paidStages)
        {
            int split = key.LastIndexOf(':');
            if (split < 0 || !int.TryParse(key.Substring(split + 1), out int stageIndex)) continue;

            var definition = QuestManager.GetDefinition(key.Substring(0, split));
            var stage = definition != null ? definition.GetStage(stageIndex) : null;
            if (stage != null) ApplyRewards(key, stage);
        }
    }

    #endregion
}
