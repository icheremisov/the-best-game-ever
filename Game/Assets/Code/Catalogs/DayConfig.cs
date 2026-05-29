using System.Collections.Generic;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class DayConfig
    {
        private static List<Mimic.Data.DayData> _days = new();
        private static int _index;

        public static Mimic.Data.DayData Current => _days[_index];
        public static bool IsLastDay => _index >= _days.Count - 1;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/day.csv");
            var rows = CsvLoader.ParseAll(ta.text);
            if (rows.Count == 0)
                throw new System.InvalidOperationException("day.csv has no data rows");
            _days = new List<Mimic.Data.DayData>();
            foreach (var r in rows)
            {
                _days.Add(new Mimic.Data.DayData
                {
                    Day = int.Parse(r[0]),
                    GoldQuota = int.Parse(r[1]),
                    StartHp = int.Parse(r[2]),
                    StartAcid = int.Parse(r[3]),
                    AdventurerIds = r[4].Split(';'),
                    GoldDamageMult = r.Length > 5 && !string.IsNullOrEmpty(r[5]) ? float.Parse(r[5], System.Globalization.CultureInfo.InvariantCulture) : 1f,
                    RansomGold = r.Length > 6 && !string.IsNullOrEmpty(r[6]) ? int.Parse(r[6]) : 999999,
                });
            }
            _index = 0;
        }

        // true если перешли на следующий день; false если уже последний
        public static bool Advance()
        {
            if (IsLastDay) return false;
            _index++;
            return true;
        }

        public static void SetForTest(IEnumerable<Mimic.Data.DayData> days)
        {
            _days = new List<Mimic.Data.DayData>(days);
            _index = 0;
        }
    }
}
