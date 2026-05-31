using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class AdjacencyResolverTests
    {
        // Item, дублирующий минимально нужное для теста (как Token из GridModelTests)
        private class Item
        {
            public string Id;
            public Shape Shape;
            public int Gold;
            public int AcidCost;
            public AdjacencyRule[] AdjacencyRules;
        }

        private static Item Mk(string id, string shape, int gold, int acid,
                               string adjTarget = null, string adjEffect = null)
        {
            string raw = (adjTarget != null && adjEffect != null) ? $"{adjTarget}|{adjEffect}" : null;
            return new Item
            {
                Id = id,
                Shape = Shape.Parse(shape),
                Gold = gold,
                AcidCost = acid,
                AdjacencyRules = AdjacencyRule.ParseRules(raw)
            };
        }

        [Test]
        public void Resolve_NoNeighbors_KeepsBaseValues()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(10, result.GetGold(sword));
            Assert.AreEqual(3, result.GetAcid(sword));
        }

        [Test]
        public void Resolve_NeighborMatchingTarget_AppliesEffect()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            var shield = Mk("shield", "X", 8, 4, adjTarget: "sword", adjEffect: "gold:+50%");
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            grid.TryPlace(shield, 1, 0, Rotation.Deg0); // adjacent on x-axis
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(8 + 4, result.GetGold(shield)); // 8 * 1.5 = 12
            Assert.AreEqual(10, result.GetGold(sword));     // sword has no effect
        }

        [Test]
        public void Resolve_AcidNegative_Reduces()
        {
            var grid = new GridModel<Item>(4, 4);
            var bread = Mk("bread", "X", 3, 1);
            var fish = Mk("fish", "X", 15, 5, adjTarget: "bread", adjEffect: "acid:-60%");
            grid.TryPlace(bread, 0, 0, Rotation.Deg0);
            grid.TryPlace(fish, 0, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            // 5 * 0.4 = 2; clamp to >= 1
            Assert.AreEqual(2, result.GetAcid(fish));
        }

        [Test]
        public void Resolve_AcidClampedToOne_NotZero()
        {
            var grid = new GridModel<Item>(4, 4);
            var bread = Mk("bread", "X", 3, 1);
            var fish = Mk("fish", "X", 15, 5, adjTarget: "bread", adjEffect: "acid:-99%");
            grid.TryPlace(bread, 0, 0, Rotation.Deg0);
            grid.TryPlace(fish, 0, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(1, result.GetAcid(fish));
        }

        [Test]
        public void Resolve_SelfAdjacency_RequiresAnotherCopy()
        {
            var grid = new GridModel<Item>(4, 4);
            var a = Mk("gem", "X", 20, 2, adjTarget: "gem", adjEffect: "gold:+25%");
            grid.TryPlace(a, 0, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            // Alone — no boost
            Assert.AreEqual(20, result.GetGold(a));

            var b = Mk("gem", "X", 20, 2, adjTarget: "gem", adjEffect: "gold:+25%");
            grid.TryPlace(b, 1, 0, Rotation.Deg0);
            var result2 = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                    i => i.AdjacencyRules);
            Assert.AreEqual(25, result2.GetGold(a)); // 20 * 1.25
            Assert.AreEqual(25, result2.GetGold(b));
        }

        [Test]
        public void Resolve_TotalGold_SumOfEffective()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            var shield = Mk("shield", "X", 8, 4, adjTarget: "sword", adjEffect: "gold:+50%");
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            grid.TryPlace(shield, 1, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(10 + 12, result.TotalGold);
        }

        // --- стак / различные инстансы / аддитив / мульти-таргет / вайлдкард ---

        [Test]
        public void Resolve_Stackable_CountsEachInstance()
        {
            var grid = new GridModel<Item>(3, 3);
            var diamond = Mk("diamond", "X", 100, 1, adjTarget: "hat", adjEffect: "gold:+50%*");
            grid.TryPlace(diamond, 1, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 0, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 2, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(200, result.GetGold(diamond)); // +50%*2 = +100%
        }

        [Test]
        public void Resolve_NonStackable_AppliesOnce_RegardlessOfCount()
        {
            var grid = new GridModel<Item>(3, 3);
            var diamond = Mk("diamond", "X", 100, 1, adjTarget: "hat", adjEffect: "gold:+50%");
            grid.TryPlace(diamond, 1, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 0, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 2, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(150, result.GetGold(diamond)); // +50% один раз
        }

        [Test]
        public void Resolve_Stackable_DoubleTouchOfSameInstance_CountsAsOne()
        {
            // source 'XX' и target 'XX' соприкасаются двумя рёбрами, но это ОДИН инстанс соседа.
            var grid = new GridModel<Item>(3, 3);
            var diamond = Mk("diamond", "XX", 100, 1, adjTarget: "hat", adjEffect: "gold:+50%*");
            grid.TryPlace(diamond, 0, 0, Rotation.Deg0); // занимает (0,0)-(1,0)
            grid.TryPlace(Mk("hat", "XX", 0, 1), 0, 1, Rotation.Deg0); // (0,1)-(1,1): касается обеих клеток
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(150, result.GetGold(diamond)); // count=1 => +50%, не +100%
        }

        [Test]
        public void Resolve_TwoBlocks_AdditiveSum_NotCompound()
        {
            var grid = new GridModel<Item>(3, 3);
            var sword = Mk("sword", "X", 10, 1);
            sword.AdjacencyRules = AdjacencyRule.ParseRules("hat|gold:+50%;sheath|gold:+50%");
            grid.TryPlace(sword, 1, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 0, 1, Rotation.Deg0);
            grid.TryPlace(Mk("sheath", "X", 0, 1), 2, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            Assert.AreEqual(20, result.GetGold(sword)); // +100% аддитивно (а не 10*1.5*1.5=22)
        }

        [Test]
        public void Resolve_MultiTarget_SingleBlock_AppliesOnce()
        {
            var grid = new GridModel<Item>(3, 3);
            var sword = Mk("sword", "X", 10, 1);
            sword.AdjacencyRules = AdjacencyRule.ParseRules("hat,sheath|gold:+50%");
            grid.TryPlace(sword, 1, 1, Rotation.Deg0);
            grid.TryPlace(Mk("hat", "X", 0, 1), 0, 1, Rotation.Deg0);
            grid.TryPlace(Mk("sheath", "X", 0, 1), 2, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            // нестак: блок срабатывает один раз несмотря на двух подходящих соседей
            Assert.AreEqual(15, result.GetGold(sword));
        }

        [Test]
        public void Resolve_Wildcard_HitsOnlyUnnamedNeighbors()
        {
            var grid = new GridModel<Item>(3, 3);
            var coin = Mk("coin", "X", 100, 1);
            coin.AdjacencyRules = AdjacencyRule.ParseRules("frog|gold:-50%;*|gold:+10%");
            grid.TryPlace(coin, 1, 1, Rotation.Deg0);
            grid.TryPlace(Mk("frog", "X", 0, 1), 0, 1, Rotation.Deg0);  // named => -50%
            grid.TryPlace(Mk("bread", "X", 0, 1), 2, 1, Rotation.Deg0); // прочий => +10%
            grid.TryPlace(Mk("gem", "X", 0, 1), 1, 0, Rotation.Deg0);   // прочий => +10%
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyRules);
            // -50% + (+10%*2, нестак) = -40% (вайлдкард НЕ бьёт по frog)
            Assert.AreEqual(60, result.GetGold(coin));
        }
    }
}
