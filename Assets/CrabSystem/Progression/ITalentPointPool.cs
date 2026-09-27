namespace NinjaGame.Progression
{
    /// <summary>
    /// A store of talent points, split by tree. Implement this on the talent module when
    /// it exists — TalentPointReward already grants through it and does nothing until then.
    /// </summary>
    public interface ITalentPointPool
    {
        /// <summary>Grants points under a key. Re-granting under the same key replaces.</summary>
        void GrantPoints(string treeId, string sourceKey, int points);

        void RevokePoints(string treeId, string sourceKey);
    }
}
