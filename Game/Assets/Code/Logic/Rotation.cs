namespace Mimic.Logic
{
    public enum Rotation { Deg0 = 0, Deg90 = 1, Deg180 = 2, Deg270 = 3 }

    public static class RotationUtil
    {
        public static bool[,] Rotate90CW(bool[,] src)
        {
            int rows = src.GetLength(0);
            int cols = src.GetLength(1);
            var dst = new bool[cols, rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    dst[c, rows - 1 - r] = src[r, c];
            return dst;
        }

        public static bool[,] Apply(bool[,] src, Rotation rot)
        {
            var result = src;
            for (int i = 0; i < (int)rot; i++)
                result = Rotate90CW(result);
            return result;
        }
    }
}
