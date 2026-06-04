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

        // Progress-fill children (Filled Image, заливка израсходованной части бара).
        private Image healthProgress;
        private Image acidProgress;

        // --- Holder-ы шага «итоги дня» (объекты в сцене, позиции правим в редакторе). ---
        [Header("Reward step holders (объекты в сцене)")]
        public Text GoldCollectedLabel;     // «Собрано золота X / Y» слева от кнопки «Подвести итоги»
        public Button StartNextDayButton;   // «Начать следующий день»
        public Button ChallengeButton;      // «Бросить вызов»

        private static readonly Color GoldOkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color GoldShortColor = new Color(0.95f, 0.85f, 0.55f);

        private void Awake()
        {
            // ApplyFontSizes();
            EnsureBarLabels();
            EnsureButtonLabels();
            CacheProgressBars();
        }

        // Шаг сбора лута: видна NextButton («Следующий!»/«Подвести итоги дня»), кнопки итогов скрыты.
        public void EnterAdventurerButtons()
        {
            if (NextButton != null) NextButton.gameObject.SetActive(true);
            if (StartNextDayButton != null) StartNextDayButton.gameObject.SetActive(false);
            if (ChallengeButton != null) ChallengeButton.gameObject.SetActive(false);
        }

        // Доступность «Начать следующий день» (живо обновляется, пока лут авантюриста не пуст).
        public void SetStartNextDayEnabled(bool e)
        {
            if (StartNextDayButton != null) StartNextDayButton.interactable = e;
        }

        // Шаг награды: NextButton скрыта, на её месте «Начать следующий день» и «Бросить вызов».
        public void ShowRewardButtons(System.Action onNextDay, bool nextDayEnabled, System.Action onChallenge)
        {
            if (NextButton != null) NextButton.gameObject.SetActive(false);
            if (StartNextDayButton != null)
            {
                StartNextDayButton.gameObject.SetActive(true);
                StartNextDayButton.interactable = nextDayEnabled;
                StartNextDayButton.onClick.RemoveAllListeners();
                StartNextDayButton.onClick.AddListener(() => onNextDay?.Invoke());
            }
            if (ChallengeButton != null)
            {
                ChallengeButton.gameObject.SetActive(true);
                ChallengeButton.onClick.RemoveAllListeners();
                ChallengeButton.onClick.AddListener(() => onChallenge?.Invoke());
            }
        }

        private void CacheProgressBars()
        {
            // Видимые бары — Health/Acid (с дочерним Progress, тип Filled).
            // HealthBar/AcidBar в инспекторе — легаси (неактивны), поэтому ищем по пути.
            healthProgress = FindProgressImage("Canvas/Stage/HUDRoot/BottomBar/Health/Progress");
            acidProgress = FindProgressImage("Canvas/Stage/HUDRoot/BottomBar/Acid/Progress");
        }

        private static Image FindProgressImage(string path)
        {
            var go = GameObject.Find(path);
            return go != null ? go.GetComponent<Image>() : null;
        }

        private void ApplyFontSizes()
        {
            foreach (var t in new[] { GoldInMimicText, DayQuotaText, DayCounterText, HeroCounterText })
                if (t != null) { t.fontSize = TopBarFontSize; t.alignment = TextAnchor.MiddleCenter; }
            if (NextButtonLabel != null) NextButtonLabel.fontSize = BottomLabelFontSize;
        }

        private void EnsureBarLabels()
        {
            // Подписи вешаем на новые бары Health/Acid; легаси HealthBar/AcidBar гасим.
            if (HealthBar != null) HealthBar.gameObject.SetActive(false);
            if (AcidBar != null) AcidBar.gameObject.SetActive(false);

            var health = GameObject.Find("Canvas/Stage/HUDRoot/BottomBar/Health");
            var acid = GameObject.Find("Canvas/Stage/HUDRoot/BottomBar/Acid");
            if (health != null) healthBarLabel = OrCreateChildLabel(health.transform, "HpLabel", "HP");
            if (acid != null) acidBarLabel = OrCreateChildLabel(acid.transform, "AcidLabel", "ЖС");
        }

        private void EnsureButtonLabels()
        {
            // У SurrenderButton подпись уже вшита в префаб (Text TMP) — свою не спавним.
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
            t.font = FontProvider.Default;
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
            if (GoldInMimicText != null) GoldInMimicText.text = $"{ctx.Resources.CurrentGoldInMimic}";
            if (DayQuotaText != null) DayQuotaText.text = $"Нужно\n{ctx.Resources.DayQuota}";

            int hpMax = Mathf.Max(1, DayConfig.Current.StartHp);
            int acidMax = Mathf.Max(1, DayConfig.Current.StartAcid);

            float hpRatio = Mathf.Clamp01(ctx.Resources.CurrentHp / (float)hpMax);
            float acidRatio = Mathf.Clamp01(ctx.Resources.CurrentAcid / (float)acidMax);

            // Progress — заливка израсходованной части: fillAmount 0 => полный бар, 1 => пустой.
            SetProgressFill(healthProgress, 1f - hpRatio);
            SetProgressFill(acidProgress, 1f - acidRatio);

            if (healthBarLabel != null) healthBarLabel.text = $"HP: {ctx.Resources.CurrentHp}/{hpMax}";
            if (acidBarLabel != null) acidBarLabel.text = $"ЖС: {ctx.Resources.CurrentAcid}/{acidMax}";

            if (GoldCollectedLabel != null)
            {
                int collected = ctx.Resources.TotalGold;
                int quota = ctx.Resources.DayQuota;
                GoldCollectedLabel.text = $"Собрано золота\n{collected} / {quota}";
                GoldCollectedLabel.color = collected >= quota ? GoldOkColor : GoldShortColor;
            }

            UpdateSurrenderHighlight(ctx);
        }

        private static void SetProgressFill(Image progress, float depleted)
        {
            if (progress == null) return;
            progress.fillAmount = Mathf.Clamp01(depleted);
        }

        public void SetHeroCounter(int current, int total)
        {
            if (HeroCounterText != null) HeroCounterText.text = $"{current}/{total}";
        }
        public void SetDayCounter(int day)
        {
            if (DayCounterText != null) DayCounterText.text = $"День\n{day}";
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
