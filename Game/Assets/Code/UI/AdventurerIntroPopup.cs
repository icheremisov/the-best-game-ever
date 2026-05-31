using System;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Data;

namespace Mimic.UI
{
    public class AdventurerIntroPopup : MonoBehaviour
    {
        public Text NameText;
        public Text PhraseText;
        public Button EatButton;

        private Action onEat;
        private bool showing; // true пока попап вызван через Show — не даём Awake погасить себя

        private void Awake()
        {
            if (EatButton != null)
            {
                PopupHelpers.EnsureButtonLabel(EatButton, "Сожрать", 28);
                EatButton.onClick.AddListener(() => { onEat?.Invoke(); gameObject.SetActive(false); });
            }
            // Если объект сохранён в сцене неактивным, Awake выполнится при первом
            // Show()→SetActive(true); не гасим себя в этом случае.
            if (!showing) gameObject.SetActive(false);
        }

        public void Show(AdventurerData data, Action onEatCallback, string eatLabel = "Сожрать")
        {
            showing = true;
            if (NameText != null) NameText.text = data.Name;
            if (PhraseText != null) PhraseText.text = data.Phrase;
            onEat = onEatCallback;
            if (EatButton != null)
                PopupHelpers.EnsureButtonLabel(EatButton, eatLabel, 28);
            gameObject.SetActive(true);
        }
    }
}
