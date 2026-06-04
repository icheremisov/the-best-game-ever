using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Mimic.UI
{
    public class EndOfDayPopup : MonoBehaviour
    {
        public Text TitleText;
        public Text SubtitleText;
        public Button PrimaryButton;
        public Text PrimaryLabel;
        public Button SecondaryButton;
        public Text SecondaryLabel;
        public Button TertiaryButton;
        public Text TertiaryLabel;

        [Header("Outcome art (holder в префабе — позицию/размер правим в редакторе)")]
        public Image OutcomeArt; // картинка исхода: победа/смерть/лопнул. Позиция — в префабе.

        private Action primary, secondary, tertiary;
        private bool showing; // true пока попап вызван через Open — не даём Awake погасить себя
        private bool styled;

        // Заполняет holder OutcomeArt спрайтом по пути в Resources; null/нет ресурса — прячет.
        // Позицию/размер не трогаем — они заданы в префабе.
        private void SetArt(string resPath)
        {
            if (OutcomeArt == null) return;
            var spr = string.IsNullOrEmpty(resPath) ? null : Resources.Load<Sprite>(resPath);
            if (spr == null)
            {
                OutcomeArt.gameObject.SetActive(false);
                return;
            }
            OutcomeArt.sprite = spr;
            OutcomeArt.preserveAspect = true;
            OutcomeArt.gameObject.SetActive(true);
        }

        private void Awake()
        {
            if (!showing) gameObject.SetActive(false);
        }

        public void Hide() => gameObject.SetActive(false);

        // Стиль зашит в код: edit-time правки на prefab-инстансе откатывались, а часть
        // лейблов оставалась с дефолтами (fs=14, LegacyRuntime, белый фон → невидимо).
        // Делаем детерминированно при первом показе.
        private static readonly Color BtnGreen = new Color(0.25f, 0.56f, 0.31f);
        private static readonly Color BtnGray  = new Color(0.33f, 0.33f, 0.36f);
        private static readonly Color BtnRed   = new Color(0.54f, 0.23f, 0.23f);
        private static readonly Color BodyGray = new Color(0.85f, 0.85f, 0.88f);

        private void ApplyStyle()
        {
            if (styled) return;
            styled = true;

            StyleText(TitleText, 40, true, Color.white);
            StyleText(SubtitleText, 26, false, BodyGray);

            StyleButton(PrimaryButton,   PrimaryLabel,   BtnGreen, -20f);
            StyleButton(SecondaryButton, SecondaryLabel, BtnGray, -105f);
            StyleButton(TertiaryButton,  TertiaryLabel,  BtnRed,  -190f);
        }

        private static void StyleText(Text t, int fontSize, bool bold, Color color)
        {
            if (t == null) return;
            t.font = FontProvider.Default;
            t.fontSize = fontSize;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void StyleButton(Button b, Text label, Color bg, float y)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = bg;
            var brt = (RectTransform)b.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, y);
            brt.sizeDelta = new Vector2(480f, 72f);
            if (label != null)
            {
                StyleText(label, 30, true, Color.white);
                var lrt = (RectTransform)label.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
            }
        }

        private void Bind(Button b, Text label, string text, Action cb, ref Action slot, bool visible)
        {
            slot = cb;
            if (b == null) return;
            b.gameObject.SetActive(visible);
            if (!visible) return;
            if (label != null) label.text = text;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => cb?.Invoke());
        }

        private void Open(string title, string subtitle)
        {
            showing = true;
            ApplyStyle();
            SetArt(null); // по умолчанию без арта; конкретный исход включит свой
            if (TitleText != null) TitleText.text = title;
            if (SubtitleText != null) SubtitleText.text = subtitle;
            gameObject.SetActive(true);
        }

        public void ShowRansomWin()
        {
            Open("Свободный мимик",
                 "Поздравляю! Ты стал свободным мимиком. Ты нашёл... счастье?");
            SetArt("Art/UI/mimic_free"); // сундук-победюк — арт свободного мимика
            Bind(PrimaryButton, PrimaryLabel, "В меню", ToMenu, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "", null, ref secondary, false);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        public void ShowChallengeStub()
        {
            Open("Вызов брошен", "[Боссфайт ещё не реализован — стаб]");
            Bind(PrimaryButton, PrimaryLabel, "В меню", ToMenu, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "", null, ref secondary, false);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        public void ShowDeath(Action onRetryDay)
        {
            Open("Здоровье на нуле",
                 "Большинство мимиков всю жизнь проводят незамеченными. О них не сложат легенд. А о тебе... может быть?");
            SetArt("Art/UI/mimic_dead_hard"); // разнесли в бою / от гадости
            Bind(PrimaryButton, PrimaryLabel, "Переиграть день", onRetryDay, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "В меню", ToMenu, ref secondary, true);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        public void ShowBurst(Action onRetryDay)
        {
            Open("Ты лопнул от переедания",
                 "46% мимиков не доживают до старости из-за переедания. Вот кто ты есть: мимик обыкновенный.");
            SetArt("Art/UI/mimic_dead"); // лопнул от переедания / не поместилось
            Bind(PrimaryButton, PrimaryLabel, "Переиграть день", onRetryDay, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "В меню", ToMenu, ref secondary, true);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        private void ToMenu() => SceneManager.LoadScene("MainMenu");
    }
}
