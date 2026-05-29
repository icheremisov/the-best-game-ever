using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;
using System.Collections.Generic;

namespace Mimic.Tests
{
    public class GlueGroupTests
    {
        private class Item { public string Id; public Shape Shape; public bool IsGlue; }
        private static Item Mk(string id, bool glue = false) =>
            new Item { Id = id, Shape = Shape.Parse("X"), IsGlue = glue };

        [Test]
        public void Group_GlueWithTwoNeighbors_ReturnsAllThree()
        {
            var grid = new GridModel<Item>(4, 4);
            var glue = Mk("glue", true);
            var a = Mk("a");
            var b = Mk("b");
            grid.TryPlace(glue, 1, 1, Rotation.Deg0);
            grid.TryPlace(a, 0, 1, Rotation.Deg0); // слева от клея
            grid.TryPlace(b, 2, 1, Rotation.Deg0); // справа
            var group = GlueGroup.Resolve(grid, a, i => i.IsGlue);
            Assert.Contains(glue, group);
            Assert.Contains(a, group);
            Assert.Contains(b, group);
            Assert.AreEqual(3, group.Count);
        }

        [Test]
        public void Group_ItemNotTouchingGlue_ReturnsItselfOnly()
        {
            var grid = new GridModel<Item>(4, 4);
            var a = Mk("a");
            var glue = Mk("glue", true);
            grid.TryPlace(a, 0, 0, Rotation.Deg0);
            grid.TryPlace(glue, 3, 3, Rotation.Deg0);
            var group = GlueGroup.Resolve(grid, a, i => i.IsGlue);
            Assert.AreEqual(1, group.Count);
            Assert.Contains(a, group);
        }
    }
}
