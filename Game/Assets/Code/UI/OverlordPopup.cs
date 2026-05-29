using System;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;
using Mimic.Catalogs;
using Mimic.Logic;

namespace Mimic.UI
{
    public class OverlordPopup : MonoBehaviour
    {
        public GameObject Root;
        public Text TitleText;
        public Text GoldText;
        public Button SettleButton;
        public Text SettleLabel;
        public Text TooltipText;

        private Action onSettled;
        private int theftSeed = 1;
        private bool showing; // true пока попап вызван через Show — не даём Awake погасить себя

        private void Awake()
        {
            if (!showing && Root != null) Root.SetActive(false);
            if (SettleButton != null) SettleButton.onClick.AddListener(OnSettleClicked);
        }

        public void Show(Action settledCallback)
        {
            showing = true;
            onSettled = settledCallback;
            if (Root != null) Root.SetActive(true);
            if (TitleText != null) TitleText.text = "Властелин пришёл";
            if (SettleLabel != null) SettleLabel.text = "Подвести итог дня";
            RefreshTexts();
        }

        public void RefreshTexts()
        {
            var ctx = GameContext.Instance;
            var r = ctx.Resources;
            int basket = 0;
            foreach (var it in ctx.AdventurerGrid.Model.AllItems())
                if (it.Data != null && !it.Data.IsFixture) basket += it.Data.Gold;
            int previewTotal = r.TotalGold + basket;
            if (GoldText != null) GoldText.text = $"Золото: {previewTotal} / {r.DayQuota}";
            if (TooltipText != null)
            {
                int dmg = Settlement.Damage(previewTotal, r.DayQuota, DayConfig.Current.GoldDamageMult);
                TooltipText.text = dmg > 0 ? $"Не хватает! Потеря HP: -{dmg} — последует наказание" : "Квота закрыта — последует награда";
                TooltipText.color = dmg > 0 ? new Color(0.9f,0.4f,0.4f) : new Color(0.5f,0.85f,0.5f);
            }
        }

        private void Update()
        {
            if (Root != null && Root.activeSelf) RefreshTexts();
        }

        private void OnSettleClicked()
        {
            var ctx = GameContext.Instance;
            ctx.BankAllInGrid(ctx.AdventurerGrid);

            var stolen = TheftResolver.PickStealable(ctx.MimicGrid.Model, v => v, theftSeed++,
                canSteal: v => !v.Data.IsFixture);
            if (stolen != null)
            {
                ctx.MimicGrid.Model.Remove(stolen);
                Destroy(stolen.gameObject);
            }
            ctx.OnGridChanged();

            var r = ctx.Resources;
            int dmg = Settlement.Damage(r.TotalGold, r.DayQuota, DayConfig.Current.GoldDamageMult);
            if (dmg > 0) r.CurrentHp -= dmg;

            if (Root != null) Root.SetActive(false);
            onSettled?.Invoke();
        }
    }
}
