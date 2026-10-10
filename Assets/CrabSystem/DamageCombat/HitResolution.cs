// What the defender's resolution of one hit came to. Built in DamageSystem.TakeDamage and handed
// out through OnHitResolved — the breakdown a damage log prints and a talent reads.
public enum HitGrade { Unrolled, Full, Glancing, Blocked, Parried }

public struct HitResolution
{
    public HitGrade grade;   // Unrolled for DoT ticks
    public int roll;         // the d20 (the higher of two with advantage)
    public bool advantage;   // riposte, or the target was guard-broken
    public bool counter;     // the target was hit in its move's startup (CF4)
    public float accuracy;   // attacker's Finesse
    public float defense;    // 5 + Avoidance + armour's to-hit bonus
    public float rolled;     // damage carried into soak: the full hit, or half of it when glancing
    public float soak;       // absorbed: by the armour shield (physical only), or all of it on a guarded hit
    public float applied;    // what reached health, after faction and block
}
