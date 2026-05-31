using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class AdjacencyActiveRulesTests
    {
        private static HashSet<string> Ids(params string[] ids) => new HashSet<string>(ids);

        [Test]
        public void NamedRule_ActiveWhenTargetPresent()
        {
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%");
            Assert.IsTrue(AdjacencyResolver.ActiveRules(rules, Ids("bread"))[0]);
        }

        [Test]
        public void NamedRule_InactiveWhenTargetAbsent()
        {
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%");
            Assert.IsFalse(AdjacencyResolver.ActiveRules(rules, Ids("sword"))[0]);
        }

        [Test]
        public void Wildcard_ActiveForUnnamedNeighbor()
        {
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%;*|gold:-25%*");
            var a = AdjacencyResolver.ActiveRules(rules, Ids("barracuda"));
            Assert.IsFalse(a[0], "bread-правило неактивно: bread рядом нет");
            Assert.IsTrue(a[1], "wildcard активен: barracuda не назван");
        }

        [Test]
        public void Wildcard_InactiveWhenOnlyNamedNeighbors()
        {
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%;*|gold:-25%*");
            var a = AdjacencyResolver.ActiveRules(rules, Ids("bread"));
            Assert.IsTrue(a[0]);
            Assert.IsFalse(a[1], "прочих нет — wildcard неактивен");
        }

        [Test]
        public void BreadCase_BothRulesActive_EvenWhenPriceNetsToZero()
        {
            // средний хлеб: рядом хлеб И барракуда — оба правила активны,
            // хотя +25% и -25% гасят друг друга и цена не меняется.
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%;*|gold:-25%*");
            var a = AdjacencyResolver.ActiveRules(rules, Ids("bread", "barracuda"));
            Assert.IsTrue(a[0]);
            Assert.IsTrue(a[1]);
        }

        [Test]
        public void MultiTarget_ActiveWhenAnyTargetPresent()
        {
            var rules = AdjacencyRule.ParseRules("hat,sheath|gold:+50%");
            Assert.IsTrue(AdjacencyResolver.ActiveRules(rules, Ids("sheath"))[0]);
            Assert.IsFalse(AdjacencyResolver.ActiveRules(rules, Ids("frog"))[0]);
        }

        [Test]
        public void NoNeighbors_AllInactive()
        {
            var rules = AdjacencyRule.ParseRules("bread|gold:+25%;*|gold:-25%*");
            var a = AdjacencyResolver.ActiveRules(rules, new HashSet<string>());
            Assert.IsFalse(a[0]);
            Assert.IsFalse(a[1]);
        }
    }
}
