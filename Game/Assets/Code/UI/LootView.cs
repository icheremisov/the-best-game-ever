using Mimic.Data;
using Mimic.Logic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mimic.Input;

namespace Mimic.UI
{
    public class LootView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public LootData Data { get; private set; }
        public Shape Shape => Data?.Shape;
        public Rotation CurrentRotation = Rotation.Deg0;

        [Header("References")]
        public RectTransform CellsRoot;
        public GameObject CellPrefab;
        public Text Label;

        [Header("Style")]
        public float CellSize = 64f;
        public int LabelFontSize = 22;

        public void Bind(LootData data)
        {
            Data = data;
            // Force layout consistency — prefab children may have inherited Unity defaults
            // (pivot 0.5/0.5, sizeDelta 100/100) that misalign positioning math.
            NormalizeLayout();
            if (Label != null) ConfigureLabel(data.Name);
            BuildCells();
        }

        private void NormalizeLayout()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);

            // The root LootItem prefab carries a solid-white Image — it shows through
            // wherever the shape has empty (".") cells. Hide it; only the per-cell
            // Images created in BuildCells should be visible.
            var rootImg = GetComponent<Image>();
            if (rootImg != null) rootImg.color = new Color(0, 0, 0, 0);

            if (CellsRoot != null)
            {
                CellsRoot.anchorMin = CellsRoot.anchorMax = new Vector2(0, 0);
                CellsRoot.pivot = new Vector2(0, 0);
                CellsRoot.anchoredPosition = Vector2.zero;

                // Same for the CellsRoot Image if it has one.
                var cellsRootImg = CellsRoot.GetComponent<Image>();
                if (cellsRootImg != null) cellsRootImg.color = new Color(0, 0, 0, 0);
            }
        }

        private void ConfigureLabel(string text)
        {
            var lt = (RectTransform)Label.transform;
            lt.anchorMin = Vector2.zero;
            lt.anchorMax = Vector2.one;
            lt.offsetMin = Vector2.zero;
            lt.offsetMax = Vector2.zero;
            Label.text = text;
            Label.fontSize = LabelFontSize;
            Label.color = Color.white;
            Label.alignment = TextAnchor.MiddleCenter;
            Label.fontStyle = FontStyle.Bold;
            Label.raycastTarget = false;
            var outline = Label.gameObject.GetComponent<Outline>();
            if (outline == null) outline = Label.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
            Label.transform.SetAsLastSibling();
        }

        private static readonly Color[] Palette =
        {
            new Color(0.85f, 0.30f, 0.30f, 1f), // red
            new Color(0.30f, 0.65f, 0.85f, 1f), // blue
            new Color(0.30f, 0.75f, 0.40f, 1f), // green
            new Color(0.90f, 0.70f, 0.20f, 1f), // gold
            new Color(0.65f, 0.45f, 0.85f, 1f), // purple
            new Color(0.95f, 0.55f, 0.30f, 1f), // orange
            new Color(0.50f, 0.85f, 0.85f, 1f), // teal
        };

        private Color ColorForId(string id)
        {
            if (string.IsNullOrEmpty(id)) return Palette[0];
            int hash = 0;
            foreach (var ch in id) hash = (hash * 31 + ch) & 0x7FFFFFFF;
            return Palette[hash % Palette.Length];
        }

        // Per-shape-cell highlight overlays, indexed by pattern (r, c).
        // null at positions where the shape has no cell.
        private Image[,] cellHighlightImages;

        private void BuildCells()
        {
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

            var color = ColorForId(Data?.Id);
            var cells = Data.Shape.GetRotatedCells(CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);

            var size = new Vector2(cols * CellSize, rows * CellSize);
            ((RectTransform)transform).sizeDelta = size;
            CellsRoot.sizeDelta = size;

            cellHighlightImages = new Image[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    var go = Instantiate(CellPrefab, CellsRoot);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(CellSize, CellSize);
                    rt.anchoredPosition = new Vector2(c * CellSize, (rows - 1 - r) * CellSize);
                    var img = go.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = null;
                        img.color = color;
                    }
                    var outline = go.GetComponent<Outline>();
                    if (outline == null) outline = go.AddComponent<Outline>();
                    outline.effectColor = new Color(0, 0, 0, 0.8f);
                    outline.effectDistance = new Vector2(2, -2);

                    // Highlight overlay on top of the cell — painted during drag,
                    // ALWAYS rendered above the colored cell (and below the Label).
                    cellHighlightImages[r, c] = CreateHighlightChild(go.transform);
                }
            }
            // Label stays at the very top (above all cells and highlights)
            if (Label != null) Label.transform.SetAsLastSibling();
        }

        private static Image CreateHighlightChild(Transform parent)
        {
            var hgo = new GameObject("Highlight",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var hrt = (RectTransform)hgo.transform;
            hrt.SetParent(parent, worldPositionStays: false);
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            var img = hgo.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(0, 0, 0, 0);
            return img;
        }

        public void SetCellHighlight(int r, int c, Color color)
        {
            if (cellHighlightImages == null) return;
            int rows = cellHighlightImages.GetLength(0);
            int cols = cellHighlightImages.GetLength(1);
            if (r < 0 || r >= rows || c < 0 || c >= cols) return;
            var img = cellHighlightImages[r, c];
            if (img != null) img.color = color;
        }

        public void ClearAllHighlights()
        {
            if (cellHighlightImages == null) return;
            int rows = cellHighlightImages.GetLength(0);
            int cols = cellHighlightImages.GetLength(1);
            var clear = new Color(0, 0, 0, 0);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (cellHighlightImages[r, c] != null) cellHighlightImages[r, c].color = clear;
        }

        public void Rotate(bool clockwise)
        {
            int v = (int)CurrentRotation + (clockwise ? 1 : 3);
            CurrentRotation = (Rotation)(v % 4);
            BuildCells();
        }

        public void SetRotation(Rotation rotation)
        {
            if (CurrentRotation == rotation) return;
            CurrentRotation = rotation;
            BuildCells();
        }

        // Visual "being dragged" state — semitransparent so the player can see
        // the green/red placement highlight on cells beneath the held shape.
        public void SetCarried(bool carried, float carriedAlpha)
        {
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = carried ? carriedAlpha : 1f;
        }

        public void OnPointerDown(PointerEventData ev)
        {
            if (ev.button == PointerEventData.InputButton.Left)
                DragController.Instance?.OnLootClicked(this);
            else if (ev.button == PointerEventData.InputButton.Right)
                DragController.Instance?.OnLootRightClicked(this);
        }

        public void OnPointerEnter(PointerEventData ev) => TooltipController.Instance?.Show(this);
        public void OnPointerExit(PointerEventData ev) => TooltipController.Instance?.Hide();
    }
}
