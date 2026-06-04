using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Общий загрузчик префабов-портретов персонажей (приключенцы, master, mimic).
    // Сначала ищет Art/Portraits/{id}, затем Art/Adventurers/{id} (реюз существующих артов).
    public static class PortraitLoader
    {
        private static readonly Dictionary<string, GameObject> cache = new();
        private static readonly Dictionary<string, Sprite> spriteCache = new();

        public static GameObject LoadPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Portraits/" + id)
                ?? Resources.Load<GameObject>("Art/Adventurers/" + id);
            cache[id] = p;
            return p;
        }

        // Спрайт-портрет по id (художник может класть просто png вместо префаба).
        public static Sprite LoadSprite(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (spriteCache.TryGetValue(id, out var s)) return s;
            s = Resources.Load<Sprite>("Art/Portraits/" + id)
                ?? Resources.Load<Sprite>("Art/Adventurers/" + id);
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
