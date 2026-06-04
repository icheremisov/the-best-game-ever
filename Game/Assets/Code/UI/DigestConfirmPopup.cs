using System;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;

namespace Mimic.UI
{
    // Модальное окно подтверждения переваривания. Предпочитает настраиваемый префаб
    // (Resources/UI/DigestConfirmPopup); если префаба нет — строит UI в коде (фолбэк).
    // Singleton, авто-добавляется GameContext.
    public class DigestConfirmPopup : MonoBehaviour
    {
        public static DigestConfirmPopup Instance { get; private set; }

        private GameObject root;
        private Image artImage;
        private Text artFallback;   // если спрайта нет — показываем имя
        private Text goldText;
        private Text healText;
        private Text acidText;
        private Button confirmButton;
        private Text confirmLabel;

        private Action onConfirm;
        private Action onCancel;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public bool IsOpen => root != null && root.activeSelf;

        public void Show(LootView item, Action onConfirm, Action onCancel)
        {
            if (item == null || item.Data == null) { onCancel?.Invoke(); return; }
            EnsureBuilt();
            this.onConfirm = onConfirm;
            this.onCancel = onCancel;

            var ctx = GameContext.Instance;
            int cost = ctx != null ? ctx.AcidCostFor(item) : item.Data.AcidCost;
            int coinGold = ctx != null ? ctx.CoinGoldFor(item.Data) : 0;
            bool canAfford = ctx != null && ctx.Resources.CurrentAcid >= cost;

            // Арт предмета (тот же путь, что в LootView).
            var sprite = Resources.Load<Sprite>("Art/Loot/" + item.Data.Id);
            if (sprite != null)
            {
                artImage.sprite = sprite;
                artImage.color = Color.white;
                artImage.enabled = true;
                artFallback.gameObject.SetActive(false);
            }
            else
            {
                artImage.enabled = false;
                artFallback.gameObject.SetActive(true);
                artFallback.text = string.IsNullOrEmpty(item.Data.Name) ? item.Data.Id : item.Data.Name;
            }

            goldText.text = $"Сохранится золота: {coinGold}/{item.Data.Gold}";
            goldText.gameObject.SetActive(item.Data.Gold > 0);

            if (item.Data.HealOnDigest > 0)
            {
                healText.gameObject.SetActive(true);
                healText.text = $"Восстановится здоровья: {item.Data.HealOnDigest}";
                healText.color = new Color(0.30f, 0.80f, 0.40f);
            }
            else if (item.Data.DamageOnDigest > 0)
            {
                healText.gameObject.SetActive(true);
                healText.text = $"Урон мимику: -{item.Data.DamageOnDigest} HP";
                healText.color = new Color(0.85f, 0.30f, 0.30f);
            }
            else healText.gameObject.SetActive(false);

            // ЖС-строку показываем только когда переваривание реально стоит сока.
            acidText.gameObject.SetActive(cost > 0);
            acidText.text = $"Желудочный сок: -{cost}";
            acidText.color = canAfford ? Color.black : new Color(0.80f, 0.20f, 0.20f);

            confirmButton.interactable = canAfford;
            confirmLabel.color = canAfford ? Color.black : new Color(0f, 0f, 0f, 0.35f);

            UiStageRoot.BringToFront(); // UIStage поверх DragLayer (бой мог поднять слой драга)
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        private void Hide() { if (root != null) root.SetActive(false); }

        private void Confirm()
        {
            Hide();
            var cb = onConfirm; onConfirm = null; onCancel = null;
            cb?.Invoke();
        }

        private void Cancel()
        {
            Hide();
            var cb = onCancel; onConfirm = null; onCancel = null;
            cb?.Invoke();
        }

        // --- runtime UI ---

        private void EnsureBuilt()
        {
            if (root != null) return;

            var canvas = FindCanvas();
            if (canvas == null) { Debug.LogError("[DigestConfirmPopup] Canvas не найден"); return; }

            // Предпочитаем настраиваемый префаб; если его нет — строим в коде.
            var prefab = Resources.Load<GameObject>("UI/DigestConfirmPopup");
            if (prefab != null && BuildFromPrefab(prefab, canvas)) return;

            BuildInCode(canvas);
        }

        // Инстанцирует префаб окна и берёт ссылки из DigestConfirmPopupView.
        private bool BuildFromPrefab(GameObject prefab, Canvas canvas)
        {
            var inst = Instantiate(prefab, UiStageRoot.For(canvas), false);
            inst.name = "DigestConfirmPopup_Auto";
            var view = inst.GetComponent<DigestConfirmPopupView>();
            if (view == null || view.ConfirmButton == null || view.CancelButton == null)
            {
                Debug.LogWarning("[DigestConfirmPopup] В префабе нет DigestConfirmPopupView/кнопок — фолбэк на код-билд");
                Destroy(inst);
                return false;
            }

            var rt = (RectTransform)inst.transform;
            Stretch(rt);

            root = inst;
            artImage = view.ArtImage;
            artFallback = view.ArtFallback;
            goldText = view.GoldText;
            healText = view.HealText;
            acidText = view.AcidText;
            confirmButton = view.ConfirmButton;
            confirmLabel = view.ConfirmLabel;

            confirmButton.onClick.AddListener(Confirm);
            view.CancelButton.onClick.AddListener(Cancel);

            root.SetActive(false);
            return true;
        }

        private void BuildInCode(Canvas canvas)
        {
            // Затемнение на весь экран — блокирует клики по игре.
            root = NewUI("DigestConfirmPopup", UiStageRoot.For(canvas));
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.raycastTarget = true;
            Stretch((RectTransform)root.transform);

            // Белая панель по центру.
            var panel = NewUI("Panel", root.transform);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, 560f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = Color.white;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);

            // Позиции — относительно центра панели (y вверх). Панель 760×560.
            var title = MakeText(prt, "Title", "Этот предмет будет переварен", 34, Color.black);
            PlaceCenter(title.rectTransform, 220f, 720f, 60f);

            // Арт предмета.
            var artGo = NewUI("Art", prt);
            artImage = artGo.AddComponent<Image>();
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;
            PlaceCenter((RectTransform)artGo.transform, 75f, 170f, 170f);

            artFallback = MakeText((RectTransform)artGo.transform, "Fallback", "", 28, Color.black);
            Stretch(artFallback.rectTransform);
            artFallback.gameObject.SetActive(false);

            goldText = MakeText(prt, "Gold", "", 26, Color.black);
            PlaceCenter(goldText.rectTransform, -70f, 720f, 36f);

            healText = MakeText(prt, "Heal", "", 26, Color.black);
            PlaceCenter(healText.rectTransform, -110f, 720f, 36f);

            acidText = MakeText(prt, "Acid", "", 28, Color.black);
            PlaceCenter(acidText.rectTransform, -150f, 720f, 36f);

            // Кнопки внизу панели.
            var cancelBtn = MakeButton(prt, "CancelButton", "ОТМЕНА", new Color(0.85f, 0.85f, 0.85f),
                new Vector2(-175f, -215f), out _);
            cancelBtn.onClick.AddListener(Cancel);

            confirmButton = MakeButton(prt, "ConfirmButton", "ПЕРЕВАРИТЬ", new Color(0.55f, 0.80f, 0.55f),
                new Vector2(175f, -215f), out confirmLabel);
            confirmButton.onClick.AddListener(Confirm);

            root.SetActive(false);
        }

        // Центрированное размещение относительно середины родителя.
        private static void PlaceCenter(RectTransform rt, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static Canvas FindCanvas()
        {
            var drag = Mimic.Input.DragController.Instance;
            if (drag != null && drag.DragLayer != null)
            {
                var c = drag.DragLayer.GetComponentInParent<Canvas>();
                if (c != null) return c;
            }
            return UnityEngine.Object.FindFirstObjectByType<Canvas>();
        }

        private static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text MakeText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = NewUI(name, parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = FontProvider.Default;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static Button MakeButton(Transform parent, string name, string label, Color bg,
            Vector2 anchoredPos, out Text labelText)
        {
            var go = NewUI(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(300f, 80f);
            labelText = MakeText(rt, "Label", label, 26, Color.black);
            Stretch(labelText.rectTransform);
            return btn;
        }
    }
}
