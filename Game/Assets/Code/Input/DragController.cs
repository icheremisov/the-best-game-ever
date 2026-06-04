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

        // Зона "Переварить" поверх желудка — видна только во время перетаскивания.
        // Накрывает РОВНО клетки, занятые формой желудка (вырез под форму), а не весь bbox.
        private RectTransform digestZone;
        private readonly System.Collections.Generic.List<UnityEngine.UI.Image> digestCellImgs =
            new System.Collections.Generic.List<UnityEngine.UI.Image>();
        private bool overDigest;
        private static readonly Color DigestIdleColor = new Color(0.30f, 0.85f, 0.45f, 0.30f);
        private static readonly Color DigestHotColor  = new Color(0.35f, 1.00f, 0.50f, 0.78f);

        // Плашка "−N ЖС" у курсора, пока предмет над желудком.
        private UnityEngine.UI.Text digestTag;
        private RectTransform digestTagRt;

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
            var mouse = Mouse.current;
            if (mouse == null) return;

            // --- Not holding anything ---
            // Переваривание теперь только через зону "Переварить" (drag), контекстного
            // меню по ПКМ больше нет.
            if (Held == null)
            {
                HideDigestZone();
                return;
            }

            var mouseScreen = mouse.position.ReadValue();

            // Зона "Переварить" активна только пока что-то перетаскиваем.
            EnsureDigestZone();
            if (digestZone != null) digestZone.gameObject.SetActive(true);
            overDigest = IsOverStomach(mouseScreen);
            var digestColor = overDigest ? DigestHotColor : DigestIdleColor;
            for (int i = 0; i < digestCellImgs.Count; i++)
                if (digestCellImgs[i] != null) digestCellImgs[i].color = digestColor;
            UpdateDigestTag(mouseScreen);

            bool overAttack = !overDigest && IsOverAttackZone(mouseScreen);
            if (overDigest || overAttack)
            {
                // Над зоной переваривания/атаки: грид не подсвечиваем, предмет следует за курсором.
                Held.ClearAllHighlights();
                hoverGrid = null;
                FollowCursor(mouseScreen);
            }
            else
            {
                UpdateHighlight(mouseScreen);
                FollowCursor(mouseScreen);
            }

            // Drop on mouse-UP — release anywhere to digest / place / cancel.
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (overDigest)
                {
                    BeginDigestConfirm();
                    HideDigestZone();
                }
                else if (IsOverAttackZone(mouseScreen))
                {
                    var item = Held;
                    Held.ClearAllHighlights();
                    var combat = Mimic.Game.CombatController.Instance;
                    if (combat != null && combat.TryAttackWith(item))
                    {
                        if (VerboseLogs) Debug.Log($"[Drag] ATTACK with {item.Data?.Id}");
                        GameContext.Instance?.DestroyAttackedItem(item);
                        Held = null;
                        hoverGrid = null;
                    }
                    else
                    {
                        // предмет без атаки (или не наш ход) — вернуть на место
                        if (VerboseLogs) Debug.Log("[Drag] ATTACK rejected (нет attack / не ход) → возврат к origin");
                        Cancel();
                    }
                    HideDigestZone();
                }
                else if (hoverGrid != null && hoverCanPlace)
                {
                    if (VerboseLogs)
                        Debug.Log($"[Drag] DROP release → {hoverGrid.name} cell=({hoverX},{hoverY}) rot={Held.CurrentRotation}");
                    TryDropAt(hoverGrid, hoverX, hoverY);
                    HideDigestZone();
                }
                else
                {
                    // Release in an invalid location → return the item to where it came from.
                    if (VerboseLogs)
                        Debug.Log($"[Drag] DROP invalid → return to origin (hover={(hoverGrid != null ? hoverGrid.name : "none")} canPlace={hoverCanPlace})");
                    SfxPlayer.PlayNegative();
                    Cancel();
                    HideDigestZone();
                }
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                // RMB during hold = explicit cancel.
                if (VerboseLogs) Debug.Log("[Drag] CANCEL via RMB");
                SfxPlayer.PlayNegative();
                Cancel();
                HideDigestZone();
            }
        }

        private void HideDigestZone()
        {
            if (digestZone != null) digestZone.gameObject.SetActive(false);
            HideDigestTag();
        }

        // Релиз над желудком: вместо мгновенного переваривания открываем окно подтверждения.
        // Предмет «висит» (уже снят с грида при Pick) до решения игрока.
        private void BeginDigestConfirm()
        {
            var item = Held;
            if (item == null) return;
            var grid = originGrid;
            int ox = originX, oy = originY;
            Rotation rot = originRot;
            item.ClearAllHighlights();

            // Куда упадёт монетка — первая занятая клетка формы на месте предмета (она свободна после Pick).
            FirstFilledCell(item, ox, oy, rot, out int coinX, out int coinY);

            Held = null;
            hoverGrid = null;
            HideDigestTag();

            var popup = Mimic.UI.DigestConfirmPopup.Instance;
            if (popup == null)
            {
                // Окна нет — fallback на мгновенное переваривание.
                GameContext.Instance?.DigestConfirmed(item, grid, coinX, coinY);
                return;
            }
            if (VerboseLogs) Debug.Log($"[Drag] DIGEST confirm popup for {item.Data?.Id} (coin @ {grid?.name} {coinX},{coinY})");
            popup.Show(item,
                onConfirm: () =>
                {
                    if (VerboseLogs) Debug.Log($"[Drag] DIGEST confirmed {item.Data?.Id}");
                    GameContext.Instance?.DigestConfirmed(item, grid, coinX, coinY);
                },
                onCancel: () =>
                {
                    if (VerboseLogs) Debug.Log($"[Drag] DIGEST cancelled → возврат {item.Data?.Id} к origin");
                    ReturnToOrigin(item, grid, ox, oy, rot);
                });
        }

        // Возврат конкретного предмета на его прежнее место (для отмены переваривания).
        private void ReturnToOrigin(LootView item, GridView grid, int x, int y, Rotation rot)
        {
            if (item == null || grid == null) return;
            item.SetRotation(rot);
            grid.Model.TryPlace(item, x, y, rot);
            SnapToGrid(item, grid, x, y);
            item.SetCarried(false, 1f);
            item.ClearAllHighlights();
            GameContext.Instance?.OnGridChanged();
        }

        // Первая занятая клетка формы (в координатах грида) при размещении в (ox,oy) с поворотом rot.
        private static void FirstFilledCell(LootView item, int ox, int oy, Rotation rot, out int gx, out int gy)
        {
            var cells = item.Shape.GetRotatedCells(rot);
            int rows = cells.GetLength(0), cols = cells.GetLength(1);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    if (cells[r, c])
                    {
                        gx = ox + c;
                        gy = oy + (rows - 1 - r); // совпадает с Y-флипом GridModel
                        return;
                    }
            gx = ox; gy = oy;
        }

        private void UpdateDigestTag(Vector2 mouseScreen)
        {
            if (!overDigest || Held == null) { HideDigestTag(); return; }
            EnsureDigestTag();
            if (digestTag == null) return;
            var ctx = GameContext.Instance;
            int cost = ctx != null ? ctx.AcidCostFor(Held) : Held.Data.AcidCost;
            bool canAfford = ctx != null && ctx.Resources.CurrentAcid >= cost;
            digestTag.text = $"-{cost} ЖС";
            digestTag.color = canAfford ? new Color(0.35f, 1f, 0.5f) : new Color(1f, 0.35f, 0.35f);
            digestTagRt.gameObject.SetActive(true);
            digestTagRt.SetAsLastSibling();
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(DragLayer, mouseScreen, UiCamera, out var wp))
                digestTagRt.position = wp + new Vector3(0f, 55f * DragLayer.lossyScale.y, 0f);
        }

        private void HideDigestTag()
        {
            if (digestTagRt != null) digestTagRt.gameObject.SetActive(false);
        }

        private void EnsureDigestTag()
        {
            if (digestTag != null || DragLayer == null) return;
            var go = new GameObject("DigestCostTag",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
            digestTagRt = (RectTransform)go.transform;
            digestTagRt.SetParent(DragLayer, worldPositionStays: false);
            digestTagRt.sizeDelta = new Vector2(170f, 46f);
            digestTag = go.GetComponent<UnityEngine.UI.Text>();
            digestTag.font = FontProvider.Default;
            digestTag.fontSize = 30;
            digestTag.fontStyle = FontStyle.Bold;
            digestTag.alignment = TextAnchor.MiddleCenter;
            digestTag.raycastTarget = false;
            digestTag.horizontalOverflow = HorizontalWrapMode.Overflow;
            digestTag.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private bool IsOverAttackZone(Vector2 mouseScreen)
        {
            var combat = Mimic.Game.CombatController.Instance;
            if (combat == null || !combat.IsActive || combat.AttackZone == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(combat.AttackZone, mouseScreen, UiCamera);
        }

        // Создаёт подсветку-оверлей по клеткам желудка один раз (рантайм, без правок сцены).
        // Виден только во время перетаскивания; ярчает при наведении предмета.
        // Оверлей строится по КАЖДОЙ клетке грида, занятой формой желудка — пустые клетки
        // bbox желудка остаются обычными ячейками грида (туда можно класть предметы).
        private void EnsureDigestZone()
        {
            if (digestZone != null) return;
            var stomach = GameContext.Instance != null ? GameContext.Instance.StomachView : null;
            if (stomach == null || MimicGrid == null || MimicGrid.Model == null) return;

            var go = new GameObject("DigestDropZone", typeof(RectTransform));
            digestZone = (RectTransform)go.transform;
            digestZone.SetParent(MimicGrid.CellsRoot, worldPositionStays: false);
            digestZone.anchorMin = digestZone.anchorMax = new Vector2(0f, 0f);
            digestZone.pivot = new Vector2(0f, 0f);
            digestZone.anchoredPosition = Vector2.zero;
            digestZone.sizeDelta = Vector2.zero;

            float cs = MimicGrid.CellSize;
            for (int x = 0; x < MimicGrid.Width; x++)
                for (int y = 0; y < MimicGrid.Height; y++)
                {
                    if (MimicGrid.Model.GetAt(x, y) != stomach) continue;
                    var cellGo = new GameObject($"DZ_{x}_{y}",
                        typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                    var crt = (RectTransform)cellGo.transform;
                    crt.SetParent(digestZone, worldPositionStays: false);
                    crt.anchorMin = crt.anchorMax = new Vector2(0f, 0f);
                    crt.pivot = new Vector2(0f, 0f);
                    crt.sizeDelta = new Vector2(cs, cs);
                    crt.anchoredPosition = new Vector2(x * cs, y * cs);
                    var img = cellGo.GetComponent<UnityEngine.UI.Image>();
                    img.color = DigestIdleColor;
                    img.raycastTarget = false; // хит-тест делаем вручную
                    digestCellImgs.Add(img);
                }

            digestZone.SetAsLastSibling();    // поверх арта/предметов грида
            digestZone.gameObject.SetActive(false);
        }

        // Курсор над клеткой грида, занятой формой желудка?
        private bool IsOverStomach(Vector2 mouseScreen)
        {
            var stomach = GameContext.Instance != null ? GameContext.Instance.StomachView : null;
            if (stomach == null || MimicGrid == null || MimicGrid.Model == null) return false;
            if (!MimicGrid.ScreenToCell(mouseScreen, UiCamera, out int cx, out int cy)) return false;
            return MimicGrid.Model.GetAt(cx, cy) == stomach;
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
            if (item != null && item.Data != null && item.Data.IsFixture)
                return; // сердце/желудок не двигаются

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

            // TODO(jam): group-drag wiring — GlueGroup.Resolve(...) ready, apply block move here.
            // The glued-group logic is implemented & tested in Mimic.Logic.GlueGroup. Full group
            // drag would require tracking N held LootViews + per-member offsets, an N-cell hover
            // highlight, and an all-or-nothing multi-place on drop (revert all if any member can't
            // fit). That is a substantial restructuring of this single-Held drag model, so it's
            // deferred to avoid regressing the working single-item path. Below we compute the group
            // as a first, side-effect-free step so the block move can be hooked in here later.
            if (grid == MimicGrid && item.Data != null)
            {
                var glueGroup = GlueGroup.Resolve(grid.Model, item, v => v.Data != null && v.Data.IsGlue);
                if (glueGroup.Count > 1 && VerboseLogs)
                    Debug.Log($"[Drag] glue group of {glueGroup.Count} detected on pick of {item.Data?.Id} (block move not yet wired)");
            }

            grid.Model.Remove(item);

            ContextMenuController.Instance?.Close(); // close any open menu when starting a drag
            Held = item;
            item.SetCarried(true, CarriedAlpha);
            item.transform.SetParent(DragLayer, worldPositionStays: false);
            SfxPlayer.PlayItemHandle(item.Data);

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
            else
            {
                SfxPlayer.PlayNegative();
                Cancel();
            }
        }

        private void TryDropAt(GridView grid, int x, int y)
        {
            // Правый грид — временный инвентарь/корзина: разрешаем складывать туда лут
            // из грида мимика в любой фазе (нужно для сортировки и временной разгрузки).
            if (grid.Model.TryPlace(Held, x, y, Held.CurrentRotation))
            {
                Held.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
                SnapToGrid(Held, grid, x, y);
                Held.SetCarried(false, 1f);
                if (VerboseLogs)
                    Debug.Log($"[Drag] DROP OK → {grid.name} ({x},{y}) rot={Held.CurrentRotation}");
                SfxPlayer.PlayItemDrop(Held.Data);
                Held.ClearAllHighlights(); // clear shape-cell tints BEFORE losing the reference
                Held = null;
                hoverGrid = null;
                GameContext.Instance?.OnGridChanged();
            }
            else
            {
                if (VerboseLogs)
                    Debug.LogWarning($"[Drag] TryPlace failed at {grid.name} ({x},{y}) rot={Held.CurrentRotation}");
                SfxPlayer.PlayNegative();
                Cancel();
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
