using System;
using UnityEngine;

/// <summary>
/// Dice roll expression — e.g. 2d6, 1d4, 1d12.
/// Serialises cleanly as an inspector field on DiceProfile or a status tick.
/// Standard die sizes (d4/d6/d8/d10/d12/d20) are just diceFaces values; any value works.
/// </summary>

namespace CrabSystem
{
    [Serializable]
    public struct DiceRoll
    {
        [Min(1)] public int diceCount;
        [Min(2)] public int diceFaces;

        // The one source of gameplay randomness (hit rolls, saves, procs, damage dice), so a server can own and seed it
        // (CrabSystem_Standard §7, tracker AU24). Presentation randomness (sparks, number spread) stays on
        // UnityEngine.Random.
        static System.Random rng = new System.Random();

        public static void Seed(int seed) => rng = new System.Random(seed);

        // 1 to faces inclusive; a die of fewer than 2 faces always shows 1.
        public static int Die(int faces) => faces < 2 ? 1 : rng.Next(1, faces + 1);

        public int Roll()
        {
            int total = 0;
            for (int i = 0; i < diceCount; i++)
                total += Die(diceFaces);
            return total;
        }

        // A stat modifier rolled as a die: Might 6 rolls 1d6. A modifier of 1 is a flat 1, and 0 or
        // below adds nothing. Any size is legal — Might 7 rolls a d7.
        public static int RollModifier(float modifier)
        {
            int faces = Mathf.FloorToInt(modifier);
            if (faces <= 0) return 0;
            return Die(faces);
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
}
