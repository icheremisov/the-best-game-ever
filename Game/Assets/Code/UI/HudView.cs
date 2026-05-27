using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;
using Mimic.Catalogs;

namespace Mimic.UI
{
    public class HudView : MonoBehaviour
    {
        [Header("Top bar")]
        public Text GoldInMimicText;
        public Text DayQuotaText;
        public Text DayCounterText;
        public Text HeroCounterText;

        [Header("Bottom bar")]
        public Image HealthBar;
        public Image AcidBar;
        public Button NextButton;
        public Button SurrenderButton;
        public Text NextButtonLabel;

        public void Refresh()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;
            if (GoldInMimicText != null) GoldInMimicText.text = $"Цена: {ctx.Resources.CurrentGoldInMimic}";
            if (DayQuotaText != null) DayQuotaText.text = $"Нужно: {ctx.Resources.DayQuota}";

            if (HealthBar != null)
                HealthBar.fillAmount = Mathf.Clamp01(
                    ctx.Resources.CurrentHp / (float)Mathf.Max(1, DayConfig.Current.StartHp));
            if (AcidBar != null)
                AcidBar.fillAmount = Mathf.Clamp01(
                    ctx.Resources.CurrentAcid / (float)Mathf.Max(1, DayConfig.Current.StartAcid));

            UpdateSurrenderHighlight(ctx);
        }

        public void SetHeroCounter(int current, int total)
        {
            if (HeroCounterText != null) HeroCounterText.text = $"Герой {current}/{total}";
        }
        public void SetDayCounter(int day)
        {
            if (DayCounterText != null) DayCounterText.text = $"День {day}";
        }
        public void SetNextButtonLabel(string text)
        {
            if (NextButtonLabel != null) NextButtonLabel.text = text;
        }
        public void SetNextButtonEnabled(bool e)
        {
            if (NextButton != null) NextButton.interactable = e;
        }

        private void UpdateSurrenderHighlight(GameContext ctx)
        {
            if (SurrenderButton == null) return;
            bool noAcid = false;
            int minAcid = int.MaxValue;
            foreach (var i in ctx.MimicGrid.Model.AllItems())
            {
                int a = ctx.LastResolved != null ? ctx.LastResolved.GetAcid(i) : i.Data.AcidCost;
                if (a < minAcid) minAcid = a;
            }
            if (minAcid != int.MaxValue && ctx.Resources.CurrentAcid < minAcid) noAcid = true;

            bool noSpace = ctx.MimicGrid.Model.FreeCellsCount < OccupiedCells(ctx.AdventurerGrid);

            bool danger = noAcid || noSpace;
            var img = SurrenderButton.GetComponent<Image>();
            if (img != null) img.color = danger ? Color.red : Color.white;
        }

        private int OccupiedCells(GridView grid)
        {
            int total = grid.Width * grid.Height;
            return total - grid.Model.FreeCellsCount;
        }
    }
}
