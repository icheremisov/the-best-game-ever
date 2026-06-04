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

        // --- Кнопки/лейбл шага «итоги дня», создаются в рантайме (клон NextButton). ---
        private Text goldCollectedLabel;       // «Собрано X / Y» слева от кнопки «Подвести итоги»
        private Button startNextDayButton;     // «Начать следующий день»
        private Text startNextDayLabel;
        private Button challengeButton;        // «Бросить вызов»
        private Text challengeLabel;
        private bool rewardUiBuilt;

        private static readonly Color GoldOkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color GoldShortColor = new Color(0.95f, 0.85f, 0.55f);

        private void Awake()
        {
            // ApplyFontSizes();
            EnsureBarLabels();
            EnsureButtonLabels();
            CacheProgressBars();
            EnsureRewardUi();
        }

        // Строит «Собрано X / Y» и две кнопки шага итогов (клон NextButton), один раз.
        private void EnsureRewardUi()
        {
            if (rewardUiBuilt || NextButton == null) return;
            rewardUiBuilt = true;

            var srt = (RectTransform)NextButton.transform;
            float baseX = srt.anchoredPosition.x;
            float baseY = srt.anchoredPosition.y;
            float h = srt.sizeDelta.y;

            // «Начать следующий день» — на месте NextButton; «Бросить вызов» — над ней.
            startNextDayButton = CloneNextButton("StartNextDayButton", baseY, "Начать\nследующий день", out startNextDayLabel);
            challengeButton = CloneNextButton("ChallengeButton", baseY + h + 10f, "Бросить вызов", out challengeLabel);
            startNextDayButton.gameObject.SetActive(false);
            challengeButton.gameObject.SetActive(false);

            EnsureGoldLabel(baseX - srt.sizeDelta.x * 0.5f - 24f, baseY);
        }

        // Клонирует NextButton (тот же спрайт/размер/якоря), смещает по Y, ставит текст.
        private Button CloneNextButton(string name, float anchoredY, string label, out Text labelText)
        {
            var src = NextButton.gameObject;
            var go = Instantiate(src, src.transform.parent);
            go.name = name;
            var srt = (RectTransform)src.transform;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = srt.anchorMin;
            rt.anchorMax = srt.anchorMax;
            rt.pivot = srt.pivot;
            rt.sizeDelta = srt.sizeDelta;
            rt.anchoredPosition = new Vector2(srt.anchoredPosition.x, anchoredY);

            var btn = go.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.interactable = true;

            labelText = go.GetComponentInChildren<Text>(true);
            if (labelText != null)
            {
                labelText.text = label;
                labelText.font = FontProvider.Default;
                labelText.color = Color.white;
                labelText.alignment = TextAnchor.MiddleCenter;
            }
            return btn;
        }

        // Лейбл «Собрано X / Y» справа-выровнен, правый край в (rightX, y).
        private void EnsureGoldLabel(float rightX, float y)
        {
            var go = new GameObject("GoldCollected", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(NextButton.transform.parent, worldPositionStays: false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 116f);
            rt.anchoredPosition = new Vector2(rightX, y);

            goldCollectedLabel = go.GetComponent<Text>();
            goldCollectedLabel.font = FontProvider.Default;
            goldCollectedLabel.fontSize = 32;
            goldCollectedLabel.fontStyle = FontStyle.Bold;
            goldCollectedLabel.alignment = TextAnchor.MiddleRight;
            goldCollectedLabel.color = Color.white;
            goldCollectedLabel.raycastTarget = false;
            goldCollectedLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            goldCollectedLabel.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
        }

        // Шаг сбора лута: видна NextButton («Следующий!»/«Подвести итоги дня»), кнопки итогов скрыты.
        public void EnterAdventurerButtons()
        {
            EnsureRewardUi();
            if (NextButton != null) NextButton.gameObject.SetActive(true);
            if (startNextDayButton != null) startNextDayButton.gameObject.SetActive(false);
            if (challengeButton != null) challengeButton.gameObject.SetActive(false);
        }

        // Шаг награды: NextButton скрыта, на её месте «Начать следующий день» и «Бросить вызов».
        public void ShowRewardButtons(System.Action onNextDay, bool nextDayEnabled, System.Action onChallenge)
        {
            EnsureRewardUi();
            if (NextButton != null) NextButton.gameObject.SetActive(false);
            if (startNextDayButton != null)
            {
                startNextDayButton.gameObject.SetActive(true);
                startNextDayButton.interactable = nextDayEnabled;
                startNextDayButton.onClick.RemoveAllListeners();
                startNextDayButton.onClick.AddListener(() => onNextDay?.Invoke());
            }
            if (challengeButton != null)
            {
                challengeButton.gameObject.SetActive(true);
                challengeButton.onClick.RemoveAllListeners();
                challengeButton.onClick.AddListener(() => onChallenge?.Invoke());
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

            if (goldCollectedLabel != null)
            {
                int collected = ctx.Resources.TotalGold;
                int quota = ctx.Resources.DayQuota;
                goldCollectedLabel.text = $"Собрано золота\n{collected} / {quota}";
                goldCollectedLabel.color = collected >= quota ? GoldOkColor : GoldShortColor;
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
