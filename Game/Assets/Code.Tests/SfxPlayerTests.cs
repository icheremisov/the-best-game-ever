using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Data;
using Mimic.Game;

namespace Mimic.Tests
{
    public class SfxPlayerTests
    {
        private static LootData Item(string id) => new LootData { Id = id };

        [Test]
        public void NormalizeGroupId_GroupsNumberedVariants()
        {
            Assert.AreEqual("sfx_bow_drop", SfxPlayer.NormalizeGroupId("sfx_bow_drop1"));
            Assert.AreEqual("sfx_bow_drop", SfxPlayer.NormalizeGroupId("sfx_bow_drop2"));
            Assert.AreEqual("sfx_bow_drop", SfxPlayer.NormalizeGroupId("sfx_bow_drop3"));
            Assert.AreEqual("sfx_crowbar_drop", SfxPlayer.NormalizeGroupId("sfx_crowbar_drop"));
            Assert.AreEqual("sfx_wood_item_handle", SfxPlayer.NormalizeGroupId("sfx_wood_item_handle 3"));
            Assert.AreEqual("sfx_mouse_click", SfxPlayer.NormalizeGroupId("sfx_mouse_click1"));
            Assert.AreEqual("sfx_mouse_click", SfxPlayer.NormalizeGroupId("sfx_mouse_click2"));
            Assert.AreEqual("sfx_mimik_eating", SfxPlayer.NormalizeGroupId("sfx_mimik_eating2"));
            Assert.AreEqual("sfx_mimik_eating", SfxPlayer.NormalizeGroupId("sfx_mimik_eating3"));
        }

        [Test]
        public void Hat_UsesCapSounds()
        {
            var item = Item("hat");

            Assert.AreEqual(SfxPlayer.PickupId, SfxPlayer.ResolveItemHandleId(item));
            Assert.AreEqual("sfx_cap_drop", SfxPlayer.ResolveItemDropId(item));
        }

        [Test]
        public void ItemsWithSpecificDrop_UseMappedHandleAndSpecificDrop()
        {
            AssertItem("bow", "sfx_bow_drop");
            AssertItem("crowbar", "sfx_crowbar_drop");
            AssertItem("quiver", "sfx_quiver_drop");
            AssertItem("pickaxe", "sfx_pickaxe_drop");
        }

        [Test]
        public void WoodenMappedItems_UseWoodAssignments()
        {
            var wooden = new[]
            {
                "magic_staff", "walkstick", "boot", "scroll", "horn",
                "rich_wand", "quill", "burger", "poop"
            };

            foreach (var id in wooden)
                AssertItem(id, "sfx_wood_item_handle 3");
        }

        [Test]
        public void SteelMappedItems_UseMetalAssignments()
        {
            var steel = new[]
            {
                "anchor", "expensive_amulet", "knife", "trap", "diamond",
                "rich_diadem", "flying_boots", "spyglass"
            };

            foreach (var id in steel)
                AssertItem(id, "sfx_metal_item_handle");
        }

        [Test]
        public void EveryInteractiveCsvItem_HasExplicitSoundMapping()
        {
            var expected = new HashSet<string>
            {
                "bow", "crowbar", "quiver", "hat", "magic_staff", "anchor",
                "expensive_amulet", "pickaxe", "walkstick", "boot", "knife",
                "scroll", "horn", "rich_wand", "trap", "diamond", "quill",
                "rich_diadem", "flying_boots", "spyglass", "burger", "poop"
            };

            var actual = new HashSet<string>(SfxPlayer.MappedItemIds);

            CollectionAssert.AreEquivalent(expected, actual);
            foreach (var id in expected)
            {
                Assert.AreEqual(SfxPlayer.PickupId, SfxPlayer.ResolveItemHandleId(Item(id)), id);
                Assert.IsNotNull(SfxPlayer.ResolveItemDropId(Item(id)), id);
            }
        }

        [Test]
        public void FixturesAndUnknownItems_DoNotResolve()
        {
            Assert.IsNull(SfxPlayer.ResolveItemHandleId(new LootData { Id = "heart", IsFixture = true }));
            Assert.IsNull(SfxPlayer.ResolveItemDropId(new LootData { Id = "stomach", IsFixture = true }));
            Assert.IsNull(SfxPlayer.ResolveItemHandleId(Item("unknown")));
            Assert.IsNull(SfxPlayer.ResolveItemDropId(Item("unknown")));
        }

        [Test]
        public void MissingClipGroup_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SfxPlayer.Play("sfx_missing_group"));
        }

        [Test]
        public void UnknownDraggableItemHandle_DoesNotRequireDropMapping()
        {
            Assert.DoesNotThrow(() => SfxPlayer.PlayItemHandle(Item("unknown")));
        }

        [Test]
        public void DigestCycle_UsesEatingClipsInOrder()
        {
            Assert.AreEqual("sfx_mimik_eating", SfxPlayer.ResolveDigestCycleId(0));
            Assert.AreEqual("sfx_mimik_eating2", SfxPlayer.ResolveDigestCycleId(1));
            Assert.AreEqual("sfx_mimik_eating3", SfxPlayer.ResolveDigestCycleId(2));
            Assert.AreEqual("sfx_mimik_eating", SfxPlayer.ResolveDigestCycleId(3));
        }

        private static void AssertItem(string id, string dropId)
        {
            var item = Item(id);
            Assert.AreEqual(SfxPlayer.PickupId, SfxPlayer.ResolveItemHandleId(item), id);
            Assert.AreEqual(dropId, SfxPlayer.ResolveItemDropId(item), id);
        }
    }
}
