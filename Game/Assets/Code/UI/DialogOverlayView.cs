using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Ссылки на части префаба диалога (Resources/UI/DialogOverlay).
    // Контроллер DialogOverlay инстанцирует префаб и берёт элементы отсюда —
    // визуал (баббл, размеры, шрифты, позиция портрета) настраивается в префабе.
    public class DialogOverlayView : MonoBehaviour
    {
        public Button Root;                    // панель-затемнение, ловит клики (листание)
        public RectTransform PortraitContainer; // куда инстанцируется портрет
        public GameObject PortraitFallback;     // заглушка, если арта нет
        public Text BodyText;                   // текст реплики
    }
}
