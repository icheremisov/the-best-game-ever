using NUnit.Framework;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class ShapeTests
    {
        [Test]
        public void Parse_SingleCell_ReturnsOneByOne()
        {
            var shape = Shape.Parse("X");
            Assert.AreEqual(1, shape.Rows);
            Assert.AreEqual(1, shape.Cols);
            Assert.IsTrue(shape.Cells[0, 0]);
        }

        [Test]
        public void Parse_TwoByTwoSquare()
        {
            var shape = Shape.Parse("XX|XX");
            Assert.AreEqual(2, shape.Rows);
            Assert.AreEqual(2, shape.Cols);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2; c++)
                    Assert.IsTrue(shape.Cells[r, c]);
        }

        [Test]
        public void Parse_LShape()
        {
            // .X
            // XX
            // .X
            // .X
            var shape = Shape.Parse(".X|XX|.X|.X");
            Assert.AreEqual(4, shape.Rows);
            Assert.AreEqual(2, shape.Cols);
            Assert.IsFalse(shape.Cells[0, 0]); Assert.IsTrue (shape.Cells[0, 1]);
            Assert.IsTrue (shape.Cells[1, 0]); Assert.IsTrue (shape.Cells[1, 1]);
            Assert.IsFalse(shape.Cells[2, 0]); Assert.IsTrue (shape.Cells[2, 1]);
            Assert.IsFalse(shape.Cells[3, 0]); Assert.IsTrue (shape.Cells[3, 1]);
        }

        [Test]
        public void Parse_RowsOfDifferentLengths_Throws()
        {
            Assert.Throws<System.FormatException>(() => Shape.Parse("X|XX"));
        }

        [Test]
        public void Parse_EmptyString_Throws()
        {
            Assert.Throws<System.FormatException>(() => Shape.Parse(""));
        }

        [Test]
        public void GetRotatedCells_Deg90_TransposesDimensions()
        {
            var shape = Shape.Parse("X|X|X"); // 3x1
            var rotated = shape.GetRotatedCells(Rotation.Deg90);
            Assert.AreEqual(1, rotated.GetLength(0));
            Assert.AreEqual(3, rotated.GetLength(1));
        }
    }
}
