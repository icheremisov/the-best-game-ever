using System.Collections.Generic;

namespace Mimic.Logic
{
    // Геометрия фигуры на сетке.
    public static class GridGeometry
    {
        // Клетки, ортогонально прилегающие к фигуре, но НЕ входящие в неё —
        // именно их надо проверять на соседей. Работает для любой фигуры
        // (выпуклой, вогнутой, с вырезами/дырами). Координаты могут выходить
        // за пределы фигуры (в т.ч. отрицательные) — обрезка по сетке снаружи.
        public static HashSet<(int x, int y)> BorderCells(IEnumerable<(int x, int y)> cells)
        {
            var occupied = new HashSet<(int x, int y)>(cells);
            var border = new HashSet<(int x, int y)>();
            foreach (var (cx, cy) in occupied)
            {
                AddIfFree(occupied, border, cx + 1, cy);
                AddIfFree(occupied, border, cx - 1, cy);
                AddIfFree(occupied, border, cx, cy + 1);
                AddIfFree(occupied, border, cx, cy - 1);
            }
            return border;
        }

        private static void AddIfFree(HashSet<(int x, int y)> occupied,
                                      HashSet<(int x, int y)> border, int x, int y)
        {
            var cell = (x, y);
            if (!occupied.Contains(cell)) border.Add(cell);
        }
    }
}
