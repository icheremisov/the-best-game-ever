using System;
using Mimic.Logic;

namespace Mimic.Data
{
    public class Shape
    {
        public int Rows { get; }
        public int Cols { get; }
        public bool[,] Cells { get; }

        public int CellCount { get; }

        public Shape(bool[,] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            Cells = cells;
            Rows = cells.GetLength(0);
            Cols = cells.GetLength(1);

            int count = 0;
            foreach (var occupied in cells)
                if (occupied) count++;
            CellCount = count;
        }

        public static Shape Parse(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new FormatException("Shape pattern is empty");

            var rows = pattern.Split('|');
            int cols = rows[0].Length;
            if (cols == 0) throw new FormatException("Shape row is empty");
            foreach (var row in rows)
                if (row.Length != cols)
                    throw new FormatException($"Shape rows must be same length: got '{pattern}'");

            var cells = new bool[rows.Length, cols];
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    char ch = rows[r][c];
                    if (ch == 'X') cells[r, c] = true;
                    else if (ch == '.') cells[r, c] = false;
                    else throw new FormatException($"Shape cell must be 'X' or '.', got '{ch}'");
                }
            }
            return new Shape(cells);
        }

        public bool[,] GetRotatedCells(Rotation rot) => RotationUtil.Apply(Cells, rot);
    }
}
