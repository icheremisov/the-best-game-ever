using System.Collections.Generic;
using UnityEngine;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
    public enum DayPhase { Adventurers, Overlord, RewardChoice, Transition, Combat }

    public class GameFlow : MonoBehaviour
    {
        [Header("Popups")]
        public AdventurerIntroPopup IntroPopup;
        public SurrenderConfirmPopup SurrenderPopup;
        public EndOfDayPopup EndPopup;
        public OverlordPopup OverlordPopup;
        public RewardChoicePopup RewardPopup;

        [Header("Scene refs")]
        public HudView Hud;

        public DayPhase Phase { get; private set; }

        private Queue<string> queue;
        private int totalInDay;
        private int processed;
        private AdventurerData current;
        private bool dayEnded;
        private DayStartSnapshot daySnapshot;

        private void Start()
        {
            BeginDay(firstDay: true);
            Hud.NextButton.onClick.AddListener(NextOrEndDay);
            Hud.SurrenderButton.onClick.AddListener(() => SurrenderPopup.Show(EndBurst));
            GameContext.Instance.GameFlowDeathHook = () => EndDeath();
        }

        private void BeginDay(bool firstDay)
        {
            var ctx = GameContext.Instance;
            if (EndPopup != null) EndPopup.Hide(); // спрятать экран конца дня при старте нового/переигранного дня
            if (!firstDay) ctx.Resources.StartDay(DayConfig.Current, firstDay: false);
            daySnapshot = ctx.Resources.SnapshotDayStart();

            Phase = DayPhase.Adventurers;
            dayEnded = false;
            queue = new Queue<string>(DayConfig.Current.AdventurerIds);
            totalInDay = DayConfig.Current.AdventurerIds.Length;
            processed = 0;
            Hud.SetDayCounter(DayConfig.Current.Day);
            Hud.SetNextButtonLabel("Следующий!");
            if (firstDay) ctx.SpawnFixtures(); // сердце/желудок ставятся один раз и живут в гриде мимика
            ctx.OnGridChanged();
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
            if (current.Battle)
                IntroPopup.Show(current, OnBattlePressed, eatLabel: "В бой");
            else
                IntroPopup.Show(current, OnEatPressed);
        }

        private void OnEatPressed()
        {
            var ctx = GameContext.Instance;
            foreach (var data in RollLoot(current))
            {
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

        // Набор лута приключенца под бюджет в клетках.
        // budget <= 0 => выдаём весь LootIds (старое поведение / «выдать всё»).
        // Иначе пока в пуле есть предмет, влезающий в остаток бюджета, берём случайный
        // из подходящих по размеру (равновероятно, с повторами) и тратим его клетки.
        private static List<LootData> RollLoot(AdventurerData adv)
        {
            var pool = new List<LootData>(adv.LootIds.Length);
            foreach (var lootId in adv.LootIds)
                pool.Add(LootCatalog.Get(lootId));

            if (adv.Budget <= 0)
                return pool;

            var result = new List<LootData>();
            int remaining = adv.Budget;
            var fitting = new List<LootData>();
            while (true)
            {
                fitting.Clear();
                foreach (var data in pool)
                    if (data.Shape.CellCount <= remaining)
                        fitting.Add(data);
                if (fitting.Count == 0) break;

                var pick = fitting[Random.Range(0, fitting.Count)];
                result.Add(pick);
                remaining -= pick.Shape.CellCount;
            }
            return result;
        }

        private void OnBattlePressed()
        {
            Phase = DayPhase.Combat;
            var enemy = Mimic.Data.CombatEnemy.FromAdventurer(current);
            CombatController.Instance.StartCombat(enemy, onWin: OnBattleWon, onLose: EndDeath);
        }

        private void OnBattleWon()
        {
            Phase = DayPhase.Adventurers;
            OnEatPressed(); // лут побеждённого приключенца падает в правую сетку
        }

        public static bool TryPlaceFirstFit(GridView grid, LootView view)
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.Model.TryPlace(view, x, y, Rotation.Deg0))
                    {
                        var rt = (RectTransform)view.transform;
                        rt.SetParent(grid.CellsRoot, worldPositionStays: false);
                        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                        rt.pivot = new Vector2(0, 0);
                        rt.position = grid.CellRects[x, y].position;
                        return true;
                    }
            return false;
        }

        public void NextOrEndDay()
        {
            if (Phase != DayPhase.Adventurers) return;
            var ctx = GameContext.Instance;
            if (ctx.AdventurerGrid.Model.FreeCellsCount < ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height)
                return;

            if (queue.Count == 0) EnterOverlord();
            else BringNext();
        }

        private void EnterOverlord()
        {
            Phase = DayPhase.Overlord;
            OverlordPopup.Show(OnSettled);
        }

        private void OnSettled()
        {
            var ctx = GameContext.Instance;
            if (ctx.Resources.CurrentHp <= 0)
            {
                EndDeath();
                return;
            }
            EnterReward();
        }

        private void EnterReward()
        {
            Phase = DayPhase.RewardChoice;
            var ctx = GameContext.Instance;
            bool win = ctx.Resources.TotalGold >= ctx.Resources.DayQuota;
            RewardPopup.Show(win, onTaken: OnRewardTaken, onOvereat: EndBurst);
        }

        private void OnRewardTaken()
        {
            EnterTransition();
        }

        private void EnterTransition()
        {
            Phase = DayPhase.Transition;
            var ctx = GameContext.Instance;
            bool canRansom = ctx.Resources.TotalGold >= DayConfig.Current.RansomGold;
            bool hasNextDay = !DayConfig.IsLastDay;
            EndPopup.ShowTransition(
                hasNextDay: hasNextDay,
                canRansom: canRansom,
                onNextDay: GoNextDay,
                onRansom: EndRansomWin,
                onChallenge: OnChallengeOverlord);
        }

        private void GoNextDay()
        {
            DayConfig.Advance();
            BeginDay(firstDay: false);
        }

        private void EndRansomWin() => EndPopup.ShowRansomWin();

        private void OnChallengeOverlord()
        {
            EndPopup.Hide();
            Phase = DayPhase.Combat;
            var enemy = Mimic.Data.CombatEnemy.FromOverlord(DayConfig.Current);
            CombatController.Instance.StartCombat(
                enemy,
                onWin: () => EndPopup.ShowRansomWin(),
                onLose: EndDeath);
        }

        private void EndDeath()
        {
            if (dayEnded) return;
            dayEnded = true;
            EndPopup.ShowDeath(onRetryDay: RetryDay);
        }

        private void EndBurst()
        {
            if (dayEnded) return;
            dayEnded = true;
            EndPopup.ShowBurst(onRetryDay: RetryDay);
        }

        private void RetryDay()
        {
            var ctx = GameContext.Instance;
            ctx.Resources.RestoreDayStart(daySnapshot);
            BeginDay(firstDay: false);
        }

        private void Update()
        {
            var ctx = GameContext.Instance;
            if (ctx == null || Hud == null || Phase != DayPhase.Adventurers) return;
            bool advEmpty = ctx.AdventurerGrid.Model.FreeCellsCount == ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height;
            Hud.SetNextButtonEnabled(advEmpty);
        }
    }
}
