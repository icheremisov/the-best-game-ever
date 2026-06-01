namespace Mimic.Data
{
    public class DayData
    {
        public int Day;
        public int GoldQuota;
        public int StartHp;
        public int StartAcid;
        public string[] AdventurerIds;
        public float GoldDamageMult;
        public int RansomGold;
        public int OverlordHp;     // hp Хозяина в финальном бою
        public int OverlordAttack; // атака Хозяина
        public int BiteDamage;     // урон стандартной атаки «Кусь» (настройка ГД)
    }
}
