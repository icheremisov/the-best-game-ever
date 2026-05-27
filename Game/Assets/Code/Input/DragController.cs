using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Mimic.Logic;
using Mimic.UI;
using Mimic.Game;

namespace Mimic.Input
{
    public class DragController : MonoBehaviour
    {
        public static DragController Instance { get; private set; }

        [Header("References")]
        public GridView MimicGrid;
        public GridView AdventurerGrid;
        public RectTransform DragLayer;
        public Camera UiCamera; // can be null if Canvas is ScreenSpaceOverlay

        [Header("Style")]
        public float CarriedAlpha = 0.5f;
        public Color HighlightFreeColor = new Color(0.30f, 0.85f, 0.30f, 0.65f);
        public Color HighlightBlockedColor = new Color(0.85f, 0.20f, 0.20f, 0.65f);

        [Header("Debug")]
        public bool VerboseLogs = true;

        public LootView Held { get; private set; }

        private GridView originGrid;
        private int originX, originY;
        private Rotation originRot;
        // Frame number when Pick happened — used to ignore the same-frame LMB press
        // (which already fired OnPointerDown → Pick via EventSystem) so Update() doesn't
        // treat it as an immediate Drop.
        private int pickedFrame = -1;
        // Offset (in grid cells) from the shape's bottom-left to the cell the user clicked.
        // Keeps the originally-clicked cell of the shape stuck to the cursor during drag.
        private int pickOffsetX, pickOffsetY;

        // Last hover state computed during UpdateHighlight.
        // Drop uses this snapshot so there's no frame-lag mismatch.
        private GridView hoverGrid;
        private int hoverX, hoverY; // placement origin (bottom-left), with pick offset already applied
        private bool hoverCanPlace;
        private int loggedHoverX = int.MinValue, loggedHoverY = int.MinValue;
        private GridView loggedHoverGrid;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (Held == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var mouseScreen = mouse.position.ReadValue();

            UpdateHighlight(mouseScreen);
            FollowCursor(mouseScreen);

            // Same-frame LMB press = the click that picked the item up; don't process it as drop.
            if (Time.frameCount == pickedFrame) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (hoverGrid != null && hoverCanPlace)
                {
                    if (VerboseLogs)
                        Debug.Log($"[Drag] DROP click → {hoverGrid.name} cell=({hoverX},{hoverY}) rot={Held.CurrentRotation}");
                    TryDropAt(hoverGrid, hoverX, hoverY);
                }
                else if (VerboseLogs)
                {
                    Debug.Log($"[Drag] DROP click ignored — hover={(hoverGrid != null ? hoverGrid.name : "none")} canPlace={hoverCanPlace}");
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                if (VerboseLogs) Debug.Log("[Drag] CANCEL via RMB");
                Cancel();
            }
        }

        private void FollowCursor(Vector2 mouseScreen)
        {
            // Snap-to-grid: place item so its bottom-left coincides with the placement origin
            // cell in WORLD space. With pick offset applied, the originally-clicked cell of
            // the shape ends up exactly under the cursor.
            if (hoverGrid != null
                && hoverX >= 0 && hoverX < hoverGrid.Width
                && hoverY >= 0 && hoverY < hoverGrid.Height)
            {
                var cellRt = hoverGrid.CellRects[hoverX, hoverY];
                Held.transform.position = cellRt.position;
                return;
            }

            // Free-follow when cursor isn't over any grid (or placement origin is out of bounds).
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(DragLayer, mouseScreen, UiCamera, out var worldPos))
                Held.transform.position = worldPos;
        }

        public void OnLootClicked(LootView item)
        {
            if (Held == null) Pick(item);
            else TryDrop(item);
        }

        public void OnLootRightClicked(LootView item)
        {
            if (Held != null) Cancel();
            else ContextMenuController.Instance?.Open(item);
        }

        public void OnEmptyCellClicked(GridView grid, int x, int y)
        {
            if (Held != null) TryDropAt(grid, x, y);
        }

        public void Pick(LootView item)
        {
            var grid = FindGridContaining(item);
            if (grid == null)
            {
                if (VerboseLogs) Debug.LogWarning($"[Drag] Pick failed — {item.name} not in any grid model");
                return;
            }
            grid.Model.TryGetPlacement(item, out originX, out originY, out originRot);
            originGrid = grid;

            // Compute pick offset: which cell of the shape was under the cursor at click time.
            // (clickCell.x - originX, clickCell.y - originY) — both in grid coords.
            pickOffsetX = 0;
            pickOffsetY = 0;
            var mouseNow = Mouse.current;
            if (mouseNow != null
                && grid.ScreenToCell(mouseNow.position.ReadValue(), UiCamera, out int clickX, out int clickY))
            {
                pickOffsetX = clickX - originX;
                pickOffsetY = clickY - originY;
            }

            grid.Model.Remove(item);

            Held = item;
            pickedFrame = Time.frameCount;
            item.SetCarried(true, CarriedAlpha);
            item.transform.SetParent(DragLayer, worldPositionStays: false);

            if (VerboseLogs)
                Debug.Log($"[Drag] PICK {item.Data?.Id ?? item.name} from {grid.name} at ({originX},{originY}) rot={originRot} offset=({pickOffsetX},{pickOffsetY})");

            // Force immediate position update so the picked item appears under the cursor
            // on the same frame, not next-frame.
            if (mouseNow != null)
            {
                var ms = mouseNow.position.ReadValue();
                UpdateHighlight(ms);
                FollowCursor(ms);
            }
        }

        public void Cancel()
        {
            if (Held == null) return;
            Held.CurrentRotation = originRot;
            originGrid.Model.TryPlace(Held, originX, originY, originRot);
            SnapToGrid(Held, originGrid, originX, originY);
            Held.SetCarried(false, 1f);
            if (VerboseLogs)
                Debug.Log($"[Drag] CANCEL → returned {Held.Data?.Id} to {originGrid.name} ({originX},{originY})");
            Held = null;
            hoverGrid = null;
            ClearHighlight();
        }

        private void TryDrop(LootView clickedItem)
        {
            // Click hit another item — try to drop onto current hover cell (already computed).
            if (hoverGrid != null && hoverCanPlace) TryDropAt(hoverGrid, hoverX, hoverY);
        }

        private void TryDropAt(GridView grid, int x, int y)
        {
            if (grid == AdventurerGrid && originGrid == MimicGrid)
            {
                if (VerboseLogs) Debug.Log("[Drag] DROP rejected — can't move from mimic to adventurer grid");
                return;
            }

            if (grid.Model.TryPlace(Held, x, y, Held.CurrentRotation))
            {
                Held.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
                SnapToGrid(Held, grid, x, y);
                Held.SetCarried(false, 1f);
                if (VerboseLogs)
                    Debug.Log($"[Drag] DROP OK → {grid.name} ({x},{y}) rot={Held.CurrentRotation}");
                Held = null;
                hoverGrid = null;
                ClearHighlight();
                GameContext.Instance?.OnGridChanged();
            }
            else
            {
                if (VerboseLogs)
                    Debug.LogWarning($"[Drag] TryPlace failed at {grid.name} ({x},{y}) rot={Held.CurrentRotation}");
            }
        }

        private void UpdateHighlight(Vector2 screenPos)
        {
            ClearHighlight();
            hoverGrid = null;

            var grid = ScreenOverGrid(screenPos);
            if (grid == null) return;
            if (!grid.ScreenToCell(screenPos, UiCamera, out int cursorX, out int cursorY)) return;

            // Placement origin = cursor cell minus the offset, so the originally-clicked cell
            // of the shape lands back under the cursor.
            int placeX = cursorX - pickOffsetX;
            int placeY = cursorY - pickOffsetY;

            var cells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);

            // Per-cell check: each occupied shape-cell is green if its target grid-cell is
            // both in-bounds and empty; red otherwise. allFree = whole footprint fits.
            bool allFree = true;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    int gx = placeX + c;
                    int gy = placeY + (rows - 1 - r); // match GridModel Y-flip
                    bool inBounds = gx >= 0 && gx < grid.Width && gy >= 0 && gy < grid.Height;
                    bool free = inBounds && grid.Model.GetAt(gx, gy) == null;
                    if (!free) allFree = false;

                    if (inBounds)
                        SetCellHighlight(grid, gx, gy, free ? HighlightFreeColor : HighlightBlockedColor);
                }
            }

            hoverGrid = grid;
            hoverX = placeX;
            hoverY = placeY;
            hoverCanPlace = allFree;

            if (VerboseLogs && (loggedHoverGrid != grid || loggedHoverX != placeX || loggedHoverY != placeY))
            {
                Debug.Log($"[Drag] HOVER {grid.name} cursor=({cursorX},{cursorY}) place=({placeX},{placeY}) canPlace={allFree} screenPos={screenPos}");
                loggedHoverGrid = grid;
                loggedHoverX = placeX;
                loggedHoverY = placeY;
            }
        }

        private void ClearHighlight()
        {
            if (MimicGrid != null) ClearGridHighlight(MimicGrid);
            if (AdventurerGrid != null) ClearGridHighlight(AdventurerGrid);
        }

        private void ClearGridHighlight(GridView grid)
        {
            if (grid.CellRects == null) return;
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var rt = grid.CellRects[x, y];
                    if (rt == null) continue;
                    var h = rt.Find("Highlight");
                    if (h != null)
                    {
                        var img = h.GetComponent<UnityEngine.UI.Image>();
                        if (img != null) img.color = new Color(0, 0, 0, 0);
                    }
                }
        }

        private void SetCellHighlight(GridView grid, int x, int y, Color color)
        {
            var rt = grid.CellRects[x, y];
            if (rt == null) return;
            var h = rt.Find("Highlight");
            if (h == null)
            {
                // Auto-create highlight overlay if the CellPrefab didn't provide one.
                var go = new GameObject("Highlight",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                var hr = (RectTransform)go.transform;
                hr.SetParent(rt, worldPositionStays: false);
                hr.anchorMin = Vector2.zero;
                hr.anchorMax = Vector2.one;
                hr.offsetMin = Vector2.zero;
                hr.offsetMax = Vector2.zero;
                var img2 = go.GetComponent<UnityEngine.UI.Image>();
                img2.raycastTarget = false;
                img2.color = color;
                return;
            }
            var img = h.GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;
            img.color = color;
        }

        private GridView ScreenOverGrid(Vector2 screenPos)
        {
            if (MimicGrid.ScreenToCell(screenPos, UiCamera, out _, out _)) return MimicGrid;
            if (AdventurerGrid.ScreenToCell(screenPos, UiCamera, out _, out _)) return AdventurerGrid;
            return null;
        }

        private GridView FindGridContaining(LootView item)
        {
            foreach (var i in MimicGrid.Model.AllItems()) if (i == item) return MimicGrid;
            foreach (var i in AdventurerGrid.Model.AllItems()) if (i == item) return AdventurerGrid;
            return null;
        }

        private void SnapToGrid(LootView item, GridView grid, int x, int y)
        {
            var rt = (RectTransform)item.transform;
            rt.SetParent(grid.CellsRoot, worldPositionStays: false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            var cellRt = grid.CellRects[x, y];
            rt.position = cellRt.position;
        }
    }
}
