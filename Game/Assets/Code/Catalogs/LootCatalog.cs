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
                    AdjacencyRules = AdjacencyRule.ParseRules(row[7]),
                    Category = ParseCategory(Col(row, 8, "normal")),
                    AcidRestoreOnDigest = int.Parse(Col(row, 9, "0")),
                    DamageOnDigest = int.Parse(Col(row, 10, "0")),
                    CanReturnToBasket = Col(row, 11, "1") != "0",
                    IsGlue = Col(row, 12, "0") == "1",
                    NeighborGoldPct = int.Parse(Col(row, 13, "0")),
                    Attack = int.Parse(Col(row, 14, "0")),
                    AttackOnDigest = int.Parse(Col(row, 15, "0")),
                };
                d.IsFixture = d.Category == LootCategory.Fixture;
                _byId[d.Id] = d;
            }
        }

        private static string Col(System.Collections.Generic.IReadOnlyList<string> row, int i, string def)
            => i < row.Count && !string.IsNullOrEmpty(row[i]) ? row[i] : def;

        private static LootCategory ParseCategory(string s) => s switch
        {
            "reward"  => LootCategory.Reward,
            "punish"  => LootCategory.Punish,
            "fixture" => LootCategory.Fixture,
            _          => LootCategory.Normal,
        };

        public static System.Collections.Generic.List<LootData> ByCategory(LootCategory cat)
        {
            var list = new System.Collections.Generic.List<LootData>();
            foreach (var d in _byId.Values) if (d.Category == cat) list.Add(d);
            return list;
        }

        public static LootData Get(string id) => _byId[id];
    }
}
