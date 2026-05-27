using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;

namespace Mimic.UI
{
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Header("References")]
        public RectTransform Panel;
        public Text NameText;
        public Text DescriptionText;
        public Text GoldText;
        public Text AcidText;
        public Text AdjacencyText;
        public Camera UiCamera;

        private void Awake()
        {
            Instance = this;
            if (Panel != null) Panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Panel == null || !Panel.gameObject.activeSelf) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Panel.parent, (UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero), UiCamera, out var local))
                Panel.anchoredPosition = local + new Vector2(20, 20);
        }

        public void Show(LootView item)
        {
            if (Panel == null || item == null || item.Data == null) return;
            Panel.gameObject.SetActive(true);
            NameText.text = item.Data.Name;
            DescriptionText.text = item.Data.Description;

            int gold = GameContext.Instance != null && GameContext.Instance.LastResolved != null
                ? GameContext.Instance.LastResolved.GetGold(item)
                : item.Data.Gold;
            int acid = GameContext.Instance != null && GameContext.Instance.LastResolved != null
                ? GameContext.Instance.LastResolved.GetAcid(item)
                : item.Data.AcidCost;
            GoldText.text = $"Цена: {gold} зол.";
            AcidText.text = $"Переварить: {acid} сока";
            AdjacencyText.text = string.IsNullOrEmpty(item.Data.AdjacencyTarget)
                ? ""
                : $"Рядом с «{item.Data.AdjacencyTarget}»";
        }

        public void Hide()
        {
            if (Panel != null) Panel.gameObject.SetActive(false);
        }
    }
}
