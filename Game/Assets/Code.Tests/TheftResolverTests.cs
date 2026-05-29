using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class TheftResolverTests
    {
        private class Item { public string Id; public Shape Shape; }
        private static Item Mk(string id, string shape) => new Item { Id = id, Shape = Shape.Parse(shape) };

        [Test]
        public void TopItem_HasFreeTopEdge()
        {
            var grid = new GridModel<Item>(4, 4);
            var a = Mk("a", "X");
            grid.TryPlace(a, 0, 3, Rotation.Deg0); // y=3 = верхняя граница
            Assert.IsTrue(TheftResolver.HasFreeTopEdge(grid, a, i => i));
        }

        [Test]
        public void CoveredItem_HasNoFreeTopEdge()
        {
            var grid = new GridModel<Item>(4, 4);
            var bottom = Mk("bottom", "X");
            var top = Mk("top", "X");
            grid.TryPlace(bottom, 0, 0, Rotation.Deg0);
            grid.TryPlace(top, 0, 1, Rotation.Deg0); // прямо над bottom
            Assert.IsFalse(TheftResolver.HasFreeTopEdge(grid, bottom, i => i),
                "над bottom стоит top -> грань закрыта");
            Assert.IsTrue(TheftResolver.HasFreeTopEdge(grid, top, i => i));
        }

        [Test]
        public void PickStealable_PicksUncoveredItem()
        {
            var grid = new GridModel<Item>(2, 2);
            var b = Mk("b", "X");
            var t = Mk("t", "X");
            grid.TryPlace(b, 0, 0, Rotation.Deg0);
            grid.TryPlace(t, 0, 1, Rotation.Deg0);
            // b закрыт t; t свободен -> вернёт t
            var picked = TheftResolver.PickStealable(grid, i => i, seed: 0);
            Assert.AreSame(t, picked);
        }

        [Test]
        public void PickStealable_ReturnsNull_WhenGridEmpty()
        {
            var grid = new GridModel<Item>(2, 2);
            Assert.IsNull(TheftResolver.PickStealable(grid, i => i, seed: 0));
        }

        [Test]
        public void PickStealable_Deterministic_WithSeed()
        {
            var grid = new GridModel<Item>(4, 1);
            var x = Mk("x", "X");
            var y = Mk("y", "X");
            grid.TryPlace(x, 0, 0, Rotation.Deg0);
            grid.TryPlace(y, 2, 0, Rotation.Deg0);
            var p1 = TheftResolver.PickStealable(grid, i => i, seed: 42);
            var p2 = TheftResolver.PickStealable(grid, i => i, seed: 42);
            Assert.AreSame(p1, p2);
            Assert.IsNotNull(p1);
        }
    }
}
