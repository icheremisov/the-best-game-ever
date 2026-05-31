using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class GridGeometryTests
    {
        [Test]
        public void Border_SingleCell_FourNeighbors()
        {
            var b = GridGeometry.BorderCells(new[] { (0, 0) });
            CollectionAssert.AreEquivalent(
                new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }, b);
        }

        [Test]
        public void Border_HorizontalLine_SixCells_NoOverlapWithFigure()
        {
            var fig = new[] { (0, 0), (1, 0) };
            var b = GridGeometry.BorderCells(fig);
            CollectionAssert.AreEquivalent(
                new[] { (-1, 0), (2, 0), (0, 1), (1, 1), (0, -1), (1, -1) }, b);
            foreach (var f in fig)
                Assert.IsFalse(b.Contains(f), "клетка фигуры не должна попадать в бордюр");
        }

        [Test]
        public void Border_TwoByTwo_EightCells()
        {
            var b = GridGeometry.BorderCells(new[] { (0, 0), (1, 0), (0, 1), (1, 1) });
            Assert.AreEqual(8, b.Count);
            CollectionAssert.AreEquivalent(
                new[] { (-1, 0), (0, -1), (2, 0), (1, -1), (-1, 1), (0, 2), (2, 1), (1, 2) }, b);
        }

        [Test]
        public void Border_LShape_InnerCornerCountedOnce()
        {
            // L: (0,0),(0,1),(1,1). Внутренний угол (1,0) граничит с двумя клетками — учитывается один раз.
            var b = GridGeometry.BorderCells(new[] { (0, 0), (0, 1), (1, 1) });
            CollectionAssert.AreEquivalent(
                new[] { (1, 0), (-1, 0), (0, -1), (-1, 1), (0, 2), (2, 1), (1, 2) }, b);
        }

        [Test]
        public void Border_UShape_NotchInsideBoundingBoxIsBorder()
        {
            // U-образная фигура с вырезом в (1,0): (0,0),(2,0),(0,1),(1,1),(2,1).
            var b = GridGeometry.BorderCells(new[] { (0, 0), (2, 0), (0, 1), (1, 1), (2, 1) });
            Assert.IsTrue(b.Contains((1, 0)), "вырез внутри bounding box — это клетка-сосед");
        }

        [Test]
        public void Border_Duplicates_AreDeduped()
        {
            // повтор клетки на входе не должен ломать результат
            var b = GridGeometry.BorderCells(new[] { (0, 0), (0, 0) });
            CollectionAssert.AreEquivalent(
                new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }, b);
        }

        [Test]
        public void Border_BarracudaShape_NoFigureCells_AndNotchPresent()
        {
            // реальная форма барракуды X.|XX|X.|X.
            var cells = ShapeCells(Shape.Parse("X.|XX|X.|X."));
            var b = GridGeometry.BorderCells(cells);
            foreach (var c in cells)
                Assert.IsFalse(b.Contains(c), "клетки фигуры не в бордюре");
            Assert.Greater(b.Count, 0);
        }

        // Преобразует Shape в список занятых клеток (col, row).
        private static List<(int x, int y)> ShapeCells(Shape s)
        {
            var g = s.GetRotatedCells(Rotation.Deg0);
            var list = new List<(int x, int y)>();
            for (int r = 0; r < g.GetLength(0); r++)
                for (int c = 0; c < g.GetLength(1); c++)
                    if (g[r, c]) list.Add((c, r));
            return list;
        }
    }
}
