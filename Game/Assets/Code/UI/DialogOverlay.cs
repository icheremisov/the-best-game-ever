using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Data;

namespace Mimic.UI
{
    // Модальный оверлей диалогов: затемняет экран, показывает баббл с портретом и текстом,
    // клик по любому месту листает цепочку. UI строится в коде под главным Canvas
    // (паттерн TooltipController.EnsurePanel). Авто-создаётся в GameContext.
    public class DialogOverlay : MonoBehaviour
    {
        public static DialogOverlay Instance { get; private set; }

        private Canvas hostCanvas;
        private GameObject root;            // затемняющая панель — ловит клики
        private RectTransform portraitContainer;
        private GameObject portraitFallback;
        private Text bodyText;
        private GameObject portraitInstance;

        private IList<DialogLine> chain;
        private int index;
        private Action onComplete;

        private void Awake()
        {
            Instance = this;
            EnsureUi();
            if (root != null) root.SetActive(false);
        }

        // Показать цепочку. Пустая/null — сразу зовём onComplete (диалога нет).
        public void Show(IList<DialogLine> lines, Action onCompleteCallback)
        {
            if (lines == null || lines.Count == 0) { onCompleteCallback?.Invoke(); return; }
            if (root == null) { onCompleteCallback?.Invoke(); return; } // нет Canvas — не блокируем игру
            if (root.activeSelf)
            {
                Debug.LogWarning("[DialogOverlay] Show вызван во время активного диалога — игнорирую повторный вызов");
                return;
            }
            chain = lines;
            index = 0;
            onComplete = onCompleteCallback;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            ShowCurrent();
        }

        private void Advance()
        {
            index++;
            if (chain == null || index >= chain.Count) { Close(); return; }
            ShowCurrent();
        }

        private void Close()
        {
            if (root != null) root.SetActive(false);
            var cb = onComplete;
            onComplete = null;
            chain = null;
            cb?.Invoke();
        }

        private void ShowCurrent()
        {
            var line = chain[index];
            if (bodyText != null) bodyText.text = line.Text;
            ShowPortrait(line.Icon);
        }

        private void ShowPortrait(string icon)
        {
            if (portraitInstance != null) Destroy(portraitInstance);
            portraitInstance = PortraitLoader.Instantiate(icon, portraitContainer);
            if (portraitFallback != null) portraitFallback.SetActive(portraitInstance == null);
        }

        private void EnsureUi()
        {
            if (root != null) return;
            if (hostCanvas == null) hostCanvas = FindFirstObjectByType<Canvas>();
            if (hostCanvas == null)
            {
                Debug.LogWarning("[DialogOverlay] Нет Canvas в сцене — диалоги не отрисуются");
                return;
            }

            // Предпочитаем настраиваемый префаб (Resources/UI/DialogOverlay); если его нет — строим в коде.
            var prefab = Resources.Load<GameObject>("UI/DialogOverlay");
            if (prefab != null && BuildFromPrefab(prefab)) return;

            BuildInCode();
        }

        // Инстанцирует префаб диалога и берёт ссылки из DialogOverlayView.
        private bool BuildFromPrefab(GameObject prefab)
        {
            var inst = Instantiate(prefab, UiStageRoot.For(hostCanvas), false);
            inst.name = "DialogOverlay_Auto";
            var view = inst.GetComponent<DialogOverlayView>();
            if (view == null || view.Root == null)
            {
                Debug.LogWarning("[DialogOverlay] В префабе нет DialogOverlayView/Root — фолбэк на код-билд");
                Destroy(inst);
                return false;
            }
            var rt = (RectTransform)inst.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            root = inst;
            portraitContainer = view.PortraitContainer;
            portraitFallback = view.PortraitFallback;
            bodyText = view.BodyText;
            view.Root.onClick.AddListener(Advance);
            inst.transform.SetAsLastSibling();
            return true;
        }

        private void BuildInCode()
        {
            // Затемняющая панель на весь экран; Button глотает клики и листает диалог.
            var panelGo = new GameObject("DialogOverlay_Auto",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            panelGo.transform.SetParent(UiStageRoot.For(hostCanvas), false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.7f);
            panelImg.raycastTarget = true;
            var btn = panelGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Advance);
            root = panelGo;
            panelRt.SetAsLastSibling(); // поверх всего

            // Баббл внизу по центру (реюзаемый визуал диалога/поп-апа).
            var bubbleGo = new GameObject("Bubble",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bubbleGo.transform.SetParent(panelRt, false);
            var bubbleRt = (RectTransform)bubbleGo.transform;
            bubbleRt.anchorMin = new Vector2(0.5f, 0f);
            bubbleRt.anchorMax = new Vector2(0.5f, 0f);
            bubbleRt.pivot = new Vector2(0.5f, 0f);
            bubbleRt.anchoredPosition = new Vector2(0f, 60f);
            bubbleRt.sizeDelta = new Vector2(760f, 220f);
            var bubbleImg = bubbleGo.GetComponent<Image>();
            var bubbleSprite = Resources.Load<Sprite>("Art/UI/bubble");
            if (bubbleSprite != null)
            {
                bubbleImg.sprite = bubbleSprite;
                bubbleImg.type = Image.Type.Simple;
                bubbleImg.color = Color.white;
            }
            else
            {
                bubbleImg.color = new Color(0.97f, 0.96f, 0.92f, 1f);
            }
            bubbleImg.raycastTarget = false;

            const float portW = 180f;

            // Контейнер портрета слева.
            var portGo = new GameObject("Portrait", typeof(RectTransform));
            portGo.transform.SetParent(bubbleRt, false);
            portraitContainer = (RectTransform)portGo.transform;
            portraitContainer.anchorMin = new Vector2(0f, 0f);
            portraitContainer.anchorMax = new Vector2(0f, 1f);
            portraitContainer.pivot = new Vector2(0f, 0.5f);
            portraitContainer.offsetMin = new Vector2(16f, 16f);
            portraitContainer.offsetMax = new Vector2(16f + portW, -16f);

            // Заглушка портрета (если арта нет).
            var fbGo = new GameObject("PortraitFallback",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fbGo.transform.SetParent(portraitContainer, false);
            var fbRt = (RectTransform)fbGo.transform;
            fbRt.anchorMin = Vector2.zero; fbRt.anchorMax = Vector2.one;
            fbRt.offsetMin = Vector2.zero; fbRt.offsetMax = Vector2.zero;
            var fbImg = fbGo.GetComponent<Image>();
            fbImg.color = new Color(0.6f, 0.55f, 0.5f, 1f);
            fbImg.raycastTarget = false;
            portraitFallback = fbGo;

            // Текст реплики справа от портрета.
            var textGo = new GameObject("Body",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(bubbleRt, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(16f + portW + 16f, 40f);
            textRt.offsetMax = new Vector2(-24f, -16f);
            bodyText = textGo.GetComponent<Text>();
            bodyText.font = FontProvider.Default;
            bodyText.fontSize = 26;
            bodyText.color = new Color(0.1f, 0.1f, 0.12f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;

            // Маркер ▶ — намёк, что диалог можно скликнуть.
            var markGo = new GameObject("ClickHint",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            markGo.transform.SetParent(bubbleRt, false);
            var markRt = (RectTransform)markGo.transform;
            markRt.anchorMin = new Vector2(1f, 0f);
            markRt.anchorMax = new Vector2(1f, 0f);
            markRt.pivot = new Vector2(1f, 0f);
            markRt.anchoredPosition = new Vector2(-18f, 14f);
            markRt.sizeDelta = new Vector2(40f, 40f);
            var mark = markGo.GetComponent<Text>();
            mark.font = FontProvider.Default;
            mark.fontSize = 30;
            mark.text = "▶";
            mark.color = new Color(0.1f, 0.1f, 0.12f);
            mark.alignment = TextAnchor.MiddleRight;
            mark.raycastTarget = false;
        }
    }
}
