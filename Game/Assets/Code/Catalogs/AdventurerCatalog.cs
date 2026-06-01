using System.Collections.Generic;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class AdventurerCatalog
    {
        private static Dictionary<string, AdventurerData> _byId;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/adventurers.csv");
            _byId = new Dictionary<string, AdventurerData>();
            foreach (var row in CsvLoader.ParseAll(ta.text))
            {
                var d = new AdventurerData
                {
                    Id = row[0],
                    Name = row[1],
                    Phrase = row[2],
                    LootIds = row[3].Split(';'),
                    Battle = row.Length > 4 && (row[4] == "1" || row[4].ToLowerInvariant() == "true"),
                    Hp = row.Length > 5 && !string.IsNullOrEmpty(row[5]) ? int.Parse(row[5]) : 0,
                    Attack = row.Length > 6 && !string.IsNullOrEmpty(row[6]) ? int.Parse(row[6]) : 0,
                    Budget = row.Length > 7 && !string.IsNullOrEmpty(row[7]) ? int.Parse(row[7]) : 0,
                };
                _byId[d.Id] = d;
            }
        }

        public static AdventurerData Get(string id) => _byId[id];
    }
}
