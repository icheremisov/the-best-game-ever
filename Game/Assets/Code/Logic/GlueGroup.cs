using System;
using System.Collections.Generic;

namespace Mimic.Logic
{
    public static class GlueGroup
    {
        // Если item НЕ касается клея — вернёт {item}.
        // Если касается — вернёт связную группу: клей + все его прямые 4-соседи.
        public static List<T> Resolve<T>(GridModel<T> grid, T item, Func<T, bool> isGlue) where T : class
        {
            var single = new List<T> { item };
            T glue = isGlue(item) ? item : FindGlueNeighbor(grid, item, isGlue);
            if (glue == null) return single;

            var group = new List<T> { glue };
            foreach (var nb in Neighbors4(grid, glue))
                if (!group.Contains(nb)) group.Add(nb);
            if (!group.Contains(item)) group.Add(item);
            return group;
        }

        private static T FindGlueNeighbor<T>(GridModel<T> grid, T item, Func<T, bool> isGlue) where T : class
        {
            foreach (var nb in Neighbors4(grid, item))
                if (isGlue(nb)) return nb;
            return null;
        }

        private static IEnumerable<T> Neighbors4<T>(GridModel<T> grid, T item) where T : class
        {
            var seen = new HashSet<T>();
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    if (!ReferenceEquals(grid.GetAt(x, y), item)) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        var nb = grid.GetAt(x + dx[k], y + dy[k]);
                        if (nb != null && !ReferenceEquals(nb, item) && seen.Add(nb)) yield return nb;
                    }
                }
        }
    }
}
