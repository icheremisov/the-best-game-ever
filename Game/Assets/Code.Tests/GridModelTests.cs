using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class GridModelTests
    {
        // Простой токен, заменяющий LootView в логических тестах
        private class Token { public Shape Shape; }

        [Test]
        public void TryPlace_EmptyGrid_FitsAndOccupies()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX|XX") };
            bool ok = grid.TryPlace(token, x: 0, y: 0, rot: Rotation.Deg0);
            Assert.IsTrue(ok);
            Assert.AreSame(token, grid.GetAt(0, 0));
            Assert.AreSame(token, grid.GetAt(1, 0));
            Assert.AreSame(token, grid.GetAt(0, 1));
            Assert.AreSame(token, grid.GetAt(1, 1));
        }

        [Test]
        public void TryPlace_OutOfBounds_Fails()
        {
            var grid = new GridModel<Token>(2, 2);
            var token = new Token { Shape = Shape.Parse("XX|XX") };
            Assert.IsFalse(grid.TryPlace(token, 1, 1, Rotation.Deg0));
        }

        [Test]
        public void TryPlace_Overlap_Fails()
        {
            var grid = new GridModel<Token>(4, 4);
            var a = new Token { Shape = Shape.Parse("XX") };
            var b = new Token { Shape = Shape.Parse("XX") };
            Assert.IsTrue(grid.TryPlace(a, 0, 0, Rotation.Deg0));
            Assert.IsFalse(grid.TryPlace(b, 1, 0, Rotation.Deg0)); // overlaps a at (1,0)
        }

        [Test]
        public void TryPlace_WithRotation90_FitsRotated()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX|XX|XX") }; // 3 rows x 2 cols
            // Without rotation: doesn't fit at (3, 1) because shape is 3 tall
            Assert.IsFalse(grid.TryPlace(token, 3, 1, Rotation.Deg0));
            // After 90° CW: becomes 2 tall x 3 wide
            Assert.IsTrue(grid.TryPlace(token, 1, 2, Rotation.Deg90));
        }

        [Test]
        public void Remove_FreesCells()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX") };
            grid.TryPlace(token, 0, 0, Rotation.Deg0);
            grid.Remove(token);
            Assert.IsNull(grid.GetAt(0, 0));
            Assert.IsNull(grid.GetAt(1, 0));
        }

        [Test]
        public void FreeCellsCount_TracksOccupancy()
        {
            var grid = new GridModel<Token>(3, 3); // 9 total
            Assert.AreEqual(9, grid.FreeCellsCount);
            grid.TryPlace(new Token { Shape = Shape.Parse("XX|XX") }, 0, 0, Rotation.Deg0);
            Assert.AreEqual(5, grid.FreeCellsCount);
        }

        [Test]
        public void AllItems_ReturnsUniqueItems()
        {
            var grid = new GridModel<Token>(4, 4);
            var a = new Token { Shape = Shape.Parse("XX|XX") };
            var b = new Token { Shape = Shape.Parse("X") };
            grid.TryPlace(a, 0, 0, Rotation.Deg0);
            grid.TryPlace(b, 3, 3, Rotation.Deg0);
            var items = new System.Collections.Generic.List<Token>(grid.AllItems());
            Assert.AreEqual(2, items.Count);
            Assert.Contains(a, items);
            Assert.Contains(b, items);
        }
    }
}
