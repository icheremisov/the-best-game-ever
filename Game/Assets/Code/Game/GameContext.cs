using UnityEngine;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
    // Снимок одного выложенного предмета для отката дня (переигровка после проигрыша).
    public class LootSnapshotEntry
    {
        public LootData Data;
        public bool InMimic; // true = грид мимика, false = правый грид
        public int X, Y;
        public Rotation Rot;
    }

    public class GameContext : MonoBehaviour
    {
        public static GameContext Instance { get; private set; }

        [Header("Scene refs")]
        public GridView MimicGrid;
        public GridView AdventurerGrid;
        public HudView Hud;
        public Transform LootContainer; // parent for spawned LootItem instances during runtime
        public GameObject LootItemPrefab;

        public GameResources Resources { get; private set; } = new GameResources();
        public AdjacencyResult<LootView> LastResolved { get; private set; }

        public LootView StomachView { get; private set; } // желудок — drop-зона переваривания

        public System.Action GameFlowDeathHook; // GameFlow подписывается, чтобы ловить смерть от переваривания

        private void Awake()
        {
            Instance = this;
            EnsureMainCamera();
            MusicPlayer.PlayMainTheme();
            EnsureRuntimeControllers();
            int swappedFonts = Mimic.UI.FontProvider.ApplyToAllScene();
            if (swappedFonts > 0)
                Debug.Log($"[GameContext] Swapped legacy font on {swappedFonts} Text components (Cyrillic fix).");
            LootCatalog.Load();
            AdventurerCatalog.Load();
            DayConfig.Load();
            DialogCatalog.Load();
            Resources.StartDay(DayConfig.Current, firstDay: true);
        }

        // If the scene-assembly step forgot to add a controller, attach it programmatically
        // so the corresponding feature still works (tooltip, context menu).
        private void EnsureRuntimeControllers()
        {
            if (TooltipController.Instance == null)
            {
                gameObject.AddComponent<TooltipController>();
                Debug.Log("[GameContext] Auto-added TooltipController");
            }
            if (ContextMenuController.Instance == null)
            {
                gameObject.AddComponent<ContextMenuController>();
                Debug.Log("[GameContext] Auto-added ContextMenuController");
            }
            if (CombatController.Instance == null)
            {
                gameObject.AddComponent<CombatController>();
                Debug.Log("[GameContext] Auto-added CombatController");
            }
            if (DigestConfirmPopup.Instance == null)
            {
                gameObject.AddComponent<DigestConfirmPopup>();
                Debug.Log("[GameContext] Auto-added DigestConfirmPopup");
            }
            if (DialogOverlay.Instance == null)
            {
                gameObject.AddComponent<DialogOverlay>();
                Debug.Log("[GameContext] Auto-added DialogOverlay");
            }
        }

        // Ensures a Camera exists in the scene so Unity doesn't complain
        // "Display 1 No cameras rendering". ScreenSpaceOverlay UI doesn't need a camera,
        // but Unity still warns; this gives us a dark clear-color background too.
        private static void EnsureMainCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black; // чёрные полосы-леттербокс по краям фикс. композиции
            cam.orthographic = true;
            cam.cullingMask = 0; // render nothing — Canvas Overlay draws separately
            go.AddComponent<AudioListener>();
        }

        public void OnGridChanged()
        {
            if (MimicGrid == null || MimicGrid.Model == null) return;
            LastResolved = AdjacencyResolver.Resolve(
                MimicGrid.Model,
                v => v.Data.Id,
                v => v.Data.Gold,
                v => v.Data.AcidCost,
                v => v.Data.AdjacencyRules,
                v => v.Data.NeighborGoldPct);
            Resources.CurrentGoldInMimic = LastResolved.TotalGold;
            if (Hud != null) Hud.Refresh();
        }

        public void Digest(LootView item) => TryDigestHeld(item);

        // Эффективная стоимость переваривания (с учётом adjacency, напр. какашка +50% ЖС).
        public int AcidCostFor(LootView item)
        {
            if (item == null || item.Data == null) return 0;
            int cost = item.Data.AcidCost;
            if (LastResolved != null && LastResolved.EffectiveAcid.TryGetValue(item, out var a)) cost = a;
            return cost;
        }

        // Можно ли переварить (хватает ли ЖС). Фикстуры — нельзя.
        public bool CanDigest(LootView item)
        {
            if (item == null || item.Data == null || item.Data.IsFixture) return false;
            return Resources.CurrentAcid >= AcidCostFor(item);
        }

        // Золото, которое вернётся монеткой (% от базовой цены). 0 => монетки нет.
        public int CoinGoldFor(LootData d)
            => d == null ? 0 : Mathf.RoundToInt(d.Gold * d.GoldOnDigestPct / 100f);

        // Возвращает true если предмет переварен; false если не хватило ЖС (или это фикстура).
        // Работает и для предмета, уже вынутого из грида (drag-to-digest): Model.Remove — no-op.
        public bool TryDigestHeld(LootView item)
        {
            if (!CanDigest(item)) return false;
            ApplyDigestEffects(item);
            MimicGrid.Model.Remove(item);
            Destroy(item.gameObject);
            OnGridChanged();
            if (Resources.CurrentHp <= 0) GameFlowDeathHook?.Invoke();
            return true;
        }

        // Подтверждённое переваривание из окна: применяет эффекты, удаляет предмет и
        // отрыгивает монетку на (grid, cellX, cellY) — на месте переваренного предмета.
        public void DigestConfirmed(LootView item, GridView grid, int cellX, int cellY)
        {
            if (item == null || item.Data == null) return;
            int coinGold = CoinGoldFor(item.Data);
            ApplyDigestEffects(item);
            MimicGrid.Model.Remove(item); // no-op если предмет был held
            Destroy(item.gameObject);
            if (coinGold > 0 && grid != null) SpawnCoin(grid, cellX, cellY, coinGold);
            OnGridChanged();
            if (Resources.CurrentHp <= 0) GameFlowDeathHook?.Invoke();
        }

        private void ApplyDigestEffects(LootView item)
        {
            SfxPlayer.PlayDigest();
            Resources.CurrentAcid -= AcidCostFor(item);
            Resources.CurrentAcid += item.Data.AcidRestoreOnDigest; // кислота/мизим восполняет ЖС
            if (item.Data.HealOnDigest > 0)                          // бургер лечит, но не выше макс. HP
            {
                int maxHp = Mathf.Max(1, DayConfig.Current.StartHp);
                Resources.CurrentHp = Mathf.Min(maxHp, Resources.CurrentHp + item.Data.HealOnDigest);
            }
            Resources.CurrentHp -= item.Data.DamageOnDigest;        // гиря/клей/какашка бьют
            CombatController.Instance?.OnItemDigested(item.Data);
        }

        // Рантайм-предмет «монета»: 1 клетка, цена = переваренное золото, сдаётся в корзину.
        private static LootData MakeCoinData(int gold) => new LootData
        {
            Id = "coin",
            Name = "Монета",
            Description = "Переваренное золото",
            Shape = Shape.Parse("X"),
            Gold = gold,
            AcidCost = 0,
            AdjacencyRules = System.Array.Empty<AdjacencyRule>(),
            Category = LootCategory.Normal,
            CanReturnToBasket = true,
        };

        private void SpawnCoin(GridView grid, int x, int y, int gold)
        {
            var view = SpawnLoot(MakeCoinData(gold), grid.CellsRoot);
            if (grid.Model.TryPlace(view, x, y, Rotation.Deg0))
            {
                var rt = (RectTransform)view.transform;
                rt.SetParent(grid.CellsRoot, worldPositionStays: false);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.position = grid.CellRects[x, y].position;
            }
            else Destroy(view.gameObject);
        }

        public void SpawnFixtures()
        {
            PlaceFixture("heart", 0, MimicGrid.Height - 2);
            StomachView = PlaceFixture("stomach", MimicGrid.Width - 3, 1);
            HideFixtureCells();
        }

        // Прячем фоновые ячейки грида под клетками фикстур (сердце/желудок), чтобы
        // пространство читалось как «дырка» в поле, а не как обычные ячейки.
        private void HideFixtureCells()
        {
            if (MimicGrid == null || MimicGrid.Model == null || MimicGrid.CellRects == null) return;
            for (int x = 0; x < MimicGrid.Width; x++)
                for (int y = 0; y < MimicGrid.Height; y++)
                {
                    var occ = MimicGrid.Model.GetAt(x, y);
                    if (occ != null && occ.Data != null && occ.Data.IsFixture && MimicGrid.CellRects[x, y] != null)
                        MimicGrid.CellRects[x, y].gameObject.SetActive(false);
                }
        }

        private LootView PlaceFixture(string id, int x, int y)
        {
            var data = LootCatalog.Get(id);
            var view = SpawnLoot(data, MimicGrid.CellsRoot);
            if (MimicGrid.Model.TryPlace(view, x, y, Rotation.Deg0))
            {
                var rt = (RectTransform)view.transform;
                rt.SetParent(MimicGrid.CellsRoot, worldPositionStays: false);
                rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                rt.pivot = new Vector2(0, 0);
                rt.position = MimicGrid.CellRects[x, y].position;
                return view;
            }
            Destroy(view.gameObject);
            return null;
        }

        public LootView SpawnLoot(LootData data, Transform parent)
        {
            var go = Instantiate(LootItemPrefab, parent);
            var view = go.GetComponent<LootView>();
            view.Bind(data);
            return view;
        }

        // --- Снимок лута для переигровки дня ---

        // Снимает весь выложенный лут обоих гридов (кроме фикстур) на момент вызова.
        public System.Collections.Generic.List<LootSnapshotEntry> SnapshotLoot()
        {
            var list = new System.Collections.Generic.List<LootSnapshotEntry>();
            CaptureGrid(MimicGrid, true, list);
            CaptureGrid(AdventurerGrid, false, list);
            return list;
        }

        private static void CaptureGrid(GridView grid, bool inMimic,
            System.Collections.Generic.List<LootSnapshotEntry> list)
        {
            if (grid == null || grid.Model == null) return;
            foreach (var item in grid.Model.AllItems())
            {
                if (item == null || item.Data == null || item.Data.IsFixture) continue;
                if (grid.Model.TryGetPlacement(item, out int x, out int y, out var rot))
                    list.Add(new LootSnapshotEntry { Data = item.Data, InMimic = inMimic, X = x, Y = y, Rot = rot });
            }
        }

        // Сносит текущий лут (кроме фикстур) и восстанавливает из снимка. Фикстуры остаются.
        public void RestoreLoot(System.Collections.Generic.List<LootSnapshotEntry> snap)
        {
            ClearLoot(MimicGrid);
            ClearLoot(AdventurerGrid);
            if (snap != null)
            {
                foreach (var e in snap)
                {
                    var grid = e.InMimic ? MimicGrid : AdventurerGrid;
                    if (grid == null) continue;
                    var view = SpawnLoot(e.Data, grid.CellsRoot);
                    view.SetRotation(e.Rot);
                    if (grid.Model.TryPlace(view, e.X, e.Y, e.Rot))
                    {
                        var rt = (RectTransform)view.transform;
                        rt.SetParent(grid.CellsRoot, worldPositionStays: false);
                        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                        rt.pivot = new Vector2(0, 0);
                        rt.position = grid.CellRects[e.X, e.Y].position;
                    }
                    else Destroy(view.gameObject);
                }
            }
            OnGridChanged();
        }

        private void ClearLoot(GridView grid)
        {
            if (grid == null || grid.Model == null) return;
            var items = new System.Collections.Generic.List<LootView>(grid.Model.AllItems());
            foreach (var it in items)
            {
                if (it == null || it.Data == null || it.Data.IsFixture) continue;
                grid.Model.Remove(it);
                Destroy(it.gameObject);
            }
        }

        // Сдать предмет Властелину: золото (с adjacency) -> BankedGold, предмет удаляется.
        public void BankItem(LootView item)
        {
            if (item.Data.IsFixture) return;
            int gold = LastResolved != null ? LastResolved.GetGold(item) : item.Data.Gold;
            Resources.BankedGold += gold;
            MimicGrid.Model.Remove(item);
            Destroy(item.gameObject);
        }

        // Сдать всё, что игрок переложил в правый (корзинный) грид.
        public int BankAllInGrid(GridView basket)
        {
            int total = 0;
            var items = new System.Collections.Generic.List<LootView>(basket.Model.AllItems());
            foreach (var it in items)
            {
                if (it.Data.IsFixture) continue;
                total += it.Data.Gold;
                Resources.BankedGold += it.Data.Gold;
                basket.Model.Remove(it);
                Destroy(it.gameObject);
            }
            return total;
        }

        // Предмет, проатаковавший врага, исчезает (он уже снят с грида при Pick).
        public void DestroyAttackedItem(LootView item)
        {
            if (item != null) Destroy(item.gameObject);
        }
    }
}
