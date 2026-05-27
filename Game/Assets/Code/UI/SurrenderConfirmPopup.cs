using System;
using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    public class SurrenderConfirmPopup : MonoBehaviour
    {
        public Button YesButton;
        public Button NoButton;

        private Action onYes;

        private void Awake()
        {
            if (YesButton != null)
            {
                PopupHelpers.EnsureButtonLabel(YesButton, "Да", 24);
                YesButton.onClick.AddListener(() => { onYes?.Invoke(); gameObject.SetActive(false); });
            }
            if (NoButton != null)
            {
                PopupHelpers.EnsureButtonLabel(NoButton, "Нет", 24);
                NoButton.onClick.AddListener(() => gameObject.SetActive(false));
            }
            gameObject.SetActive(false);
        }

        public void Show(Action onYesCallback)
        {
            onYes = onYesCallback;
            gameObject.SetActive(true);
        }
    }
}
