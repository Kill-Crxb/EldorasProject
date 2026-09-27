using UnityEngine;

/// <summary>
/// DiceProfile — a dice expression and a name. Formerly WeaponData.
///
/// Renamed because it stopped being about weapons. Spell tiers roll one, thrown projectiles carry
/// one, fists have one, and an equipped weapon supplies one — everything that deals damage in this
/// project resolves to a DiceProfile and nothing on it is weapon-specific.
///
/// Damage resolution order (DamageEffect.GetWeaponDamage):
///   damageDice.Roll() + flatBonus → passed as baseDamage into the full pipeline.
///
/// Deliberately small. Attack speed, reach, block, parry and moveset are NOT here and never were,
/// in any form the code read — see the dev insight note on this cleanup. If weapon behaviour comes
/// back it belongs in its own asset, not bolted onto the dice.
/// </summary>
[CreateAssetMenu(fileName = "New Dice Profile", menuName = "Combat/Dice Profile")]
public class DiceProfile : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Shown in tooltips and logs — 'Katana', 'Fireball', 'Fists'.")]
    public string weaponName = "New Dice Profile";

    [Header("Dice Damage")]
    [Tooltip("Dice expression rolled on each hit — e.g. 1d8 for a longsword, 1d4 for a dagger")]
    public DiceRoll damageDice = DiceRoll.D6();

    [Tooltip("Flat value added to every roll (can be negative for off-hand penalty)")]
    public float flatBonus = 0f;

    /// <summary>
    /// Roll the damage expression and add the flat bonus.
    /// Call once per hit — do not cache.
    /// </summary>
    public float RollDamage() => damageDice.Roll() + flatBonus;

    // Base roll plus flat bonus; explosion rolls come back separately in 'extra'.
    public float RollExploding(out int extra, out int explosions) => damageDice.RollExploding(out extra, out explosions) + flatBonus;

    /// <summary>Human-readable label for tooltips — e.g. "1d8 + 2".</summary>
    public string DamageLabel() => flatBonus == 0f
        ? damageDice.Label()
        : $"{damageDice.Label()} {(flatBonus > 0f ? "+" : "")}{flatBonus}";
}
