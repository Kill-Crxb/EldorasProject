using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace CrabSystem.Tests
{
    public class BlackboardKeyTests
    {
        [Test]
        public void HashIsFnv1a()
        {
            Assert.AreEqual(0, BlackboardKey.Hash(""));
            Assert.AreEqual(0, BlackboardKey.Hash(null));
            Assert.AreEqual(-468965076, BlackboardKey.Hash("a"));
        }

        [Test]
        public void KeyStructUsesTheSameHash()
        {
            Assert.AreEqual(BlackboardKey.Hash("IsDead"), new BlackboardKey("IsDead").hash);
        }

        // Every constant must be the hash of its own name (assets name facts by string), and no two may collide.
        [Test]
        public void ConstantsMatchTheirNamesAndNeverCollide()
        {
            var seen = new Dictionary<int, string>();
            foreach (FieldInfo field in typeof(BlackboardKey).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(int)) continue;
                int value = (int)field.GetValue(null);
                Assert.AreEqual(BlackboardKey.Hash(field.Name), value, field.Name);
                Assert.IsFalse(seen.ContainsKey(value), $"{field.Name} collides with {(seen.TryGetValue(value, out var other) ? other : "")}");
                seen[value] = field.Name;
            }
        }
    }
}
