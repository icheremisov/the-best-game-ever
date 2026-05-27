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

        private void Awake()
        {
            if (EatButton != null)
            {
                PopupHelpers.EnsureButtonLabel(EatButton, "Сожрать", 28);
                EatButton.onClick.AddListener(() => { onEat?.Invoke(); gameObject.SetActive(false); });
            }
            gameObject.SetActive(false);
        }

        public void Show(AdventurerData data, Action onEatCallback)
        {
            if (NameText != null) NameText.text = data.Name;
            if (PhraseText != null) PhraseText.text = data.Phrase;
            onEat = onEatCallback;
            gameObject.SetActive(true);
        }
    }
}
