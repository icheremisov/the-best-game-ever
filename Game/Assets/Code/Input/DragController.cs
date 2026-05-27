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

        // Last hover state computed during UpdateHighlight.
        // Drop uses this snapshot so there's no frame-lag mismatch.
        private GridView hoverGrid;
        private int hoverX, hoverY;
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
            // Without this gate the item would teleport to hover cell on the very same click.
            if (Time.frameCount == pickedFrame) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Use the locked hover from this frame's UpdateHighlight, NOT a recomputed
                // ScreenToCell — eliminates the 1-frame drift between highlight and drop.
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
            // Snap-to-grid follow: place item so its bottom-left pivot coincides with the
            // hovered cell's bottom-left pivot in WORLD space — this works regardless of
            // any parent pivot/anchor weirdness in the scene hierarchy.
            if (hoverGrid != null)
            {
                var cellRt = hoverGrid.CellRects[hoverX, hoverY];
                Held.transform.position = cellRt.position;
                return;
            }

            // Free-follow when cursor isn't over any grid.
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
            grid.Model.Remove(item);

            Held = item;
            pickedFrame = Time.frameCount;
            item.transform.SetParent(DragLayer, worldPositionStays: false);

            if (VerboseLogs)
                Debug.Log($"[Drag] PICK {item.Data?.Id ?? item.name} from {grid.name} at ({originX},{originY}) rot={originRot}");

            // Force immediate position update so the picked item appears under the cursor
            // on the same frame, not next-frame.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var ms = mouse.position.ReadValue();
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
            if (VerboseLogs)
                Debug.Log($"[Drag] CANCEL → returned {Held.Data?.Id} to {originGrid.name} ({originX},{originY})");
            Held = null;
            hoverGrid = null;
            ClearHighlight();
        }

        private void TryDrop(LootView clickedItem)
        {
            // Click hit another item — drop onto its origin cell.
            var grid = FindGridContaining(clickedItem);
            if (grid != null)
            {
                grid.Model.TryGetPlacement(clickedItem, out var x, out var y, out _);
                TryDropAt(grid, x, y);
            }
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
            if (!grid.ScreenToCell(screenPos, UiCamera, out int x, out int y)) return;

            bool canPlace = grid.Model.TryPlace(Held, x, y, Held.CurrentRotation);
            if (canPlace) grid.Model.Remove(Held);

            hoverGrid = grid;
            hoverX = x;
            hoverY = y;
            hoverCanPlace = canPlace;

            // Log only when hover cell or grid changes — avoids spamming every frame.
            if (VerboseLogs && (loggedHoverGrid != grid || loggedHoverX != x || loggedHoverY != y))
            {
                Debug.Log($"[Drag] HOVER {grid.name} cell=({x},{y}) canPlace={canPlace} screenPos={screenPos}");
                loggedHoverGrid = grid;
                loggedHoverX = x;
                loggedHoverY = y;
            }

            var cells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    int cx = x + c, cy = y + r;
                    if (cx < 0 || cx >= grid.Width || cy < 0 || cy >= grid.Height) continue;
                    SetCellHighlight(grid, cx, cy, canPlace ? Color.green : Color.red, 0.4f);
                }
            }
        }

        private void ClearHighlight()
        {
            ClearGridHighlight(MimicGrid);
            ClearGridHighlight(AdventurerGrid);
        }

        private void ClearGridHighlight(GridView grid)
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var h = grid.CellRects[x, y].Find("Highlight");
                    if (h != null)
                    {
                        var img = h.GetComponent<UnityEngine.UI.Image>();
                        if (img != null) img.color = new Color(0, 0, 0, 0);
                    }
                }
        }

        private void SetCellHighlight(GridView grid, int x, int y, Color color, float alpha)
        {
            var h = grid.CellRects[x, y].Find("Highlight");
            if (h == null) return;
            var img = h.GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;
            var c = color; c.a = alpha;
            img.color = c;
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
            // Use world-space cell position — robust against parent pivot/anchor.
            var cellRt = grid.CellRects[x, y];
            rt.position = cellRt.position;
        }
    }
}
