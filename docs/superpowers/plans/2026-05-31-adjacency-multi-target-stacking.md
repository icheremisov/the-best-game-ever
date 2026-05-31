# Adjacency блочный формат, мульти-таргет, вайлдкард, стак — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Adjacency задаётся одним полем CSV с блочной грамматикой `targets|effects` (блоки через `;`, таргеты/эффекты через `,`, вайлдкард `*`, стак-суффикс `*`); множители одного типа складываются аддитивно и применяются к базе один раз.

**Architecture:** `AdjacencyEffect.Parse` парсит один эффект (знак опционален, `*` = стак). `AdjacencyRule.ParseRules` парсит всё поле: блоки по `;`, внутри `targets|effects`, таргеты/эффекты по `,`, `*`-таргет = вайлдкард. `LootData` хранит `AdjacencyRule[]`. `AdjacencyResolver` для каждого предмета считает прилегающие инстансы по каждому блоку (вайлдкард — по неназванным id), суммирует вклады по типам и применяет к базе единожды.

**Tech Stack:** Unity 6000.3.16f1, C# (Assembly-CSharp, без asmdef). Автотестов нет — верификация через Unity MCP `execute_code` после компиляции.

---

## Порядок и компиляция

`LootData` / `LootCatalog` / `AdjacencyResolver` / `GameContext` / `TooltipController` взаимозависимы — проект не компилируется, пока все не согласованы. Код пишется задачами 1–8 подряд (коммит на каждую), единый верификационный прогон — задача 9.

После правок `.cs`: `mcp__unityMCP__refresh_unity` → дождаться `editor_state.isCompiling == false` → `mcp__unityMCP__read_console` (filter Error). Полную компиляцию проверяем после Task 8. Правки делаются инструментом `Edit`.

---

## File Structure

- `Game/Assets/Code/Data/AdjacencyEffect.cs` — **modify**: `Stackable`, публичный `Parse`, опциональный знак, удалить `ParseList`.
- `Game/Assets/Code/Data/AdjacencyRule.cs` — **create**: struct + `ParseRules`.
- `Game/Assets/Code/Data/LootData.cs` — **modify**: поле `AdjacencyRules`.
- `Game/Assets/Code/Catalogs/LootCatalog.cs` — **modify**: `ParseRules(row[8])` + реиндексация 9–14.
- `Game/Assets/Code/Logic/AdjacencyResolver.cs` — **modify**: вайлдкард + аддитив + стак.
- `Game/Assets/Code/Game/GameContext.cs` — **modify**: передать `AdjacencyRules`.
- `Game/Assets/Code/UI/TooltipController.cs` — **modify**: описание по блокам.
- `Game/Assets/Resources/Configs/loot.csv.txt` — **modify**: удалить столбец `adjacencyTarget`, склеить поле.

---

## Task 1: AdjacencyEffect — Parse, Stackable, опциональный знак

**Files:**
- Modify (rewrite целиком): `Game/Assets/Code/Data/AdjacencyEffect.cs`

- [ ] **Step 1: Переписать файл целиком**

Полное новое содержимое `Game/Assets/Code/Data/AdjacencyEffect.cs`:

```csharp
using System;

namespace Mimic.Data
{
    public enum EffectType { Gold, Acid }

    public struct AdjacencyEffect
    {
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%
        public bool Stackable;   // '*' суффикс: применять за каждый прилегающий инстанс

        // Парсит ОДИН эффект: '<type>:<sign?><n>%' с опциональным '*' в конце.
        // Знак опционален: 'gold:5%' == 'gold:+5%'.
        public static AdjacencyEffect Parse(string token)
        {
            token = token.Trim();

            bool stackable = false;
            if (token.EndsWith("*"))
            {
                stackable = true;
                token = token.Substring(0, token.Length - 1).Trim();
            }

            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Эффект должен быть '<type>:<sign><n>%': '{token}'");

            string typeStr = token.Substring(0, colon).Trim().ToLowerInvariant();
            EffectType type = typeStr switch
            {
                "gold" => EffectType.Gold,
                "acid" => EffectType.Acid,
                _ => throw new FormatException($"Неизвестный тип эффекта '{typeStr}'")
            };

            string val = token.Substring(colon + 1).Trim();
            if (!val.EndsWith("%"))
                throw new FormatException($"Значение эффекта должно оканчиваться на %: '{val}'");
            val = val.Substring(0, val.Length - 1).Trim();

            // NumberStyles.Float допускает ведущий знак, поэтому '+50', '-50' и '50' все валидны.
            if (!float.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float pct))
                throw new FormatException($"Значение эффекта не число: '{val}'");

            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f, Stackable = stackable };
        }
    }
}
```

- [ ] **Step 2: Refresh Unity и проверить компиляцию**

Run: `mcp__unityMCP__refresh_unity`, дождаться `isCompiling == false`, `mcp__unityMCP__read_console` (filter Error).
Expected: появится ошибка в `LootCatalog.cs` (`AdjacencyEffect.ParseList` больше нет) — это ожидаемо, чинится в Task 4. Других ошибок в этом файле быть не должно.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/Data/AdjacencyEffect.cs
git commit -m "feat(adjacency): AdjacencyEffect.Parse — стак '*' и опциональный знак"
```

---

## Task 2: AdjacencyRule — блочный парсер

**Files:**
- Create: `Game/Assets/Code/Data/AdjacencyRule.cs`

- [ ] **Step 1: Создать файл**

Содержимое `Game/Assets/Code/Data/AdjacencyRule.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Mimic.Data
{
    // Один блок «таргеты | эффекты». Несколько блоков на предмете разделяются ';'.
    public struct AdjacencyRule
    {
        public string[] Targets;          // id соседей; пусто при Wildcard
        public bool Wildcard;             // таргет '*' — все НЕназванные соседи
        public AdjacencyEffect[] Effects; // эффекты блока (never null/empty в валидном правиле)

        // Грамматика поля:
        //   <block>(';'<block>)*, block := <targets>'|'<effects>
        //   targets через ',', effects через ',', '*' слева — вайлдкард, '*' в конце эффекта — стак.
        public static AdjacencyRule[] ParseRules(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<AdjacencyRule>();

            // Переносы строк (из многострочного оформления в Google Docs) — как пробел.
            raw = raw.Replace('\r', ' ').Replace('\n', ' ');

            var blocks = raw.Split(';');
            var rules = new List<AdjacencyRule>();
            foreach (var blockRaw in blocks)
            {
                var block = blockRaw.Trim();
                if (block.Length == 0) continue; // допускаем хвостовой ';'

                int bar = block.IndexOf('|');
                if (bar <= 0 || bar == block.Length - 1)
                    throw new FormatException($"Блок adjacency должен быть '<targets>|<effects>': '{block}'");

                string targetsPart = block.Substring(0, bar);
                string effectsPart = block.Substring(bar + 1);

                // --- таргеты ---
                var targets = new List<string>();
                bool wildcard = false;
                foreach (var tt in targetsPart.Split(','))
                {
                    var t = tt.Trim();
                    if (t.Length == 0) continue;
                    if (t == "*") wildcard = true;
                    else targets.Add(t);
                }
                if (wildcard && targets.Count > 0)
                    throw new FormatException($"Вайлдкард '*' нельзя смешивать с id: '{targetsPart}'");
                if (!wildcard && targets.Count == 0)
                    throw new FormatException($"Пустой список таргетов в блоке: '{block}'");

                // --- эффекты ---
                var effects = new List<AdjacencyEffect>();
                foreach (var et in effectsPart.Split(','))
                {
                    var e = et.Trim();
                    if (e.Length == 0) continue;
                    effects.Add(AdjacencyEffect.Parse(e));
                }
                if (effects.Count == 0)
                    throw new FormatException($"Пустой список эффектов в блоке: '{block}'");

                rules.Add(new AdjacencyRule
                {
                    Targets = targets.ToArray(),
                    Wildcard = wildcard,
                    Effects = effects.ToArray()
                });
            }
            return rules.ToArray();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Data/AdjacencyRule.cs Game/Assets/Code/Data/AdjacencyRule.cs.meta
git commit -m "feat(adjacency): AdjacencyRule.ParseRules — блочный формат с вайлдкардом"
```

---

## Task 3: LootData — поле AdjacencyRules

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

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Data/LootData.cs
git commit -m "refactor(adjacency): LootData хранит AdjacencyRule[]"
```

---

## Task 4: LootCatalog — ParseRules и реиндексация столбцов

**Files:**
- Modify: `Game/Assets/Code/Catalogs/LootCatalog.cs:30-37`

После удаления столбца `adjacencyTarget` индексы столбцов 9–15 сдвигаются на −1.

- [ ] **Step 1: Заменить блок присваиваний**

Заменить (строки 30–37):

```csharp
                    AdjacencyTarget = row[8],
                    AdjacencyEffects = AdjacencyEffect.ParseList(row[9]),
                    Category = ParseCategory(Col(row, 10, "normal")),
                    AcidRestoreOnDigest = int.Parse(Col(row, 11, "0")),
                    DamageOnDigest = int.Parse(Col(row, 12, "0")),
                    CanReturnToBasket = Col(row, 13, "1") != "0",
                    IsGlue = Col(row, 14, "0") == "1",
                    NeighborGoldPct = int.Parse(Col(row, 15, "0")),
```

на:

```csharp
                    AdjacencyRules = AdjacencyRule.ParseRules(row[8]),
                    Category = ParseCategory(Col(row, 9, "normal")),
                    AcidRestoreOnDigest = int.Parse(Col(row, 10, "0")),
                    DamageOnDigest = int.Parse(Col(row, 11, "0")),
                    CanReturnToBasket = Col(row, 12, "1") != "0",
                    IsGlue = Col(row, 13, "0") == "1",
                    NeighborGoldPct = int.Parse(Col(row, 14, "0")),
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Catalogs/LootCatalog.cs
git commit -m "refactor(adjacency): LootCatalog грузит AdjacencyRules, реиндексация столбцов"
```

---

## Task 5: AdjacencyResolver — вайлдкард, аддитив, стак

**Files:**
- Modify: `Game/Assets/Code/Logic/AdjacencyResolver.cs`

- [ ] **Step 1: Заменить обе перегрузки `Resolve` (строки 18–96)**

Заменить блок от первой `public static AdjacencyResult<T> Resolve<T>(` (строка 18) до закрывающей `return result; }` второй перегрузки (строка 96) на:

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

                // множество явно названных таргетов предмета (для вайлдкарда)
                var named = new HashSet<string>();
                foreach (var rule in rules)
                    if (!rule.Wildcard && rule.Targets != null)
                        foreach (var t in rule.Targets) named.Add(t);

                float sumGold = 0f;
                float sumAcid = 0f;
                foreach (var rule in rules)
                {
                    if (rule.Effects == null || rule.Effects.Length == 0) continue;

                    int count = 0;
                    foreach (var n in neighbors)
                    {
                        if (ReferenceEquals(n, item)) continue;
                        string nid = idOf(n);
                        bool match = rule.Wildcard ? !named.Contains(nid) : ContainsId(rule.Targets, nid);
                        if (match) count++;
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

        private static bool ContainsId(string[] ids, string id)
        {
            if (ids == null) return false;
            for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return true;
            return false;
        }
```

(Методы `GetEdgeNeighbors` и `TryAddNeighbor` ниже — без изменений.)

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Logic/AdjacencyResolver.cs
git commit -m "feat(adjacency): вайлдкард, аддитивная сумма блоков, стак по инстансам"
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
git commit -m "refactor(adjacency): GameContext передаёт AdjacencyRules"
```

---

## Task 7: TooltipController — описание по блокам

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

                string who;
                if (rule.Wildcard)
                {
                    who = "прочими предметами";
                }
                else
                {
                    var names = new string[rule.Targets.Length];
                    for (int i = 0; i < rule.Targets.Length; i++) names[i] = LookupName(rule.Targets[i]);
                    who = "«" + string.Join("» или «", names) + "»";
                }
                sb.Append($"Рядом с {who}:");

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

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/UI/TooltipController.cs
git commit -m "feat(adjacency): тултип описывает блоки (мульти-таргет, вайлдкард, 'за каждый')"
```

---

## Task 8: loot.csv — удалить столбец adjacencyTarget, склеить поле

**Files:**
- Modify (rewrite): `Game/Assets/Resources/Configs/loot.csv.txt`

Старые `adjacencyTarget` + `adjacencyEffect` склеиваются в одно поле
`target|effect`. Пустые → пусто. Ни одна существующая строка не содержит запятых
в adjacency-поле, поэтому кавычки не нужны (поле `description` остаётся как было).

- [ ] **Step 1: Записать новое содержимое файла**

Полное новое содержимое `Game/Assets/Resources/Configs/loot.csv.txt`:

```
id,name,description,shape,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyEffect,category,acidRestoreOnDigest,damageOnDigest,canReturnToBasket,glue,neighborGoldPct
sword,Меч,"Острый, но тупой",X|X|X|X,10,3,0,0,,normal,0,0,1,0,0
shield,Щит,Защита от себя,XX|XX,8,4,5,0,sword|gold:+50%,normal,0,0,1,0,0
barracuda,Барракуда,Ещё трепыхается,X.|XX|X.|X.,15,5,0,2,bread|acid:-30%,normal,0,0,1,0,0
bread,Хлеб,Несвежий,XX,3,1,2,0,,normal,0,0,1,0,0
gem,Самоцвет,Дешёвый блеск,X,20,2,0,0,gem|gold:+25%,normal,0,0,1,0,0
heart,Сердце,Бьётся,X,0,99,0,0,,fixture,0,0,0,0,0
stomach,Желудок,Урчит,XX|XX,0,99,0,0,,fixture,0,0,0,0,0
mimicburger,Мимик-бургер,Зубастый!,XX,0,3,8,0,,reward,0,0,1,0,0
acidbottle,Бутылка кислоты,Шипит,X|X,0,2,0,0,,reward,6,0,1,0,0
firecola,Огне-кола,Отрыжка пламенем,X,0,2,0,0,,reward,0,0,1,0,0
weight,Гиря 60кг,Тяжёлая,XX|XX|XX,0,5,0,0,,punish,0,30,0,0,0
poop,Какашка,Портит всё!,X,0,4,0,0,stomach|acid:+50%,punish,0,8,0,0,-50
glue,Масса клея,Липкая,XX,0,3,0,0,,punish,0,4,0,1,0
tooth,Зуб с кариесом,Болит,X,0,9,0,0,,punish,0,0,0,0,0
```

- [ ] **Step 2: Refresh Unity и проверить компиляцию всего проекта**

Run: `mcp__unityMCP__refresh_unity`, дождаться `isCompiling == false`, `mcp__unityMCP__read_console` (filter Error).
Expected: 0 ошибок — все зависимые файлы согласованы, проект компилируется.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Resources/Configs/loot.csv.txt
git commit -m "config(adjacency): удалить столбец adjacencyTarget, склеить в adjacencyEffect"
```

---

## Task 9: Верификация логики через Unity MCP

**Files:** нет (прогон в редакторе после компиляции).

- [ ] **Step 1: Прогнать верификационный сниппет**

Через `mcp__unityMCP__execute_code` выполнить:

```csharp
using System;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

string Fail(string m) { Debug.LogError("VERIFY FAIL: " + m); return "FAIL: " + m; }

// --- Парсинг блочной строки ---
var rules = AdjacencyRule.ParseRules("hat | gold:+50% , acid:+50% ; sheath , frog | gold:-50% ; *|gold:5%");
if (rules.Length != 3) return Fail($"ожидал 3 блока, получил {rules.Length}");
if (rules[0].Wildcard || rules[0].Targets.Length != 1 || rules[0].Targets[0] != "hat") return Fail("блок 0 != hat");
if (rules[0].Effects.Length != 2) return Fail("блок 0: ожидал 2 эффекта");
if (rules[1].Targets.Length != 2 || rules[1].Targets[1] != "frog") return Fail("блок 1 != sheath,frog");
if (!rules[2].Wildcard) return Fail("блок 2 не вайлдкард");
if (Mathf.Abs(rules[2].Effects[0].Multiplier - 0.05f) > 1e-4f) return Fail("вайлдкард gold:5% != 0.05");

if (!AdjacencyEffect.Parse("gold:+50%*").Stackable) return Fail("'*' не распознан как Stackable");
if (AdjacencyEffect.Parse("gold:5%").Stackable) return Fail("без '*' Stackable должен быть false");

bool threw = false;
try { AdjacencyRule.ParseRules("hat"); } catch (FormatException) { threw = true; }
if (!threw) return Fail("блок без '|' не кинул FormatException");

// многострочный вариант (как в Google Docs) парсится так же, как однострочный
var multiline = AdjacencyRule.ParseRules("hat | gold:+50% , acid:+50% ;\n sheath , frog | gold:-50% ;\n *|gold:5%");
if (multiline.Length != 3) return Fail($"многострочный: ожидал 3 блока, получил {multiline.Length}");
if (multiline[1].Targets.Length != 2 || multiline[1].Targets[0] != "sheath") return Fail("многострочный блок 1 != sheath,frog");

// --- Resolver ---
LootData Mk(string id, int gold, int acid, AdjacencyRule[] rs) => new LootData {
    Id = id, Gold = gold, AcidCost = acid, Shape = Shape.Parse("X"),
    AdjacencyRules = rs ?? Array.Empty<AdjacencyRule>()
};
AdjacencyResult<LootData> Run(GridModel<LootData> g) => AdjacencyResolver.Resolve(
    g, v => v.Id, v => v.Gold, v => v.AcidCost, v => v.AdjacencyRules, v => v.NeighborGoldPct);

// A: стак +50%* с двумя hat => +100% => 200
{
    var g = new GridModel<LootData>(3, 3);
    var d = Mk("diamond", 100, 1, AdjacencyRule.ParseRules("hat|gold:+50%*"));
    g.TryPlace(d, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(d); if (got != 200) return Fail($"A стак: ожидал 200, получил {got}");
}
// B: нестак +50% с двумя hat => +50% => 150
{
    var g = new GridModel<LootData>(3, 3);
    var d = Mk("diamond", 100, 1, AdjacencyRule.ParseRules("hat|gold:+50%"));
    g.TryPlace(d, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(d); if (got != 150) return Fail($"B нестак: ожидал 150, получил {got}");
}
// C: мульти-таргет в одном блоке (нестак), hat+sheath => один раз +50% => 15
{
    var g = new GridModel<LootData>(3, 3);
    var s = Mk("sword", 10, 1, AdjacencyRule.ParseRules("hat,sheath|gold:+50%"));
    g.TryPlace(s, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("sheath", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(s); if (got != 15) return Fail($"C мульти-таргет: ожидал 15, получил {got}");
}
// D: два блока, hat(+50%) + sheath(+50%) => +100% => 20
{
    var g = new GridModel<LootData>(3, 3);
    var s = Mk("sword", 10, 1, AdjacencyRule.ParseRules("hat|gold:+50%;sheath|gold:+50%"));
    g.TryPlace(s, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("hat", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("sheath", 0, 1, null), 2, 1, Rotation.Deg0);
    int got = Run(g).GetGold(s); if (got != 20) return Fail($"D два блока: ожидал 20, получил {got}");
}
// E: вайлдкард — frog(-50%) + двое прочих(+10% нестак) => -40% => 60
{
    var g = new GridModel<LootData>(3, 3);
    var c = Mk("coin", 100, 1, AdjacencyRule.ParseRules("frog|gold:-50%;*|gold:+10%"));
    g.TryPlace(c, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("bread", 0, 1, null), 2, 1, Rotation.Deg0);
    g.TryPlace(Mk("gem", 0, 1, null), 1, 0, Rotation.Deg0);
    int got = Run(g).GetGold(c); if (got != 60) return Fail($"E вайлдкард: ожидал 60, получил {got}");
}
// F: отрицательный стак -50%* с тремя frog => -150% => пол 0
{
    var g = new GridModel<LootData>(3, 3);
    var c = Mk("coin", 100, 1, AdjacencyRule.ParseRules("frog|gold:-50%*"));
    g.TryPlace(c, 1, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 0, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 2, 1, Rotation.Deg0);
    g.TryPlace(Mk("frog", 0, 1, null), 1, 0, Rotation.Deg0);
    int got = Run(g).GetGold(c); if (got != 0) return Fail($"F минус-стак: ожидал 0, получил {got}");
}

return "ALL ADJACENCY CHECKS PASSED";
```

Expected: возвращает `"ALL ADJACENCY CHECKS PASSED"`, в консоли нет `VERIFY FAIL`.

- [ ] **Step 2: Проверить загрузку обновлённого конфига**

Через `mcp__unityMCP__execute_code`:

```csharp
Mimic.Catalogs.LootCatalog.Load();
var shield = Mimic.Catalogs.LootCatalog.Get("shield");
return $"loot.csv OK, items={Mimic.Catalogs.LootCatalog.ById.Count}, shield.rules={shield.AdjacencyRules.Length}, shield.target={(shield.AdjacencyRules.Length>0 ? shield.AdjacencyRules[0].Targets[0] : \"-\")}";
```

Expected: без исключений, `items=14`, `shield.rules=1`, `shield.target=sword`.

---

## Self-Review (выполнено автором плана)

- **Покрытие спеки:** грамматика блоков/`,`/`*`/вайлдкард → Task 1+2; опциональный
  знак → Task 1; модель `AdjacencyRule`/`AdjacencyRules` → Task 2+3; реиндексация
  CSV → Task 4+8; вайлдкард + аддитив + стак по инстансам → Task 5; `NeighborGoldPct`
  сохранён → Task 5; тултип (мульти-таргет/вайлдкард/«за каждый») → Task 7;
  миграция конфига → Task 8; контрольные кейсы → Task 9. Пробелов нет.
- **Плейсхолдеры:** нет TBD/TODO; весь код приведён целиком.
- **Согласованность типов:** `AdjacencyEffect.Parse(token)`, поля `{Type, Multiplier,
  Stackable}`; `AdjacencyRule{ Targets[], Wildcard, Effects[] }` + `ParseRules(raw)`;
  `LootData.AdjacencyRules`; resolver-делегат `Func<T, AdjacencyRule[]>`; helper
  `ContainsId(string[], string)`. Имена согласованы во всех задачах и верификации.
- **Реиндексация:** удаление столбца 8 сдвигает 9→8…15→14; LootCatalog (Task 4) и
  CSV (Task 8) согласованы по новым индексам.
```

