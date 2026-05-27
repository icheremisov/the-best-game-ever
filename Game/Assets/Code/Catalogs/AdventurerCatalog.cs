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
                    LootIds = row[3].Split(';')
                };
                _byId[d.Id] = d;
            }
        }

        public static AdventurerData Get(string id) => _byId[id];
    }
}
