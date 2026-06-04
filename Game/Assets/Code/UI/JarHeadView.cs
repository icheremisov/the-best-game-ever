using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Голова текущего героя в банке (Sack/Jar/Head).
    // Спрайт берётся по id приключенца: сначала вариант "{id}_jar", иначе обычный портрет "{id}".
    public class JarHeadView : MonoBehaviour
    {
        public static JarHeadView Instance;

        private Image img;

        private void Awake()
        {
            Instance = this;
            img = GetComponent<Image>();
            if (img != null)
            {
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            Clear();
        }

        public void Show(string id)
        {
            if (img == null) return;
            var sprite = PortraitLoader.LoadSprite(id + "_jar") ?? PortraitLoader.LoadSprite(id);
            img.sprite = sprite;
            img.enabled = sprite != null;
        }

        public void Clear()
        {
            if (img == null) return;
            img.sprite = null;
            img.enabled = false;
        }
    }
}
