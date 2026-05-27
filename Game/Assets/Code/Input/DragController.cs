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

        private void Awake()
        {
            Instance = this;
            // Force DragLayer to not block pointer events — otherwise OnPointerEnter
            // never fires on the items underneath (tooltip silently dies).
            if (DragLayer != null)
            {
                var img = DragLayer.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.raycastTarget = false;
            }
        }

        private void Update()
        {
            if (Held == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var mouseScreen = mouse.position.ReadValue();

            UpdateHighlight(mouseScreen);
            FollowCursor(mouseScreen);

            // Standard drag&drop: Pick happens on mouse-down (via EventSystem → OnPointerDown).
            // Drop happens on mouse-UP — release the button anywhere to commit or cancel.
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (hoverGrid != null && hoverCanPlace)
                {
                    if (VerboseLogs)
                        Debug.Log($"[Drag] DROP release → {hoverGrid.name} cell=({hoverX},{hoverY}) rot={Held.CurrentRotation}");
                    TryDropAt(hoverGrid, hoverX, hoverY);
                }
                else
                {
                    // Release in an invalid location → return the item to where it came from.
                    if (VerboseLogs)
                        Debug.Log($"[Drag] DROP invalid → return to origin (hover={(hoverGrid != null ? hoverGrid.name : "none")} canPlace={hoverCanPlace})");
                    Cancel();
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                // RMB during hold = explicit cancel.
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

            // Free-follow: shift item so the originally-clicked cell of the shape stays
            // exactly under the cursor (consistent with snap-to-grid behaviour).
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(DragLayer, mouseScreen, UiCamera, out var worldPos))
            {
                float cellSize = originGrid != null ? originGrid.CellSize : Held.CellSize;
                float worldCellSize = cellSize * DragLayer.lossyScale.x;
                Held.transform.position = worldPos - new Vector3(
                    pickOffsetX * worldCellSize,
                    pickOffsetY * worldCellSize,
                    0);
            }
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

        // Rotates the held item AND remaps pickOffset so the originally-grabbed cell
        // stays under the cursor (instead of the shape's bottom-left pivot).
        //
        // 90° CW : (ox, oy) → (oy, oldCols - 1 - ox)
        // 90° CCW: (ox, oy) → (oldRows - 1 - oy, ox)
        // where oldCols/oldRows are from the CURRENT rotation before this step,
        // and offsets are in grid cells from the shape's bottom-left.
        public void RotateHeld(bool clockwise)
        {
            if (Held == null) return;

            var oldCells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            int oldRows = oldCells.GetLength(0);
            int oldCols = oldCells.GetLength(1);
            int ox = pickOffsetX;
            int oy = pickOffsetY;

            Held.Rotate(clockwise);

            if (clockwise)
            {
                pickOffsetX = oy;
                pickOffsetY = oldCols - 1 - ox;
            }
            else
            {
                pickOffsetX = oldRows - 1 - oy;
                pickOffsetY = ox;
            }

            if (VerboseLogs)
                Debug.Log($"[Drag] ROTATE {(clockwise ? "CW" : "CCW")} → pickOffset=({pickOffsetX},{pickOffsetY}) rot={Held.CurrentRotation}");
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
            // SetRotation rebuilds cells so the visual matches originRot — without this
            // the shape stays in its last drag-time rotation even though Model placed it
            // back at originRot.
            Held.SetRotation(originRot);
            originGrid.Model.TryPlace(Held, originX, originY, originRot);
            SnapToGrid(Held, originGrid, originX, originY);
            Held.SetCarried(false, 1f);
            if (VerboseLogs)
                Debug.Log($"[Drag] CANCEL → returned {Held.Data?.Id} to {originGrid.name} ({originX},{originY}) rot={originRot}");
            Held.ClearAllHighlights();
            Held = null;
            hoverGrid = null;
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
                Held.ClearAllHighlights(); // clear shape-cell tints BEFORE losing the reference
                Held = null;
                hoverGrid = null;
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
            // Clear last frame's highlights on the held shape itself.
            Held.ClearAllHighlights();
            hoverGrid = null;

            var cells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);

            var grid = ScreenOverGrid(screenPos);
            if (grid == null) return;
            if (!grid.ScreenToCell(screenPos, UiCamera, out int cursorX, out int cursorY)) return;

            // Placement origin = cursor cell minus the offset, so the originally-clicked cell
            // of the shape lands back under the cursor.
            int placeX = cursorX - pickOffsetX;
            int placeY = cursorY - pickOffsetY;

            // Per-cell check: green if target grid-cell is in-bounds AND empty, red otherwise.
            // Highlights are painted ON the held shape so they're visible regardless of grid alpha.
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
                    Held.SetCellHighlight(r, c, free ? HighlightFreeColor : HighlightBlockedColor);
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
            if (Held != null) Held.ClearAllHighlights();
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
