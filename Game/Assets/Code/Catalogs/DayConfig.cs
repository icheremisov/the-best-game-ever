using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class DayConfig
    {
        public static Mimic.Data.DayData Current { get; private set; }

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/day.csv");
            var rows = CsvLoader.ParseAll(ta.text);
            if (rows.Count == 0)
                throw new System.InvalidOperationException("day.csv has no data rows");
            var r = rows[0];
            Current = new Mimic.Data.DayData
            {
                Day = int.Parse(r[0]),
                GoldQuota = int.Parse(r[1]),
                StartHp = int.Parse(r[2]),
                StartAcid = int.Parse(r[3]),
                AdventurerIds = r[4].Split(';')
            };
        }
    }
}
