using UnityEngine;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
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

        public System.Action GameFlowDeathHook; // GameFlow подписывается, чтобы ловить смерть от переваривания

        private void Awake()
        {
            Instance = this;
            EnsureMainCamera();
            EnsureRuntimeControllers();
            int swappedFonts = Mimic.UI.FontProvider.ApplyToAllScene();
            if (swappedFonts > 0)
                Debug.Log($"[GameContext] Swapped legacy font on {swappedFonts} Text components (Cyrillic fix).");
            LootCatalog.Load();
            AdventurerCatalog.Load();
            DayConfig.Load();
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
            cam.backgroundColor = new Color(0.08f, 0.06f, 0.10f, 1f);
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

        // Возвращает true если предмет переварен; false если не хватило ЖС (или это фикстура).
        // Работает и для предмета, уже вынутого из грида (drag-to-digest): Model.Remove — no-op.
        public bool TryDigestHeld(LootView item)
        {
            if (item == null || item.Data == null || item.Data.IsFixture) return false;
            int cost = item.Data.AcidCost;
            if (LastResolved != null && LastResolved.EffectiveAcid.TryGetValue(item, out var a)) cost = a;
            if (Resources.CurrentAcid < cost) return false;
            Resources.CurrentAcid -= cost;
            Resources.CurrentAcid += item.Data.AcidRestoreOnDigest; // кислота/мизим восполняет ЖС
            Resources.CurrentHp += item.Data.HealOnDigest;          // бургер лечит
            Resources.CurrentHp -= item.Data.DamageOnDigest;        // гиря/клей/какашка бьют
            CombatController.Instance?.OnItemDigested(item.Data);
            MimicGrid.Model.Remove(item);
            Destroy(item.gameObject);
            OnGridChanged();
            if (Resources.CurrentHp <= 0) GameFlowDeathHook?.Invoke();
            return true;
        }

        public void SpawnFixtures()
        {
            PlaceFixture("heart", 0, MimicGrid.Height - 2);
            PlaceFixture("stomach", MimicGrid.Width - 3, 1);
        }

        private void PlaceFixture(string id, int x, int y)
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
            }
            else Destroy(view.gameObject);
        }

        public LootView SpawnLoot(LootData data, Transform parent)
        {
            var go = Instantiate(LootItemPrefab, parent);
            var view = go.GetComponent<LootView>();
            view.Bind(data);
            return view;
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
