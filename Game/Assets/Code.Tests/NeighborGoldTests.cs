using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class NeighborGoldTests
    {
        private class Item
        {
            public string Id; public Shape Shape; public int Gold; public int AcidCost;
            public string AdjacencyTarget; public AdjacencyEffect[] AdjacencyEffects;
            public int NeighborGoldPct;
        }
        private static Item Mk(string id, int gold, int npct = 0) => new Item {
            Id = id, Shape = Shape.Parse("X"), Gold = gold, AcidCost = 1,
            AdjacencyTarget = null, AdjacencyEffects = AdjacencyEffect.ParseList(null),
            NeighborGoldPct = npct };

        [Test]
        public void Poop_Reduces_NeighborGold_By50pct()
        {
            var grid = new GridModel<Item>(4, 4);
            var gem = Mk("gem", 20);
            var poop = Mk("poop", 0, npct: -50);
            grid.TryPlace(gem, 0, 0, Rotation.Deg0);
            grid.TryPlace(poop, 1, 0, Rotation.Deg0); // сосед gem по x
            var res = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                i => i.AdjacencyTarget, i => i.AdjacencyEffects, i => i.NeighborGoldPct);
            Assert.AreEqual(10, res.GetGold(gem), "20 * (1 - 0.5)");
        }

        [Test]
        public void NoPoop_KeepsGold()
        {
            var grid = new GridModel<Item>(4, 4);
            var gem = Mk("gem", 20);
            grid.TryPlace(gem, 0, 0, Rotation.Deg0);
            var res = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                i => i.AdjacencyTarget, i => i.AdjacencyEffects, i => i.NeighborGoldPct);
            Assert.AreEqual(20, res.GetGold(gem));
        }
    }
}
