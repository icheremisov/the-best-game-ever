namespace Mimic.Game
{
    public struct DayStartSnapshot
    {
        public int Hp;
        public int BankedGold;
    }

    public class GameResources
    {
        public int CurrentHp;
        public int CurrentAcid;
        public int CurrentGoldInMimic; // Held — живая сумма золота лута в мимике
        public int BankedGold;         // сдано в корзину, копится навсегда
        public int DayQuota;

        public int TotalGold => CurrentGoldInMimic + BankedGold;

        public void StartDay(Mimic.Data.DayData day, bool firstDay)
        {
            if (firstDay)
            {
                CurrentHp = day.StartHp;
                BankedGold = 0;
            }
            // HP и BankedGold переносятся со второго дня
            CurrentAcid = day.StartAcid; // ЖС всегда в фул
            DayQuota = day.GoldQuota;
            if (firstDay) CurrentGoldInMimic = 0;
        }

        public DayStartSnapshot SnapshotDayStart() =>
            new DayStartSnapshot { Hp = CurrentHp, BankedGold = BankedGold };

        public void RestoreDayStart(DayStartSnapshot s)
        {
            CurrentHp = s.Hp;
            BankedGold = s.BankedGold;
        }
    }
}
