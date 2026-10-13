using NUnit.Framework;

namespace CrabSystem.Tests
{
    public class TimeTests
    {
        [Test]
        public void ClockAdvancesByScaledDelta()
        {
            var clock = new EntityClock(10f);
            clock.Advance(0.5f);
            Assert.AreEqual(10.5f, clock.Now, 1e-5f);

            clock.Scale = 0.5f;
            clock.Advance(1f);
            Assert.AreEqual(0.5f, clock.Delta, 1e-5f);
            Assert.AreEqual(11f, clock.Now, 1e-5f);
        }

        [Test]
        public void CountdownFinishesOnceAndClamps()
        {
            var timer = new CountdownTimer(1f);
            int finished = 0;
            timer.OnTimerFinished += () => finished++;

            timer.Tick(0.6f);
            Assert.IsFalse(timer.IsFinished);
            timer.Tick(0.6f);
            timer.Tick(0.6f);
            Assert.IsTrue(timer.IsFinished);
            Assert.AreEqual(0f, timer.CurrentTime);
            Assert.AreEqual(1, finished);
        }

        [Test]
        public void AbilityInstanceExpiresOnItsOwnersClock()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.abilityId = "test";
            var instance = new AbilityInstance(definition, AbilitySource.Temporary, now: 100f, durationSeconds: 5f);

            Assert.IsTrue(instance.IsUsable(104f));
            Assert.AreEqual(1f, instance.GetRemainingDuration(104f), 1e-5f);
            Assert.IsFalse(instance.IsUsable(105f));
            Assert.IsTrue(instance.ShouldRemove(105f));
            UnityEngine.Object.DestroyImmediate(definition);
        }

        [Test]
        public void ConsumableRunsOutOfUses()
        {
            var definition = UnityEngine.ScriptableObject.CreateInstance<AbilityDefinition>();
            definition.abilityId = "potion";
            var instance = new AbilityInstance(definition, AbilitySource.Consumable, 0f, maxUses: 2);

            Assert.IsTrue(instance.ConsumeUse());
            Assert.IsTrue(instance.ConsumeUse());
            Assert.IsFalse(instance.ConsumeUse());
            Assert.IsFalse(instance.IsUsable(0f));
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }
}
