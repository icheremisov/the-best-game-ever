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

        private void Awake()
        {
            if (Root != null) Root.SetActive(false);
            if (SettleButton != null) SettleButton.onClick.AddListener(OnSettleClicked);
        }

        public void Show(Action settledCallback)
        {
            onSettled = settledCallback;
            if (Root != null) Root.SetActive(true);
            if (TitleText != null) TitleText.text = "Властелин пришёл";
            if (SettleLabel != null) SettleLabel.text = "Подвести итог дня";
            RefreshTexts();
        }

        public void RefreshTexts()
        {
            var r = GameContext.Instance.Resources;
            if (GoldText != null) GoldText.text = $"{r.TotalGold}/{r.DayQuota}";
            if (TooltipText != null)
            {
                int dmg = Settlement.Damage(r.TotalGold, r.DayQuota, DayConfig.Current.GoldDamageMult);
                TooltipText.text = dmg > 0
                    ? $"Потеря HP: -{dmg}\nПоследует наказание"
                    : "Последует награда";
            }
        }

        private void OnSettleClicked()
        {
            var ctx = GameContext.Instance;
            ctx.BankAllInGrid(ctx.AdventurerGrid);

            var stolen = TheftResolver.PickStealable(ctx.MimicGrid.Model, v => v, theftSeed++);
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
