using NUnit.Framework;
using Mimic.Data;

namespace Mimic.Tests
{
    public class AdjacencyEffectTests
    {
        [Test]
        public void ParseRules_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, AdjacencyRule.ParseRules("").Length);
            Assert.AreEqual(0, AdjacencyRule.ParseRules(null).Length);
        }

        [Test]
        public void Parse_SingleGoldPlus50()
        {
            var fx = AdjacencyEffect.Parse("gold:+50%");
            Assert.AreEqual(EffectType.Gold, fx.Type);
            Assert.AreEqual(0.5f, fx.Multiplier, 0.0001f);
        }

        [Test]
        public void Parse_AcidMinus30()
        {
            var fx = AdjacencyEffect.Parse("acid:-30%");
            Assert.AreEqual(EffectType.Acid, fx.Type);
            Assert.AreEqual(-0.3f, fx.Multiplier, 0.0001f);
        }

        [Test]
        public void Parse_OptionalSign()
        {
            Assert.AreEqual(0.05f, AdjacencyEffect.Parse("gold:5%").Multiplier, 0.0001f);
        }

        [Test]
        public void Parse_StackableSuffix()
        {
            Assert.IsTrue(AdjacencyEffect.Parse("gold:+50%*").Stackable);
            Assert.IsFalse(AdjacencyEffect.Parse("gold:+50%").Stackable);
        }

        [Test]
        public void ParseRules_BlockWithTwoEffects()
        {
            var rules = AdjacencyRule.ParseRules("sword|gold:+50%,acid:-30%");
            Assert.AreEqual(1, rules.Length);
            Assert.AreEqual("sword", rules[0].Targets[0]);
            Assert.AreEqual(2, rules[0].Effects.Length);
            Assert.AreEqual(EffectType.Gold, rules[0].Effects[0].Type);
            Assert.AreEqual(EffectType.Acid, rules[0].Effects[1].Type);
        }

        [Test]
        public void Parse_BadFormat_Throws()
        {
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.Parse("gold+50"));
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.Parse("foo:+50%"));
            Assert.Throws<System.FormatException>(() => AdjacencyRule.ParseRules("sword"));
        }
    }
}
