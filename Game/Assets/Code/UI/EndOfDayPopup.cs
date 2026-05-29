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

        private Action primary, secondary, tertiary;

        private void Awake() => gameObject.SetActive(false);

        public void Hide() => gameObject.SetActive(false);

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
            if (TitleText != null) TitleText.text = title;
            if (SubtitleText != null) SubtitleText.text = subtitle;
            gameObject.SetActive(true);
        }

        public void ShowTransition(bool hasNextDay, bool canRansom,
            Action onNextDay, Action onRansom, Action onChallenge)
        {
            Open("День завершён", "Что дальше?");
            Bind(PrimaryButton, PrimaryLabel, "Следующий день", onNextDay, ref primary, hasNextDay);
            Bind(SecondaryButton, SecondaryLabel, "Выкупить себя", onRansom, ref secondary, canRansom);
            Bind(TertiaryButton, TertiaryLabel, "Бросить вызов (КУСЬ)", onChallenge, ref tertiary, true);
        }

        public void ShowRansomWin()
        {
            Open("Свободный мимик",
                 "Поздравляю! Ты стал свободным мимиком. Ты нашёл... счастье?");
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
            Bind(PrimaryButton, PrimaryLabel, "Переиграть день", onRetryDay, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "В меню", ToMenu, ref secondary, true);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        public void ShowBurst(Action onRetryDay)
        {
            Open("Ты лопнул от переедания",
                 "46% мимиков не доживают до старости из-за переедания. Вот кто ты есть: мимик обыкновенный.");
            Bind(PrimaryButton, PrimaryLabel, "Переиграть день", onRetryDay, ref primary, true);
            Bind(SecondaryButton, SecondaryLabel, "В меню", ToMenu, ref secondary, true);
            Bind(TertiaryButton, TertiaryLabel, "", null, ref tertiary, false);
        }

        private void ToMenu() => SceneManager.LoadScene("MainMenu");
    }
}
