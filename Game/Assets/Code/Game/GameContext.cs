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
            Resources.StartDay(DayConfig.Current, true);
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
                v => v.Data.AdjacencyTarget,
                v => v.Data.AdjacencyEffects);
            Resources.CurrentGoldInMimic = LastResolved.TotalGold;
            if (Hud != null) Hud.Refresh();
        }

        public void Digest(LootView item)
        {
            int cost = LastResolved != null ? LastResolved.GetAcid(item) : item.Data.AcidCost;
            if (Resources.CurrentAcid < cost) return;
            Resources.CurrentAcid -= cost;
            Resources.CurrentHp += item.Data.HealOnDigest;
            MimicGrid.Model.Remove(item);
            Destroy(item.gameObject);
            OnGridChanged();
        }

        public LootView SpawnLoot(LootData data, Transform parent)
        {
            var go = Instantiate(LootItemPrefab, parent);
            var view = go.GetComponent<LootView>();
            view.Bind(data);
            return view;
        }
    }
}
