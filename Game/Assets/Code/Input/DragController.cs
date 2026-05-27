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

        public LootView Held { get; private set; }

        private GridView originGrid;
        private int originX, originY;
        private Rotation originRot;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (Held == null) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            var mouseScreen = mouse.position.ReadValue();

            // Follow cursor
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, mouseScreen, UiCamera, out var local))
                ((RectTransform)Held.transform).anchoredPosition = local;

            UpdateHighlight(mouseScreen);

            // Click on empty cell while holding → drop there.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var grid = ScreenOverGrid(mouseScreen);
                if (grid != null
                    && grid.ScreenToCell(mouseScreen, UiCamera, out int cx, out int cy)
                    && grid.Model.GetAt(cx, cy) == null)
                {
                    TryDropAt(grid, cx, cy);
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                Cancel();
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
            // Called from GridView click handler — see Task 4.4
            if (Held != null) TryDropAt(grid, x, y);
        }

        public void Pick(LootView item)
        {
            // Determine origin grid + remove from model
            var grid = FindGridContaining(item);
            if (grid == null) return; // Already in DragLayer somehow
            grid.Model.TryGetPlacement(item, out originX, out originY, out originRot);
            originGrid = grid;
            grid.Model.Remove(item);

            Held = item;
            item.transform.SetParent(DragLayer, worldPositionStays: false);
        }

        public void Cancel()
        {
            if (Held == null) return;
            Held.CurrentRotation = originRot;
            originGrid.Model.TryPlace(Held, originX, originY, originRot);
            SnapToGrid(Held, originGrid, originX, originY);
            Held = null;
            ClearHighlight();
        }

        private void TryDrop(LootView clickedItem)
        {
            // Если кликнули по другому item — попытка положить на его клетку
            var grid = FindGridContaining(clickedItem);
            if (grid != null)
            {
                grid.Model.TryGetPlacement(clickedItem, out var x, out var y, out _);
                TryDropAt(grid, x, y);
            }
        }

        private void TryDropAt(GridView grid, int x, int y)
        {
            // MVP rule: items cannot return to adventurer grid
            if (grid == AdventurerGrid && originGrid == MimicGrid) return;

            if (grid.Model.TryPlace(Held, x, y, Held.CurrentRotation))
            {
                Held.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
                SnapToGrid(Held, grid, x, y);
                Held = null;
                ClearHighlight();
                GameContext.Instance?.OnGridChanged();
            }
        }

        private void UpdateHighlight(Vector2 screenPos)
        {
            ClearHighlight();
            // Determine which grid the cursor is over
            var grid = ScreenOverGrid(screenPos);
            if (grid == null) return;
            if (!grid.ScreenToCell(screenPos, UiCamera, out int x, out int y)) return;

            var cells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            bool canPlace = grid.Model.TryPlace(Held, x, y, Held.CurrentRotation);
            if (canPlace) grid.Model.Remove(Held); // undo trial placement

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
            item.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
            ((RectTransform)item.transform).anchoredPosition = grid.CellToLocal(x, y);
        }
    }
}
