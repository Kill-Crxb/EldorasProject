using System.Collections.Generic;

namespace NinjaGame.Progression
{
    // One of a character's tree slots and what has been learned in it. Runtime state owned by TalentModule.
    public class TalentSlot
    {
        public readonly int Index;

        public TalentTree Tree;

        // nodeId → rank.
        public readonly Dictionary<string, int> Ranks = new();

        public TalentSlot(int index)
        {
            Index = index;
        }

        public int Rank(string nodeId) => nodeId != null && Ranks.TryGetValue(nodeId, out int rank) ? rank : 0;
    }
}
