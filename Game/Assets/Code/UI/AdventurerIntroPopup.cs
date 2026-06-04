using System;
using System.Collections.Generic;
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

        [Header("Портрет")]
        public RectTransform PortraitContainer; // куда инстанцируется арт-префаб
        public GameObject PortraitFallback;     // статичная картинка-заглушка, если префаба нет

        private Action onEat;
        private bool showing; // true пока попап вызван через Show — не даём Awake погасить себя
        private GameObject portraitInstance;    // текущий инстанс арт-префаба портрета
        private GameObject blocker;              // фуллскрин-перехватчик кликов под попапом

        private void Awake()
        {
            if (EatButton != null)
            {
                PopupHelpers.EnsureButtonLabel(EatButton, "Сожрать", 28);
                EatButton.onClick.AddListener(() => { var cb = onEat; onEat = null; HidePopup(); cb?.Invoke(); });
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
            ShowPortrait(data.Name);

            // Блокер перехватывает все клики мимо панели — иначе они проходят сквозь
            // попап на кнопку «Следующий» в HUD под ним.
            EnsureBlocker();
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
            transform.SetAsLastSibling(); // панель — поверх блокера
            gameObject.SetActive(true);
        }

        private void HidePopup()
        {
            if (blocker != null) blocker.SetActive(false);
            gameObject.SetActive(false);
        }

        private void EnsureBlocker()
        {
            if (blocker != null) return;
            var parent = transform.parent; // Canvas
            blocker = new GameObject("IntroPopupBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)blocker.transform;
            rt.SetParent(parent, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = blocker.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = true;
            blocker.SetActive(false);
        }

        private void ShowPortrait(string id)
        {
            if (portraitInstance != null) Destroy(portraitInstance);
            // Префаб или спрайт-интерфейс (Art/Adventurers/{id}); заглушка если арта нет.
            portraitInstance = PortraitLoader.Instantiate(id, PortraitContainer);
            if (PortraitFallback != null) PortraitFallback.SetActive(portraitInstance == null);
        }
    }
}
