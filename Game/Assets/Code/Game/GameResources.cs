namespace Mimic.Game
{
    public class GameResources
    {
        public int CurrentHp;
        public int CurrentAcid;
        public int CurrentGoldInMimic;
        public int DayQuota;

        public void StartDay(Mimic.Data.DayData day)
        {
            CurrentHp = day.StartHp;
            CurrentAcid = day.StartAcid;
            CurrentGoldInMimic = 0;
            DayQuota = day.GoldQuota;
        }
    }
}
