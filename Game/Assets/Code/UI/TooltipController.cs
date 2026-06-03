using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Game;
using Mimic.Logic;

namespace Mimic.UI
{
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Header("References (auto-created if null)")]
        public RectTransform Panel;
        public Text NameText;
        public Text DescriptionText;
        public Text GoldText;
        public Text AcidText;
        public Text CombatText;
        public Text AdjacencyText;
        public Camera UiCamera;
        public Canvas HostCanvas; // parent for auto-created Panel

        [Header("Style")]
        public Vector2 ItemSideGap = new Vector2(12, 0); // pixel gap between item and tooltip
        public int NameFontSize = 26;
        public int DescriptionFontSize = 18;
        public int StatFontSize = 20;
        public int AdjacencyFontSize = 18;
        public Color BoostColor = new Color(0.45f, 0.95f, 0.45f, 1f);
        public Color PenaltyColor = new Color(0.95f, 0.45f, 0.45f, 1f);

        [Header("Debug")]
        public bool VerboseLogs = false;

        private void Awake()
        {
            Instance = this;
            EnsurePanel();
            EnsureLayout();
            if (Panel != null) Panel.gameObject.SetActive(false);
        }

        // If the prefab reference wasn't set or the Panel disappeared, build one
        // programmatically under the main Canvas so the tooltip always has somewhere to live.
        private void EnsurePanel()
        {
            if (Panel != null) return;

            if (HostCanvas == null) HostCanvas = FindFirstObjectByType<Canvas>();
            if (HostCanvas == null)
            {
                Debug.LogWarning("[Tooltip] No Canvas in scene — tooltip won't render");
                return;
            }

            var go = new GameObject("TooltipPanel_Auto",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(HostCanvas.transform, worldPositionStays: false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(280, 160);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.10f, 0.92f);
            img.raycastTarget = false;
            Panel = rt;
            // Make sure it draws above everything in the canvas
            Panel.SetAsLastSibling();
            if (VerboseLogs) Debug.Log("[Tooltip] Auto-created Panel under " + HostCanvas.name);
        }

        // Ensures the tooltip Panel has the expected child Text fields and pivot.
        private void EnsureLayout()
        {
            if (Panel == null) return;
            Panel.pivot = new Vector2(0, 1);
            var bg = Panel.GetComponent<Image>();
            if (bg != null)
            {
                if (bg.color.a < 0.1f) bg.color = new Color(0.05f, 0.05f, 0.10f, 0.92f);
                bg.raycastTarget = false;
            }

            NameText        = NameText        != null ? NameText        : CreateText("NameText",        NameFontSize,        FontStyle.Bold);
            DescriptionText = DescriptionText != null ? DescriptionText : CreateText("DescriptionText", DescriptionFontSize, FontStyle.Italic);
            GoldText        = GoldText        != null ? GoldText        : CreateText("GoldText",        StatFontSize,        FontStyle.Normal);
            AcidText        = AcidText        != null ? AcidText        : CreateText("AcidText",        StatFontSize,        FontStyle.Normal);
            CombatText      = CombatText      != null ? CombatText      : CreateText("CombatText",      StatFontSize,        FontStyle.Normal);
            AdjacencyText   = AdjacencyText   != null ? AdjacencyText   : CreateText("AdjacencyText",   AdjacencyFontSize,   FontStyle.Normal);

            if (Panel.GetComponent<LayoutGroup>() == null)
            {
                var vlg = Panel.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset(12, 12, 8, 8);
                vlg.spacing = 4;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;
                vlg.childAlignment = TextAnchor.UpperLeft;
            }
            if (Panel.GetComponent<ContentSizeFitter>() == null)
            {
                var csf = Panel.gameObject.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            EnforceParent(NameText);
            EnforceParent(DescriptionText);
            EnforceParent(GoldText);
            EnforceParent(AcidText);
            EnforceParent(CombatText);
            EnforceParent(AdjacencyText);
        }

        private Text CreateText(string name, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Panel, worldPositionStays: false);
            var t = go.GetComponent<Text>();
            t.color = Color.white;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.font = FontProvider.Default;
            return t;
        }

        private void EnforceParent(Text t)
        {
            if (t == null) return;
            if (t.transform.parent != Panel) t.transform.SetParent(Panel, false);
        }

        // Anchored to the hovered item, not the cursor — repositions every frame so
        // that if the item moves the tooltip follows it.
        private LootView trackedItem;

        private void LateUpdate()
        {
            if (Panel == null || !Panel.gameObject.activeSelf || trackedItem == null) return;
            PositionRightOf(trackedItem);
        }

        private void PositionRightOf(LootView item)
        {
            var itemRt = (RectTransform)item.transform;
            var corners = new Vector3[4];
            itemRt.GetWorldCorners(corners);
            // corners: 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right
            Vector3 topRight = corners[2];
            Vector3 topLeft = corners[1];

            // Force a layout pass so Panel.rect.width is up-to-date for the edge check.
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);
            float panelW = Panel.rect.width;
            float panelH = Panel.rect.height;

            // Default: place to the right of the item, top aligned to item top.
            Vector3 placeAt = topRight + new Vector3(ItemSideGap.x, ItemSideGap.y, 0);

            // If it would extend past the right edge of the screen, flip to the left side.
            if (placeAt.x + panelW > Screen.width)
                placeAt = topLeft + new Vector3(-ItemSideGap.x - panelW, ItemSideGap.y, 0);

            // If it would extend past the bottom edge, shift up.
            if (placeAt.y - panelH < 0)
                placeAt.y = panelH;

            Panel.position = placeAt;
        }

        public void Show(LootView item)
        {
            if (item == null || item.Data == null) return;
            if (Panel == null) { EnsurePanel(); EnsureLayout(); }
            if (Panel == null)
            {
                if (VerboseLogs) Debug.LogWarning("[Tooltip] Panel still null after EnsurePanel — cannot show");
                return;
            }

            Panel.gameObject.SetActive(true);
            Panel.SetAsLastSibling(); // draw above other UI
            trackedItem = item;

            var data = item.Data;
            NameText.text = data.Name;
            NameText.color = LootView.ColorForGroup(data.Group); // цвет имени по сету (как на подписи предмета)
            DescriptionText.text = string.IsNullOrEmpty(data.Description) ? "" : $"«{data.Description}»";
            DescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(data.Description));

            bool inMimic = IsItemInMimic(item);

            int effGold = inMimic && GameContext.Instance?.LastResolved != null
                ? GameContext.Instance.LastResolved.GetGold(item)
                : data.Gold;
            int effAcid = inMimic && GameContext.Instance?.LastResolved != null
                ? GameContext.Instance.LastResolved.GetAcid(item)
                : data.AcidCost;

            GoldText.text = FormatStat("Цена", data.Gold, effGold, "зол.", betterIsHigher: true);
            AcidText.text = FormatStat("Переварить", data.AcidCost, effAcid, "сока", betterIsHigher: false);

            string combatDesc = DescribeCombat(data);
            if (string.IsNullOrEmpty(combatDesc))
            {
                CombatText.gameObject.SetActive(false);
            }
            else
            {
                CombatText.gameObject.SetActive(true);
                CombatText.text = combatDesc;
                CombatText.color = Color.white; // цвета задаются пер-строкой через rich text
            }

            var ctx = GameContext.Instance;
            var neighborIds = (inMimic && ctx != null && ctx.MimicGrid != null && ctx.MimicGrid.Model != null)
                ? AdjacencyResolver.NeighborIds(ctx.MimicGrid.Model, item, v => v.Data.Id)
                : new System.Collections.Generic.HashSet<string>();
            bool[] activeRules = AdjacencyResolver.ActiveRules(data.AdjacencyRules, neighborIds);

            string adjacencyDesc = DescribeAdjacency(data, activeRules);
            if (string.IsNullOrEmpty(adjacencyDesc))
            {
                AdjacencyText.gameObject.SetActive(false);
            }
            else
            {
                AdjacencyText.gameObject.SetActive(true);
                AdjacencyText.text = adjacencyDesc;
                AdjacencyText.color = Color.white; // подсветка задаётся пер-правилом через rich text
            }

            // First-frame placement (LateUpdate will keep it pinned every frame after).
            PositionRightOf(item);

            if (VerboseLogs) Debug.Log($"[Tooltip] Show {data.Id} (inMimic={inMimic})");
        }

        // Боевые параметры: атака по врагу (бросок), урон врагу при переваривании,
        // и самоурон мимику при переваривании. Показываем только ненулевые.
        private string DescribeCombat(LootData d)
        {
            string boostHex = ColorUtility.ToHtmlStringRGB(BoostColor);
            string penaltyHex = ColorUtility.ToHtmlStringRGB(PenaltyColor);
            var sb = new StringBuilder();
            if (d.Attack > 0)
                sb.Append($"<color=#{boostHex}>Атака: {d.Attack}</color>");
            if (d.AttackOnDigest > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<color=#{boostHex}>Урон врагу при переваривании: {d.AttackOnDigest}</color>");
            }
            if (d.DamageOnDigest > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<color=#{penaltyHex}>Самоурон при переваривании: {d.DamageOnDigest}</color>");
            }
            return sb.ToString();
        }

        private string FormatStat(string label, int baseVal, int effective, string unit, bool betterIsHigher)
        {
            if (baseVal == effective) return $"{label}: {baseVal} {unit}";
            bool boost = betterIsHigher ? (effective > baseVal) : (effective < baseVal);
            string color = ColorUtility.ToHtmlStringRGB(boost ? BoostColor : PenaltyColor);
            return $"{label}: <color=#{color}>{effective}</color> {unit} <color=#888888>(база {baseVal})</color>";
        }

        // active[i] — активно ли правило i по фактическим соседям (есть подходящий сосед),
        // независимо от того, изменилась ли итоговая цена. Каждый блок подсвечивается отдельно.
        private string DescribeAdjacency(LootData data, bool[] active)
        {
            if (data.AdjacencyRules == null || data.AdjacencyRules.Length == 0) return "";

            string boostHex = ColorUtility.ToHtmlStringRGB(BoostColor);
            const string grayHex = "888888";

            var sb = new StringBuilder();
            for (int ri = 0; ri < data.AdjacencyRules.Length; ri++)
            {
                var rule = data.AdjacencyRules[ri];
                if (rule.Effects == null || rule.Effects.Length == 0) continue;
                bool on = active != null && ri < active.Length && active[ri];
                if (sb.Length > 0) sb.Append('\n');

                string who;
                if (rule.Wildcard)
                {
                    who = "прочими предметами";
                }
                else
                {
                    var names = new string[rule.Targets.Length];
                    for (int i = 0; i < rule.Targets.Length; i++) names[i] = LookupName(rule.Targets[i]);
                    who = "«" + string.Join("» или «", names) + "»";
                }

                string hex = on ? boostHex : grayHex;
                string mark = on ? "✓" : "○";
                sb.Append($"<color=#{hex}>{mark} Рядом с {who}:");
                foreach (var fx in rule.Effects)
                {
                    string kind = fx.Type == EffectType.Gold ? "цена" : "стоимость переваривания";
                    string sign = fx.Multiplier >= 0 ? "+" : "";
                    int pct = Mathf.RoundToInt(fx.Multiplier * 100f);
                    string per = fx.Stackable ? " за каждый" : "";
                    sb.Append($"\n   • {kind} {sign}{pct}%{per}");
                }
                sb.Append("</color>");
            }
            return sb.ToString();
        }

        private static string LookupName(string id)
        {
            if (LootCatalog.ById != null && LootCatalog.ById.TryGetValue(id, out var d)) return d.Name;
            return id;
        }

        private static bool IsItemInMimic(LootView item)
        {
            var ctx = GameContext.Instance;
            if (ctx == null || ctx.MimicGrid == null) return false;
            foreach (var i in ctx.MimicGrid.Model.AllItems()) if (i == item) return true;
            return false;
        }

        public void Hide()
        {
            trackedItem = null;
            if (Panel != null) Panel.gameObject.SetActive(false);
        }
    }
}
