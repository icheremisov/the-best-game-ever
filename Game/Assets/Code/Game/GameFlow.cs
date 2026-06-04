using System.Collections.Generic;
using UnityEngine;
using Mimic.Catalogs;
using Mimic.Data;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
    public enum DayPhase { Adventurers, Reward, Combat }

    public class GameFlow : MonoBehaviour
    {
        [Header("Popups")]
        public AdventurerIntroPopup IntroPopup;
        public SurrenderConfirmPopup SurrenderPopup;
        public EndOfDayPopup EndPopup;

        [Header("Scene refs")]
        public HudView Hud;

        public DayPhase Phase { get; private set; }

        private Queue<string> queue;
        private int totalInDay;
        private int processed;
        private AdventurerData current;
        private bool dayEnded;
        private int theftSeed = 1; // зерно кражи Властелином, инкрементится при каждом итоге дня
        private DayStartSnapshot daySnapshot;
        private System.Collections.Generic.List<LootSnapshotEntry> lootSnapshot;

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
            // Снимок лута на старте дня (до прихода приключенцев) — для отката при переигровке.
            lootSnapshot = ctx.SnapshotLoot();

            Phase = DayPhase.Adventurers;
            dayEnded = false;
            queue = new Queue<string>(DayConfig.Current.AdventurerIds);
            totalInDay = DayConfig.Current.AdventurerIds.Length;
            processed = 0;
            if (Mimic.UI.JarHeadView.Instance != null) Mimic.UI.JarHeadView.Instance.Clear();
            Hud.SetDayCounter(DayConfig.Current.Day);
            Hud.SetNextButtonLabel("Следующий!");
            Hud.EnterAdventurerButtons(); // вернуть NextButton, спрятать кнопки итогов
            if (firstDay) ctx.SpawnFixtures(); // сердце/желудок ставятся один раз и живут в гриде мимика
            ctx.OnGridChanged();
            PlayTrigger($"start_day_{DayConfig.Current.Day}", BringNext);
        }

        // Запускает цепочку диалога по ключу триггера; по завершении вызывает onDone.
        // Если для триггера нет реплик — onDone вызывается сразу (без задержки).
        private void PlayTrigger(string key, System.Action onDone)
        {
            var chain = Mimic.Catalogs.DialogCatalog.Get(key);
            if (chain == null || chain.Count == 0) { onDone(); return; }
            var lines = new System.Collections.Generic.List<Mimic.Data.DialogLine>(chain);
            if (DialogOverlay.Instance != null)
                DialogOverlay.Instance.Show(lines, onDone);
            else
                onDone();
        }

        private void BringNext()
        {
            if (queue.Count == 0)
            {
                Hud.SetNextButtonLabel("Подвести итоги дня");
                Hud.SetNextButtonEnabled(true);
                return;
            }
            string id = queue.Dequeue();
            current = AdventurerCatalog.Get(id);
            processed++;
            Hud.SetHeroCounter(processed, totalInDay);
            // Банку очищаем — голова появится только после «Сожрать» (когда лут на столе).
            if (Mimic.UI.JarHeadView.Instance != null) Mimic.UI.JarHeadView.Instance.Clear();
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
            // Лут героя выпал и доступен для перетаскивания — показываем его голову в банке.
            if (Mimic.UI.JarHeadView.Instance != null) Mimic.UI.JarHeadView.Instance.Show(current.Name);
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

            if (queue.Count == 0) Settle();
            else BringNext();
        }

        // «Подвести итоги дня»: проигрываем реплику Властелина, затем считаем итог без попапа.
        private void Settle()
        {
            Phase = DayPhase.Reward; // блокируем повторный NextOrEndDay
            PlayTrigger($"end_day_{DayConfig.Current.Day}", DoSettleAndReward);
        }

        // Итог дня (бывшая логика OverlordPopup, но без окна и без ручной сдачи предметов):
        // банк правой сетки (обычно пусто) → Властелин крадёт 1 предмет → урон за недобор квоты.
        // Затем сразу выдаём предмет награды/штрафа в правую сетку и показываем 2 кнопки.
        private void DoSettleAndReward()
        {
            var ctx = GameContext.Instance;
            SfxPlayer.PlayGold();
            ctx.BankAllInGrid(ctx.AdventurerGrid);

            var stolen = TheftResolver.PickStealable(ctx.MimicGrid.Model, v => v, theftSeed++,
                canSteal: v => !v.Data.IsFixture);
            if (stolen != null)
            {
                ctx.MimicGrid.Model.Remove(stolen);
                Destroy(stolen.gameObject);
            }
            ctx.OnGridChanged();

            var r = ctx.Resources;
            int dmg = Settlement.Damage(r.TotalGold, r.DayQuota, DayConfig.Current.GoldDamageMult);
            if (dmg > 0)
            {
                r.CurrentHp -= dmg;
                SfxPlayer.PlayMimicDamage();
            }

            if (r.CurrentHp <= 0)
            {
                EndDeath();
                return;
            }

            bool win = r.TotalGold >= r.DayQuota;
            SpawnRewardItem(win);
            EnterRewardButtons();
        }

        // Кладёт предмет награды (квота закрыта) или штрафа (недобор) в правую сетку —
        // игрок сам решит, тащить его в мимика или нет. Если в категории предметов нет
        // (напр. punish-каталог пуст) — не спавним ничего.
        private void SpawnRewardItem(bool win)
        {
            var ctx = GameContext.Instance;
            var pool = LootCatalog.ByCategory(win ? LootCategory.Reward : LootCategory.Punish);
            if (pool.Count == 0) return;
            var data = pool[Random.Range(0, pool.Count)];
            var view = ctx.SpawnLoot(data, ctx.AdventurerGrid.CellsRoot);
            if (!TryPlaceFirstFit(ctx.AdventurerGrid, view))
            {
                Destroy(view.gameObject);
                return;
            }
            ctx.OnGridChanged();
        }

        // Две кнопки на месте «Следующий»: «Начать следующий день» (нет в последний день)
        // и «Бросить вызов».
        private void EnterRewardButtons()
        {
            Hud.ShowRewardButtons(
                onNextDay: GoNextDay,
                nextDayEnabled: !DayConfig.IsLastDay,
                onChallenge: OnChallengeOverlord);
        }

        private void GoNextDay()
        {
            DayConfig.Advance();
            BeginDay(firstDay: false);
        }

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
            ctx.RestoreLoot(lootSnapshot); // вернуть лут гридов к состоянию на начало дня
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
