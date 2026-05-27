using System.Collections.Generic;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class LootCatalog
    {
        private static Dictionary<string, LootData> _byId;

        public static IReadOnlyDictionary<string, LootData> ById => _byId;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/loot.csv");
            _byId = new Dictionary<string, LootData>();
            foreach (var row in CsvLoader.ParseAll(ta.text))
            {
                var d = new LootData
                {
                    Id = row[0],
                    Name = row[1],
                    Description = row[2],
                    Shape = Shape.Parse(row[3]),
                    Gold = int.Parse(row[4]),
                    AcidCost = int.Parse(row[5]),
                    HealOnDigest = int.Parse(row[6]),
                    CellsRestoredOnDigest = int.Parse(row[7]),
                    AdjacencyTarget = row[8],
                    AdjacencyEffects = AdjacencyEffect.ParseList(row[9])
                };
                _byId[d.Id] = d;
            }
        }

        public static LootData Get(string id) => _byId[id];
    }
}
