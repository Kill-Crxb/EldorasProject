using NUnit.Framework;
using CrabSystem.Stats;
using UnityEngine;

namespace CrabSystem.Tests
{
    public class CombatTests
    {
        [Test]
        public void SoakBarAbsorbsThenRefillsAfterQuiet()
        {
            var bar = new SoakBar();
            bar.Configure(5f, 0, 0, 0);
            Assert.AreEqual(5f, bar.Max);

            Assert.AreEqual(0f, bar.Absorb(3f, now: 0f));
            Assert.AreEqual(2f, bar.Current);
            Assert.AreEqual(8f, bar.Absorb(10f, now: 0f));
            Assert.IsTrue(bar.IsEmpty);

            bar.Tick(1f);
            Assert.IsTrue(bar.IsEmpty, "refilled inside the delay");
            bar.Tick(10f);
            Assert.AreEqual(5f, bar.Current);
        }

        [Test]
        public void DamageTypeLookupFallsBackToDefault()
        {
            var physical = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            physical.id = DamageTypeIds.Physical;
            var fallback = ScriptableObject.CreateInstance<DamageTypeDefinition>();
            fallback.id = "unconfigured";
            var config = ScriptableObject.CreateInstance<DamageCalculationConfig>();
            config.damageTypes.Add(null);
            config.damageTypes.Add(physical);
            config.defaultType = fallback;

            Assert.AreSame(physical, config.Find(DamageTypeIds.Physical));
            Assert.AreSame(fallback, config.Find("no_such_type"));

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(physical);
            Object.DestroyImmediate(fallback);
        }
    }
}
