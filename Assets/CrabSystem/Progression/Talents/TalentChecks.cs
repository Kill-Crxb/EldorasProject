using System.Collections.Generic;

namespace NinjaGame.Progression
{
    // The spend and refund rules for one tree, on plain ranks (nodeId → rank), so the game (TalentModule) and the
    // wizard's Simulate tab ask the same questions. Each returns null when allowed, else the reason shown.
    public static class TalentChecks
    {
        public static int Rank(IReadOnlyDictionary<string, int> ranks, TalentNode node) =>
            node != null && ranks.TryGetValue(node.nodeId ?? "", out int rank) ? rank : 0;

        // Points spent on tiers under `belowTier` — what a tier's gate reads. No tier given: everything.
        public static int Spent(TalentTree tree, IReadOnlyDictionary<string, int> ranks, int belowTier = int.MaxValue)
        {
            if (tree == null) return 0;

            int spent = 0;
            foreach (TalentPlacement placement in tree.placements)
            {
                if (placement?.node == null || placement.tier >= belowTier) continue;
                spent += Rank(ranks, placement.node) * placement.node.costPerRank;
            }
            return spent;
        }

        public static string WhyNotSpend(TalentTree tree, TalentRules rules, IReadOnlyDictionary<string, int> ranks, TalentNode node, int pointsAvailable)
        {
            TalentPlacement placement = tree != null ? tree.Find(node) : null;
            if (placement == null) return "Not in this tree";
            if (Rank(ranks, node) >= node.maxRank) return "Fully learned";
            if (placement.exclusiveWith != null && Rank(ranks, placement.exclusiveWith) > 0) return $"Locked by {placement.exclusiveWith.Label}";

            foreach (TalentNode required in placement.requires)
            {
                if (required != null && Rank(ranks, required) == 0) return $"Needs {required.Label}";
            }

            int gate = rules.Gate(placement.tier);
            if (Spent(tree, ranks, placement.tier) < gate) return $"Needs {gate} points in lower tiers";
            if (pointsAvailable < node.costPerRank) return "Not enough points";
            return null;
        }

        public static string WhyNotRefund(TalentTree tree, TalentRules rules, IReadOnlyDictionary<string, int> ranks, TalentNode node)
        {
            TalentPlacement placement = tree != null ? tree.Find(node) : null;
            if (placement == null) return "Not in this tree";

            int rank = Rank(ranks, node);
            if (rank == 0) return "Not learned";

            foreach (TalentPlacement other in tree.placements)
            {
                if (other?.node == null || Rank(ranks, other.node) == 0) continue;
                if (rank == 1 && other.requires.Contains(node)) return $"{other.node.Label} needs it";
                if (other.tier <= placement.tier) continue;
                if (Spent(tree, ranks, other.tier) - node.costPerRank < rules.Gate(other.tier)) return $"{other.node.Label} would lose its tier";
            }
            return null;
        }
    }
}
