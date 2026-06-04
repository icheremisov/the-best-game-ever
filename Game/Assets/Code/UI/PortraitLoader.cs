using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Общий загрузчик портретов персонажей (приключенцы по ИМЕНИ, master, mimic).
    // Ищет в Art/Portraits, Art/Adventurers, Art/UI. Имя героя/диалог-иконка
    // приводятся к латинскому ключу через alias (см. Resolve).
    public static class PortraitLoader
    {
        private static readonly Dictionary<string, GameObject> cache = new();
        private static readonly Dictionary<string, Sprite> spriteCache = new();

        // Имя приключенца / иконка диалога -> ключ арт-файла.
        private static readonly Dictionary<string, string> alias = new()
        {
            { "Воин", "warrior" },
            { "Плут", "rogue" },
            { "Маг", "mage" },
            { "Властелин", "overlord" },
            { "master", "overlord" },
        };

        // Приводит имя героя / иконку к ключу арта (если есть в alias), иначе возвращает как есть.
        public static string Resolve(string key)
            => !string.IsNullOrEmpty(key) && alias.TryGetValue(key, out var v) ? v : key;

        public static GameObject LoadPrefab(string id)
        {
            id = Resolve(id);
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Portraits/" + id)
                ?? Resources.Load<GameObject>("Art/Adventurers/" + id)
                ?? Resources.Load<GameObject>("Art/UI/" + id);
            cache[id] = p;
            return p;
        }

        // Спрайт-портрет по ключу (художник может класть просто png вместо префаба).
        public static Sprite LoadSprite(string id)
        {
            id = Resolve(id);
            if (string.IsNullOrEmpty(id)) return null;
            if (spriteCache.TryGetValue(id, out var s)) return s;
            s = Resources.Load<Sprite>("Art/Portraits/" + id)
                ?? Resources.Load<Sprite>("Art/Adventurers/" + id)
                ?? Resources.Load<Sprite>("Art/UI/" + id);
            spriteCache[id] = s;
            return s;
        }

        // Инстанцирует портрет в контейнер на весь его размер, гасит raycast.
        // Сначала префаб, иначе спрайт (как Image с preserveAspect).
        // Возвращает инстанс или null (арта нет — вызывающий показывает заглушку).
        public static GameObject Instantiate(string id, RectTransform container)
        {
            if (container == null) return null;
            var prefab = LoadPrefab(id);
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, container, false);
                if (inst.transform is RectTransform rt)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                foreach (var g in inst.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                return inst;
            }

            var sprite = LoadSprite(id);
            if (sprite == null) return null;
            var go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var srt = (RectTransform)go.transform;
            srt.SetParent(container, false);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return go;
        }
    }
}
