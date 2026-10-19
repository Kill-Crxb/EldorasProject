using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Edit-mode tests for LootRoller (Loot_Build.md §3). They move to the package's Tests/EditMode with the loot code.

namespace CrabSystem.Tests
{
    public class LootRollerTests
    {
        private readonly List<Object> made = new List<Object>();

        [SetUp]
        public void Seed() => DiceRoll.Seed(4321);

        [TearDown]
        public void Clean()
        {
            foreach (Object asset in made) Object.DestroyImmediate(asset);
            made.Clear();
        }

        private LootTable Table()
        {
            var table = ScriptableObject.CreateInstance<LootTable>();
            made.Add(table);
            return table;
        }

        private static LootQuantity One() => new LootQuantity { dice = 0, faces = 6, flat = 1 };

        private static int Count(List<LootDrop> drops, string itemId)
        {
            int count = 0;
            foreach (LootDrop drop in drops)
                if (drop.itemId == itemId) count++;
            return count;
        }

        [Test]
        public void AlwaysDropsEveryKill()
        {
            LootTable table = Table();
            table.always.Add(new LootLine { itemId = "bone", quantity = One() });
            table.mainRolls = 0;

            for (int i = 0; i < 50; i++)
                Assert.AreEqual(1, Count(LootRoller.Roll(table), "bone"));
        }

        [Test]
        public void NothingDropsNothing()
        {
            LootTable table = Table();
            table.main.Add(new LootEntry { weight = 10 });

            for (int i = 0; i < 50; i++)
                Assert.IsEmpty(LootRoller.Roll(table));
        }

        [Test]
        public void WeightsLandNearTheirOdds()
        {
            LootTable table = Table();
            table.main.Add(new LootEntry { weight = 1, itemId = "rare", quantity = One() });
            table.main.Add(new LootEntry { weight = 3, itemId = "common", quantity = One() });

            int rare = 0;
            for (int i = 0; i < 8000; i++) rare += Count(LootRoller.Roll(table), "rare");

            Assert.That(rare, Is.InRange(1800, 2200), "1/4 of 8000 is 2000");
        }

        [Test]
        public void ZeroWeightIsNeverPicked()
        {
            LootTable table = Table();
            table.main.Add(new LootEntry { weight = 0, itemId = "never", quantity = One() });
            table.main.Add(new LootEntry { weight = 5, itemId = "always", quantity = One() });

            for (int i = 0; i < 200; i++)
                Assert.AreEqual(0, Count(LootRoller.Roll(table), "never"));
        }

        [Test]
        public void MainRollsRollThatManyTimes()
        {
            LootTable table = Table();
            table.mainRolls = 3;
            table.main.Add(new LootEntry { weight = 1, itemId = "coin", quantity = One() });

            Assert.AreEqual(3, Count(LootRoller.Roll(table), "coin"));
        }

        [Test]
        public void SubTableRollsItsMainListOnly()
        {
            LootTable gems = Table();
            gems.always.Add(new LootLine { itemId = "skipped_always", quantity = One() });
            gems.tertiary.Add(new LootChance { oneIn = 1, itemId = "skipped_tertiary", quantity = One() });
            gems.main.Add(new LootEntry { weight = 1, itemId = "ruby", quantity = One() });

            LootTable monster = Table();
            monster.main.Add(new LootEntry { weight = 1, subTable = gems });

            List<LootDrop> drops = LootRoller.Roll(monster);
            Assert.AreEqual(1, drops.Count);
            Assert.AreEqual("ruby", drops[0].itemId);
        }

        [Test]
        public void TertiaryOneInOneAlwaysDrops()
        {
            LootTable table = Table();
            table.mainRolls = 0;
            table.tertiary.Add(new LootChance { oneIn = 1, itemId = "clue", quantity = One() });

            Assert.AreEqual(1, Count(LootRoller.Roll(table), "clue"));
        }

        [Test]
        public void LoopStopsAtTheDepthCap()
        {
            LootTable a = Table();
            LootTable b = Table();
            a.main.Add(new LootEntry { weight = 1, subTable = b });
            b.main.Add(new LootEntry { weight = 1, subTable = a });

            LogAssert.Expect(LogType.Error, new Regex("nest deeper than"));
            Assert.IsEmpty(LootRoller.Roll(a));
        }

        [Test]
        public void QuantityRollsItsDice()
        {
            var quantity = new LootQuantity { dice = 3, faces = 6, flat = 0 };
            Assert.AreEqual("3d6", quantity.Label());

            for (int i = 0; i < 500; i++)
                Assert.That(quantity.Roll(), Is.InRange(3, 18));
        }

        [Test]
        public void QuantityIsAtLeastOne()
        {
            var quantity = new LootQuantity { dice = 0, faces = 6, flat = -4 };
            Assert.AreEqual(1, quantity.Roll());
        }
    }
}
