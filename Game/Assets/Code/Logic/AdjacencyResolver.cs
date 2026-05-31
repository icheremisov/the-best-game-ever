using System;
using System.Collections.Generic;
using Mimic.Data;

namespace Mimic.Logic
{
    public class AdjacencyResult<T>
    {
        public Dictionary<T, int> EffectiveGold = new();
        public Dictionary<T, int> EffectiveAcid = new();
        public int TotalGold;
        public int GetGold(T item) => EffectiveGold.TryGetValue(item, out var v) ? v : 0;
        public int GetAcid(T item) => EffectiveAcid.TryGetValue(item, out var v) ? v : 1;
    }

    public static class AdjacencyResolver
    {
        public static AdjacencyResult<T> Resolve<T>(
            GridModel<T> grid,
            Func<T, string> idOf,
            Func<T, int> baseGoldOf,
            Func<T, int> baseAcidOf,
            Func<T, AdjacencyRule[]> adjacencyRulesOf
        ) where T : class
            => Resolve(grid, idOf, baseGoldOf, baseAcidOf, adjacencyRulesOf, _ => 0);

        public static AdjacencyResult<T> Resolve<T>(
            GridModel<T> grid,
            Func<T, string> idOf,
            Func<T, int> baseGoldOf,
            Func<T, int> baseAcidOf,
            Func<T, AdjacencyRule[]> adjacencyRulesOf,
            Func<T, int> neighborGoldPctOf
        ) where T : class
        {
            var result = new AdjacencyResult<T>();

            // 1. Базовые значения
            foreach (var item in grid.AllItems())
            {
                result.EffectiveGold[item] = baseGoldOf(item);
                result.EffectiveAcid[item] = baseAcidOf(item);
            }

            // 2. Входящие эффекты: суммируем множители по типам аддитивно, применяем к базе один раз
            foreach (var item in grid.AllItems())
            {
                var rules = adjacencyRulesOf(item);
                if (rules == null || rules.Length == 0) continue;

                var neighbors = GetEdgeNeighbors(grid, item);

                // множество явно названных таргетов предмета (для вайлдкарда)
                var named = new HashSet<string>();
                foreach (var rule in rules)
                    if (!rule.Wildcard && rule.Targets != null)
                        foreach (var t in rule.Targets) named.Add(t);

                float sumGold = 0f;
                float sumAcid = 0f;
                foreach (var rule in rules)
                {
                    if (rule.Effects == null || rule.Effects.Length == 0) continue;

                    int count = 0;
                    foreach (var n in neighbors)
                    {
                        if (ReferenceEquals(n, item)) continue;
                        string nid = idOf(n);
                        bool match = rule.Wildcard ? !named.Contains(nid) : ContainsId(rule.Targets, nid);
                        if (match) count++;
                    }
                    if (count == 0) continue;

                    foreach (var fx in rule.Effects)
                    {
                        int times = fx.Stackable ? count : 1;
                        float contribution = fx.Multiplier * times;
                        if (fx.Type == EffectType.Gold) sumGold += contribution;
                        else if (fx.Type == EffectType.Acid) sumAcid += contribution;
                    }
                }

                if (sumGold != 0f)
                {
                    float g = baseGoldOf(item) * (1f + sumGold);
                    result.EffectiveGold[item] = (int)System.Math.Max(0, System.Math.Round(g));
                }
                if (sumAcid != 0f)
                {
                    float a = baseAcidOf(item) * (1f + sumAcid);
                    result.EffectiveAcid[item] = (int)System.Math.Max(1, System.Math.Round(a));
                }
            }

            // 2b. Исходящий эффект: предмет с NeighborGoldPct != 0 меняет золото 4-соседей.
            foreach (var src in grid.AllItems())
            {
                int pct = neighborGoldPctOf(src);
                if (pct == 0) continue;
                foreach (var nb in GetEdgeNeighbors(grid, src))
                {
                    if (ReferenceEquals(nb, src)) continue;
                    int g = result.EffectiveGold[nb];
                    result.EffectiveGold[nb] = (int)System.Math.Max(0, System.Math.Round(g * (1f + pct / 100f)));
                }
            }

            // 3. Total (после исходящих эффектов)
            foreach (var v in result.EffectiveGold.Values) result.TotalGold += v;
            return result;
        }

        private static bool ContainsId(string[] ids, string id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return true;
            return false;
        }

        private static HashSet<T> GetEdgeNeighbors<T>(GridModel<T> grid, T item) where T : class
        {
            var seen = new HashSet<T>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (!ReferenceEquals(grid.GetAt(x, y), item)) continue;
                    TryAddNeighbor(grid, x + 1, y, item, seen);
                    TryAddNeighbor(grid, x - 1, y, item, seen);
                    TryAddNeighbor(grid, x, y + 1, item, seen);
                    TryAddNeighbor(grid, x, y - 1, item, seen);
                }
            }
            return seen;
        }

        private static void TryAddNeighbor<T>(GridModel<T> grid, int x, int y, T self, HashSet<T> set) where T : class
        {
            var n = grid.GetAt(x, y);
            if (n == null || ReferenceEquals(n, self)) return;
            set.Add(n);
        }
    }
}
