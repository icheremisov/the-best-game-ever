using System.Collections.Generic;
using UnityEngine;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.UI;

namespace Mimic.Game
{
    public class GameFlow : MonoBehaviour
    {
        [Header("Popups")]
        public AdventurerIntroPopup IntroPopup;
        public SurrenderConfirmPopup SurrenderPopup;
        public EndOfDayPopup EndPopup;

        [Header("Scene refs")]
        public HudView Hud;

        private Queue<string> queue;
        private int totalInDay;
        private int processed;
        private AdventurerData current;
        private bool dayEnded;

        private void Start()
        {
            queue = new Queue<string>(DayConfig.Current.AdventurerIds);
            totalInDay = DayConfig.Current.AdventurerIds.Length;
            processed = 0;
            Hud.SetDayCounter(DayConfig.Current.Day);
            Hud.SetNextButtonLabel("Следующий!");
            Hud.NextButton.onClick.AddListener(NextOrEndDay);
            Hud.SurrenderButton.onClick.AddListener(() => SurrenderPopup.Show(EndLose));
            BringNext();
        }

        private void BringNext()
        {
            if (queue.Count == 0)
            {
                Hud.SetNextButtonLabel("Завершить день");
                Hud.SetNextButtonEnabled(true);
                return;
            }
            string id = queue.Dequeue();
            current = AdventurerCatalog.Get(id);
            processed++;
            Hud.SetHeroCounter(processed, totalInDay);
            IntroPopup.Show(current, OnEatPressed);
        }

        private void OnEatPressed()
        {
            var ctx = GameContext.Instance;
            foreach (var lootId in current.LootIds)
            {
                var data = LootCatalog.Get(lootId);
                var view = ctx.SpawnLoot(data, ctx.AdventurerGrid.CellsRoot);
                if (!TryPlaceFirstFit(ctx.AdventurerGrid, view))
                {
                    Destroy(view.gameObject);
                    continue;
                }
            }
            Hud.SetNextButtonEnabled(false);
            ctx.OnGridChanged();
        }

        private bool TryPlaceFirstFit(GridView grid, LootView view)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.Model.TryPlace(view, x, y, Mimic.Logic.Rotation.Deg0))
                    {
                        ((RectTransform)view.transform).anchoredPosition = grid.CellToLocal(x, y);
                        return true;
                    }
            return false;
        }

        public void NextOrEndDay()
        {
            var ctx = GameContext.Instance;
            // Allow next only when adventurer grid is empty
            if (ctx.AdventurerGrid.Model.FreeCellsCount < ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height)
                return;

            if (queue.Count == 0) EndDay();
            else BringNext();
        }

        private void EndDay()
        {
            if (dayEnded) return;
            dayEnded = true;
            var ctx = GameContext.Instance;
            int gold = ctx.Resources.CurrentGoldInMimic;
            int quota = ctx.Resources.DayQuota;
            if (gold >= quota) EndPopup.ShowWin(gold, quota);
            else EndPopup.ShowLose("День провален", gold, quota);
        }

        private void EndLose()
        {
            if (dayEnded) return;
            dayEnded = true;
            var ctx = GameContext.Instance;
            EndPopup.ShowLose("Вы лопнули от переедания", ctx.Resources.CurrentGoldInMimic, ctx.Resources.DayQuota);
        }

        private void Update()
        {
            var ctx = GameContext.Instance;
            if (ctx == null || Hud == null || dayEnded) return;
            bool advEmpty = ctx.AdventurerGrid.Model.FreeCellsCount == ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height;
            Hud.SetNextButtonEnabled(advEmpty);
        }
    }
}
