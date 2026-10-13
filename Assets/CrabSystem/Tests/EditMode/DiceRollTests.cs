using NUnit.Framework;
using UnityEngine;

namespace CrabSystem.Tests
{
    public class DiceRollTests
    {
        [SetUp]
        public void Seed() => DiceRoll.Seed(1234);

        [Test]
        public void DieShowsEveryFaceAndNothingElse()
        {
            var seen = new bool[7];
            for (int i = 0; i < 2000; i++)
            {
                int face = DiceRoll.Die(6);
                Assert.That(face, Is.InRange(1, 6));
                seen[face] = true;
            }
            for (int face = 1; face <= 6; face++) Assert.IsTrue(seen[face], $"never rolled {face}");
        }

        [Test]
        public void DieUnderTwoFacesIsOne()
        {
            Assert.AreEqual(1, DiceRoll.Die(1));
            Assert.AreEqual(1, DiceRoll.Die(0));
        }

        [Test]
        public void SameSeedSameRolls()
        {
            DiceRoll.Seed(99);
            int a = DiceRoll.Die(20), b = DiceRoll.Die(20), c = DiceRoll.Die(20);
            DiceRoll.Seed(99);
            Assert.AreEqual(a, DiceRoll.Die(20));
            Assert.AreEqual(b, DiceRoll.Die(20));
            Assert.AreEqual(c, DiceRoll.Die(20));
        }

        [Test]
        public void IntRangeExcludesMax()
        {
            for (int i = 0; i < 500; i++) Assert.That(DiceRoll.Range(0, 3), Is.InRange(0, 2));
            Assert.AreEqual(5, DiceRoll.Range(5, 5));
        }

        [Test]
        public void FloatRangeAndValueStayInside()
        {
            for (int i = 0; i < 500; i++)
            {
                Assert.That(DiceRoll.Value(), Is.InRange(0f, 1f));
                Assert.That(DiceRoll.Range(2f, 4f), Is.InRange(2f, 4f));
                Assert.That(DiceRoll.InsideUnitCircle().magnitude, Is.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void ModifierRollsAsADie()
        {
            Assert.AreEqual(0, DiceRoll.RollModifier(0f));
            Assert.AreEqual(0, DiceRoll.RollModifier(-3f));
            Assert.AreEqual(1, DiceRoll.RollModifier(1f));
            for (int i = 0; i < 200; i++) Assert.That(DiceRoll.RollModifier(6.9f), Is.InRange(1, 6));
        }

        [Test]
        public void ExpressionRollsBetweenMinAndMax()
        {
            DiceRoll dice = DiceRoll.D8(2);
            Assert.AreEqual(2, dice.Min());
            Assert.AreEqual(16, dice.Max());
            Assert.AreEqual(9f, dice.Average());
            Assert.AreEqual("2d8", dice.Label());
            for (int i = 0; i < 300; i++) Assert.That(dice.Roll(), Is.InRange(2, 16));
        }
    }
}
