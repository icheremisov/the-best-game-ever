using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Mimic.UI
{
    public class EndOfDayPopup : MonoBehaviour
    {
        public Text TitleText;
        public Text SubtitleText;
        public Button RetryButton;
        public Button MenuButton;

        private void Awake()
        {
            if (RetryButton != null)
            {
                PopupHelpers.EnsureButtonLabel(RetryButton, "Начать заново", 24);
                RetryButton.onClick.AddListener(() => SceneManager.LoadScene("Game"));
            }
            if (MenuButton != null)
            {
                PopupHelpers.EnsureButtonLabel(MenuButton, "В меню", 24);
                MenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
            }
            gameObject.SetActive(false);
        }

        public void ShowWin(int gold, int quota)
        {
            if (TitleText != null) TitleText.text = "День спасён!";
            if (SubtitleText != null) SubtitleText.text = $"Заработано {gold} / {quota}";
            if (RetryButton != null) RetryButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        public void ShowLose(string reason, int gold, int quota)
        {
            if (TitleText != null) TitleText.text = reason;
            if (SubtitleText != null) SubtitleText.text = $"Заработано {gold} / {quota}";
            if (RetryButton != null) RetryButton.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }
    }
}
