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

        [Header("Style")]
        public Vector2 CursorOffset = new Vector2(24, 24);
        public int NameFontSize = 26;
        public int DescriptionFontSize = 18;
        public int StatFontSize = 20;
        public int AdjacencyFontSize = 18;
        public Color BoostColor = new Color(0.45f, 0.95f, 0.45f, 1f);
        public Color PenaltyColor = new Color(0.95f, 0.45f, 0.45f, 1f);

        private void Awake()
        {
            Instance = this;
            EnsureLayout();
            if (Panel != null) Panel.gameObject.SetActive(false);
        }

        // Ensures the tooltip Panel has the expected child Text fields. If the
        // prefab didn't wire them up (or has bad pivots), this fixes it at runtime.
        private void EnsureLayout()
        {
            if (Panel == null) return;
            Panel.pivot = new Vector2(0, 1); // top-left so OffsetX/Y go down-right from cursor
            var bg = Panel.GetComponent<Image>();
            if (bg != null && bg.color.a < 0.1f) bg.color = new Color(0.05f, 0.05f, 0.10f, 0.92f);
            // Make background non-blocking so hovered item still receives events
            if (bg != null) bg.raycastTarget = false;

            NameText        = NameText        != null ? NameText        : CreateText("NameText",        NameFontSize,        FontStyle.Bold);
            DescriptionText = DescriptionText != null ? DescriptionText : CreateText("DescriptionText", DescriptionFontSize, FontStyle.Italic);
            GoldText        = GoldText        != null ? GoldText        : CreateText("GoldText",        StatFontSize,        FontStyle.Normal);
            AcidText        = AcidText        != null ? AcidText        : CreateText("AcidText",        StatFontSize,        FontStyle.Normal);
            AdjacencyText   = AdjacencyText   != null ? AdjacencyText   : CreateText("AdjacencyText",   AdjacencyFontSize,   FontStyle.Normal);

            // Use a VerticalLayoutGroup if Panel doesn't already manage layout — keeps
            // the text rows stacked even with auto-created children.
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

            // Ensure all texts are children of Panel and in the right order.
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
            t.font = t.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        private void EnforceParent(Text t)
        {
            if (t == null) return;
            if (t.transform.parent != Panel) t.transform.SetParent(Panel, false);
        }

        private void Update()
        {
            if (Panel == null || !Panel.gameObject.activeSelf) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Panel.parent, mouse.position.ReadValue(), UiCamera, out var local))
                Panel.anchoredPosition = local + CursorOffset;
        }

        public void Show(LootView item)
        {
            if (Panel == null || item == null || item.Data == null) return;
            Panel.gameObject.SetActive(true);

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
        }

        private string FormatStat(string label, int baseVal, int effective, string unit, bool betterIsHigher)
        {
            if (baseVal == effective) return $"{label}: {baseVal} {unit}";
            // Color the effective value based on direction
            bool boost = betterIsHigher ? (effective > baseVal) : (effective < baseVal);
            string color = ColorUtility.ToHtmlStringRGB(boost ? BoostColor : PenaltyColor);
            return $"{label}: <color=#{color}>{effective}</color> {unit} <color=#888>(база {baseVal})</color>";
        }

        private string DescribeAdjacency(LootData data)
        {
            if (string.IsNullOrEmpty(data.AdjacencyTarget) ||
                data.AdjacencyEffects == null || data.AdjacencyEffects.Length == 0) return "";

            string targetName = LookupName(data.AdjacencyTarget);
            var sb = new StringBuilder();
            sb.Append($"Рядом с «{targetName}»:");
            foreach (var fx in data.AdjacencyEffects)
            {
                string kind = fx.Type == EffectType.Gold ? "цена" : "стоимость переваривания";
                string sign = fx.Multiplier >= 0 ? "+" : "";
                int pct = Mathf.RoundToInt(fx.Multiplier * 100f);
                sb.Append($"\n   • {kind} {sign}{pct}%");
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
            // Adjacency is active when LastResolved boosted/reduced the value
            // relative to base. Crude but correct without exposing the resolver internals.
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
            if (Panel != null) Panel.gameObject.SetActive(false);
        }
    }
}
