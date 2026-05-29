using NUnit.Framework;
using Mimic.Game;
using Mimic.Data;

namespace Mimic.Tests
{
    public class GameResourcesTests
    {
        private static DayData Day(int day, int quota, int hp, int acid) =>
            new DayData { Day = day, GoldQuota = quota, StartHp = hp, StartAcid = acid,
                          AdventurerIds = new[] { "warrior" }, GoldDamageMult = 1f, RansomGold = 1000 };

        [Test]
        public void StartDay_FirstDay_InitsHpAcidAndQuota()
        {
            var r = new GameResources();
            r.StartDay(Day(1, 40, 3, 15), firstDay: true);
            Assert.AreEqual(3, r.CurrentHp);
            Assert.AreEqual(15, r.CurrentAcid);
            Assert.AreEqual(40, r.DayQuota);
            Assert.AreEqual(0, r.BankedGold);
        }

        [Test]
        public void StartDay_NextDay_KeepsHpAndBanked_RefillsAcid()
        {
            var r = new GameResources();
            r.StartDay(Day(1, 40, 5, 15), firstDay: true);
            r.CurrentHp = 2;
            r.CurrentAcid = 0;
            r.BankedGold = 30;
            r.StartDay(Day(2, 60, 5, 15), firstDay: false);
            Assert.AreEqual(2, r.CurrentHp, "HP переносится");
            Assert.AreEqual(15, r.CurrentAcid, "ЖС восстановлен");
            Assert.AreEqual(30, r.BankedGold, "Banked копится");
            Assert.AreEqual(60, r.DayQuota);
        }

        [Test]
        public void TotalGold_IsHeldPlusBanked()
        {
            var r = new GameResources();
            r.CurrentGoldInMimic = 25;
            r.BankedGold = 40;
            Assert.AreEqual(65, r.TotalGold);
        }

        [Test]
        public void Snapshot_RestoresHpAndBanked()
        {
            var r = new GameResources();
            r.CurrentHp = 5; r.BankedGold = 10;
            var snap = r.SnapshotDayStart();
            r.CurrentHp = 1; r.BankedGold = 50;
            r.RestoreDayStart(snap);
            Assert.AreEqual(5, r.CurrentHp);
            Assert.AreEqual(10, r.BankedGold);
        }
    }
}
