using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Ссылки на части префаба окна подтверждения переваривания (Resources/UI/DigestConfirmPopup).
    // Контроллер DigestConfirmPopup инстанцирует префаб и берёт элементы отсюда —
    // визуал (панель, шрифты, позиции, цвета кнопок) настраивается прямо в префабе.
    public class DigestConfirmPopupView : MonoBehaviour
    {
        public Image ArtImage;        // спрайт предмета
        public Text ArtFallback;      // имя предмета, если спрайта нет
        public Text GoldText;         // «Сохранится золота…»
        public Text HealText;         // лечение / урон при переваривании
        public Text AcidText;         // «Желудочный сок: -N»
        public Button ConfirmButton;  // «ПЕРЕВАРИТЬ»
        public Text ConfirmLabel;     // подпись на кнопке подтверждения
        public Button CancelButton;   // «ОТМЕНА»
    }
}
