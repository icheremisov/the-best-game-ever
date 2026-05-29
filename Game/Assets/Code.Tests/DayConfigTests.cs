using NUnit.Framework;
using Mimic.Catalogs;
using Mimic.Data;

namespace Mimic.Tests
{
    public class DayConfigTests
    {
        private static DayData D(int n) => new DayData { Day = n, GoldQuota = n * 10,
            StartHp = 3, StartAcid = 15, AdventurerIds = new[] { "warrior" },
            GoldDamageMult = 2f, RansomGold = 500 };

        [Test]
        public void Advance_MovesToNextDay()
        {
            DayConfig.SetForTest(new[] { D(1), D(2), D(3) });
            Assert.AreEqual(1, DayConfig.Current.Day);
            Assert.IsFalse(DayConfig.IsLastDay);
            Assert.IsTrue(DayConfig.Advance());
            Assert.AreEqual(2, DayConfig.Current.Day);
        }

        [Test]
        public void Advance_OnLastDay_ReturnsFalse()
        {
            DayConfig.SetForTest(new[] { D(1), D(2) });
            DayConfig.Advance(); // -> day 2
            Assert.IsTrue(DayConfig.IsLastDay);
            Assert.IsFalse(DayConfig.Advance());
            Assert.AreEqual(2, DayConfig.Current.Day, "не уходит за последний день");
        }
    }
}
