using NUnit.Framework;
using Mimic.Data;

namespace Mimic.Tests
{
    public class AdjacencyEffectTests
    {
        [Test]
        public void ParseList_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, AdjacencyEffect.ParseList("").Length);
            Assert.AreEqual(0, AdjacencyEffect.ParseList(null).Length);
        }

        [Test]
        public void ParseList_SingleGoldPlus50()
        {
            var fx = AdjacencyEffect.ParseList("gold:+50%");
            Assert.AreEqual(1, fx.Length);
            Assert.AreEqual(EffectType.Gold, fx[0].Type);
            Assert.AreEqual(0.5f, fx[0].Multiplier, 0.0001f);
        }

        [Test]
        public void ParseList_AcidMinus30()
        {
            var fx = AdjacencyEffect.ParseList("acid:-30%");
            Assert.AreEqual(1, fx.Length);
            Assert.AreEqual(EffectType.Acid, fx[0].Type);
            Assert.AreEqual(-0.3f, fx[0].Multiplier, 0.0001f);
        }

        [Test]
        public void ParseList_Multiple_SemicolonSeparated()
        {
            var fx = AdjacencyEffect.ParseList("gold:+50%;acid:-30%");
            Assert.AreEqual(2, fx.Length);
            Assert.AreEqual(EffectType.Gold, fx[0].Type);
            Assert.AreEqual(EffectType.Acid, fx[1].Type);
        }

        [Test]
        public void Parse_BadFormat_Throws()
        {
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.ParseList("gold+50"));
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.ParseList("foo:+50%"));
        }
    }
}
