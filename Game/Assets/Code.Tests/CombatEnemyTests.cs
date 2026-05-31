using NUnit.Framework;
using Mimic.Data;

namespace Mimic.Tests
{
    public class CombatEnemyTests
    {
        [Test]
        public void FromAdventurer_CopiesHpAttackName()
        {
            var a = new AdventurerData { Name = "Дракон", Hp = 12, Attack = 3, Battle = true };
            var e = CombatEnemy.FromAdventurer(a);
            Assert.AreEqual("Дракон", e.Name);
            Assert.AreEqual(12, e.MaxHp);
            Assert.AreEqual(12, e.Hp);
            Assert.AreEqual(3, e.Attack);
        }

        [Test]
        public void FromOverlord_UsesOverlordStats()
        {
            var d = new DayData { OverlordHp = 30, OverlordAttack = 4 };
            var e = CombatEnemy.FromOverlord(d);
            Assert.AreEqual(30, e.MaxHp);
            Assert.AreEqual(30, e.Hp);
            Assert.AreEqual(4, e.Attack);
            Assert.IsFalse(string.IsNullOrEmpty(e.Name), "у Хозяина есть имя");
        }
    }
}
