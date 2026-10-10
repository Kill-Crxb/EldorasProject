// Something on the attacker that changes a hit as DamageSystem resolves it: to-hit, advantage and extra damage
// before the full / glancing grade. KeywordModule is the first (Ambushing). Found through the attacker brain's
// provider cache.
public interface IHitModifier
{
    void ModifyHit(ControllerBrain defender, ref float accuracy, ref bool advantage, ref float bonusDamage);
}
