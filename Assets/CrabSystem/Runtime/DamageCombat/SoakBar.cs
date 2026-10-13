using System;
using UnityEngine;

// A bar made of soak dice (Combat_Framework §3.5) — the Armour shield and the Guard bar. Each die is one
// segment, and a segment's size is rolled when it refills from empty; a flat part sits under the dice.
// Damage drains from the top segment down. After a quiet spell the bar refills one segment at a time
// from the bottom up. An owner can also trickle it — one segment per interval even while being hit — so
// the bar still counts under constant pressure.
//
// The owner calls Configure whenever its stats might have changed; the bar only rebuilds (full, freshly
// rolled) when the layout actually differs.
[Serializable]
public class SoakBar : IBarSource
{
    [Tooltip("Seconds without taking damage before the bar starts to refill.")]
    [SerializeField] private float refillDelay = 3f;

    [Tooltip("Seconds per segment once the bar is refilling.")]
    [SerializeField] private float secondsPerSegment = 0.75f;

    private float[] remaining = new float[0];
    private float[] capacity = new float[0];
    private int[] faces = new int[0];   // 0 marks the flat segment
    private float flat;
    private int diceCount = -1;         // -1 forces the first Configure to build
    private int dieFaces;
    private int bonusFaces;
    private float nextRefillAt;
    private float nextTrickleAt;

    public float Current => Sum(remaining);
    public float Max => Sum(capacity);
    public bool IsEmpty => Current <= 0f;

    // flat: a segment that is always its full value. count × d(faces): the soak dice. bonusFaces: one
    // extra die, e.g. the Deflection modifier rolled as a die (1 is a flat 1, 0 or below is none).
    public void Configure(float flatValue, int count, int faces, int bonus)
    {
        bool same = flatValue == flat && count == diceCount && faces == dieFaces && bonus == bonusFaces;
        if (same) return;

        flat = flatValue;
        diceCount = Mathf.Max(0, count);
        dieFaces = faces;
        bonusFaces = bonus;
        Build();
    }

    // Takes what it can and returns the rest. Any damage, even fully absorbed, restarts the refill delay.
    public float Absorb(float amount, float now)
    {
        if (amount <= 0f) return 0f;

        nextRefillAt = now + refillDelay;
        for (int i = remaining.Length - 1; i >= 0 && amount > 0f; i--)
        {
            float take = Mathf.Min(amount, remaining[i]);
            remaining[i] -= take;
            amount -= take;
        }
        return amount;
    }

    public void Tick(float now)
    {
        if (now < nextRefillAt) return;

        nextRefillAt = now + secondsPerSegment;
        RefillLowest();
    }

    // One segment every secondsPerTrickle, whether or not the bar is taking damage. 0 or less is off.
    public void Trickle(float now, float secondsPerTrickle)
    {
        if (secondsPerTrickle <= 0f || now < nextTrickleAt) return;

        nextTrickleAt = now + secondsPerTrickle;
        RefillLowest();
    }

    // A segment that refills from empty rolls its die again.
    private void RefillLowest()
    {
        int segment = LowestNotFull();
        if (segment < 0) return;

        if (remaining[segment] <= 0f) capacity[segment] = RollSegment(segment);
        remaining[segment] = capacity[segment];
    }

    private void Build()
    {
        int count = (flat > 0f ? 1 : 0) + diceCount + (bonusFaces > 0 ? 1 : 0);
        faces = new int[count];
        capacity = new float[count];
        remaining = new float[count];

        int index = 0;
        if (flat > 0f) index++;
        for (int i = 0; i < diceCount; i++) faces[index++] = dieFaces;
        if (bonusFaces > 0) faces[index] = bonusFaces;

        for (int i = 0; i < count; i++)
        {
            capacity[i] = RollSegment(i);
            remaining[i] = capacity[i];
        }
    }

    private float RollSegment(int segment)
    {
        if (faces[segment] == 0) return flat;
        return DiceRoll.RollModifier(faces[segment]);
    }

    private int LowestNotFull()
    {
        for (int i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] < capacity[i]) return i;
        }
        return -1;
    }

    private static float Sum(float[] values)
    {
        float total = 0f;
        foreach (float value in values) total += value;
        return total;
    }
}
