using System.Collections.Generic;
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
            Label.font = FontProvider.Default;
            Label.fontSize = LabelFontSize;
            Label.color = ColorForGroup(Data?.Group);
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

        // Цвет названия по группе (сету): у одной группы — один цвет. Цвет назначается
        // палитрой в порядке первого появления группы (стабильно, без коллизий до 7 групп).
        // Предметы без группы (пустое поле group в loot.csv) — белое имя.
        private static readonly System.Collections.Generic.Dictionary<string, Color> groupColors =
            new System.Collections.Generic.Dictionary<string, Color>();
        private static int nextGroupColor;

        public static Color ColorForGroup(string group)
        {
            if (string.IsNullOrEmpty(group)) return Color.white;
            if (!groupColors.TryGetValue(group, out var color))
            {
                color = Palette[nextGroupColor % Palette.Length];
                groupColors[group] = color;
                nextGroupColor++;
            }
            return color;
        }

        // Per-shape-cell highlight overlays, indexed by pattern (r, c).
        // null at positions where the shape has no cell.
        private Image[,] cellHighlightImages;
        private GameObject artOverlay; // инстанс арт-префаба предмета поверх формы

        private static readonly Dictionary<string, GameObject> artPrefabCache = new Dictionary<string, GameObject>();
        private static Sprite cellFullSprite;
        private static bool cellFullLoaded;

        // Арт-префаб предмета (Resources/Art/Loot/{id}.prefab). Кэшируем; null если префаба нет.
        // Визуал (спрайт/масштаб/сдвиг/эффекты) настраивается художником прямо в префабе.
        private static GameObject LoadArtPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (artPrefabCache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Loot/" + id);
            artPrefabCache[id] = p;
            return p;
        }

        // Нейтральный «занятый слот» для предметов с артом.
        private static Sprite LoadCellFull()
        {
            if (!cellFullLoaded) { cellFullSprite = Resources.Load<Sprite>("Art/cell_full"); cellFullLoaded = true; }
            return cellFullSprite;
        }

        private void BuildCells()
        {
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);
            artOverlay = null;

            // Фикстуры (сердце/желудок) «вшиты в панель»: только спрайт, без клеток,
            // подсветки, тултипа и интерактивности.
            bool fixture = Data != null && Data.IsFixture;
            // Корневой Image прозрачный и накрывает весь bbox формы (включая пустые клетки) —
            // если он ловит клики, предмет «крадёт» клики по своим пустым клеткам у соседей.
            // Поэтому raycast делаем ТОЛЬКО на заполненных клетках (см. ниже), а корень глушим.
            var rootImg = GetComponent<Image>();
            if (rootImg != null) rootImg.raycastTarget = false;

            var artPrefab = LoadArtPrefab(Data?.Id);
            bool hasArt = artPrefab != null;
            var cellFull = hasArt ? LoadCellFull() : null;
            var color = ColorForId(Data?.Id);
            var cells = Data.Shape.GetRotatedCells(CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);

            var size = new Vector2(cols * CellSize, rows * CellSize);
            ((RectTransform)transform).sizeDelta = size;
            CellsRoot.sizeDelta = size;

            cellHighlightImages = new Image[rows, cols];

            // Слой 1 — фоны клеток (нейтральный слот с артом / цветная заливка без арта).
            // Для фикстур не строим — они часть панели.
            if (!fixture)
            {
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
                            img.sprite = cellFull;
                            img.color = cellFull != null ? Color.white : color;
                            img.raycastTarget = true; // клики по предмету ловят именно клетки формы
                        }
                        // Без Outline — рамку даёт сам спрайт клетки.
                        var outline = go.GetComponent<Outline>();
                        if (outline != null) Destroy(outline);
                    }
                }
            }

            // Слой 2 — арт-префаб предмета поверх фонов клеток.
            // Базовый (неповёрнутый) bounding box — арт вращаем вместе с формой.
            if (hasArt)
            {
                var baseCells = Data.Shape.GetRotatedCells(Rotation.Deg0);
                var baseSize = new Vector2(baseCells.GetLength(1) * CellSize, baseCells.GetLength(0) * CellSize);
                artOverlay = CreateArtOverlay(artPrefab, size, baseSize, (int)CurrentRotation);
            }

            // Слой 3 — подсветка размещения (зелёная/красная), всегда поверх арта.
            // Для фикстур не строим.
            if (!fixture)
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (!cells[r, c]) continue;
                        cellHighlightImages[r, c] = CreateHighlightCell(c * CellSize, (rows - 1 - r) * CellSize);
                    }
            }

            // Слой 4 — лейбл. С артом и у фикстур прячем (имя есть в тултипе).
            if (Label != null)
            {
                Label.gameObject.SetActive(!hasArt && !fixture);
                if (!hasArt && !fixture) Label.transform.SetAsLastSibling();
            }
        }

        // Обёртка арта: центрирована по форме, размер = bbox в ориентации Deg0,
        // повёрнута вместе с фигурой. Внутрь инстанцируется арт-префаб как есть —
        // художник настраивает спрайт/масштаб/сдвиг/эффекты прямо в префабе.
        // Префаб с Image на всю обёртку (stretch + preserveAspect) даёт авто-вписывание.
        // footprint — повёрнутый bbox (для центра), baseSize — bbox в Deg0 (размер обёртки).
        private GameObject CreateArtOverlay(GameObject prefab, Vector2 footprint, Vector2 baseSize, int rotSteps)
        {
            var go = new GameObject("Art", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(CellsRoot, worldPositionStays: false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = baseSize;
            rt.localRotation = Quaternion.Euler(0, 0, -90f * (rotSteps & 3)); // по часовой
            rt.anchoredPosition = footprint * 0.5f;

            var inst = Instantiate(prefab, rt, false); // сохраняет локальный трансформ префаба
            // Арт не перехватывает клики — взаимодействие идёт по клеткам/корню.
            foreach (var g in inst.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            return go;
        }

        // Подсветка одной клетки — отдельным верхним слоем в CellsRoot (поверх арта).
        private Image CreateHighlightCell(float x, float y)
        {
            var hgo = new GameObject("Highlight",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var hrt = (RectTransform)hgo.transform;
            hrt.SetParent(CellsRoot, worldPositionStays: false);
            hrt.anchorMin = hrt.anchorMax = new Vector2(0, 0);
            hrt.pivot = new Vector2(0, 0);
            hrt.sizeDelta = new Vector2(CellSize, CellSize);
            hrt.anchoredPosition = new Vector2(x, y);
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
            // Пока тащим — не перехватываем клики/ховер, чтобы клетки грида и предметы
            // под курсором были доступны (drop/hover считаются вручную в DragController).
            cg.blocksRaycasts = !carried;
        }

        public void OnPointerDown(PointerEventData ev)
        {
            // Only left-click is handled via EventSystem (reliable). Right-click for
            // the context menu is polled directly in DragController.Update because the
            // new Input System UI module doesn't always forward RMB pointer events.
            if (ev.button == PointerEventData.InputButton.Left)
                DragController.Instance?.OnLootClicked(this);
        }

        public void OnPointerEnter(PointerEventData ev)
        {
            if (Data != null && Data.IsFixture) return; // фикстуры — часть панели, без тултипа
            TooltipController.Instance?.Show(this);
        }
        public void OnPointerExit(PointerEventData ev) => TooltipController.Instance?.Hide();
    }
}
