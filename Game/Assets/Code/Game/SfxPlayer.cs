using System.Collections.Generic;
using Mimic.Data;
using UnityEngine;

namespace Mimic.Game
{
    public static class SfxPlayer
    {
        public const string PickupId = "sfx_mouse_click";
        public const string NegativeId = "sfx_UI_negative";

        private static readonly string[] DigestCycleIds =
        {
            "sfx_mimik_eating",
            "sfx_mimik_eating2",
            "sfx_mimik_eating3",
        };

        private const string ClipRoot = "Audio/sound";
        private const string UiClipRoot = "Audio/sound/UI";

        private static readonly Dictionary<string, List<AudioClip>> groups = new Dictionary<string, List<AudioClip>>();
        private static readonly Dictionary<string, AudioClip> exactClips = new Dictionary<string, AudioClip>();
        private static bool loaded;
        private static AudioSource source;
        private static int digestCycleIndex;

        private struct ItemSfx
        {
            public string DropGroupId;
            public string DropExactClipId;

            public ItemSfx(string dropGroupId, string dropExactClipId = null)
            {
                DropGroupId = dropGroupId;
                DropExactClipId = dropExactClipId;
            }
        }

        private static readonly Dictionary<string, ItemSfx> itemMap = new Dictionary<string, ItemSfx>
        {
            { "hat", new ItemSfx("sfx_cap_drop") },
            { "bow", new ItemSfx("sfx_bow_drop") },
            { "crowbar", new ItemSfx("sfx_crowbar_drop") },
            { "quiver", new ItemSfx("sfx_quiver_drop") },
            { "pickaxe", new ItemSfx("sfx_pickaxe_drop") },

            { "magic_staff", Wood() },
            { "walkstick", Wood() },
            { "boot", Wood() },
            { "scroll", Wood() },
            { "horn", Wood() },
            { "rich_wand", Wood() },
            { "quill", Wood() },
            { "burger", Wood() },
            { "poop", Wood() },

            { "anchor", Metal() },
            { "expensive_amulet", Metal() },
            { "knife", Metal() },
            { "trap", Metal() },
            { "diamond", Metal() },
            { "rich_diadem", Metal() },
            { "flying_boots", Metal() },
            { "spyglass", Metal() },
        };

        private static ItemSfx Wood()
            => new ItemSfx(null, "sfx_wood_item_handle 3");

        private static ItemSfx Metal()
            => new ItemSfx("sfx_metal_item_handle");

        public static IReadOnlyCollection<string> MappedItemIds => itemMap.Keys;

        public static string ResolveItemHandleId(LootData item)
            => TryGetItemSfx(item, out _) ? PickupId : null;

        public static string ResolveItemDropId(LootData item)
            => TryGetItemSfx(item, out var sfx) ? (sfx.DropExactClipId ?? sfx.DropGroupId) : null;

        public static string ResolveDigestCycleId(int cycleIndex)
        {
            int index = cycleIndex % DigestCycleIds.Length;
            if (index < 0) index += DigestCycleIds.Length;
            return DigestCycleIds[index];
        }

        public static string NormalizeGroupId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            id = id.TrimEnd();
            int end = id.Length - 1;
            while (end >= 0 && char.IsDigit(id[end])) end--;
            return id.Substring(0, end + 1).TrimEnd();
        }

        public static void Play(string id)
        {
            var groupId = NormalizeGroupId(id);
            if (string.IsNullOrEmpty(groupId)) return;

            EnsureLoaded();
            if (!groups.TryGetValue(groupId, out var clips) || clips.Count == 0) return;

            PlayClip(clips[Random.Range(0, clips.Count)]);
        }

        public static void PlayItemHandle(LootData item)
        {
            if (item != null && !item.IsFixture && !string.IsNullOrEmpty(item.Id)) Play(PickupId);
        }

        public static void PlayItemDrop(LootData item)
        {
            if (!TryGetItemSfx(item, out var sfx)) return;

            if (!string.IsNullOrEmpty(sfx.DropExactClipId)) PlayExact(sfx.DropExactClipId);
            else if (!string.IsNullOrEmpty(sfx.DropGroupId)) Play(sfx.DropGroupId);
        }

        public static void PlayNegative() => Play(NegativeId);

        public static void PlayDigest()
        {
            PlayExact(ResolveDigestCycleId(digestCycleIndex));
            digestCycleIndex = (digestCycleIndex + 1) % DigestCycleIds.Length;
        }

        private static bool TryGetItemSfx(LootData item, out ItemSfx sfx)
        {
            if (item != null && !item.IsFixture && !string.IsNullOrEmpty(item.Id))
                return itemMap.TryGetValue(item.Id, out sfx);

            sfx = default;
            return false;
        }

        private static void PlayExact(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            EnsureLoaded();
            if (exactClips.TryGetValue(id.TrimEnd(), out var clip)) PlayClip(clip);
        }

        private static void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            EnsureSource();
            source.PlayOneShot(clip);
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            LoadClips(ClipRoot);
            LoadClips(UiClipRoot);
        }

        private static void LoadClips(string resourcePath)
        {
            var clips = Resources.LoadAll<AudioClip>(resourcePath);
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.name)) continue;

                var exactId = clip.name.TrimEnd();
                if (exactClips.ContainsKey(exactId)) continue;
                exactClips[exactId] = clip;

                var groupId = NormalizeGroupId(exactId);
                if (string.IsNullOrEmpty(groupId)) continue;
                if (!groups.TryGetValue(groupId, out var list))
                {
                    list = new List<AudioClip>();
                    groups[groupId] = list;
                }
                list.Add(clip);
            }
        }

        private static void EnsureSource()
        {
            if (source != null) return;

            var go = new GameObject("SfxPlayer");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }
    }
}
