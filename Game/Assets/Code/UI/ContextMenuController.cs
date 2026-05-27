using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;

namespace Mimic.UI
{
    public class ContextMenuController : MonoBehaviour
    {
        public static ContextMenuController Instance { get; private set; }

        [Header("References")]
        public RectTransform Panel;
        public Button DigestButton;
        public Text DigestLabel;
        public Camera UiCamera;

        private LootView target;

        private void Awake()
        {
            Instance = this;
            if (Panel != null) Panel.gameObject.SetActive(false);
            if (DigestButton != null) DigestButton.onClick.AddListener(OnDigestClicked);
        }

        public void Open(LootView item)
        {
            var ctx = GameContext.Instance;
            if (ctx == null || ctx.MimicGrid == null) return;
            bool inMimic = false;
            foreach (var i in ctx.MimicGrid.Model.AllItems())
                if (i == item) { inMimic = true; break; }
            if (!inMimic) return;

            target = item;
            int cost = ctx.LastResolved != null ? ctx.LastResolved.GetAcid(item) : item.Data.AcidCost;
            if (DigestLabel != null) DigestLabel.text = $"Переварить ({cost} сока)";
            if (DigestButton != null) DigestButton.interactable = ctx.Resources.CurrentAcid >= cost;
            if (Panel == null) return;
            Panel.gameObject.SetActive(true);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Panel.parent, (UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero), UiCamera, out var local))
                Panel.anchoredPosition = local;
        }

        public void Close()
        {
            if (Panel != null) Panel.gameObject.SetActive(false);
            target = null;
        }

        private void OnDigestClicked()
        {
            if (target == null) return;
            GameContext.Instance?.Digest(target);
            Close();
        }
    }
}
