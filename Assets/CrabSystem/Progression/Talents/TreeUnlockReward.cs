using UnityEngine;

namespace NinjaGame.Progression
{
    // Makes a tree known, so an empty slot can take it. A tree once slotted stays known.
    [CreateAssetMenu(fileName = "Reward_TreeUnlock", menuName = "NinjaGame/Rewards/Tree Unlock")]
    public class TreeUnlockReward : Reward
    {
        [SerializeField] private TalentTree tree;

        public override void Apply(RewardContext context)
        {
            if (tree == null) return;
            Talents(context)?.Unlock(tree.treeId, context.SourceKey);
        }

        public override void Remove(RewardContext context)
        {
            if (tree == null) return;
            Talents(context)?.Lock(tree.treeId, context.SourceKey);
        }

        public override string Summarise() => $"Learn the {(tree != null ? tree.Label : "?")} tree";

        private static TalentModule Talents(RewardContext context) => context.Brain != null ? context.Brain.GetModule<TalentModule>() : null;
    }
}
