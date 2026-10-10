using System;
using System.Collections.Generic;
using NinjaGame.Progression;
using UnityEngine;

// The character's classes (Talent_Trees.md, revised 10 Oct by Crxb). Three slots each hold a known tree. Every
// character level pays points (TalentRules.pointsPerLevel) into one pool, spent in any slotted tree; each row of
// a tree opens once enough points are spent in that tree's lower rows. Character level is RPGSystem's XP level.
//
// A learned talent's rewards are held under talent:{nodeId}:{r}, scaled by rank, so it can always be re-applied
// or taken back exactly. The save holds only slots and ranks; everything granted is derived on load.
public class TalentModule : MonoBehaviour, IBrainModule, ISaveable, ITalentPointPool
{
    public int InitOrder => 150;

    [SerializeField] private TalentRules rules;

    public bool IsEnabled { get; set; } = true;

    private ControllerBrain brain;
    private RPGSystem rpg;

    private readonly List<TalentSlot> slots = new();
    private readonly HashSet<string> learned = new();

    // Bonus points and tree unlocks held by rewards (source key → amount), derived on load, never saved.
    private readonly Dictionary<string, int> grantedPoints = new();
    private readonly Dictionary<string, HashSet<string>> unlocks = new();

    public TalentRules Rules => rules;
    public IReadOnlyList<TalentSlot> Slots => slots;

    public event Action Changed;
    public event Action<ISaveable> Dirty;

    public void Initialize(ControllerBrain controllerBrain)
    {
        brain = controllerBrain;
        if (rules == null) return;

        for (int i = 0; i < rules.slots; i++) slots.Add(new TalentSlot(i));
    }

    public void LateInitialize()
    {
        if (rules == null)
        {
            Debug.LogError($"[TalentModule] No TalentRules on {brain.EntityName}.", this);
            return;
        }

        rpg = brain.RPG;
        if (rpg != null) rpg.OnLevelChanged += HandleLevelChanged;

        ApplyAll();
        Changed?.Invoke();
    }

    public void UpdateModule() { }

    private void OnDestroy()
    {
        if (rpg != null) rpg.OnLevelChanged -= HandleLevelChanged;
    }

    private void HandleLevelChanged(int oldLevel, int newLevel) => Changed?.Invoke();

    // ---- Reads ----

    public int Level => rpg != null ? rpg.CurrentLevel : 1;

    public int PointsEarned
    {
        get
        {
            int bonus = 0;
            foreach (int points in grantedPoints.Values) bonus += points;
            return Mathf.Min(Level, rules.levelCap) * rules.pointsPerLevel + bonus;
        }
    }

    public int PointsSpent(TalentSlot slot) => TalentChecks.Spent(slot?.Tree, slot?.Ranks);

    public int TotalSpent
    {
        get
        {
            int spent = 0;
            foreach (TalentSlot slot in slots) spent += PointsSpent(slot);
            return spent;
        }
    }

    public int PointsLeft => PointsEarned - TotalSpent;

    public bool Knows(TalentTree tree) => tree != null && (learned.Contains(tree.treeId) || unlocks.ContainsKey(tree.treeId) || rules.knownAtStart.Contains(tree));

    public List<TalentTree> KnownTrees()
    {
        var known = new List<TalentTree>();
        foreach (TalentTree tree in rules.trees)
        {
            if (Knows(tree)) known.Add(tree);
        }
        return known;
    }

    public TalentSlot SlotHolding(TalentTree tree)
    {
        foreach (TalentSlot slot in slots)
        {
            if (tree != null && slot.Tree == tree) return slot;
        }
        return null;
    }

    // ---- Why not: null when allowed, else the reason the UI shows ----

    public string WhyNotSpend(TalentSlot slot, TalentNode node)
    {
        if (slot?.Tree == null || node == null) return "Choose a tree first";
        if (slot.Rank(node.nodeId) == 0 && TakenElsewhere(slot, node.nodeId)) return "Already learned in another tree";
        return TalentChecks.WhyNotSpend(slot.Tree, rules, slot.Ranks, node, PointsLeft);
    }

    public string WhyNotRefund(TalentSlot slot, TalentNode node)
    {
        if (slot?.Tree == null || node == null) return "Choose a tree first";
        return TalentChecks.WhyNotRefund(slot.Tree, rules, slot.Ranks, node);
    }

    public string WhyNotSetTree(TalentSlot slot, TalentTree tree)
    {
        if (slot == null || tree == null) return "Nothing to set";
        if (tree.isLibrary) return "The library can't be slotted";
        if (!Knows(tree)) return "Tree not known";
        if (slot.Tree == tree) return "Already in this slot";
        if (SlotHolding(tree) != null) return "Already in another slot";
        return null;
    }

    // ---- Actions ----

    public bool Spend(TalentSlot slot, TalentNode node)
    {
        if (WhyNotSpend(slot, node) != null) return false;

        slot.Ranks[node.nodeId] = slot.Rank(node.nodeId) + 1;
        ApplySlot(slot);
        Notify();
        return true;
    }

    // Free while the respec rule is undecided (10 Oct).
    public bool Refund(TalentSlot slot, TalentNode node)
    {
        if (WhyNotRefund(slot, node) != null) return false;

        int rank = slot.Rank(node.nodeId) - 1;
        if (rank > 0) slot.Ranks[node.nodeId] = rank;
        else slot.Ranks.Remove(node.nodeId);

        ApplySlot(slot);
        Notify();
        return true;
    }

    public bool ResetTree(TalentSlot slot)
    {
        if (slot?.Tree == null || slot.Ranks.Count == 0) return false;

        ClearSlot(slot);
        slot.Ranks.Clear();
        Notify();
        return true;
    }

    // The replaced tree's points come back to the pool; a tree once slotted stays known.
    public bool SetTree(TalentSlot slot, TalentTree tree)
    {
        if (WhyNotSetTree(slot, tree) != null) return false;

        ClearSlot(slot);
        slot.Ranks.Clear();
        learned.Add(tree.treeId);
        slot.Tree = tree;
        Notify();
        return true;
    }

    // ---- Granted by rewards (keyed, not saved: the granter re-applies) ----

    // Every granted point goes to the shared pool, whatever tree the reward names.
    public void GrantPoints(string treeId, string sourceKey, int points)
    {
        grantedPoints[sourceKey] = points;
        Changed?.Invoke();
    }

    public void RevokePoints(string treeId, string sourceKey)
    {
        if (grantedPoints.Remove(sourceKey)) Changed?.Invoke();
    }

    public void Unlock(string treeId, string sourceKey)
    {
        if (string.IsNullOrEmpty(treeId)) return;
        if (!unlocks.TryGetValue(treeId, out HashSet<string> sources)) unlocks[treeId] = sources = new HashSet<string>();

        sources.Add(sourceKey);
        Changed?.Invoke();
    }

    public void Lock(string treeId, string sourceKey)
    {
        if (!unlocks.TryGetValue(treeId ?? "", out HashSet<string> sources)) return;

        sources.Remove(sourceKey);
        if (sources.Count == 0) unlocks.Remove(treeId);
        Changed?.Invoke();
    }

    private bool TakenElsewhere(TalentSlot slot, string nodeId)
    {
        foreach (TalentSlot other in slots)
        {
            if (other != slot && other.Rank(nodeId) > 0) return true;
        }
        return false;
    }

    private void Notify()
    {
        Dirty?.Invoke(this);
        Changed?.Invoke();
    }

    // ---- Applying ----

    private void ApplyAll()
    {
        foreach (TalentSlot slot in slots) ApplySlot(slot);
    }

    // A generic placed in two trees is held by whichever slot learned it; the other mustn't take it back.
    private void ApplySlot(TalentSlot slot)
    {
        if (slot.Tree == null) return;

        foreach (TalentPlacement placement in slot.Tree.placements)
        {
            if (placement?.node == null) continue;
            int rank = slot.Rank(placement.node.nodeId);
            if (rank == 0 && TakenElsewhere(slot, placement.node.nodeId)) continue;
            SetRewards(placement.node, rank);
        }
    }

    private void ClearSlot(TalentSlot slot)
    {
        if (slot.Tree == null) return;

        foreach (TalentPlacement placement in slot.Tree.placements)
        {
            if (placement?.node == null || TakenElsewhere(slot, placement.node.nodeId)) continue;
            SetRewards(placement.node, 0);
        }
    }

    // The index keeps two rewards on one node from sharing a key when they feed the same stat.
    private void SetRewards(TalentNode node, int rank)
    {
        for (int r = 0; r < node.rewards.Count; r++)
        {
            Reward reward = node.rewards[r];
            if (reward == null) continue;

            var context = new RewardContext(brain, $"talent:{node.nodeId}:{r}", rank);
            if (rank > 0) reward.Apply(context);
            else reward.Remove(context);
        }
    }

    // ---- ISaveable ----

    public string GetSaveId() => "talents";

    public int GetSaveVersion() => 2;

    public string GetSaveData()
    {
        var data = new SaveData { version = GetSaveVersion() };
        data.learned.AddRange(learned);

        foreach (TalentSlot slot in slots)
        {
            var record = new SlotRecord { treeId = slot.Tree != null ? slot.Tree.treeId : "" };
            foreach (KeyValuePair<string, int> rank in slot.Ranks) record.spent.Add(new SpentRecord { nodeId = rank.Key, rank = rank.Value });
            data.slots.Add(record);
        }

        return JsonUtility.ToJson(data);
    }

    public void LoadSaveData(string json)
    {
        if (string.IsNullOrEmpty(json) || rules == null) return;

        var data = JsonUtility.FromJson<SaveData>(json);
        if (data == null) return;

        foreach (TalentSlot slot in slots) ClearSlot(slot);

        learned.Clear();
        learned.UnionWith(data.learned);

        for (int i = 0; i < slots.Count; i++) LoadSlot(slots[i], i < data.slots.Count ? data.slots[i] : null);

        ApplyAll();
        Changed?.Invoke();
    }

    // A tree or talent that no longer exists is dropped with a warning rather than failing the whole load.
    private void LoadSlot(TalentSlot slot, SlotRecord record)
    {
        slot.Tree = null;
        slot.Ranks.Clear();
        if (record == null || string.IsNullOrEmpty(record.treeId)) return;

        slot.Tree = rules.FindTree(record.treeId);
        if (slot.Tree == null)
        {
            Debug.LogWarning($"[TalentModule] Saved tree '{record.treeId}' isn't in {rules.name}; slot cleared.", this);
            return;
        }

        foreach (SpentRecord spent in record.spent)
        {
            TalentPlacement placement = slot.Tree.Find(spent.nodeId);
            if (placement == null || spent.rank <= 0) continue;
            slot.Ranks[spent.nodeId] = Mathf.Min(spent.rank, placement.node.maxRank);
        }
    }

    [Serializable]
    private class SaveData
    {
        public int version;
        public List<string> learned = new();
        public List<SlotRecord> slots = new();
    }

    [Serializable]
    private class SlotRecord
    {
        public string treeId;
        public List<SpentRecord> spent = new();
    }

    [Serializable]
    private class SpentRecord
    {
        public string nodeId;
        public int rank;
    }
}
