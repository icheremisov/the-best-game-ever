# Adjacency мульти-таргет и стакающиеся эффекты — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Один предмет может иметь несколько пар «таргет → эффект» (через `|`), а каждый эффект может быть стакающимся (`*`); множители одного типа складываются аддитивно и применяются к базе один раз.

**Architecture:** Парсинг пар выносится в новый struct `AdjacencyRule.ParseRules(targetRaw, effectRaw)`, который zip-ует два столбца CSV по `|` и переиспользует `AdjacencyEffect.ParseList` (с поддержкой суффикса `*`). `LootData` хранит `AdjacencyRule[]`. `AdjacencyResolver` для каждого предмета суммирует вклады всех правил по типам (`gold`/`acid`), учитывая число прилегающих инстансов для стакающихся, и применяет сумму к базе единожды.

**Tech Stack:** Unity 6000.3.16f1, C# (Assembly-CSharp, без asmdef). Автотестов нет (game jam, CI/asmdef запрещены) — верификация прогоняется через Unity MCP `execute_code` после компиляции.

---

## Замечание про порядок и компиляцию

Изменения в `LootData` / `AdjacencyResolver` / `GameContext` / `TooltipController` взаимозависимы: проект не скомпилируется, пока все они не согласованы. Поэтому код пишется задачами 1–7 подряд (коммит на каждую), а единый верификационный прогон — задача 8 (после успешной компиляции). Это сознательная адаптация TDD под Unity без тестовой сборки: «тест» — это сниппет ассертов из задачи 8, он гоняется в конце.

После каждой правки `.cs` исполнитель обновляет Unity и проверяет консоль:
- Refresh: `mcp__unityMCP__refresh_unity`
- Дождаться `editor_state.isCompiling == false`
- `mcp__unityMCP__read_console` (filter Error) — ошибок быть не должно.

Правки файлов делаются инструментом `Edit` по точным строкам ниже.

---

## File Structure

- `Game/Assets/Code/Data/AdjacencyEffect.cs` — **modify**: поле `Stackable`, парсинг суффикса `*`.
- `Game/Assets/Code/Data/AdjacencyRule.cs` — **create**: struct `AdjacencyRule` + `ParseRules`.
- `Game/Assets/Code/Data/LootData.cs` — **modify**: замена двух полей на `AdjacencyRule[] AdjacencyRules`.
- `Game/Assets/Code/Catalogs/LootCatalog.cs` — **modify**: вызов `AdjacencyRule.ParseRules`.
- `Game/Assets/Code/Logic/AdjacencyResolver.cs` — **modify**: новая сигнатура + аддитивная логика.
- `Game/Assets/Code/Game/GameContext.cs` — **modify**: передать `AdjacencyRules`.
- `Game/Assets/Code/UI/TooltipController.cs` — **modify**: `DescribeAdjacency` по правилам.

---

## Task 1: AdjacencyEffect — поле Stackable и парсинг `*`

**Files:**
- Modify: `Game/Assets/Code/Data/AdjacencyEffect.cs`

- [ ] **Step 1: Добавить поле `Stackable`**

Заменить (строки 10–11):

```csharp
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%
```

на:

```csharp
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%
        public bool Stackable;   // '*' в конце строки эффекта: применять за каждый прилегающий инстанс
```

- [ ] **Step 2: Срезать `*` в начале `ParseOne` и проставить флаг**

Заменить начало метода `ParseOne` (строки 27–31):

```csharp
        private static AdjacencyEffect ParseOne(string token)
        {
            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Effect must be '<type>:<sign><n>%': got '{token}'");
```

на:

```csharp
        private static AdjacencyEffect ParseOne(string token)
        {
            bool stackable = false;
            if (token.EndsWith("*"))
            {
                stackable = true;
                token = token.Substring(0, token.Length - 1).Trim();
            }

            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Effect must be '<type>:<sign><n>%': got '{token}'");
```

- [ ] **Step 3: Прокинуть флаг в результат**

Заменить последнюю строку метода `ParseOne` (строка 52):

```csharp
            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f };
```

на:

```csharp
            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f, Stackable = stackable };
```

- [ ] **Step 4: Refresh Unity и проверить компиляцию**

Run: `mcp__unityMCP__refresh_unity`, дождаться `isCompiling == false`, затем `mcp__unityMCP__read_console` (filter Error).
Expected: 0 ошибок (этот файл самодостаточен).

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Data/AdjacencyEffect.cs
git commit -m "feat(adjacency): поддержка стакающегося эффекта '*' в AdjacencyEffect"
```

---

## Task 2: AdjacencyRule — новый struct и ParseRules

**Files:**
- Create: `Game/Assets/Code/Data/AdjacencyRule.cs`

- [ ] **Step 1: Создать файл**

Содержимое `Game/Assets/Code/Data/AdjacencyRule.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Mimic.Data
{
    // Одна пара «таргет → эффекты». Несколько пар на предмете задаются в конфиге через '|'.
    public struct AdjacencyRule
    {
        public string Target;             // id соседа-триггера
        public AdjacencyEffect[] Effects; // эффекты этой пары (never null/empty в валидном правиле)

        // targetRaw/effectRaw — столбцы CSV. '|' разбивает на пары по индексу,
        // ';' внутри сегмента эффекта — несколько эффектов на один таргет.
        public static AdjacencyRule[] ParseRules(string targetRaw, string effectRaw)
        {
            bool noTarget = string.IsNullOrWhiteSpace(targetRaw);
            bool noEffect = string.IsNullOrWhiteSpace(effectRaw);
            if (noTarget && noEffect) return Array.Empty<AdjacencyRule>();
            if (noTarget || noEffect)
                throw new FormatException(
                    $"Adjacency: target и effect должны быть оба заданы или оба пусты (target='{targetRaw}', effect='{effectRaw}')");

            var targets = targetRaw.Split('|');
            var effects = effectRaw.Split('|');
            if (targets.Length != effects.Length)
                throw new FormatException(
                    $"Adjacency: число таргетов ({targets.Length}) != числу эффектов ({effects.Length}) ('{targetRaw}' | '{effectRaw}')");

            var rules = new List<AdjacencyRule>(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                var t = targets[i].Trim();
                if (t.Length == 0)
                    throw new FormatException($"Adjacency: пустой таргет #{i} в '{targetRaw}'");
                var fx = AdjacencyEffect.ParseList(effects[i]);
                if (fx.Length == 0)
                    throw new FormatException($"Adjacency: пустой эффект #{i} в '{effectRaw}'");
                rules.Add(new AdjacencyRule { Target = t, Effects = fx });
            }
            return rules.ToArray();
        }
    }
}
```

- [ ] **Step 2: Refresh Unity и проверить компиляцию**

Run: `mcp__unityMCP__refresh_unity`, дождаться `isCompiling == false`, `mcp__unityMCP__read_console` (filter Error).
Expected: 0 ошибок.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/Data/AdjacencyRule.cs Game/Assets/Code/Data/AdjacencyRule.cs.meta
git commit -m "feat(adjacency): AdjacencyRule.ParseRules — пары таргет/эффект через '|'"
```

---

## Task 3: LootData — заменить поля на AdjacencyRules

**Files:**
- Modify: `Game/Assets/Code/Data/LootData.cs:15-16`

- [ ] **Step 1: Заменить два поля одним**

Заменить (строки 15–16):

```csharp
        public string AdjacencyTarget;            // empty = no property
        public AdjacencyEffect[] AdjacencyEffects; // never null; empty if no property
```

на:

```csharp
        public AdjacencyRule[] AdjacencyRules;    // never null; пустой = нет свойства
```

- [ ] **Step 2: НЕ рефрешить отдельно**

Этот файл ломает компиляцию `LootCatalog`/`GameContext`/`TooltipController` до их обновления — это ожидаемо. Компиляцию проверяем после Task 7. Сразу переходим к коммиту (код самосогласован внутри файла).

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/Data/LootData.cs
git commit -m "refactor(adjacency): LootData хранит AdjacencyRule[] вместо target+effects"
```

---

## Task 4: LootCatalog — вызвать ParseRules

**Files:**
- Modify: `Game/Assets/Code/Catalogs/LootCatalog.cs:30-31`

- [ ] **Step 1: Заменить заполнение полей**

Заменить (строки 30–31):

```csharp
                    AdjacencyTarget = row[8],
                    AdjacencyEffects = AdjacencyEffect.ParseList(row[9]),
```

на:

```csharp
                    AdjacencyRules = AdjacencyRule.ParseRules(row[8], row[9]),
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Catalogs/LootCatalog.cs
git commit -m "refactor(adjacency): LootCatalog грузит AdjacencyRules через ParseRules"
```

---

## Task 5: AdjacencyResolver — новая сигнатура и аддитивная логика

**Files:**
- Modify: `Game/Assets/Code/Logic/AdjacencyResolver.cs`

- [ ] **Step 1: Заменить обе перегрузки `Resolve` (строки 18–96)**

Заменить блок от `public static AdjacencyResult<T> Resolve<T>(` (первая перегрузка, строка 18) до закрывающей `return result; }` второй перегрузки (строка 96) на:

```csharp
        public static AdjacencyResult<T> Resolve<T>(
            GridModel<T> grid,
            Func<T, string> idOf,
            Func<T, int> baseGoldOf,
            Func<T, int> baseAcidOf,
            Func<T, AdjacencyRule[]> adjacencyRulesOf
        ) where T : class
            => Resolve(grid, idOf, baseGoldOf, baseAcidOf, adjacencyRulesOf, _ => 0);

        public static AdjacencyResult<T> Resolve<T>(
            GridModel<T> grid,
            Func<T, string> idOf,
            Func<T, int> baseGoldOf,
            Func<T, int> baseAcidOf,
            Func<T, AdjacencyRule[]> adjacencyRulesOf,
            Func<T, int> neighborGoldPctOf
        ) where T : class
        {
            var result = new AdjacencyResult<T>();

            // 1. Базовые значения
            foreach (var item in grid.AllItems())
            {
                result.EffectiveGold[item] = baseGoldOf(item);
                result.EffectiveAcid[item] = baseAcidOf(item);
            }

            // 2. Входящие эффекты: суммируем множители по типам аддитивно, применяем к базе один раз
            foreach (var item in grid.AllItems())
            {
                var rules = adjacencyRulesOf(item);
                if (rules == null || rules.Length == 0) continue;

                var neighbors = GetEdgeNeighbors(grid, item);

                float sumGold = 0f;
                float sumAcid = 0f;
                foreach (var rule in rules)
                {
                    if (string.IsNullOrEmpty(rule.Target) || rule.Effects == null || rule.Effects.Length == 0)
                        continue;

                    // число различных прилегающих инстансов с нужным id
                    int count = 0;
                    foreach (var n in neighbors)
                    {
                        if (ReferenceEquals(n, item)) continue;
                        if (idOf(n) == rule.Target) count++;
                    }
                    if (count == 0) continue;

                    foreach (var fx in rule.Effects)
                    {
                        int times = fx.Stackable ? count : 1;
                        float contribution = fx.Multiplier * times;
                        if (fx.Type == EffectType.Gold) sumGold += contribution;
                        else if (fx.Type == EffectType.Acid) sumAcid += contribution;
                    }
                }

                if (sumGold != 0f)
                {
                    float g = baseGoldOf(item) * (1f + sumGold);
                    result.EffectiveGold[item] = (int)System.Math.Max(0, System.Math.Round(g));
                }
                if (sumAcid != 0f)
                {
                    float a = baseAcidOf(item) * (1f + sumAcid);
                    result.EffectiveAcid[item] = (int)System.Math.Max(1, System.Math.Round(a));
                }
            }

            // 2b. Исходящий эффект: предмет с NeighborGoldPct != 0 меняет золото 4-соседей.
            foreach (var src in grid.AllItems())
            {
                int pct = neighborGoldPctOf(src);
                if (pct == 0) continue;
                foreach (var nb in GetEdgeNeighbors(grid, src))
                {
                    if (ReferenceEquals(nb, src)) continue;
                    int g = result.EffectiveGold[nb];
                    result.EffectiveGold[nb] = (int)System.Math.Max(0, System.Math.Round(g * (1f + pct / 100f)));
                }
            }

            // 3. Total (после исходящих эффектов)
            foreach (var v in result.EffectiveGold.Values) result.TotalGold += v;
            return result;
        }
```

(Методы `GetEdgeNeighbors` и `TryAddNeighbor` ниже — без изменений.)

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Logic/AdjacencyResolver.cs
git commit -m "feat(adjacency): аддитивное суммирование правил + стак по числу инстансов"
```

---

## Task 6: GameContext — передать AdjacencyRules

**Files:**
- Modify: `Game/Assets/Code/Game/GameContext.cs:79-80`

- [ ] **Step 1: Заменить два делегата одним**

Заменить (строки 79–80):

```csharp
                v => v.Data.AdjacencyTarget,
                v => v.Data.AdjacencyEffects,
```

на:

```csharp
                v => v.Data.AdjacencyRules,
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Game/GameContext.cs
git commit -m "refactor(adjacency): GameContext передаёт AdjacencyRules в resolver"
```

---

## Task 7: TooltipController — описание по правилам

**Files:**
- Modify: `Game/Assets/Code/UI/TooltipController.cs:238-254`

- [ ] **Step 1: Переписать `DescribeAdjacency`**

Заменить весь метод (строки 238–254):

```csharp
        private string DescribeAdjacency(LootData data)
        {
            if (string.IsNullOrEmpty(data.AdjacencyTarget) ||
                data.AdjacencyEffects == null || data.AdjacencyEffects.Length == 0) return "";

            string targetName = LookupName(data.AdjacencyTarget);
            var sb = new StringBuilder();
            sb.Append($"Рядом с «{targetName}»:");
            foreach (var fx in data.AdjacencyEffects)
            {
                string kind = fx.Type == EffectType.Gold ? "цена" : "стоимость переваривания";
                string sign = fx.Multiplier >= 0 ? "+" : "";
                int pct = Mathf.RoundToInt(fx.Multiplier * 100f);
                sb.Append($"\n   • {kind} {sign}{pct}%");
            }
            return sb.ToString();
        }
```

на:

```csharp
        private string DescribeAdjacency(LootData data)
        {
            if (data.AdjacencyRules == null || data.AdjacencyRules.Length == 0) return "";

            var sb = new StringBuilder();
            foreach (var rule in data.AdjacencyRules)
            {
                if (rule.Effects == null || rule.Effects.Length == 0) continue;
                if (sb.Length > 0) sb.Append('\n');
                string targetName = LookupName(rule.Target);
                sb.Append($"Рядом с «{targetName}»:");
                foreach (var fx in rule.Effects)
                {
                    string kind = fx.Type == EffectType.Gold ? "цена" : "стоимость переваривания";
                    string sign = fx.Multiplier >= 0 ? "+" : "";
                    int pct = Mathf.RoundToInt(fx.Multiplier * 100f);
                    string per = fx.Stackable ? " за каждый" : "";
                    sb.Append($"\n   • {kind} {sign}{pct}%{per}");
                }
            }
            return sb.ToString();
        }
```

- [ ] **Step 2: Refresh Unity и проверить компиляцию всего проекта**

Run: `mcp__unityMCP__refresh_unity`, дождаться `isCompiling == false`, `mcp__unityMCP__read_console` (filter Error).
Expected: 0 ошибок. Теперь все зависимые файлы (Tasks 3–7) согласованы и проект компилируется.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/TooltipController.cs
git commit -m "feat(adjacency): тултип показывает несколько правил и пометку 'за каждый'"
```

---

## Task 8: Верификация логики через Unity MCP

**Files:** нет (прогон в редакторе).

Цель — автоматически проверить парсинг и resolver на контрольных кейсах из спеки. Прогоняется после успешной компиляции (Task 7).

- [ ] **Step 1: Прогнать верификационный сниппет**

Через `mcp__unityMCP__execute_code` выполнить C#:

```csharp
using System;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

string Fail(string m) { Debug.LogError("VERIFY FAIL: " + m); return "FAIL: " + m; }

// --- Парсинг ---
var rules = AdjacencyRule.ParseRules("hat|sheath|frog", "gold:+50%|gold:+50%|gold:-50%");
if (rules.Length != 3) return Fail($"ожидал 3 правила, получил {rules.Length}");
if (rules[2].Target != "frog") return Fail("3-й таргет не frog");
if (Mathf.Abs(rules[0].Effects[0].Multiplier - 0.5f) > 1e-4f) return Fail("hat множитель != 0.5");

var stackFx = AdjacencyEffect.ParseList("gold:+50%*");
if (!stackFx[0].Stackable) return Fail("'*' не распознан как Stackable");
var plainFx = AdjacencyEffect.ParseList("gold:+50%");
if (plainFx[0].Stackable) return Fail("без '*' Stackable должен быть false");

bool mismatchThrew = false;
try { AdjacencyRule.ParseRules("hat|sheath", "gold:+50%"); }
catch (FormatException) { mismatchThrew = true; }
if (!mismatchThrew) return Fail("несовпадение длин не кинуло FormatException");

// --- Resolver: хелпер для одноклеточных предметов ---
LootData Mk(string id, int gold, int acid, AdjacencyRule[] rs) => new LootData {
    Id = id, Gold = gold, AcidCost = acid, Shape = Shape.Parse("X"),
    AdjacencyRules = rs ?? Array.Empty<AdjacencyRule>()
};
AdjacencyResult<LootData> Run(GridModel<LootData> g) => AdjacencyResolver.Resolve(
    g, v => v.Id, v => v.Gold, v => v.AcidCost, v => v.AdjacencyRules, v => v.NeighborGoldPct);

// Кейс A: стак +50%* с двумя hat => +100% => 200
{
    var g = new GridModel<LootData>(3, 3);
    var diamond = Mk("diamond", 100, 1, AdjacencyRule.ParseRules("hat", "gold:+50%*"));
    g.TryPlace(diamond, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(diamond);
    if (got != 200) return Fail($"стак: ожидал 200, получил {got}");
}

// Кейс B: нестак +50% с двумя hat => +50% => 150
{
    var g = new GridModel<LootData>(3, 3);
    var diamond = Mk("diamond", 100, 1, AdjacencyRule.ParseRules("hat", "gold:+50%"));
    g.TryPlace(diamond, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(diamond);
    if (got != 150) return Fail($"нестак: ожидал 150, получил {got}");
}

// Кейс C: две пары, оба соседа => +50% + +50% = +100% => 20
{
    var g = new GridModel<LootData>(3, 3);
    var sword = Mk("sword", 10, 1, AdjacencyRule.ParseRules("hat|sheath", "gold:+50%|gold:+50%"));
    g.TryPlace(sword, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("sheath", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(sword);
    if (got != 20) return Fail($"две пары: ожидал 20, получил {got}");
}

// Кейс D: отрицательный стак -50%* с тремя frog => -150% => пол 0
{
    var g = new GridModel<LootData>(3, 3);
    var coin = Mk("coin", 100, 1, AdjacencyRule.ParseRules("frog", "gold:-50%*"));
    g.TryPlace(coin, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 2, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 1, 0, Rotation.Deg0);
    int got = Run(g).GetGold(coin);
    if (got != 0) return Fail($"минус-стак: ожидал 0, получил {got}");
}

return "ALL ADJACENCY CHECKS PASSED";
```

Expected: возвращает `"ALL ADJACENCY CHECKS PASSED"`, в консоли нет `VERIFY FAIL`.

- [ ] **Step 2: Проверить, что текущий конфиг грузится**

Через `mcp__unityMCP__execute_code`:

```csharp
Mimic.Catalogs.LootCatalog.Load();
return "loot.csv OK, items=" + Mimic.Catalogs.LootCatalog.ById.Count;
```

Expected: без исключений, `items=14` (число строк лута в `loot.csv.txt`). Существующие одиночные пары (`shield`, `gem`, `barracuda`, `poop`) валидны как одно правило.

- [ ] **Step 3: Финальный коммит-маркер (если были правки конфига/прочее)**

Если на этом шаге ничего не менялось в файлах — пропустить. Иначе:

```bash
git add -A
git commit -m "test(adjacency): верификация мульти-таргет и стака пройдена"
```

---

## Self-Review (выполнено автором плана)

- **Покрытие спеки:** формат `|`/`;`/`*` → Task 1+2; модель `AdjacencyRule`/`Stackable` → Task 1+2+3; аддитивная логика + стак по инстансам → Task 5; ошибка при несовпадении длин → Task 2 (+ проверка в Task 8); тултип → Task 7; `NeighborGoldPct` не тронут → сохранён в Task 5; контрольные кейсы спеки → Task 8. Пробелов нет.
- **Плейсхолдеры:** нет TBD/TODO; весь код приведён целиком.
- **Согласованность типов:** `AdjacencyRule{ Target, Effects }`, `AdjacencyEffect{ Type, Multiplier, Stackable }`, `LootData.AdjacencyRules`, `AdjacencyRule.ParseRules(targetRaw, effectRaw)`, resolver-делегат `Func<T, AdjacencyRule[]>` — имена совпадают во всех задачах и в верификации.
