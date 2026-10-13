using System.Collections.Generic;
using NinjaGame.Progression;
using UnityEditor;
using UnityEngine;

// The rules that make a tree a good tree, as checks (Talent_Wizard.md §4). Errors: the tree is wrong.
// Warnings: the tree is weak. No UI: the wizard shows the list, and Tools → Crab → Talents → Validate All
// prints it for every tree. Each issue names the node it's about so the wizard can select it.
//
// Not checked yet: "at most one Hard keyword" (statuses carry no tier). The budget, track and content-size checks
// went with the 10 Oct revision (no tree levels).
public static class TalentTreeValidator
{
    public class Issue
    {
        public bool IsError;
        public string Message;
        public TalentNode Node;
    }

    public static List<Issue> Validate(TalentTree tree, TalentRules rules)
    {
        var issues = new List<Issue>();
        if (tree == null || rules == null) return issues;

        CheckPlacements(tree, rules, issues);
        CheckIds(tree, issues);
        CheckOrphans(tree, issues);
        CheckSockets(tree, issues);
        if (tree.isLibrary) return issues;

        CheckKeywords(tree, issues);
        CheckCapstones(tree, rules, issues);
        return issues;
    }

    [MenuItem("Tools/Crab/Talents/Validate All")]
    static void ValidateAll()
    {
        TalentRules rules = FindRules();
        int errors = 0;
        int warnings = 0;

        foreach (TalentTree tree in CrabWizardGUI.LoadAll<TalentTree>())
        {
            foreach (Issue issue in Validate(tree, rules))
            {
                string line = $"[Talents] {tree.Label}{(issue.Node != null ? " / " + issue.Node.Label : "")}: {issue.Message}";
                if (issue.IsError) Debug.LogError(line, tree);
                else Debug.LogWarning(line, tree);

                if (issue.IsError) errors++;
                else warnings++;
            }
        }

        Debug.Log($"[Talents] Validated: {errors} errors, {warnings} warnings.");
    }

    public static TalentRules FindRules()
    {
        List<TalentRules> found = CrabWizardGUI.LoadAll<TalentRules>();
        return found.Count > 0 ? found[0] : null;
    }

    static void Error(List<Issue> issues, string message, TalentNode node = null) =>
        issues.Add(new Issue { IsError = true, Message = message, Node = node });

    static void Warn(List<Issue> issues, string message, TalentNode node = null) =>
        issues.Add(new Issue { IsError = false, Message = message, Node = node });

    static void CheckPlacements(TalentTree tree, TalentRules rules, List<Issue> issues)
    {
        var cells = new HashSet<int>();
        int maxPoints = rules.MaxPoints;

        foreach (TalentPlacement placement in tree.placements)
        {
            TalentNode node = placement?.node;
            if (node == null)
            {
                Error(issues, "A placement has no node.");
                continue;
            }

            if (placement.tier < 1 || placement.tier > rules.Tiers) Error(issues, $"Tier {placement.tier} is outside 1–{rules.Tiers}.", node);
            if (placement.column < 0 || placement.column >= rules.columns) Error(issues, $"Column {placement.column} is outside 0–{rules.columns - 1}.", node);
            if (!cells.Add(placement.tier * 100 + placement.column)) Error(issues, "Shares a cell with another node.", node);
            if (rules.Gate(placement.tier) + node.costPerRank > maxPoints) Error(issues, "Can't be afforded even with every point at the level cap.", node);
            if (node.maxRank > 1 && node.category != TalentCategory.Generic) Warn(issues, "Only generics should have ranks.", node);

            foreach (TalentNode required in placement.requires)
            {
                TalentPlacement requiredAt = tree.Find(required);
                if (requiredAt == null) Error(issues, $"Requires {(required != null ? required.Label : "nothing")}, which isn't placed in this tree.", node);
                else if (requiredAt.tier >= placement.tier) Error(issues, $"Requires {required.Label}, which isn't in a lower tier.", node);
            }

            if (placement.exclusiveWith == null) continue;

            TalentPlacement partner = tree.Find(placement.exclusiveWith);
            if (partner == null) Error(issues, $"Exclusive with {placement.exclusiveWith.Label}, which isn't placed in this tree.", node);
            else if (partner.exclusiveWith != node) Warn(issues, $"Exclusive with {placement.exclusiveWith.Label}, but not the other way round.", node);
        }
    }

    // talent:{nodeId} is the grant key, so ids must be unique across every tree and the library.
    static void CheckIds(TalentTree tree, List<Issue> issues)
    {
        var byId = new Dictionary<string, TalentNode>();
        foreach (TalentTree other in CrabWizardGUI.LoadAll<TalentTree>())
        {
            foreach (TalentPlacement placement in other.placements)
            {
                TalentNode node = placement?.node;
                if (node == null || string.IsNullOrEmpty(node.nodeId)) continue;
                if (!byId.ContainsKey(node.nodeId)) byId[node.nodeId] = node;
            }
        }

        foreach (TalentPlacement placement in tree.placements)
        {
            TalentNode node = placement?.node;
            if (node == null) continue;

            if (string.IsNullOrEmpty(node.nodeId)) Error(issues, "Has no node id.", node);
            else if (byId.TryGetValue(node.nodeId, out TalentNode owner) && owner != node) Error(issues, $"Node id '{node.nodeId}' is used by another node.", node);
        }
    }

    // A tree-specific keyword can only be socketed by its own tree; a socket's unlock talent must be placed here.
    static void CheckSockets(TalentTree tree, List<Issue> issues)
    {
        foreach (KeywordSocket socket in tree.sockets)
        {
            if (socket?.keyword == null)
            {
                Error(issues, "A keyword socket has no keyword.");
                continue;
            }

            if (socket.keyword.ownerTree != null && socket.keyword.ownerTree != tree)
                Error(issues, $"Socket {socket.Label}: {socket.keyword.Label} belongs to {socket.keyword.ownerTree.Label}.");
            if (socket.unlockedBy != null && tree.Find(socket.unlockedBy) == null)
                Error(issues, $"Socket {socket.Label} opens on {socket.unlockedBy.Label}, which isn't placed in this tree.");
        }
    }

    static void CheckOrphans(TalentTree tree, List<Issue> issues)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(tree)))
        {
            if (asset is TalentNode node && tree.Find(node) == null) Warn(issues, "Node inside this tree's file isn't placed.", node);
        }
    }

    // Two harmful and two beneficial owned; only the owner Inflicts or Grants, and not in tier 1 (§8).
    static void CheckKeywords(TalentTree tree, List<Issue> issues)
    {
        var statuses = new Dictionary<string, StatusDefinition>();
        foreach (StatusDefinition status in CrabWizardGUI.LoadAll<StatusDefinition>())
        {
            if (!string.IsNullOrEmpty(status.id)) statuses[status.id] = status;
        }

        int harmful = 0;
        int beneficial = 0;
        foreach (string id in tree.ownedKeywords)
        {
            if (!statuses.TryGetValue(id ?? "", out StatusDefinition status))
            {
                Error(issues, $"Owned keyword '{id}' isn't a status.");
                continue;
            }

            if (status.beneficial) beneficial++;
            else harmful++;
        }

        if (harmful != 2 || beneficial != 2) Error(issues, $"Owns {harmful} harmful and {beneficial} beneficial keywords; should be 2 and 2.");

        foreach (TalentPlacement placement in tree.placements)
        {
            TalentNode node = placement?.node;
            if (node == null) continue;

            if (node.keywords.Count > 0 && node.rewards.Count == 0) Warn(issues, "Declares keywords but has no rewards to do them.", node);

            foreach (KeywordUse use in node.keywords)
            {
                bool owning = use.verb == KeywordVerb.Inflict || use.verb == KeywordVerb.Grant;
                if (!owning) continue;

                if (!tree.ownedKeywords.Contains(use.statusId)) Error(issues, $"{use.verb}s {use.statusId}, which this tree doesn't own.", node);
                if (placement.tier == 1) Warn(issues, $"{use.verb} on row 1 — owned keywords belong deeper.", node);
            }
        }
    }

    // Two capstones on the top row, a pick-one pair; stat rewards only on the bottom row (§7 filler rules).
    static void CheckCapstones(TalentTree tree, TalentRules rules, List<Issue> issues)
    {
        var capstones = new List<TalentPlacement>();
        foreach (TalentPlacement placement in tree.placements)
        {
            TalentNode node = placement?.node;
            if (node == null) continue;

            if (node.category == TalentCategory.Capstone) capstones.Add(placement);
            if (node.category == TalentCategory.Capstone && placement.tier != rules.Tiers) Warn(issues, $"Capstones belong on row {rules.Tiers}.", node);
            if (placement.tier > 1 && HasStatReward(node)) Warn(issues, "Stat rewards belong on row 1.", node);
        }

        if (capstones.Count != 2 || capstones[0].exclusiveWith != capstones[1].node)
            Warn(issues, "Should have two capstones, exclusive with each other.");
    }

    static bool HasStatReward(TalentNode node)
    {
        foreach (Reward reward in node.rewards)
        {
            if (reward is StatBonusReward || reward is StatRateReward) return true;
        }
        return false;
    }
}
