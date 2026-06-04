using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Ссылки на части префаба боевой панели (Resources/UI/CombatPanel).
    // CombatController инстанцирует префаб и берёт элементы отсюда — визуал
    // (фон, шрифты, позиции, цвета, HP-бар, кнопка «Кусь») настраивается прямо
    // в префабе. Позицию/размер корня контроллер подгоняет под правую сетку.
    public class CombatPanelView : MonoBehaviour
    {
        public Image Background;      // фон панели; по нему же идёт «флеш» атаки врага
        public Image Preview;         // портрет героя/властелина вверху панели
        public Text NameText;         // имя врага
        public Text EnemyAtkText;     // «⚔ N» — атака врага
        public Image EnemyHpFill;     // заливка HP-бара (Image.Type.Filled)
        public Text EnemyHpLabel;     // «hp/max»
        public Button BiteButton;     // кнопка «Кусь!»
        public Text BiteLabel;        // подпись на кнопке «Кусь»
    }
}
