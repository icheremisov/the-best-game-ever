using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mimic.UI
{
    public class MainMenu : MonoBehaviour
    {
        public Button StartButton;
        public Button QuitButton;

        private void Awake()
        {
            if (StartButton != null)
                StartButton.onClick.AddListener(() => SceneManager.LoadScene("Game"));
            if (QuitButton != null)
                QuitButton.onClick.AddListener(() =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
        }
    }
}
