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

        [Header("Style overrides")]
        public int TopBarFontSize = 36;
        public int BottomLabelFontSize = 28;
        public int BarLabelFontSize = 24;

        private Text healthBarLabel;
        private Text acidBarLabel;
        private Text surrenderLabel;

        private void Awake()
        {
            ApplyFontSizes();
            EnsureBarLabels();
            EnsureButtonLabels();
        }

        private void ApplyFontSizes()
        {
            foreach (var t in new[] { GoldInMimicText, DayQuotaText, DayCounterText, HeroCounterText })
                if (t != null) { t.fontSize = TopBarFontSize; t.alignment = TextAnchor.MiddleCenter; }
            if (NextButtonLabel != null) NextButtonLabel.fontSize = BottomLabelFontSize;
        }

        private void EnsureBarLabels()
        {
            if (HealthBar != null) healthBarLabel = OrCreateChildLabel(HealthBar.transform, "HpLabel", "HP");
            if (AcidBar != null) acidBarLabel = OrCreateChildLabel(AcidBar.transform, "AcidLabel", "ЖС");
        }

        private void EnsureButtonLabels()
        {
            if (SurrenderButton != null)
            {
                surrenderLabel = OrCreateChildLabel(SurrenderButton.transform, "Label", "Сдаться");
                surrenderLabel.color = Color.white;
            }
            if (NextButton != null && NextButtonLabel == null)
            {
                NextButtonLabel = OrCreateChildLabel(NextButton.transform, "Label", "Следующий!");
                NextButtonLabel.fontSize = BottomLabelFontSize;
                NextButtonLabel.color = Color.white;
            }
        }

        // Reuse existing child if present, otherwise create a centered, anchored-stretched Text.
        private Text OrCreateChildLabel(Transform parent, string name, string defaultText)
        {
            var existing = parent.Find(name);
            Text t;
            if (existing != null)
            {
                t = existing.GetComponent<Text>();
                if (t == null) t = existing.gameObject.AddComponent<Text>();
            }
            else
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, worldPositionStays: false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                t = go.GetComponent<Text>();
            }
            if (string.IsNullOrEmpty(t.text)) t.text = defaultText;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = BarLabelFontSize;
            t.color = Color.white;
            t.font = t.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.raycastTarget = false;
            t.fontStyle = FontStyle.Bold;
            // Black outline for readability over filled bars
            var outline = t.gameObject.GetComponent<Outline>();
            if (outline == null) outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
            return t;
        }

        public void Refresh()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;
            if (GoldInMimicText != null) GoldInMimicText.text = $"Цена: {ctx.Resources.CurrentGoldInMimic}";
            if (DayQuotaText != null) DayQuotaText.text = $"Нужно: {ctx.Resources.DayQuota}";

            int hpMax = Mathf.Max(1, DayConfig.Current.StartHp);
            int acidMax = Mathf.Max(1, DayConfig.Current.StartAcid);

            if (HealthBar != null)
                HealthBar.fillAmount = Mathf.Clamp01(ctx.Resources.CurrentHp / (float)hpMax);
            if (AcidBar != null)
                AcidBar.fillAmount = Mathf.Clamp01(ctx.Resources.CurrentAcid / (float)acidMax);

            if (healthBarLabel != null) healthBarLabel.text = $"HP: {ctx.Resources.CurrentHp}/{hpMax}";
            if (acidBarLabel != null) acidBarLabel.text = $"ЖС: {ctx.Resources.CurrentAcid}/{acidMax}";

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
