using NUnit.Framework;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class CombatResolverTests
    {
        private static CombatEnemy Enemy(int hp, int atk) =>
            new CombatEnemy { Name = "E", MaxHp = hp, Hp = hp, Attack = atk };

        [Test]
        public void ApplyDamageToEnemy_SubtractsHp()
        {
            var e = Enemy(10, 2);
            CombatResolver.ApplyDamageToEnemy(e, 3);
            Assert.AreEqual(7, e.Hp);
        }

        [Test]
        public void ApplyDamageToEnemy_ClampsAtZero()
        {
            var e = Enemy(2, 2);
            CombatResolver.ApplyDamageToEnemy(e, 5);
            Assert.AreEqual(0, e.Hp);
        }

        [Test]
        public void ApplyDamageToEnemy_IgnoresNonPositive()
        {
            var e = Enemy(10, 2);
            CombatResolver.ApplyDamageToEnemy(e, 0);
            CombatResolver.ApplyDamageToEnemy(e, -4);
            Assert.AreEqual(10, e.Hp);
        }

        [Test]
        public void EnemyAttackDamage_ReturnsAttack()
        {
            Assert.AreEqual(4, CombatResolver.EnemyAttackDamage(Enemy(10, 4)));
        }

        [Test]
        public void IsEnemyDead_TrueAtZeroOrBelow()
        {
            var e = Enemy(1, 1);
            Assert.IsFalse(CombatResolver.IsEnemyDead(e));
            CombatResolver.ApplyDamageToEnemy(e, 1);
            Assert.IsTrue(CombatResolver.IsEnemyDead(e));
        }

        [Test]
        public void IsPlayerDead_TrueAtZeroOrBelow()
        {
            Assert.IsFalse(CombatResolver.IsPlayerDead(1));
            Assert.IsTrue(CombatResolver.IsPlayerDead(0));
            Assert.IsTrue(CombatResolver.IsPlayerDead(-3));
        }

        [Test]
        public void ItemAttackDamage_ZeroMeansNoAttack()
        {
            // предмет без атаки урона не наносит и не «расходуется»
            Assert.AreEqual(0, CombatResolver.ItemAttackDamage(new LootData { Attack = 0 }));
            Assert.AreEqual(6, CombatResolver.ItemAttackDamage(new LootData { Attack = 6 }));
        }
    }
}
