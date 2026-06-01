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

        public static GameObject LoadPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Portraits/" + id)
                ?? Resources.Load<GameObject>("Art/Adventurers/" + id);
            cache[id] = p;
            return p;
        }

        // Инстанцирует портрет в контейнер на весь его размер, гасит raycast.
        // Возвращает инстанс или null (арта нет — вызывающий показывает заглушку).
        public static GameObject Instantiate(string id, RectTransform container)
        {
            var prefab = LoadPrefab(id);
            if (prefab == null || container == null) return null;
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
    }
}
