using System;
using System.Collections.Generic;

namespace Mimic.Logic
{
    public static class Settlement
    {
        // Урон = недостающее золото * множитель, округление вверх. 0 если квота закрыта.
        public static int Damage(int total, int quota, float mult)
        {
            int shortfall = quota - total;
            if (shortfall <= 0) return 0;
            return (int)Math.Ceiling(shortfall * mult);
        }

        // До 3 различных элементов из пула (Fisher–Yates по копии).
        public static List<T> Pick3<T>(IReadOnlyList<T> pool, int seed)
        {
            var copy = new List<T>(pool);
            var rng = new Random(seed);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (copy[i], copy[j]) = (copy[j], copy[i]);
            }
            int take = Math.Min(3, copy.Count);
            return copy.GetRange(0, take);
        }
    }
}
