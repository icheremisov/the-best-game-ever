using UnityEngine;
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

        public GridModel<LootView> Model { get; private set; }
        public RectTransform[,] CellRects { get; private set; }

        private void Awake()
        {
            Model = new GridModel<LootView>(Width, Height);
            CellRects = new RectTransform[Width, Height];
            BuildCells();
        }

        private void BuildCells()
        {
            // Clear existing
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

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
                    // Make grid cells visibly bordered + slightly darker fill so the grid is readable
                    var img = cell.GetComponent<UnityEngine.UI.Image>();
                    if (img != null) img.color = new Color(0.18f, 0.18f, 0.22f, 1f);
                    var outline = cell.GetComponent<UnityEngine.UI.Outline>();
                    if (outline == null) outline = cell.AddComponent<UnityEngine.UI.Outline>();
                    outline.effectColor = new Color(0.45f, 0.45f, 0.50f, 1f);
                    outline.effectDistance = new Vector2(1, -1);
                    CellRects[x, y] = rt;
                }
            }
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
