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
            EnsureMainCamera();
            FontProvider.ApplyToAllScene();
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

        private static void EnsureMainCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.10f, 1f);
            cam.orthographic = true;
            cam.cullingMask = 0;
            go.AddComponent<AudioListener>();
        }
    }
}
