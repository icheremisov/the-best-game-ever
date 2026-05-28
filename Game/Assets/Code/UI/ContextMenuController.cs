using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;

namespace Mimic.UI
{
    public class ContextMenuController : MonoBehaviour
    {
        public static ContextMenuController Instance { get; private set; }

        [Header("References (auto-created if null)")]
        public RectTransform Panel;
        public Button DigestButton;
        public Text DigestLabel;
        public Camera UiCamera;
        public Canvas HostCanvas;

        private LootView target;

        private void Awake()
        {
            Instance = this;
            EnsurePanel();
            if (Panel != null) Panel.gameObject.SetActive(false);
            if (DigestButton != null) DigestButton.onClick.AddListener(OnDigestClicked);
        }

        // Build the popup at runtime if the prefab reference wasn't wired up.
        private void EnsurePanel()
        {
            if (Panel != null && DigestButton != null && DigestLabel != null) return;

            if (HostCanvas == null) HostCanvas = FindObjectOfType<Canvas>();
            if (HostCanvas == null) return;

            if (Panel == null)
            {
                var go = new GameObject("ContextMenuPanel_Auto",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(HostCanvas.transform, worldPositionStays: false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 1);
                rt.sizeDelta = new Vector2(220, 56);
                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
                bg.raycastTarget = true; // ContextMenu IS interactive (click on it)
                Panel = rt;
            }

            if (DigestButton == null)
            {
                var bgo = new GameObject("DigestButton",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                bgo.transform.SetParent(Panel, worldPositionStays: false);
                var brt = (RectTransform)bgo.transform;
                brt.anchorMin = new Vector2(0, 0);
                brt.anchorMax = new Vector2(1, 1);
                brt.offsetMin = new Vector2(8, 8);
                brt.offsetMax = new Vector2(-8, -8);
                var btnImg = bgo.GetComponent<Image>();
                btnImg.color = new Color(0.20f, 0.25f, 0.35f, 1f);
                DigestButton = bgo.GetComponent<Button>();
            }

            if (DigestLabel == null)
            {
                var lgo = new GameObject("Label",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lgo.transform.SetParent(DigestButton.transform, worldPositionStays: false);
                var lrt = (RectTransform)lgo.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                DigestLabel = lgo.GetComponent<Text>();
                DigestLabel.color = Color.white;
                DigestLabel.alignment = TextAnchor.MiddleCenter;
                DigestLabel.fontSize = 20;
                DigestLabel.fontStyle = FontStyle.Bold;
                DigestLabel.raycastTarget = false;
                DigestLabel.font = FontProvider.Default;
                DigestLabel.text = "Переварить";
            }
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
            Panel.pivot = new Vector2(0, 1); // top-left so it opens down-right from cursor
            Panel.gameObject.SetActive(true);
            Panel.SetAsLastSibling();

            // World-space positioning — robust regardless of Canvas pivot/anchor.
            // For a ScreenSpaceOverlay canvas, world coords == screen pixels.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2 screenPos = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Canvas.ForceUpdateCanvases();
            float w = Panel.rect.width, h = Panel.rect.height;
            float x = screenPos.x;
            float y = screenPos.y;
            // Keep on-screen.
            if (x + w > Screen.width) x = Screen.width - w;
            if (y - h < 0) y = h;
            Panel.position = new Vector3(x, y, 0);
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
