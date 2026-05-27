using UnityEngine;
using UnityEngine.UI;
using Mimic.Logic;

namespace Mimic.UI
{
    public class GridView : MonoBehaviour
    {
        [Header("Grid size")]
        public int Width = 14;
        public int Height = 8;
        public float CellSize = 64f;

        [Header("References")]
        public RectTransform CellsRoot;
        public GameObject CellPrefab;

        [Header("Style")]
        public Color CellFillColor = new Color(0.18f, 0.18f, 0.22f, 1f);
        public Color CellBorderColor = new Color(0.45f, 0.45f, 0.50f, 1f);
        public int BorderPixels = 2;

        public GridModel<LootView> Model { get; private set; }
        public RectTransform[,] CellRects { get; private set; }

        private void Awake()
        {
            Model = new GridModel<LootView>(Width, Height);
            CellRects = new RectTransform[Width, Height];
            NormalizeCellsRoot();
            BuildCells();
        }

        // Make sure CellsRoot anchors/pivot are (0,0) so cell anchoredPositions
        // map straight to bottom-left of the grid. Prefab defaults may be wrong.
        private void NormalizeCellsRoot()
        {
            if (CellsRoot == null) return;
            CellsRoot.anchorMin = CellsRoot.anchorMax = new Vector2(0, 0);
            CellsRoot.pivot = new Vector2(0, 0);
            CellsRoot.sizeDelta = new Vector2(Width * CellSize, Height * CellSize);
        }

        private void BuildCells()
        {
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

            // Procedural cell sprite — opaque dark fill with a solid border on all 4 sides.
            // Independent of screen resolution / Outline component glitches.
            var cellSprite = CreateBorderedCellSprite((int)CellSize, BorderPixels, CellFillColor, CellBorderColor);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = Instantiate(CellPrefab, CellsRoot);
                    cell.name = $"Cell_{x}_{y}";
                    var rt = (RectTransform)cell.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(CellSize, CellSize);
                    rt.anchoredPosition = new Vector2(x * CellSize, y * CellSize);

                    var img = cell.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = cellSprite;
                        img.color = Color.white;
                        img.type = Image.Type.Simple;
                    }

                    // Remove any leftover Outline component — texture has the border.
                    var oldOutline = cell.GetComponent<Outline>();
                    if (oldOutline != null) Destroy(oldOutline);

                    CellRects[x, y] = rt;
                }
            }
        }

        private static Sprite cachedSprite;
        private static int cachedSize, cachedBorder;
        private static Color cachedFill, cachedBorderColor;

        private static Sprite CreateBorderedCellSprite(int size, int border, Color fill, Color borderColor)
        {
            if (cachedSprite != null && cachedSize == size && cachedBorder == border
                && cachedFill == fill && cachedBorderColor == borderColor)
                return cachedSprite;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[size * size];
            var fill32 = (Color32)fill;
            var border32 = (Color32)borderColor;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool onBorder = x < border || y < border || x >= size - border || y >= size - border;
                    pixels[y * size + x] = onBorder ? border32 : fill32;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            cachedSize = size;
            cachedBorder = border;
            cachedFill = fill;
            cachedBorderColor = borderColor;
            return cachedSprite;
        }

        public bool ScreenToCell(Vector2 screenPos, Camera cam, out int x, out int y)
        {
            x = y = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(CellsRoot, screenPos, cam, out var local))
                return false;
            int cx = Mathf.FloorToInt(local.x / CellSize);
            int cy = Mathf.FloorToInt(local.y / CellSize);
            if (cx < 0 || cx >= Width || cy < 0 || cy >= Height) return false;
            x = cx; y = cy;
            return true;
        }

        public Vector2 CellToLocal(int x, int y) => new Vector2(x * CellSize, y * CellSize);
    }
}
