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

        // Арт-префаб приключенца (Resources/Art/Adventurers/{id}.prefab). Кэшируем; null если нет.
        // Визуал (спрайт/масштаб/сдвиг) настраивается художником прямо в префабе — как у лута.
        private static readonly Dictionary<string, GameObject> portraitCache = new Dictionary<string, GameObject>();

        private static GameObject LoadPortraitPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (portraitCache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Adventurers/" + id);
            portraitCache[id] = p;
            return p;
        }

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
            ShowPortrait(data.Id);
            gameObject.SetActive(true);
        }

        private void ShowPortrait(string id)
        {
            if (portraitInstance != null) Destroy(portraitInstance);

            var prefab = LoadPortraitPrefab(id);
            bool hasArt = prefab != null && PortraitContainer != null;

            if (hasArt)
            {
                portraitInstance = Instantiate(prefab, PortraitContainer, false);
                var rt = portraitInstance.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                // Портрет не перехватывает клики.
                foreach (var g in portraitInstance.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            }

            if (PortraitFallback != null) PortraitFallback.SetActive(!hasArt);
        }
    }
}
