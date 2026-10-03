// What the defender's resolution of one hit came to. Built in DamageSystem.TakeDamage and handed
// out through OnHitResolved — the breakdown a damage log prints and a talent reads.
public enum HitGrade { Unrolled, Full, Glancing }

public struct HitResolution
{
    public HitGrade grade;   // Unrolled for DoT ticks
    public int roll;         // the d20 (the higher of two with advantage)
    public bool advantage;   // riposte, or the target was guard-broken
    public float accuracy;   // attacker's Finesse
    public float defense;    // 5 + Avoidance + armour's to-hit bonus
    public float rolled;     // damage carried into soak: full includes explosions, glancing is half the base
    public float soak;       // armour dice + flat, after the type factor
    public float applied;    // what reached health, after faction and block
}
