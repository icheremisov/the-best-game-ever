using NUnit.Framework;
using Mimic.Data;

namespace Mimic.Tests
{
    public class LootDataDefaultsTests
    {
        [Test]
        public void NewFields_HaveSaneDefaults()
        {
            var d = new LootData();
            Assert.AreEqual(LootCategory.Normal, d.Category);
            Assert.AreEqual(0, d.AcidRestoreOnDigest);
            Assert.AreEqual(0, d.DamageOnDigest);
            Assert.IsTrue(d.CanReturnToBasket, "по умолчанию можно вернуть");
            Assert.IsFalse(d.IsGlue);
            Assert.IsFalse(d.IsFixture);
            Assert.AreEqual(0, d.NeighborGoldPct);
        }
    }
}
