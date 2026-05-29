using System;
using System.Collections.Generic;

namespace Mimic.Logic
{
    public static class TheftResolver
    {
        // У предмета свободная верхняя грань, если есть его клетка, над которой
        // (по тому же x, y+1..Height-1) все клетки пусты.
        public static bool HasFreeTopEdge<T>(GridModel<T> grid, T item, Func<T, T> id) where T : class
        {
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (!ReferenceEquals(grid.GetAt(x, y), item)) continue;
                    bool clearAbove = true;
                    for (int yy = y + 1; yy < grid.Height; yy++)
                        if (grid.GetAt(x, yy) != null) { clearAbove = false; break; }
                    if (clearAbove) return true;
                }
            }
            return false;
        }

        // Случайный предмет со свободной верхней гранью, либо null.
        public static T PickStealable<T>(GridModel<T> grid, Func<T, T> id, int seed) where T : class
        {
            var candidates = new List<T>();
            foreach (var item in grid.AllItems())
                if (HasFreeTopEdge(grid, item, id)) candidates.Add(item);
            if (candidates.Count == 0) return null;
            var rng = new Random(seed);
            return candidates[rng.Next(candidates.Count)];
        }
    }
}
