using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Голова героя в банке (Sack/Jar/Head). Спрайт ищется по ИМЕНИ героя.
    // Обычно — «голова в банке» (вариант "{key}_jar"); в бою — полный портрет героя
    // на всю область банки (саму банку прячем).
    public class JarHeadView : MonoBehaviour
    {
        public static JarHeadView Instance;

        private Image img;
        private Image jarGlass;                  // Image банки (родитель) — прячем в бою
        private RectTransform rt;
        private Vector2 headOffMin, headOffMax;   // исходные отступы головы внутри банки

        private void Awake()
        {
            Instance = this;
            img = GetComponent<Image>();
            rt = (RectTransform)transform;
            headOffMin = rt.offsetMin;
            headOffMax = rt.offsetMax;
            if (transform.parent != null) jarGlass = transform.parent.GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            Clear();
        }

        // Голова переваренного героя в банке (вариант "{key}_jar", иначе обычный портрет).
        public void Show(string heroName)
        {
            if (img == null) return;
            string key = PortraitLoader.Resolve(heroName);
            var sprite = PortraitLoader.LoadSprite(key + "_jar") ?? PortraitLoader.LoadSprite(key);
            if (jarGlass != null) jarGlass.enabled = true;
            SetInsets(headOffMin, headOffMax);
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        // Бой: вместо банки — полный портрет героя на всю область.
        public void ShowCombatPortrait(string heroName)
        {
            if (img == null) return;
            var sprite = PortraitLoader.LoadSprite(heroName); // интерфейсный вариант (без "_jar")
            if (jarGlass != null) jarGlass.enabled = false;   // банку прячем
            SetInsets(Vector2.zero, Vector2.zero);            // портрет на всю область банки
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        // Конец боя: вернуть банку, голову очистить.
        public void Restore()
        {
            if (jarGlass != null) jarGlass.enabled = true;
            SetInsets(headOffMin, headOffMax);
            Clear();
        }

        public void Clear()
        {
            if (img == null) return;
            img.sprite = null;
            img.enabled = false;
        }

        private void SetInsets(Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.offsetMin = min;
            rt.offsetMax = max;
        }
    }
}
