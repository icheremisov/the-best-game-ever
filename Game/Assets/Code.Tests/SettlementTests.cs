using NUnit.Framework;
using Mimic.Logic;
using System.Collections.Generic;

namespace Mimic.Tests
{
    public class SettlementTests
    {
        [Test]
        public void Damage_Zero_WhenTotalMeetsQuota()
        {
            Assert.AreEqual(0, Settlement.Damage(total: 40, quota: 40, mult: 2f));
            Assert.AreEqual(0, Settlement.Damage(total: 55, quota: 40, mult: 2f));
        }

        [Test]
        public void Damage_ShortfallTimesMult_RoundedUp()
        {
            // недобор 10, mult 2 => 20
            Assert.AreEqual(20, Settlement.Damage(total: 30, quota: 40, mult: 2f));
            // недобор 5, mult 0.5 => 2.5 -> ceil 3
            Assert.AreEqual(3, Settlement.Damage(total: 35, quota: 40, mult: 0.5f));
        }

        [Test]
        public void Pick3_ReturnsUpToThree_NoDuplicates()
        {
            var pool = new List<string> { "a", "b", "c", "d", "e" };
            var picked = Settlement.Pick3(pool, seed: 1);
            Assert.AreEqual(3, picked.Count);
            CollectionAssert.AllItemsAreUnique(picked);
            foreach (var p in picked) Assert.Contains(p, pool);
        }

        [Test]
        public void Pick3_SmallPool_ReturnsAll()
        {
            var pool = new List<string> { "a", "b" };
            var picked = Settlement.Pick3(pool, seed: 1);
            Assert.AreEqual(2, picked.Count);
        }

        [Test]
        public void Damage_FractionalMult_NoFloatOvershoot()
        {
            // 10 * 1.1 = 11 — не должно округлиться до 12 из-за float-погрешности
            Assert.AreEqual(11, Settlement.Damage(total: 30, quota: 40, mult: 1.1f));
        }

        [Test]
        public void Damage_NegativeMult_ReturnsZero()
        {
            Assert.AreEqual(0, Settlement.Damage(total: 30, quota: 40, mult: -1f));
        }

        [Test]
        public void Pick3_Deterministic_WithSeed()
        {
            var pool = new List<string> { "a", "b", "c", "d", "e" };
            var p1 = Settlement.Pick3(pool, seed: 7);
            var p2 = Settlement.Pick3(pool, seed: 7);
            CollectionAssert.AreEqual(p1, p2);
        }

        [Test]
        public void Pick3_EmptyPool_ReturnsEmpty()
        {
            var picked = Settlement.Pick3(new List<string>(), seed: 1);
            Assert.AreEqual(0, picked.Count);
        }
    }
}
