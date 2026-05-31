using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Data;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Game
{
    // Пошаговый бой поверх сцены сортировки. Враг ходит первым; ход игрока
    // завершает только атака («Кусь» или бросок предмета с attack>0).
    public class CombatController : MonoBehaviour
    {
        public static CombatController Instance { get; private set; }

        public bool IsActive { get; private set; }
        public bool PlayerTurn { get; private set; }

        // Зона для хит-теста броска предмета (используется DragController).
        public RectTransform AttackZone => panelRect;

        private CombatEnemy enemy;
        private Action onWin, onLose;

        // --- runtime UI ---
        private RectTransform panelRect;
        private Text nameText;
        private Image enemyHpFill;
        private Text enemyHpLabel;
        private Text enemyAtkText;
        private Button biteButton;
        private Text biteLabel;
        private GameObject panelGo;

        private const float EnemyTurnDelay = 0.45f;

        private void Awake()
        {
            Instance = this;
        }

        public void StartCombat(CombatEnemy e, Action win, Action lose)
        {
            enemy = e;
            onWin = win;
            onLose = lose;
            IsActive = true;
            PlayerTurn = false;

            EnsureUI();
            RefreshUI();
            panelGo.SetActive(true);

            var ctx = GameContext.Instance;
            // спрятать правую сетку и кнопки дня на время боя
            if (ctx.AdventurerGrid != null) ctx.AdventurerGrid.gameObject.SetActive(false);
            if (ctx.Hud != null)
            {
                if (ctx.Hud.NextButton != null) ctx.Hud.NextButton.gameObject.SetActive(false);
                if (ctx.Hud.SurrenderButton != null) ctx.Hud.SurrenderButton.gameObject.SetActive(false);
            }

            StartCoroutine(EnemyTurn()); // враг ходит первым
        }

        private IEnumerator EnemyTurn()
        {
            PlayerTurn = false;
            yield return new WaitForSeconds(EnemyTurnDelay);

            var ctx = GameContext.Instance;
            ctx.Resources.CurrentHp -= CombatResolver.EnemyAttackDamage(enemy);
            ctx.Hud?.Refresh();
            FlashEnemyAttack();

            if (CombatResolver.IsPlayerDead(ctx.Resources.CurrentHp))
            {
                EndCombat(win: false);
                yield break;
            }
            PlayerTurn = true;
        }

        // Кнопка «Кусь» — стандартная атака.
        public void Bite()
        {
            if (!IsActive || !PlayerTurn) return;
            int dmg = Catalogs.DayConfig.Current.BiteDamage;
            CombatResolver.ApplyDamageToEnemy(enemy, dmg);
            AfterPlayerAttack();
        }

        // Бросок предмета во врага. true => предмет израсходован (атаковал); false => вернуть на место.
        public bool TryAttackWith(LootView item)
        {
            if (!IsActive || !PlayerTurn || item == null || item.Data == null) return false;
            int dmg = CombatResolver.ItemAttackDamage(item.Data);
            if (dmg <= 0) return false; // предмет без атаки — не расходуется
            CombatResolver.ApplyDamageToEnemy(enemy, dmg);
            AfterPlayerAttack();
            return true;
        }

        // Бесплатный бонус: урон при переваривании во время боя (низкий приоритет).
        // Не завершает ход. Вызывается из GameContext.TryDigestHeld.
        public void OnItemDigested(LootData data)
        {
            if (!IsActive || data == null || data.AttackOnDigest <= 0) return;
            CombatResolver.ApplyDamageToEnemy(enemy, data.AttackOnDigest);
            RefreshUI();
            if (CombatResolver.IsEnemyDead(enemy)) EndCombat(win: true);
        }

        private void AfterPlayerAttack()
        {
            RefreshUI();
            if (CombatResolver.IsEnemyDead(enemy))
            {
                EndCombat(win: true);
                return;
            }
            StartCoroutine(EnemyTurn());
        }

        private void EndCombat(bool win)
        {
            if (!IsActive) return;
            IsActive = false;
            PlayerTurn = false;
            if (panelGo != null) panelGo.SetActive(false);

            var ctx = GameContext.Instance;
            if (ctx.Hud != null)
            {
                if (ctx.Hud.NextButton != null) ctx.Hud.NextButton.gameObject.SetActive(true);
                if (ctx.Hud.SurrenderButton != null) ctx.Hud.SurrenderButton.gameObject.SetActive(true);
            }
            if (ctx.AdventurerGrid != null) ctx.AdventurerGrid.gameObject.SetActive(true);

            var cb = win ? onWin : onLose;
            onWin = onLose = null;
            cb?.Invoke();
        }

        private void RefreshUI()
        {
            if (panelGo == null) return;
            if (nameText != null) nameText.text = enemy != null ? enemy.Name : "";
            if (enemy != null && enemy.MaxHp > 0)
            {
                if (enemyHpFill != null) enemyHpFill.fillAmount = Mathf.Clamp01(enemy.Hp / (float)enemy.MaxHp);
                if (enemyHpLabel != null) enemyHpLabel.text = $"{Mathf.Max(0, enemy.Hp)}/{enemy.MaxHp}";
            }
            if (enemyAtkText != null) enemyAtkText.text = enemy != null ? $"⚔ {enemy.Attack}" : "";
            if (biteLabel != null) biteLabel.text = $"Кусь! {Catalogs.DayConfig.Current.BiteDamage}";
        }

        private void FlashEnemyAttack()
        {
            // дешёвый фидбэк: мигнуть фоном панели
            if (panelRect == null) return;
            var img = panelRect.GetComponent<Image>();
            if (img != null) StartCoroutine(Flash(img));
        }

        private IEnumerator Flash(Image img)
        {
            var orig = img.color;
            img.color = new Color(0.8f, 0.2f, 0.2f, orig.a);
            yield return new WaitForSeconds(0.15f);
            img.color = orig;
        }

        // Боевая панель строится один раз поверх правой сетки (рантайм, без правок сцены).
        private void EnsureUI()
        {
            if (panelGo != null) return;
            var ctx = GameContext.Instance;
            var anchor = ctx.AdventurerGrid != null ? (RectTransform)ctx.AdventurerGrid.transform : null;
            var canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) { Debug.LogError("[Combat] нет Canvas для боевой панели"); return; }

            panelGo = new GameObject("CombatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelRect = (RectTransform)panelGo.transform;
            panelRect.SetParent(canvas.transform, worldPositionStays: false);
            // повторить прямоугольник правой сетки, если он есть; иначе правая половина экрана
            if (anchor != null)
            {
                panelRect.position = anchor.position;
                panelRect.sizeDelta = anchor.GetComponentInChildren<RectTransform>() != null
                    ? new Vector2(GridSizeX(ctx), GridSizeY(ctx))
                    : new Vector2(600, 600);
                panelRect.pivot = anchor.pivot;
                panelRect.anchorMin = anchor.anchorMin;
                panelRect.anchorMax = anchor.anchorMax;
                panelRect.anchoredPosition = anchor.anchoredPosition;
            }
            else
            {
                panelRect.anchorMin = new Vector2(0.55f, 0.2f);
                panelRect.anchorMax = new Vector2(0.95f, 0.85f);
                panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            }
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0.15f, 0.10f, 0.12f, 0.95f);
            bg.raycastTarget = false; // хит-тест броска — вручную в DragController

            nameText = MakeText(panelRect, "Name", new Vector2(0.5f, 0.92f), 34, FontStyle.Bold);
            enemyAtkText = MakeText(panelRect, "Atk", new Vector2(0.5f, 0.80f), 26, FontStyle.Normal);

            // HP-бар врага
            var barGo = new GameObject("EnemyHpBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var barRt = (RectTransform)barGo.transform;
            barRt.SetParent(panelRect, false);
            barRt.anchorMin = new Vector2(0.1f, 0.66f);
            barRt.anchorMax = new Vector2(0.9f, 0.74f);
            barRt.offsetMin = barRt.offsetMax = Vector2.zero;
            barGo.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.SetParent(barRt, false);
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            enemyHpFill = fillGo.GetComponent<Image>();
            enemyHpFill.color = new Color(0.80f, 0.25f, 0.25f, 1f);
            enemyHpFill.type = Image.Type.Filled;
            enemyHpFill.fillMethod = Image.FillMethod.Horizontal;
            enemyHpFill.fillOrigin = 0;
            enemyHpFill.fillAmount = 1f;
            enemyHpLabel = MakeText(barRt, "HpLabel", new Vector2(0.5f, 0.5f), 22, FontStyle.Bold);

            // Кнопка «Кусь»
            var btnGo = new GameObject("BiteButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var btnRt = (RectTransform)btnGo.transform;
            btnRt.SetParent(panelRect, false);
            btnRt.anchorMin = new Vector2(0.25f, 0.06f);
            btnRt.anchorMax = new Vector2(0.75f, 0.18f);
            btnRt.offsetMin = btnRt.offsetMax = Vector2.zero;
            btnGo.GetComponent<Image>().color = new Color(0.55f, 0.20f, 0.20f, 1f);
            biteButton = btnGo.GetComponent<Button>();
            biteButton.onClick.AddListener(Bite);
            biteLabel = MakeText(btnRt, "Label", new Vector2(0.5f, 0.5f), 28, FontStyle.Bold);

            panelGo.SetActive(false);
        }

        private static float GridSizeX(GameContext ctx) => ctx.AdventurerGrid.Width * ctx.AdventurerGrid.CellSize;
        private static float GridSizeY(GameContext ctx) => ctx.AdventurerGrid.Height * ctx.AdventurerGrid.CellSize;

        private static Text MakeText(RectTransform parent, string name, Vector2 anchorCenter, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.05f, anchorCenter.y - 0.05f);
            rt.anchorMax = new Vector2(0.95f, anchorCenter.y + 0.05f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = FontProvider.Default;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
            return t;
        }
    }
}
