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
            Func<T, string> adjacencyTargetOf,
            Func<T, AdjacencyEffect[]> adjacencyEffectsOf
        ) where T : class
        {
            var result = new AdjacencyResult<T>();

            // 1. Заполнить базовые значения
            foreach (var item in grid.AllItems())
            {
                result.EffectiveGold[item] = baseGoldOf(item);
                result.EffectiveAcid[item] = baseAcidOf(item);
            }

            // 2. Для каждого item найти 4-соседей и применить эффект
            foreach (var item in grid.AllItems())
            {
                string target = adjacencyTargetOf(item);
                if (string.IsNullOrEmpty(target)) continue;
                var effects = adjacencyEffectsOf(item);
                if (effects == null || effects.Length == 0) continue;

                var neighbors = GetEdgeNeighbors(grid, item);

                bool triggered = false;
                foreach (var n in neighbors)
                {
                    if (n == item) continue; // самососедство требует другую копию
                    if (idOf(n) == target) { triggered = true; break; }
                }
                if (!triggered) continue;

                foreach (var fx in effects)
                {
                    if (fx.Type == EffectType.Gold)
                    {
                        float newVal = result.EffectiveGold[item] * (1f + fx.Multiplier);
                        result.EffectiveGold[item] = (int)System.Math.Max(0, System.Math.Round(newVal));
                    }
                    else if (fx.Type == EffectType.Acid)
                    {
                        float newVal = result.EffectiveAcid[item] * (1f + fx.Multiplier);
                        result.EffectiveAcid[item] = (int)System.Math.Max(1, System.Math.Round(newVal));
                    }
                }
            }

            // 3. Total
            foreach (var v in result.EffectiveGold.Values) result.TotalGold += v;
            return result;
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
