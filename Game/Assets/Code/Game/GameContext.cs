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
            LootCatalog.Load();
            AdventurerCatalog.Load();
            DayConfig.Load();
            Resources.StartDay(DayConfig.Current);
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
