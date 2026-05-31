using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Game;

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

            if (HostCanvas == null) HostCanvas = FindObjectOfType<Canvas>();
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
            NameText.color = ColorForId(data.Id);
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

            string adjacencyDesc = DescribeAdjacency(data);
            if (string.IsNullOrEmpty(adjacencyDesc))
            {
                AdjacencyText.gameObject.SetActive(false);
            }
            else
            {
                AdjacencyText.gameObject.SetActive(true);
                bool active = inMimic && AdjacencyActive(item);
                AdjacencyText.text = (active ? "✓ " : "○ ") + adjacencyDesc;
                AdjacencyText.color = active ? BoostColor : Color.gray;
            }

            // First-frame placement (LateUpdate will keep it pinned every frame after).
            PositionRightOf(item);

            if (VerboseLogs) Debug.Log($"[Tooltip] Show {data.Id} (inMimic={inMimic})");
        }

        private string FormatStat(string label, int baseVal, int effective, string unit, bool betterIsHigher)
        {
            if (baseVal == effective) return $"{label}: {baseVal} {unit}";
            bool boost = betterIsHigher ? (effective > baseVal) : (effective < baseVal);
            string color = ColorUtility.ToHtmlStringRGB(boost ? BoostColor : PenaltyColor);
            return $"{label}: <color=#{color}>{effective}</color> {unit} <color=#888888>(база {baseVal})</color>";
        }

        private string DescribeAdjacency(LootData data)
        {
            if (data.AdjacencyRules == null || data.AdjacencyRules.Length == 0) return "";

            var sb = new StringBuilder();
            foreach (var rule in data.AdjacencyRules)
            {
                if (rule.Effects == null || rule.Effects.Length == 0) continue;
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
                sb.Append($"Рядом с {who}:");

                foreach (var fx in rule.Effects)
                {
                    string kind = fx.Type == EffectType.Gold ? "цена" : "стоимость переваривания";
                    string sign = fx.Multiplier >= 0 ? "+" : "";
                    int pct = Mathf.RoundToInt(fx.Multiplier * 100f);
                    string per = fx.Stackable ? " за каждый" : "";
                    sb.Append($"\n   • {kind} {sign}{pct}%{per}");
                }
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

        private static bool AdjacencyActive(LootView item)
        {
            var resolved = GameContext.Instance?.LastResolved;
            if (resolved == null) return false;
            int g = resolved.GetGold(item);
            int a = resolved.GetAcid(item);
            return g != item.Data.Gold || a != item.Data.AcidCost;
        }

        private static readonly Color[] HashPalette =
        {
            new Color(0.95f, 0.55f, 0.55f, 1f),
            new Color(0.55f, 0.80f, 0.95f, 1f),
            new Color(0.55f, 0.90f, 0.65f, 1f),
            new Color(0.95f, 0.85f, 0.45f, 1f),
            new Color(0.80f, 0.65f, 0.95f, 1f),
        };
        private static Color ColorForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return Color.white;
            int hash = 0;
            foreach (var ch in id) hash = (hash * 31 + ch) & 0x7FFFFFFF;
            return HashPalette[hash % HashPalette.Length];
        }

        public void Hide()
        {
            trackedItem = null;
            if (Panel != null) Panel.gameObject.SetActive(false);
        }
    }
}
