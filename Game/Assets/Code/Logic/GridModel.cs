using System.Collections.Generic;
using Mimic.Data;

namespace Mimic.Logic
{
    public class GridModel<T> where T : class
    {
        public int Width { get; }
        public int Height { get; }
        private readonly T[,] cells;
        private readonly Dictionary<T, (int x, int y, Rotation rot)> placements = new();

        public GridModel(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new T[width, height];
        }

        public T GetAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return cells[x, y];
        }

        public bool TryPlace(T item, int x, int y, Rotation rot)
        {
            var shape = GetShape(item);
            var rotated = shape.GetRotatedCells(rot);
            int sh = rotated.GetLength(0); // rows
            int sw = rotated.GetLength(1); // cols

            // Bounds + overlap check
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    int gx = x + c;
                    int gy = y + r;
                    if (gx < 0 || gx >= Width || gy < 0 || gy >= Height) return false;
                    if (cells[gx, gy] != null) return false;
                }
            }

            // Commit
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    cells[x + c, y + r] = item;
                }
            }
            placements[item] = (x, y, rot);
            return true;
        }

        public void Remove(T item)
        {
            if (!placements.TryGetValue(item, out var p)) return;
            var rotated = GetShape(item).GetRotatedCells(p.rot);
            int sh = rotated.GetLength(0);
            int sw = rotated.GetLength(1);
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    cells[p.x + c, p.y + r] = null;
                }
            }
            placements.Remove(item);
        }

        public int FreeCellsCount
        {
            get
            {
                int count = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (cells[x, y] == null) count++;
                return count;
            }
        }

        public IEnumerable<T> AllItems() => placements.Keys;

        public bool TryGetPlacement(T item, out int x, out int y, out Rotation rot)
        {
            if (placements.TryGetValue(item, out var p))
            {
                x = p.x; y = p.y; rot = p.rot;
                return true;
            }
            x = y = 0; rot = Rotation.Deg0;
            return false;
        }

        // Shape provider — implemented either by reflection on the item, or via injected delegate.
        // For tests we use a duck-typed field `.Shape`.
        private static Shape GetShape(T item)
        {
            var prop = typeof(T).GetField("Shape");
            if (prop != null) return (Shape)prop.GetValue(item);
            var p = typeof(T).GetProperty("Shape");
            if (p != null) return (Shape)p.GetValue(item);
            throw new System.InvalidOperationException(
                $"Type {typeof(T).Name} must expose a public 'Shape' field or property");
        }
    }
}
