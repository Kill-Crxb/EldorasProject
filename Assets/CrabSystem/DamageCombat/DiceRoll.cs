using System;
using UnityEngine;

/// <summary>
/// Dice roll expression — e.g. 2d6, 1d4, 1d12.
/// Serialises cleanly as an inspector field on WeaponData or DamageOverTimeEffect.
/// Standard die sizes (d4/d6/d8/d10/d12/d20) are just diceFaces values; any value works.
/// </summary>
[Serializable]
public struct DiceRoll
{
    [Min(1)] public int diceCount;
    [Min(2)] public int diceFaces;

    public int Roll()
    {
        int total = 0;
        for (int i = 0; i < diceCount; i++)
            total += UnityEngine.Random.Range(1, diceFaces + 1);
        return total;
    }

    public int Min() => diceCount;
    public int Max() => diceCount * diceFaces;
    public float Average() => diceCount * (diceFaces + 1) * 0.5f;
    public string Label() => $"{diceCount}d{diceFaces}";

    public static DiceRoll D4(int count = 1)  => new DiceRoll { diceCount = count, diceFaces = 4  };
    public static DiceRoll D6(int count = 1)  => new DiceRoll { diceCount = count, diceFaces = 6  };
    public static DiceRoll D8(int count = 1)  => new DiceRoll { diceCount = count, diceFaces = 8  };
    public static DiceRoll D10(int count = 1) => new DiceRoll { diceCount = count, diceFaces = 10 };
    public static DiceRoll D12(int count = 1) => new DiceRoll { diceCount = count, diceFaces = 12 };
    public static DiceRoll D20(int count = 1) => new DiceRoll { diceCount = count, diceFaces = 20 };
}
