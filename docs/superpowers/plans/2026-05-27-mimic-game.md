# «Is this a mimic?» Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Сделать играбельный MVP пазл-инвентаря «Is this a mimic?» по [spec](../specs/2026-05-27-mimic-game-design.md) — drag&drop сетки 14×8 и 6×10, переваривание за желудочный сок, свойства «ЕСЛИ РЯДОМ С», цикл 5 приключенцев за один день, win/lose попап.

**Architecture:** Game jam — без архитектуры. Чистая логика (Shape/GridModel/AdjacencyResolver/CsvLoader) — обычные C# классы под EditMode-тестами. Игровые скрипты — обычные MonoBehaviour, без DI, ссылки через Inspector и `FindObjectOfType`. CSV-данные парсятся при старте `Game.unity`. Всё в uGUI Canvas (один layer для HUD/сеток, один поверх для drag/popup/tooltip).

**Tech Stack:** Unity 6000.3.16f1, URP 2D, новый Input System, uGUI, Unity Test Framework (NUnit) — EditMode only. Минимум 2 asmdef: `Code.asmdef` для всего игрового кода + `Code.Tests.asmdef` для тестов (асмдефы — единственный prag-компромисс с «no-architecture» правилом спеки, т.к. UTF без них не дружит).

---

## File Structure

Все пути относительно `Game/Assets/`.

### Создаваемые

| Путь | Ответственность |
|---|---|
| `Code/Code.asmdef` | Сборка игрового кода |
| `Code/Data/Shape.cs` | Парсинг pattern-string в `bool[,]`, поворот |
| `Code/Data/LootData.cs` | POCO одной строки `loot.csv` |
| `Code/Data/AdventurerData.cs` | POCO одной строки `adventurers.csv` |
| `Code/Data/DayData.cs` | POCO первой строки `day.csv` |
| `Code/Data/AdjacencyEffect.cs` | POCO эффекта (`gold`/`acid`, sign, percent) + парсер строки |
| `Code/Logic/GridModel.cs` | 2D массив клеток + `TryPlace`/`Remove`/`AllItems` |
| `Code/Logic/Rotation.cs` | enum + утилита поворота `bool[,]` |
| `Code/Logic/AdjacencyResolver.cs` | Пересчёт `effectiveGold/Acid` для всех item в мимике |
| `Code/Logic/CsvLoader.cs` | Парсер CSV с экранированием кавычками |
| `Code/Catalogs/LootCatalog.cs` | static-обёртка вокруг `List<LootData>` + lookup по id |
| `Code/Catalogs/AdventurerCatalog.cs` | то же для приключенцев |
| `Code/Catalogs/DayConfig.cs` | то же для дня |
| `Code/UI/GridView.cs` | визуализация сетки, обработка кликов по клеткам |
| `Code/UI/LootView.cs` | визуал предмета, `IPointerDownHandler`/`IPointerEnter/Exit` |
| `Code/UI/HudView.cs` | биндинг ресурсов в текстовые поля и бары |
| `Code/UI/TooltipController.cs` | показ/скрытие общего тултипа |
| `Code/UI/ContextMenuController.cs` | popup с кнопкой «Переварить» |
| `Code/UI/AdventurerIntroPopup.cs` | попап входа приключенца |
| `Code/UI/SurrenderConfirmPopup.cs` | confirm перед сдачей |
| `Code/UI/EndOfDayPopup.cs` | win/lose финальный попап |
| `Code/Input/InputBridge.cs` | подписки на Input System actions, форвард в DragController/ContextMenu |
| `Code/Input/DragController.cs` | синглтон, состояние «в руке», pick/drop/cancel/rotate |
| `Code/Game/GameFlow.cs` | FSM состояний дня |
| `Code/Game/GameResources.cs` | HP/ЖС/золото/квота (имя не `Resources` чтобы не конфликтовать с `UnityEngine.Resources`) |
| `Code/Game/GameContext.cs` | один MonoBehaviour-синглтон со ссылками на ключевые системы (`mimicGrid`, `adventurerGrid`, `resources`, `flow`, `catalogs`) — заменяет DI |
| `Code/Game/SfxPlayer.cs` | no-op `Play(string)` под будущие звуки |
| `Code.Tests/Code.Tests.asmdef` | Сборка тестов (EditMode) |
| `Code.Tests/ShapeTests.cs` | парсинг, поворот, валидация |
| `Code.Tests/GridModelTests.cs` | TryPlace/Remove, свободные клетки, occupancy |
| `Code.Tests/AdjacencyResolverTests.cs` | модификаторы, самососедство, clamp |
| `Code.Tests/AdjacencyEffectTests.cs` | парсинг строки эффекта |
| `Code.Tests/CsvLoaderTests.cs` | базовый парсер, экранирование кавычками |
| `Configs/loot.csv` | каталог предметов (5 шт MVP) |
| `Configs/adventurers.csv` | приключенцы (3 шт MVP) |
| `Configs/day.csv` | один день |
| `Prefabs/LootItem.prefab` | RectTransform-предмет с дочерними клетками |
| `Prefabs/MimicGrid.prefab` | Canvas-панель 14×8 |
| `Prefabs/AdventurerGrid.prefab` | Canvas-панель 6×10 |
| `Prefabs/TooltipPanel.prefab` | общий тултип |
| `Prefabs/ContextMenuPanel.prefab` | меню переваривания |
| `Prefabs/AdventurerIntroPopup.prefab` | попап входа |
| `Prefabs/SurrenderConfirmPopup.prefab` | confirm сдачи |
| `Prefabs/EndOfDayPopup.prefab` | финальный попап |
| `Scenes/Game.unity` | основная сцена |
| `Scenes/MainMenu.unity` | стартовое меню |

### Модифицируемые

| Путь | Что меняется |
|---|---|
| `Game/ProjectSettings/EditorBuildSettings.asset` | добавить `MainMenu.unity` и `Game.unity` в Build Scenes (index 0 = MainMenu) |

---

## Phase 0 — Project plumbing

### Task 0.1: Создать asmdef для кода и для тестов

**Files:**
- Create: `Game/Assets/Code/Code.asmdef`
- Create: `Game/Assets/Code.Tests/Code.Tests.asmdef`
- Modify: `Game/Assets/Code.Tests/` (создать папку)

- [ ] **Step 1: Создать `Code/Code.asmdef`**

```json
{
    "name": "Code",
    "rootNamespace": "Mimic",
    "references": [
        "GUID:75469ad4d38634e559750d17036d5f7c"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

(GUID — это `com.unity.inputsystem`. Если Unity ругнётся — заменить через UI в Inspector на ссылку «InputSystem».)

- [ ] **Step 2: Создать папку `Game/Assets/Code.Tests/` и удалить .gitkeep там, если есть**

```bash
mkdir -p /Volumes/Storage/Projects/MimicGame/Game/Assets/Code.Tests
```

- [ ] **Step 3: Создать `Code.Tests/Code.Tests.asmdef`**

```json
{
    "name": "Code.Tests",
    "rootNamespace": "Mimic.Tests",
    "references": [
        "Code",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferenced": false,
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Refresh Unity и проверить компиляцию**

В Unity Editor: Assets → Refresh (Cmd+R). Открыть Window → General → Test Runner → EditMode tab. Должен появиться пустой набор тестов от `Code.Tests`.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Code.asmdef Game/Assets/Code.Tests/Code.Tests.asmdef
git commit -m "chore: add Code and Code.Tests asmdef"
```

---

## Phase 1 — Core logic (TDD)

### Task 1.1: `Rotation` enum + поворот `bool[,]`

**Files:**
- Create: `Code/Logic/Rotation.cs`
- Create: `Code.Tests/RotationTests.cs`

- [ ] **Step 1: Написать падающий тест**

`Code.Tests/RotationTests.cs`:
```csharp
using NUnit.Framework;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class RotationTests
    {
        [Test]
        public void Rotate90CW_TurnsRowsIntoColumns()
        {
            // Input: 2x3 (rows=2, cols=3)
            //  T T F
            //  F T F
            var src = new bool[2, 3] { { true, true, false }, { false, true, false } };
            // Expected after 90° CW: 3x2 (rows=3, cols=2)
            //  F T
            //  T T
            //  F F
            var rotated = RotationUtil.Rotate90CW(src);

            Assert.AreEqual(3, rotated.GetLength(0));
            Assert.AreEqual(2, rotated.GetLength(1));
            Assert.IsFalse(rotated[0, 0]); Assert.IsTrue (rotated[0, 1]);
            Assert.IsTrue (rotated[1, 0]); Assert.IsTrue (rotated[1, 1]);
            Assert.IsFalse(rotated[2, 0]); Assert.IsFalse(rotated[2, 1]);
        }

        [Test]
        public void Rotate_FourTimes_ReturnsToOriginal()
        {
            var src = new bool[2, 3] { { true, false, true }, { false, true, false } };
            var rotated = RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(RotationUtil.Rotate90CW(src))));

            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Assert.AreEqual(src[r, c], rotated[r, c], $"mismatch at [{r},{c}]");
        }
    }
}
```

- [ ] **Step 2: Запустить тесты — должны не компилироваться**

В Unity Test Runner: EditMode → Run All. Ожидаемо: ошибки компиляции «type or namespace `Mimic.Logic` not found».

- [ ] **Step 3: Создать `Code/Logic/Rotation.cs`**

```csharp
namespace Mimic.Logic
{
    public enum Rotation { Deg0 = 0, Deg90 = 1, Deg180 = 2, Deg270 = 3 }

    public static class RotationUtil
    {
        public static bool[,] Rotate90CW(bool[,] src)
        {
            int rows = src.GetLength(0);
            int cols = src.GetLength(1);
            var dst = new bool[cols, rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    dst[c, rows - 1 - r] = src[r, c];
            return dst;
        }

        public static bool[,] Apply(bool[,] src, Rotation rot)
        {
            var result = src;
            for (int i = 0; i < (int)rot; i++)
                result = Rotate90CW(result);
            return result;
        }
    }
}
```

- [ ] **Step 4: Запустить тесты — должны пройти**

В Unity Test Runner: EditMode → Run All. Оба теста зелёные.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Logic/Rotation.cs Game/Assets/Code.Tests/RotationTests.cs
git commit -m "feat(logic): Rotation enum and 90° CW rotation util"
```

---

### Task 1.2: `Shape` — парсинг pattern-string + bounding box

**Files:**
- Create: `Code/Data/Shape.cs`
- Create: `Code.Tests/ShapeTests.cs`

- [ ] **Step 1: Написать падающие тесты**

`Code.Tests/ShapeTests.cs`:
```csharp
using NUnit.Framework;
using Mimic.Data;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class ShapeTests
    {
        [Test]
        public void Parse_SingleCell_ReturnsOneByOne()
        {
            var shape = Shape.Parse("X");
            Assert.AreEqual(1, shape.Rows);
            Assert.AreEqual(1, shape.Cols);
            Assert.IsTrue(shape.Cells[0, 0]);
        }

        [Test]
        public void Parse_TwoByTwoSquare()
        {
            var shape = Shape.Parse("XX|XX");
            Assert.AreEqual(2, shape.Rows);
            Assert.AreEqual(2, shape.Cols);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 2; c++)
                    Assert.IsTrue(shape.Cells[r, c]);
        }

        [Test]
        public void Parse_LShape()
        {
            // .X
            // XX
            // .X
            // .X
            var shape = Shape.Parse(".X|XX|.X|.X");
            Assert.AreEqual(4, shape.Rows);
            Assert.AreEqual(2, shape.Cols);
            Assert.IsFalse(shape.Cells[0, 0]); Assert.IsTrue (shape.Cells[0, 1]);
            Assert.IsTrue (shape.Cells[1, 0]); Assert.IsTrue (shape.Cells[1, 1]);
            Assert.IsFalse(shape.Cells[2, 0]); Assert.IsTrue (shape.Cells[2, 1]);
            Assert.IsFalse(shape.Cells[3, 0]); Assert.IsTrue (shape.Cells[3, 1]);
        }

        [Test]
        public void Parse_RowsOfDifferentLengths_Throws()
        {
            Assert.Throws<System.FormatException>(() => Shape.Parse("X|XX"));
        }

        [Test]
        public void Parse_EmptyString_Throws()
        {
            Assert.Throws<System.FormatException>(() => Shape.Parse(""));
        }

        [Test]
        public void GetRotatedCells_Deg90_TransposesDimensions()
        {
            var shape = Shape.Parse("X|X|X"); // 3x1
            var rotated = shape.GetRotatedCells(Rotation.Deg90);
            Assert.AreEqual(1, rotated.GetLength(0));
            Assert.AreEqual(3, rotated.GetLength(1));
        }
    }
}
```

- [ ] **Step 2: Запустить — должны не компилироваться**

EditMode → Run All. Ошибка: `Mimic.Data` / `Shape` not found.

- [ ] **Step 3: Создать `Code/Data/Shape.cs`**

```csharp
using System;
using Mimic.Logic;

namespace Mimic.Data
{
    public class Shape
    {
        public int Rows { get; }
        public int Cols { get; }
        public bool[,] Cells { get; }

        public Shape(bool[,] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            Cells = cells;
            Rows = cells.GetLength(0);
            Cols = cells.GetLength(1);
        }

        public static Shape Parse(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new FormatException("Shape pattern is empty");

            var rows = pattern.Split('|');
            int cols = rows[0].Length;
            if (cols == 0) throw new FormatException("Shape row is empty");
            foreach (var row in rows)
                if (row.Length != cols)
                    throw new FormatException($"Shape rows must be same length: got '{pattern}'");

            var cells = new bool[rows.Length, cols];
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    char ch = rows[r][c];
                    if (ch == 'X') cells[r, c] = true;
                    else if (ch == '.') cells[r, c] = false;
                    else throw new FormatException($"Shape cell must be 'X' or '.', got '{ch}'");
                }
            }
            return new Shape(cells);
        }

        public bool[,] GetRotatedCells(Rotation rot) => RotationUtil.Apply(Cells, rot);
    }
}
```

- [ ] **Step 4: Запустить — все 6 тестов зелёные**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Data/Shape.cs Game/Assets/Code.Tests/ShapeTests.cs
git commit -m "feat(data): Shape pattern-string parser with rotation"
```

---

### Task 1.3: `GridModel` — TryPlace, Remove, AllItems

**Files:**
- Create: `Code/Logic/GridModel.cs`
- Create: `Code.Tests/GridModelTests.cs`

- [ ] **Step 1: Тесты**

`Code.Tests/GridModelTests.cs`:
```csharp
using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class GridModelTests
    {
        // Простой токен, заменяющий LootView в логических тестах
        private class Token { public Shape Shape; }

        [Test]
        public void TryPlace_EmptyGrid_FitsAndOccupies()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX|XX") };
            bool ok = grid.TryPlace(token, x: 0, y: 0, rot: Rotation.Deg0);
            Assert.IsTrue(ok);
            Assert.AreSame(token, grid.GetAt(0, 0));
            Assert.AreSame(token, grid.GetAt(1, 0));
            Assert.AreSame(token, grid.GetAt(0, 1));
            Assert.AreSame(token, grid.GetAt(1, 1));
        }

        [Test]
        public void TryPlace_OutOfBounds_Fails()
        {
            var grid = new GridModel<Token>(2, 2);
            var token = new Token { Shape = Shape.Parse("XX|XX") };
            Assert.IsFalse(grid.TryPlace(token, 1, 1, Rotation.Deg0));
        }

        [Test]
        public void TryPlace_Overlap_Fails()
        {
            var grid = new GridModel<Token>(4, 4);
            var a = new Token { Shape = Shape.Parse("XX") };
            var b = new Token { Shape = Shape.Parse("XX") };
            Assert.IsTrue(grid.TryPlace(a, 0, 0, Rotation.Deg0));
            Assert.IsFalse(grid.TryPlace(b, 1, 0, Rotation.Deg0)); // overlaps a at (1,0)
        }

        [Test]
        public void TryPlace_WithRotation90_FitsRotated()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX|XX|XX") }; // 3 rows x 2 cols
            // Without rotation: doesn't fit at (3, 1) because shape is 3 tall
            Assert.IsFalse(grid.TryPlace(token, 3, 1, Rotation.Deg0));
            // After 90° CW: becomes 2 tall x 3 wide
            Assert.IsTrue(grid.TryPlace(token, 1, 2, Rotation.Deg90));
        }

        [Test]
        public void Remove_FreesCells()
        {
            var grid = new GridModel<Token>(4, 4);
            var token = new Token { Shape = Shape.Parse("XX") };
            grid.TryPlace(token, 0, 0, Rotation.Deg0);
            grid.Remove(token);
            Assert.IsNull(grid.GetAt(0, 0));
            Assert.IsNull(grid.GetAt(1, 0));
        }

        [Test]
        public void FreeCellsCount_TracksOccupancy()
        {
            var grid = new GridModel<Token>(3, 3); // 9 total
            Assert.AreEqual(9, grid.FreeCellsCount);
            grid.TryPlace(new Token { Shape = Shape.Parse("XX|XX") }, 0, 0, Rotation.Deg0);
            Assert.AreEqual(5, grid.FreeCellsCount);
        }

        [Test]
        public void AllItems_ReturnsUniqueItems()
        {
            var grid = new GridModel<Token>(4, 4);
            var a = new Token { Shape = Shape.Parse("XX|XX") };
            var b = new Token { Shape = Shape.Parse("X") };
            grid.TryPlace(a, 0, 0, Rotation.Deg0);
            grid.TryPlace(b, 3, 3, Rotation.Deg0);
            var items = new System.Collections.Generic.List<Token>(grid.AllItems());
            Assert.AreEqual(2, items.Count);
            Assert.Contains(a, items);
            Assert.Contains(b, items);
        }
    }
}
```

- [ ] **Step 2: Запустить — не компилируется**

- [ ] **Step 3: Создать `Code/Logic/GridModel.cs`**

```csharp
using System.Collections.Generic;
using Mimic.Data;

namespace Mimic.Logic
{
    public class GridModel<T> where T : class
    {
        public int Width { get; }
        public int Height { get; }
        private readonly T[,] cells;
        private readonly Dictionary<T, (int x, int y, Rotation rot)> placements = new();

        public GridModel(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new T[width, height];
        }

        public T GetAt(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return cells[x, y];
        }

        public bool TryPlace(T item, int x, int y, Rotation rot)
        {
            var shape = GetShape(item);
            var rotated = shape.GetRotatedCells(rot);
            int sh = rotated.GetLength(0); // rows
            int sw = rotated.GetLength(1); // cols

            // Bounds + overlap check
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    int gx = x + c;
                    int gy = y + r;
                    if (gx < 0 || gx >= Width || gy < 0 || gy >= Height) return false;
                    if (cells[gx, gy] != null) return false;
                }
            }

            // Commit
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    cells[x + c, y + r] = item;
                }
            }
            placements[item] = (x, y, rot);
            return true;
        }

        public void Remove(T item)
        {
            if (!placements.TryGetValue(item, out var p)) return;
            var rotated = GetShape(item).GetRotatedCells(p.rot);
            int sh = rotated.GetLength(0);
            int sw = rotated.GetLength(1);
            for (int r = 0; r < sh; r++)
            {
                for (int c = 0; c < sw; c++)
                {
                    if (!rotated[r, c]) continue;
                    cells[p.x + c, p.y + r] = null;
                }
            }
            placements.Remove(item);
        }

        public int FreeCellsCount
        {
            get
            {
                int count = 0;
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        if (cells[x, y] == null) count++;
                return count;
            }
        }

        public IEnumerable<T> AllItems() => placements.Keys;
        public bool TryGetPlacement(T item, out int x, out int y, out Rotation rot)
        {
            if (placements.TryGetValue(item, out var p))
            {
                x = p.x; y = p.y; rot = p.rot;
                return true;
            }
            x = y = 0; rot = Rotation.Deg0;
            return false;
        }

        // Shape provider — implemented either by reflection on the item, or via injected delegate.
        // For tests we use a duck-typed field `.Shape`.
        private static Shape GetShape(T item)
        {
            var prop = typeof(T).GetField("Shape");
            if (prop != null) return (Shape)prop.GetValue(item);
            var p = typeof(T).GetProperty("Shape");
            if (p != null) return (Shape)p.GetValue(item);
            throw new System.InvalidOperationException(
                $"Type {typeof(T).Name} must expose a public 'Shape' field or property");
        }
    }
}
```

- [ ] **Step 4: Запустить — все 7 тестов зелёные**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Logic/GridModel.cs Game/Assets/Code.Tests/GridModelTests.cs
git commit -m "feat(logic): GridModel with TryPlace/Remove/AllItems"
```

---

### Task 1.4: `AdjacencyEffect` — POCO + парсер строки `gold:+50%;acid:-30%`

**Files:**
- Create: `Code/Data/AdjacencyEffect.cs`
- Create: `Code.Tests/AdjacencyEffectTests.cs`

- [ ] **Step 1: Тесты**

`Code.Tests/AdjacencyEffectTests.cs`:
```csharp
using NUnit.Framework;
using Mimic.Data;

namespace Mimic.Tests
{
    public class AdjacencyEffectTests
    {
        [Test]
        public void ParseList_Empty_ReturnsEmpty()
        {
            Assert.AreEqual(0, AdjacencyEffect.ParseList("").Length);
            Assert.AreEqual(0, AdjacencyEffect.ParseList(null).Length);
        }

        [Test]
        public void ParseList_SingleGoldPlus50()
        {
            var fx = AdjacencyEffect.ParseList("gold:+50%");
            Assert.AreEqual(1, fx.Length);
            Assert.AreEqual(EffectType.Gold, fx[0].Type);
            Assert.AreEqual(0.5f, fx[0].Multiplier, 0.0001f);
        }

        [Test]
        public void ParseList_AcidMinus30()
        {
            var fx = AdjacencyEffect.ParseList("acid:-30%");
            Assert.AreEqual(1, fx.Length);
            Assert.AreEqual(EffectType.Acid, fx[0].Type);
            Assert.AreEqual(-0.3f, fx[0].Multiplier, 0.0001f);
        }

        [Test]
        public void ParseList_Multiple_SemicolonSeparated()
        {
            var fx = AdjacencyEffect.ParseList("gold:+50%;acid:-30%");
            Assert.AreEqual(2, fx.Length);
            Assert.AreEqual(EffectType.Gold, fx[0].Type);
            Assert.AreEqual(EffectType.Acid, fx[1].Type);
        }

        [Test]
        public void Parse_BadFormat_Throws()
        {
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.ParseList("gold+50"));
            Assert.Throws<System.FormatException>(() => AdjacencyEffect.ParseList("foo:+50%"));
        }
    }
}
```

- [ ] **Step 2: Запустить — не компилируется**

- [ ] **Step 3: Создать `Code/Data/AdjacencyEffect.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace Mimic.Data
{
    public enum EffectType { Gold, Acid }

    public struct AdjacencyEffect
    {
        public EffectType Type;
        public float Multiplier; // +0.5 = +50%, -0.3 = -30%

        public static AdjacencyEffect[] ParseList(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return Array.Empty<AdjacencyEffect>();
            var parts = raw.Split(';');
            var result = new List<AdjacencyEffect>(parts.Length);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;
                result.Add(ParseOne(trimmed));
            }
            return result.ToArray();
        }

        private static AdjacencyEffect ParseOne(string token)
        {
            int colon = token.IndexOf(':');
            if (colon <= 0 || colon == token.Length - 1)
                throw new FormatException($"Effect must be '<type>:<sign><n>%': got '{token}'");

            string typeStr = token.Substring(0, colon).Trim().ToLowerInvariant();
            EffectType type = typeStr switch
            {
                "gold" => EffectType.Gold,
                "acid" => EffectType.Acid,
                _ => throw new FormatException($"Unknown effect type '{typeStr}'")
            };

            string val = token.Substring(colon + 1).Trim();
            if (!val.EndsWith("%"))
                throw new FormatException($"Effect value must end with %: '{val}'");
            val = val.Substring(0, val.Length - 1);
            if (!val.StartsWith("+") && !val.StartsWith("-"))
                throw new FormatException($"Effect value must start with + or -: '{val}'");

            if (!float.TryParse(val, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float pct))
                throw new FormatException($"Effect value not a number: '{val}'");

            return new AdjacencyEffect { Type = type, Multiplier = pct / 100f };
        }
    }
}
```

- [ ] **Step 4: Запустить — все 5 тестов зелёные**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Data/AdjacencyEffect.cs Game/Assets/Code.Tests/AdjacencyEffectTests.cs
git commit -m "feat(data): AdjacencyEffect parser for 'gold:+50%;acid:-30%'"
```

---

### Task 1.5: `LootData` + `AdventurerData` + `DayData` POCOs (без логики, без тестов)

**Files:**
- Create: `Code/Data/LootData.cs`
- Create: `Code/Data/AdventurerData.cs`
- Create: `Code/Data/DayData.cs`

- [ ] **Step 1: `LootData.cs`**

```csharp
namespace Mimic.Data
{
    public class LootData
    {
        public string Id;
        public string Name;
        public string Description;
        public Shape Shape;
        public int Gold;
        public int AcidCost;
        public int HealOnDigest;
        public int CellsRestoredOnDigest;
        public string AdjacencyTarget;       // empty = no property
        public AdjacencyEffect[] AdjacencyEffects; // never null; empty if no property
    }
}
```

- [ ] **Step 2: `AdventurerData.cs`**

```csharp
namespace Mimic.Data
{
    public class AdventurerData
    {
        public string Id;
        public string Name;
        public string Phrase;
        public string[] LootIds;
    }
}
```

- [ ] **Step 3: `DayData.cs`**

```csharp
namespace Mimic.Data
{
    public class DayData
    {
        public int Day;
        public int GoldQuota;
        public int StartHp;
        public int StartAcid;
        public string[] AdventurerIds;
    }
}
```

- [ ] **Step 4: Refresh — компилируется без ошибок**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Data/LootData.cs Game/Assets/Code/Data/AdventurerData.cs Game/Assets/Code/Data/DayData.cs
git commit -m "feat(data): POCOs for loot, adventurer, day"
```

---

### Task 1.6: `CsvLoader` — низкоуровневый парсер строк с экранированием кавычками

**Files:**
- Create: `Code/Logic/CsvLoader.cs`
- Create: `Code.Tests/CsvLoaderTests.cs`

- [ ] **Step 1: Тесты**

```csharp
using NUnit.Framework;
using Mimic.Logic;

namespace Mimic.Tests
{
    public class CsvLoaderTests
    {
        [Test]
        public void ParseLine_SimpleCommas()
        {
            var fields = CsvLoader.ParseLine("a,b,c");
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, fields);
        }

        [Test]
        public void ParseLine_QuotedFieldWithComma()
        {
            var fields = CsvLoader.ParseLine("a,\"b,c\",d");
            CollectionAssert.AreEqual(new[] { "a", "b,c", "d" }, fields);
        }

        [Test]
        public void ParseLine_EmptyFieldsInMiddle()
        {
            var fields = CsvLoader.ParseLine("a,,c");
            CollectionAssert.AreEqual(new[] { "a", "", "c" }, fields);
        }

        [Test]
        public void ParseLine_TrailingEmpty()
        {
            var fields = CsvLoader.ParseLine("a,b,");
            CollectionAssert.AreEqual(new[] { "a", "b", "" }, fields);
        }

        [Test]
        public void ParseAll_SkipsHeader_AndBlankLines()
        {
            string csv = "h1,h2\na,1\n\nb,2\n";
            var rows = CsvLoader.ParseAll(csv);
            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "a", "1" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "b", "2" }, rows[1]);
        }
    }
}
```

- [ ] **Step 2: Запустить — не компилируется**

- [ ] **Step 3: Создать `Code/Logic/CsvLoader.cs`**

```csharp
using System.Collections.Generic;
using System.Text;

namespace Mimic.Logic
{
    public static class CsvLoader
    {
        // Parses a single CSV line with double-quote escaping.
        // Does NOT handle multi-line quoted fields (kept simple for jam).
        public static string[] ParseLine(string line)
        {
            var result = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            sb.Append('"');
                            i++;
                        }
                        else inQuotes = false;
                    }
                    else sb.Append(ch);
                }
                else
                {
                    if (ch == ',')
                    {
                        result.Add(sb.ToString());
                        sb.Clear();
                    }
                    else if (ch == '"' && sb.Length == 0) inQuotes = true;
                    else sb.Append(ch);
                }
            }
            result.Add(sb.ToString());
            return result.ToArray();
        }

        // Parses full CSV (LF or CRLF), skips header (first non-blank line), skips blank lines.
        public static List<string[]> ParseAll(string text)
        {
            var rows = new List<string[]>();
            var lines = text.Replace("\r\n", "\n").Split('\n');
            bool headerSkipped = false;
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                if (!headerSkipped) { headerSkipped = true; continue; }
                rows.Add(ParseLine(raw));
            }
            return rows;
        }
    }
}
```

- [ ] **Step 4: Запустить — все 5 тестов зелёные**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Logic/CsvLoader.cs Game/Assets/Code.Tests/CsvLoaderTests.cs
git commit -m "feat(logic): CsvLoader.ParseLine and ParseAll"
```

---

### Task 1.7: `AdjacencyResolver` — пересчёт effectiveGold/Acid

**Files:**
- Create: `Code/Logic/AdjacencyResolver.cs`
- Create: `Code.Tests/AdjacencyResolverTests.cs`

- [ ] **Step 1: Тесты**

```csharp
using NUnit.Framework;
using Mimic.Logic;
using Mimic.Data;

namespace Mimic.Tests
{
    public class AdjacencyResolverTests
    {
        // Item, дублирующий минимально нужное для теста (как Token из GridModelTests)
        private class Item
        {
            public string Id;
            public Shape Shape;
            public int Gold;
            public int AcidCost;
            public string AdjacencyTarget;
            public AdjacencyEffect[] AdjacencyEffects;
        }

        private static Item Mk(string id, string shape, int gold, int acid,
                               string adjTarget = null, string adjEffect = null)
        {
            return new Item
            {
                Id = id,
                Shape = Shape.Parse(shape),
                Gold = gold,
                AcidCost = acid,
                AdjacencyTarget = adjTarget,
                AdjacencyEffects = AdjacencyEffect.ParseList(adjEffect)
            };
        }

        [Test]
        public void Resolve_NoNeighbors_KeepsBaseValues()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            Assert.AreEqual(10, result.GetGold(sword));
            Assert.AreEqual(3, result.GetAcid(sword));
        }

        [Test]
        public void Resolve_NeighborMatchingTarget_AppliesEffect()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            var shield = Mk("shield", "X", 8, 4, adjTarget: "sword", adjEffect: "gold:+50%");
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            grid.TryPlace(shield, 1, 0, Rotation.Deg0); // adjacent on x-axis
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            Assert.AreEqual(8 + 4, result.GetGold(shield)); // 8 * 1.5 = 12
            Assert.AreEqual(10, result.GetGold(sword));     // sword has no effect
        }

        [Test]
        public void Resolve_AcidNegative_Reduces()
        {
            var grid = new GridModel<Item>(4, 4);
            var bread = Mk("bread", "X", 3, 1);
            var fish = Mk("fish", "X", 15, 5, adjTarget: "bread", adjEffect: "acid:-60%");
            grid.TryPlace(bread, 0, 0, Rotation.Deg0);
            grid.TryPlace(fish, 0, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            // 5 * 0.4 = 2; clamp to >= 1
            Assert.AreEqual(2, result.GetAcid(fish));
        }

        [Test]
        public void Resolve_AcidClampedToOne_NotZero()
        {
            var grid = new GridModel<Item>(4, 4);
            var bread = Mk("bread", "X", 3, 1);
            var fish = Mk("fish", "X", 15, 5, adjTarget: "bread", adjEffect: "acid:-99%");
            grid.TryPlace(bread, 0, 0, Rotation.Deg0);
            grid.TryPlace(fish, 0, 1, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            Assert.AreEqual(1, result.GetAcid(fish));
        }

        [Test]
        public void Resolve_SelfAdjacency_RequiresAnotherCopy()
        {
            var grid = new GridModel<Item>(4, 4);
            var a = Mk("gem", "X", 20, 2, adjTarget: "gem", adjEffect: "gold:+25%");
            grid.TryPlace(a, 0, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            // Alone — no boost
            Assert.AreEqual(20, result.GetGold(a));

            var b = Mk("gem", "X", 20, 2, adjTarget: "gem", adjEffect: "gold:+25%");
            grid.TryPlace(b, 1, 0, Rotation.Deg0);
            var result2 = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                    i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            Assert.AreEqual(25, result2.GetGold(a)); // 20 * 1.25
            Assert.AreEqual(25, result2.GetGold(b));
        }

        [Test]
        public void Resolve_TotalGold_SumOfEffective()
        {
            var grid = new GridModel<Item>(4, 4);
            var sword = Mk("sword", "X", 10, 3);
            var shield = Mk("shield", "X", 8, 4, adjTarget: "sword", adjEffect: "gold:+50%");
            grid.TryPlace(sword, 0, 0, Rotation.Deg0);
            grid.TryPlace(shield, 1, 0, Rotation.Deg0);
            var result = AdjacencyResolver.Resolve(grid, i => i.Id, i => i.Gold, i => i.AcidCost,
                                                   i => i.AdjacencyTarget, i => i.AdjacencyEffects);
            Assert.AreEqual(10 + 12, result.TotalGold);
        }
    }
}
```

- [ ] **Step 2: Запустить — не компилируется**

- [ ] **Step 3: Создать `Code/Logic/AdjacencyResolver.cs`**

```csharp
using System;
using System.Collections.Generic;
using Mimic.Data;

namespace Mimic.Logic
{
    public class AdjacencyResult<T>
    {
        public Dictionary<T, int> EffectiveGold = new();
        public Dictionary<T, int> EffectiveAcid = new();
        public int TotalGold;
        public int GetGold(T item) => EffectiveGold.TryGetValue(item, out var v) ? v : 0;
        public int GetAcid(T item) => EffectiveAcid.TryGetValue(item, out var v) ? v : 1;
    }

    public static class AdjacencyResolver
    {
        public static AdjacencyResult<T> Resolve<T>(
            GridModel<T> grid,
            Func<T, string> idOf,
            Func<T, int> baseGoldOf,
            Func<T, int> baseAcidOf,
            Func<T, string> adjacencyTargetOf,
            Func<T, AdjacencyEffect[]> adjacencyEffectsOf
        ) where T : class
        {
            var result = new AdjacencyResult<T>();

            // 1. Заполнить базовые значения
            foreach (var item in grid.AllItems())
            {
                result.EffectiveGold[item] = baseGoldOf(item);
                result.EffectiveAcid[item] = baseAcidOf(item);
            }

            // 2. Для каждого item найти 4-соседей и применить эффект
            foreach (var item in grid.AllItems())
            {
                string target = adjacencyTargetOf(item);
                if (string.IsNullOrEmpty(target)) continue;
                var effects = adjacencyEffectsOf(item);
                if (effects == null || effects.Length == 0) continue;

                var neighbors = GetEdgeNeighbors(grid, item);

                bool triggered = false;
                foreach (var n in neighbors)
                {
                    if (n == item) continue; // самососедство требует другую копию
                    if (idOf(n) == target) { triggered = true; break; }
                }
                if (!triggered) continue;

                foreach (var fx in effects)
                {
                    if (fx.Type == EffectType.Gold)
                    {
                        float newVal = result.EffectiveGold[item] * (1f + fx.Multiplier);
                        result.EffectiveGold[item] = (int)System.Math.Max(0, System.Math.Round(newVal));
                    }
                    else if (fx.Type == EffectType.Acid)
                    {
                        float newVal = result.EffectiveAcid[item] * (1f + fx.Multiplier);
                        result.EffectiveAcid[item] = (int)System.Math.Max(1, System.Math.Round(newVal));
                    }
                }
            }

            // 3. Total
            foreach (var v in result.EffectiveGold.Values) result.TotalGold += v;
            return result;
        }

        private static HashSet<T> GetEdgeNeighbors<T>(GridModel<T> grid, T item) where T : class
        {
            var seen = new HashSet<T>();
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    if (!ReferenceEquals(grid.GetAt(x, y), item)) continue;
                    TryAddNeighbor(grid, x + 1, y, item, seen);
                    TryAddNeighbor(grid, x - 1, y, item, seen);
                    TryAddNeighbor(grid, x, y + 1, item, seen);
                    TryAddNeighbor(grid, x, y - 1, item, seen);
                }
            }
            return seen;
        }

        private static void TryAddNeighbor<T>(GridModel<T> grid, int x, int y, T self, HashSet<T> set) where T : class
        {
            var n = grid.GetAt(x, y);
            if (n == null || ReferenceEquals(n, self)) return;
            set.Add(n);
        }
    }
}
```

- [ ] **Step 4: Запустить — все 6 тестов зелёные**

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Logic/AdjacencyResolver.cs Game/Assets/Code.Tests/AdjacencyResolverTests.cs
git commit -m "feat(logic): AdjacencyResolver with edge-neighbor effect application"
```

---

## Phase 2 — Data files & catalogs

### Task 2.1: CSV-файлы лута, приключенцев, дня

**Files:**
- Create: `Game/Assets/Configs/loot.csv`
- Create: `Game/Assets/Configs/adventurers.csv`
- Create: `Game/Assets/Configs/day.csv`

- [ ] **Step 1: `loot.csv`**

```csv
id,name,description,shape,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyTarget,adjacencyEffect
sword,Меч,"Острый, но тупой",X|X|X|X,10,3,0,0,,
shield,Щит,"Защита от себя",XX|XX,8,4,5,0,sword,gold:+50%
barracuda,Барракуда,"Ещё трепыхается",.X|XX|.X|.X,15,5,0,2,bread,acid:-30%
bread,Хлеб,Несвежий,XX,3,1,2,0,,
gem,Самоцвет,Дешёвый блеск,X,20,2,0,0,gem,gold:+25%
```

- [ ] **Step 2: `adventurers.csv`**

```csv
id,name,phrase,lootIds
warrior,Воин,"definitely not a mimic!",sword;shield;bread
rogue,Плут,"я просто посмотрю",gem;gem;bread;barracuda
mage,Маг,"что в этом сундуке?",gem;shield;bread
```

- [ ] **Step 3: `day.csv`**

```csv
day,goldQuota,startHp,startAcid,adventurerIds
1,40,3,15,warrior;rogue;mage;warrior;rogue
```

- [ ] **Step 4: Commit**

```bash
git add Game/Assets/Configs/loot.csv Game/Assets/Configs/adventurers.csv Game/Assets/Configs/day.csv
git commit -m "data: initial loot, adventurers, day csv"
```

---

### Task 2.2: Catalogs — загрузка CSV в каталоги

**Files:**
- Create: `Code/Catalogs/LootCatalog.cs`
- Create: `Code/Catalogs/AdventurerCatalog.cs`
- Create: `Code/Catalogs/DayConfig.cs`

- [ ] **Step 1: `LootCatalog.cs`**

```csharp
using System.Collections.Generic;
using System.IO;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class LootCatalog
    {
        private static Dictionary<string, LootData> _byId;

        public static IReadOnlyDictionary<string, LootData> ById => _byId;

        public static void Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "loot.csv");
            string text;
            if (File.Exists(path)) text = File.ReadAllText(path);
            else text = ((TextAsset)Resources.Load("loot")).text;
            // Fallback: read directly from Configs (Editor only)
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(text))
                text = File.ReadAllText("Assets/Configs/loot.csv");
            #endif

            _byId = new Dictionary<string, LootData>();
            foreach (var row in CsvLoader.ParseAll(text))
            {
                var d = new LootData
                {
                    Id = row[0],
                    Name = row[1],
                    Description = row[2],
                    Shape = Shape.Parse(row[3]),
                    Gold = int.Parse(row[4]),
                    AcidCost = int.Parse(row[5]),
                    HealOnDigest = int.Parse(row[6]),
                    CellsRestoredOnDigest = int.Parse(row[7]),
                    AdjacencyTarget = row[8],
                    AdjacencyEffects = AdjacencyEffect.ParseList(row[9])
                };
                _byId[d.Id] = d;
            }
        }

        public static LootData Get(string id) => _byId[id];
    }
}
```

Замечание: для джем-проекта проще держать CSV в `Assets/Configs/` и в **Editor** читать прямо оттуда. Для билда — копировать в `StreamingAssets`. На MVP делаем простейший вариант: положить CSV в `Assets/Resources/` (Unity грузит как `TextAsset` через `Resources.Load`).

**Перенесём** CSV в `Assets/Resources/Configs/`:

```bash
mkdir -p Game/Assets/Resources/Configs
mv Game/Assets/Configs/loot.csv Game/Assets/Resources/Configs/loot.csv.txt
mv Game/Assets/Configs/adventurers.csv Game/Assets/Resources/Configs/adventurers.csv.txt
mv Game/Assets/Configs/day.csv Game/Assets/Resources/Configs/day.csv.txt
```

(Unity грузит `.txt` через `Resources.Load<TextAsset>("Configs/loot.csv")` — расширение `.txt` нужно чтобы Unity распознал как TextAsset.)

Тогда `LootCatalog.Load`:

```csharp
public static void Load()
{
    var ta = Resources.Load<TextAsset>("Configs/loot.csv");
    _byId = new Dictionary<string, LootData>();
    foreach (var row in CsvLoader.ParseAll(ta.text))
    {
        var d = new LootData {
            Id = row[0], Name = row[1], Description = row[2],
            Shape = Shape.Parse(row[3]),
            Gold = int.Parse(row[4]), AcidCost = int.Parse(row[5]),
            HealOnDigest = int.Parse(row[6]), CellsRestoredOnDigest = int.Parse(row[7]),
            AdjacencyTarget = row[8],
            AdjacencyEffects = AdjacencyEffect.ParseList(row[9])
        };
        _byId[d.Id] = d;
    }
}
```

- [ ] **Step 2: `AdventurerCatalog.cs`**

```csharp
using System.Collections.Generic;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class AdventurerCatalog
    {
        private static Dictionary<string, AdventurerData> _byId;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/adventurers.csv");
            _byId = new Dictionary<string, AdventurerData>();
            foreach (var row in CsvLoader.ParseAll(ta.text))
            {
                var d = new AdventurerData
                {
                    Id = row[0],
                    Name = row[1],
                    Phrase = row[2],
                    LootIds = row[3].Split(';')
                };
                _byId[d.Id] = d;
            }
        }

        public static AdventurerData Get(string id) => _byId[id];
    }
}
```

- [ ] **Step 3: `DayConfig.cs`**

```csharp
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class DayConfig
    {
        public static Mimic.Data.DayData Current { get; private set; }

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/day.csv");
            var rows = CsvLoader.ParseAll(ta.text);
            if (rows.Count == 0)
                throw new System.InvalidOperationException("day.csv has no data rows");
            var r = rows[0];
            Current = new Mimic.Data.DayData
            {
                Day = int.Parse(r[0]),
                GoldQuota = int.Parse(r[1]),
                StartHp = int.Parse(r[2]),
                StartAcid = int.Parse(r[3]),
                AdventurerIds = r[4].Split(';')
            };
        }
    }
}
```

- [ ] **Step 4: Переместить CSV в `Resources/Configs/` с расширением `.csv.txt`**

```bash
mkdir -p Game/Assets/Resources/Configs
git mv Game/Assets/Configs/loot.csv Game/Assets/Resources/Configs/loot.csv.txt
git mv Game/Assets/Configs/adventurers.csv Game/Assets/Resources/Configs/adventurers.csv.txt
git mv Game/Assets/Configs/day.csv Game/Assets/Resources/Configs/day.csv.txt
```

Замечание: внутри Unity ссылаемся как `Resources.Load<TextAsset>("Configs/loot.csv")` — `.txt` Unity отбрасывает автоматически.

- [ ] **Step 5: Refresh Unity, проверить компиляцию**

- [ ] **Step 6: Commit**

```bash
git add Game/Assets/Code/Catalogs Game/Assets/Resources/Configs
git commit -m "feat(catalogs): LootCatalog/AdventurerCatalog/DayConfig load from Resources"
```

---

## Phase 3 — UI prefabs (без логики)

Префабы создаём в Unity Editor вручную или через MCP. Эти задачи описывают результат — каждый префаб должен иметь нужные компоненты и быть сохранён в `Assets/Prefabs/`.

### Task 3.1: `LootItem.prefab` — RectTransform с placeholder-визуалом

**Files:**
- Create: `Game/Assets/Prefabs/LootItem.prefab`

- [ ] **Step 1: Создать GameObject `LootItem` в `Game.unity` (временно)**

В Unity Editor: GameObject → UI → Image. Назвать `LootItem`. Свойства:
- `RectTransform`: anchor min/max = (0,0)–(0,0), pivot = (0,0), sizeDelta = (64, 64) (одна клетка по дефолту).
- `Image`: цвет placeholder (например светло-синий), `Raycast Target = true`.

- [ ] **Step 2: Добавить дочерний `LootCellsRoot` (RectTransform, без Image), под него — клетки шейпа**

В рантайме `LootView.Build()` будет инстансировать дочерние Image-клетки по шейпу. На префабе оставляем пустой `LootCellsRoot`.

- [ ] **Step 3: Добавить дочерний `LootLabel` (Text-Mesh Pro Text или Legacy Text)**

Для placeholder — TMP Text с центрированием, шрифт по умолчанию, текст пустой (заполнит код).

- [ ] **Step 4: Сохранить как префаб**

Перетащить из Hierarchy в `Assets/Prefabs/LootItem.prefab`. Удалить из сцены.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Prefabs/LootItem.prefab Game/Assets/Prefabs/LootItem.prefab.meta
git commit -m "feat(prefab): LootItem skeleton"
```

---

### Task 3.2: `MimicGrid.prefab` (14×8) и `AdventurerGrid.prefab` (6×10)

**Files:**
- Create: `Game/Assets/Prefabs/MimicGrid.prefab`
- Create: `Game/Assets/Prefabs/AdventurerGrid.prefab`

- [ ] **Step 1: `MimicGrid.prefab`**

В Game.unity: GameObject → UI → Panel, назвать `MimicGrid`. Под ним:
- Image (бэкграунд сетки)
- Дочерний `CellsRoot` — пустой RectTransform, под ним 14×8 = 112 дочерних Image-клеток размером 64×64 каждая (или меньше — нужен расчёт под Canvas Scaler). Каждой клетке имя `Cell_X_Y` (X = 0..13, Y = 0..7). Клетка имеет полупрозрачный Image (grid line) и опциональный Image-оверлей `Highlight` (color = clear по умолчанию).

Для скорости: создать одну клетку-префаб (внутренний), потом скопировать в скрипте OnValidate в эдиторе, или вручную накликать.

**Прагматично:** в `GridView.Awake()` будем генерировать клетки runtime по `width × height`. Тогда на префабе достаточно одного `CellsRoot` пустого + одной заготовки `CellPrefab` (на префабе в полях GridView).

Финальная структура префаба:
- `MimicGrid` (Image background)
  - `CellsRoot` (пустой RectTransform)
- Компонент `GridView` (см. Task 4.1) с полями `width=14, height=8, cellSize=64, cellPrefab=<reference to CellPrefab.prefab>`.

- [ ] **Step 2: Создать `CellPrefab.prefab`** (одна клетка для генерации)

GameObject → UI → Image, `Cell`. Image: цвет полупрозрачный серый, raycast=true. Дочерний `Highlight` Image (color=clear). Сохранить в `Assets/Prefabs/CellPrefab.prefab`.

- [ ] **Step 3: `AdventurerGrid.prefab`**

Скопировать `MimicGrid.prefab`, переименовать, выставить `width=6, height=10`.

- [ ] **Step 4: Commit**

```bash
git add Game/Assets/Prefabs/MimicGrid.prefab Game/Assets/Prefabs/AdventurerGrid.prefab Game/Assets/Prefabs/CellPrefab.prefab
git commit -m "feat(prefab): MimicGrid 14x8, AdventurerGrid 6x10, CellPrefab"
```

---

### Task 3.3: Popups — `AdventurerIntroPopup`, `SurrenderConfirmPopup`, `EndOfDayPopup`, `ContextMenuPanel`, `TooltipPanel`

**Files:**
- Create: 5 префабов в `Game/Assets/Prefabs/`

Каждый popup — Panel с CanvasGroup, дочерние Text + Button. Расположение элементов — горизонтально/вертикально, конкретный layout сделать в Editor.

- [ ] **Step 1: `AdventurerIntroPopup.prefab`** — Image background + Text(`adventurerName`) + Text(`phrase`) + Button(`button_eat` с подписью «Сожрать»).

- [ ] **Step 2: `SurrenderConfirmPopup.prefab`** — Text(`Сдаться?`) + Button(`button_yes`) + Button(`button_no`).

- [ ] **Step 3: `EndOfDayPopup.prefab`** — Text(`title`) + Text(`subtitle`) + Button(`button_retry`) + Button(`button_menu`). Все элементы есть, видимость управляется кодом.

- [ ] **Step 4: `ContextMenuPanel.prefab`** — Image small (~150x40) + Button(`button_digest`) с подписью «Переварить (N)».

- [ ] **Step 5: `TooltipPanel.prefab`** — Image background + Text(`name`) + Text(`description`) + Text(`gold`) + Text(`acid`) + Text(`adjacency`). Фон полупрозрачный, размер растягивается по контенту.

- [ ] **Step 6: Commit**

```bash
git add Game/Assets/Prefabs/AdventurerIntroPopup.prefab \
        Game/Assets/Prefabs/SurrenderConfirmPopup.prefab \
        Game/Assets/Prefabs/EndOfDayPopup.prefab \
        Game/Assets/Prefabs/ContextMenuPanel.prefab \
        Game/Assets/Prefabs/TooltipPanel.prefab
git commit -m "feat(prefab): popups (intro, surrender, end-of-day, context menu, tooltip)"
```

---

## Phase 4 — Game scripts (MonoBehaviour) and binding

### Task 4.1: `GridView` — рантайм генерация клеток + helper для координат

**Files:**
- Create: `Code/UI/GridView.cs`

- [ ] **Step 1: Создать `Code/UI/GridView.cs`**

```csharp
using UnityEngine;
using Mimic.Logic;

namespace Mimic.UI
{
    public class GridView : MonoBehaviour
    {
        [Header("Grid size")]
        public int Width = 14;
        public int Height = 8;
        public float CellSize = 64f;

        [Header("References")]
        public RectTransform CellsRoot;
        public GameObject CellPrefab;

        public GridModel<LootView> Model { get; private set; }
        public RectTransform[,] CellRects { get; private set; }

        private void Awake()
        {
            Model = new GridModel<LootView>(Width, Height);
            CellRects = new RectTransform[Width, Height];
            BuildCells();
        }

        private void BuildCells()
        {
            // Clear existing
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var cell = Instantiate(CellPrefab, CellsRoot);
                    cell.name = $"Cell_{x}_{y}";
                    var rt = (RectTransform)cell.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(CellSize, CellSize);
                    rt.anchoredPosition = new Vector2(x * CellSize, y * CellSize);
                    CellRects[x, y] = rt;
                }
            }
        }

        public bool ScreenToCell(Vector2 screenPos, Camera cam, out int x, out int y)
        {
            x = y = -1;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(CellsRoot, screenPos, cam, out var local))
                return false;
            int cx = Mathf.FloorToInt(local.x / CellSize);
            int cy = Mathf.FloorToInt(local.y / CellSize);
            if (cx < 0 || cx >= Width || cy < 0 || cy >= Height) return false;
            x = cx; y = cy;
            return true;
        }

        public Vector2 CellToLocal(int x, int y) => new Vector2(x * CellSize, y * CellSize);
    }
}
```

- [ ] **Step 2: Привязать `GridView` к `MimicGrid.prefab` и `AdventurerGrid.prefab`**

В Unity Editor: открыть каждый префаб → Add Component → `GridView`. Поля `CellsRoot` = child `CellsRoot`, `CellPrefab` = `CellPrefab.prefab`. `Width/Height/CellSize` — по 14/8/64 и 6/10/64 соответственно.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/GridView.cs Game/Assets/Prefabs/MimicGrid.prefab Game/Assets/Prefabs/AdventurerGrid.prefab
git commit -m "feat(ui): GridView with runtime cell generation"
```

---

### Task 4.2: `LootView` — визуал предмета, биндинг к `LootData`

**Files:**
- Create: `Code/UI/LootView.cs`

- [ ] **Step 1: Создать `Code/UI/LootView.cs`**

```csharp
using System.Collections.Generic;
using Mimic.Data;
using Mimic.Logic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mimic.UI
{
    public class LootView : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public LootData Data { get; private set; }
        public Shape Shape => Data.Shape;
        public Rotation CurrentRotation = Rotation.Deg0;

        [Header("References")]
        public RectTransform CellsRoot;
        public GameObject CellPrefab;
        public Text Label;

        public void Bind(LootData data)
        {
            Data = data;
            Label.text = data.Name.Substring(0, System.Math.Min(2, data.Name.Length));
            BuildCells();
        }

        private void BuildCells()
        {
            for (int i = CellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(CellsRoot.GetChild(i).gameObject);

            float size = 64f; // matches GridView.CellSize
            var cells = Shape.GetRotatedCells(CurrentRotation);
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    var go = Instantiate(CellPrefab, CellsRoot);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
                    rt.pivot = new Vector2(0, 0);
                    rt.sizeDelta = new Vector2(size, size);
                    rt.anchoredPosition = new Vector2(c * size, r * size);
                }
            }
        }

        public void Rotate(bool clockwise)
        {
            int v = (int)CurrentRotation + (clockwise ? 1 : 3);
            CurrentRotation = (Rotation)(v % 4);
            BuildCells();
        }

        public void OnPointerDown(PointerEventData ev)
        {
            if (ev.button == PointerEventData.InputButton.Left)
                DragController.Instance?.OnLootClicked(this);
            else if (ev.button == PointerEventData.InputButton.Right)
                DragController.Instance?.OnLootRightClicked(this);
        }

        public void OnPointerEnter(PointerEventData ev) => TooltipController.Instance?.Show(this);
        public void OnPointerExit(PointerEventData ev) => TooltipController.Instance?.Hide();
    }
}
```

- [ ] **Step 2: Привязать `LootView` к `LootItem.prefab`**

В префабе: Add Component `LootView`. Поля: `CellsRoot` = child `LootCellsRoot`, `CellPrefab` = `CellPrefab.prefab`, `Label` = child `LootLabel`. На root самого префаба — `Image` с RaycastTarget=false (чтобы клик прошёл к дочерним клеткам). Каждая клетка `CellPrefab` должна иметь Image с RaycastTarget=true.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/LootView.cs Game/Assets/Prefabs/LootItem.prefab
git commit -m "feat(ui): LootView with shape rebuild and pointer events"
```

---

### Task 4.3: `DragController` — pick/drop/cancel/rotate

**Files:**
- Create: `Code/Input/DragController.cs`

- [ ] **Step 1: Создать `Code/Input/DragController.cs`**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using Mimic.Logic;
using Mimic.UI;

namespace Mimic.Input
{
    public class DragController : MonoBehaviour
    {
        public static DragController Instance { get; private set; }

        [Header("References")]
        public GridView MimicGrid;
        public GridView AdventurerGrid;
        public RectTransform DragLayer;
        public Camera UiCamera; // can be null if Canvas is ScreenSpaceOverlay

        public LootView Held { get; private set; }

        private GridView originGrid;
        private int originX, originY;
        private Rotation originRot;

        private void Awake() => Instance = this;

        private void Update()
        {
            if (Held == null) return;

            // Follow cursor
            var mouseScreen = UnityEngine.Input.mousePosition;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragLayer, mouseScreen, UiCamera, out var local))
                ((RectTransform)Held.transform).anchoredPosition = local;

            UpdateHighlight(mouseScreen);
        }

        public void OnLootClicked(LootView item)
        {
            if (Held == null) Pick(item);
            else TryDrop(item);
        }

        public void OnLootRightClicked(LootView item)
        {
            if (Held != null) Cancel();
            else ContextMenuController.Instance?.Open(item);
        }

        public void OnEmptyCellClicked(GridView grid, int x, int y)
        {
            // Called from GridView click handler — see Task 4.4
            if (Held != null) TryDropAt(grid, x, y);
        }

        public void Pick(LootView item)
        {
            // Determine origin grid + remove from model
            var grid = FindGridContaining(item);
            if (grid == null) return; // Already in DragLayer somehow
            grid.Model.TryGetPlacement(item, out originX, out originY, out originRot);
            originGrid = grid;
            grid.Model.Remove(item);

            Held = item;
            item.transform.SetParent(DragLayer, worldPositionStays: false);
        }

        public void Cancel()
        {
            if (Held == null) return;
            Held.CurrentRotation = originRot;
            originGrid.Model.TryPlace(Held, originX, originY, originRot);
            SnapToGrid(Held, originGrid, originX, originY);
            Held = null;
            ClearHighlight();
        }

        private void TryDrop(LootView clickedItem)
        {
            // Если кликнули по другому item — попытка положить на его клетку
            var grid = FindGridContaining(clickedItem);
            if (grid != null)
            {
                grid.Model.TryGetPlacement(clickedItem, out var x, out var y, out _);
                TryDropAt(grid, x, y);
            }
        }

        private void TryDropAt(GridView grid, int x, int y)
        {
            // MVP rule: items cannot return to adventurer grid
            if (grid == AdventurerGrid && originGrid == MimicGrid) return;

            if (grid.Model.TryPlace(Held, x, y, Held.CurrentRotation))
            {
                Held.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
                SnapToGrid(Held, grid, x, y);
                Held = null;
                ClearHighlight();
                GameContext.Instance?.OnGridChanged();
            }
        }

        private void UpdateHighlight(Vector2 screenPos)
        {
            ClearHighlight();
            // Determine which grid the cursor is over
            var grid = ScreenOverGrid(screenPos);
            if (grid == null) return;
            if (!grid.ScreenToCell(screenPos, UiCamera, out int x, out int y)) return;

            var cells = Held.Shape.GetRotatedCells(Held.CurrentRotation);
            bool canPlace = grid.Model.TryPlace(Held, x, y, Held.CurrentRotation);
            if (canPlace) grid.Model.Remove(Held); // undo trial placement

            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!cells[r, c]) continue;
                    int cx = x + c, cy = y + r;
                    if (cx < 0 || cx >= grid.Width || cy < 0 || cy >= grid.Height) continue;
                    SetCellHighlight(grid, cx, cy, canPlace ? Color.green : Color.red, 0.4f);
                }
            }
        }

        private void ClearHighlight()
        {
            ClearGridHighlight(MimicGrid);
            ClearGridHighlight(AdventurerGrid);
        }

        private void ClearGridHighlight(GridView grid)
        {
            for (int x = 0; x < grid.Width; x++)
                for (int y = 0; y < grid.Height; y++)
                {
                    var h = grid.CellRects[x, y].Find("Highlight");
                    if (h != null)
                    {
                        var img = h.GetComponent<UnityEngine.UI.Image>();
                        if (img != null) img.color = new Color(0, 0, 0, 0);
                    }
                }
        }

        private void SetCellHighlight(GridView grid, int x, int y, Color color, float alpha)
        {
            var h = grid.CellRects[x, y].Find("Highlight");
            if (h == null) return;
            var img = h.GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;
            var c = color; c.a = alpha;
            img.color = c;
        }

        private GridView ScreenOverGrid(Vector2 screenPos)
        {
            if (MimicGrid.ScreenToCell(screenPos, UiCamera, out _, out _)) return MimicGrid;
            if (AdventurerGrid.ScreenToCell(screenPos, UiCamera, out _, out _)) return AdventurerGrid;
            return null;
        }

        private GridView FindGridContaining(LootView item)
        {
            foreach (var i in MimicGrid.Model.AllItems()) if (i == item) return MimicGrid;
            foreach (var i in AdventurerGrid.Model.AllItems()) if (i == item) return AdventurerGrid;
            return null;
        }

        private void SnapToGrid(LootView item, GridView grid, int x, int y)
        {
            item.transform.SetParent(grid.CellsRoot, worldPositionStays: false);
            ((RectTransform)item.transform).anchoredPosition = grid.CellToLocal(x, y);
        }
    }
}
```

- [ ] **Step 2: Привязать к `Game.unity`**

(Сцена ещё не собрана, оставим до Phase 6.)

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/Input/DragController.cs
git commit -m "feat(input): DragController with pick/drop/rotate and highlight"
```

---

### Task 4.4: `InputBridge` — биндинг Input System Action Asset → DragController

**Files:**
- Create: `Code/Input/InputBridge.cs`

- [ ] **Step 1: Создать `Code/Input/InputBridge.cs`**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimic.Input
{
    public class InputBridge : MonoBehaviour
    {
        public InputActionReference RotateCWAction;  // E or wheel down
        public InputActionReference RotateCCWAction; // Q or wheel up
        public InputActionReference CancelAction;    // Right mouse (when carrying — handled by DragController via PointerEvents)

        private void OnEnable()
        {
            if (RotateCWAction != null) RotateCWAction.action.performed += OnRotateCW;
            if (RotateCCWAction != null) RotateCCWAction.action.performed += OnRotateCCW;
            RotateCWAction?.action.Enable();
            RotateCCWAction?.action.Enable();
        }

        private void OnDisable()
        {
            if (RotateCWAction != null) RotateCWAction.action.performed -= OnRotateCW;
            if (RotateCCWAction != null) RotateCCWAction.action.performed -= OnRotateCCW;
        }

        private void OnRotateCW(InputAction.CallbackContext _)
        {
            var held = DragController.Instance?.Held;
            if (held != null) held.Rotate(clockwise: true);
        }

        private void OnRotateCCW(InputAction.CallbackContext _)
        {
            var held = DragController.Instance?.Held;
            if (held != null) held.Rotate(clockwise: false);
        }
    }
}
```

- [ ] **Step 2: Открыть `Assets/InputSystem_Actions.inputactions` в редакторе**

Добавить Action Map `Mimic`, в нём действия:
- `RotateCW`: Button. Bindings: `<Keyboard>/e`, `<Mouse>/scroll/down`.
- `RotateCCW`: Button. Bindings: `<Keyboard>/q`, `<Mouse>/scroll/up`.

Save Asset.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/Input/InputBridge.cs Game/Assets/InputSystem_Actions.inputactions
git commit -m "feat(input): InputBridge for rotation actions"
```

---

### Task 4.5: `TooltipController`

**Files:**
- Create: `Code/UI/TooltipController.cs`

- [ ] **Step 1: Создать `Code/UI/TooltipController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using Mimic.UI;

namespace Mimic.UI
{
    public class TooltipController : MonoBehaviour
    {
        public static TooltipController Instance { get; private set; }

        [Header("References")]
        public RectTransform Panel;
        public Text NameText;
        public Text DescriptionText;
        public Text GoldText;
        public Text AcidText;
        public Text AdjacencyText;
        public Camera UiCamera;

        private void Awake()
        {
            Instance = this;
            Panel.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Panel.gameObject.activeSelf) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Panel.parent, UnityEngine.Input.mousePosition, UiCamera, out var local))
                Panel.anchoredPosition = local + new Vector2(20, 20);
        }

        public void Show(LootView item)
        {
            Panel.gameObject.SetActive(true);
            NameText.text = item.Data.Name;
            DescriptionText.text = item.Data.Description;
            // Effective values are looked up from GameContext (filled by AdjacencyResolver)
            int gold = GameContext.Instance?.LastResolved?.GetGold(item) ?? item.Data.Gold;
            int acid = GameContext.Instance?.LastResolved?.GetAcid(item) ?? item.Data.AcidCost;
            GoldText.text = $"Цена: {gold} зол.";
            AcidText.text = $"Переварить: {acid} сока";
            AdjacencyText.text = string.IsNullOrEmpty(item.Data.AdjacencyTarget)
                ? ""
                : $"Рядом с «{item.Data.AdjacencyTarget}» → {string.Join(", ", item.Data.AdjacencyEffects.Length)} эффект(а)";
        }

        public void Hide() => Panel.gameObject.SetActive(false);
    }
}
```

- [ ] **Step 2: Привязать к `TooltipPanel.prefab`** (Add Component, заполнить поля из дочерних Text).

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/TooltipController.cs Game/Assets/Prefabs/TooltipPanel.prefab
git commit -m "feat(ui): TooltipController bound to LootView events"
```

---

### Task 4.6: `ContextMenuController` — popup переваривания

**Files:**
- Create: `Code/UI/ContextMenuController.cs`

- [ ] **Step 1: Создать `Code/UI/ContextMenuController.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using Mimic.UI;

namespace Mimic.UI
{
    public class ContextMenuController : MonoBehaviour
    {
        public static ContextMenuController Instance { get; private set; }

        [Header("References")]
        public RectTransform Panel;
        public Button DigestButton;
        public Text DigestLabel;
        public Camera UiCamera;

        private LootView target;

        private void Awake()
        {
            Instance = this;
            Panel.gameObject.SetActive(false);
            DigestButton.onClick.AddListener(OnDigestClicked);
        }

        public void Open(LootView item)
        {
            // Only items in mimic grid
            var ctx = GameContext.Instance;
            if (ctx == null || ctx.MimicGrid == null) return;
            bool inMimic = false;
            foreach (var i in ctx.MimicGrid.Model.AllItems())
                if (i == item) { inMimic = true; break; }
            if (!inMimic) return;

            target = item;
            int cost = ctx.LastResolved?.GetAcid(item) ?? item.Data.AcidCost;
            DigestLabel.text = $"Переварить ({cost} сока)";
            DigestButton.interactable = ctx.Resources.CurrentAcid >= cost;
            Panel.gameObject.SetActive(true);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)Panel.parent, UnityEngine.Input.mousePosition, UiCamera, out var local))
                Panel.anchoredPosition = local;
        }

        public void Close()
        {
            Panel.gameObject.SetActive(false);
            target = null;
        }

        private void OnDigestClicked()
        {
            if (target == null) return;
            GameContext.Instance?.Digest(target);
            Close();
        }
    }
}
```

- [ ] **Step 2: Привязать к `ContextMenuPanel.prefab`**.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/ContextMenuController.cs Game/Assets/Prefabs/ContextMenuPanel.prefab
git commit -m "feat(ui): ContextMenuController (digest action)"
```

---

### Task 4.7: `GameResources` — HP/ЖС/золото

**Files:**
- Create: `Code/Game/GameResources.cs`

- [ ] **Step 1: Создать `Code/Game/GameResources.cs`**

```csharp
namespace Mimic.Game
{
    public class GameResources
    {
        public int CurrentHp;
        public int CurrentAcid;
        public int CurrentGoldInMimic;
        public int DayQuota;

        public void StartDay(Mimic.Data.DayData day)
        {
            CurrentHp = day.StartHp;
            CurrentAcid = day.StartAcid;
            CurrentGoldInMimic = 0;
            DayQuota = day.GoldQuota;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Game/GameResources.cs
git commit -m "feat(game): GameResources struct"
```

---

### Task 4.8: `GameContext` — главный синглтон с провайдерами всех зависимостей

**Files:**
- Create: `Code/Game/GameContext.cs`

- [ ] **Step 1: Создать `Code/Game/GameContext.cs`**

```csharp
using UnityEngine;
using Mimic.Catalogs;
using Mimic.Logic;
using Mimic.UI;
using Mimic.Data;

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
            LastResolved = AdjacencyResolver.Resolve(
                MimicGrid.Model,
                v => v.Data.Id,
                v => v.Data.Gold,
                v => v.Data.AcidCost,
                v => v.Data.AdjacencyTarget,
                v => v.Data.AdjacencyEffects);
            Resources.CurrentGoldInMimic = LastResolved.TotalGold;
            Hud?.Refresh();
        }

        public void Digest(LootView item)
        {
            int cost = LastResolved?.GetAcid(item) ?? item.Data.AcidCost;
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
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Game/GameContext.cs
git commit -m "feat(game): GameContext singleton with grid binding"
```

---

### Task 4.9: `HudView` — биндинг ресурсов в UI

**Files:**
- Create: `Code/UI/HudView.cs`

- [ ] **Step 1: Создать `Code/UI/HudView.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using Mimic.Game;

namespace Mimic.UI
{
    public class HudView : MonoBehaviour
    {
        [Header("Top bar")]
        public Text GoldInMimicText;
        public Text DayQuotaText;
        public Text DayCounterText;
        public Text HeroCounterText;

        [Header("Bottom bar")]
        public Image HealthBar;
        public Image AcidBar;
        public Button NextButton;
        public Button SurrenderButton;
        public Text NextButtonLabel;

        public void Refresh()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;
            GoldInMimicText.text = $"Цена: {ctx.Resources.CurrentGoldInMimic}";
            DayQuotaText.text = $"Нужно: {ctx.Resources.DayQuota}";

            // HP/Acid bars — assume starting values as max
            HealthBar.fillAmount = Mathf.Clamp01(ctx.Resources.CurrentHp / (float)Mathf.Max(1, Mimic.Catalogs.DayConfig.Current.StartHp));
            AcidBar.fillAmount = Mathf.Clamp01(ctx.Resources.CurrentAcid / (float)Mathf.Max(1, Mimic.Catalogs.DayConfig.Current.StartAcid));

            UpdateSurrenderHighlight(ctx);
        }

        public void SetHeroCounter(int current, int total) => HeroCounterText.text = $"Герой {current}/{total}";
        public void SetDayCounter(int day) => DayCounterText.text = $"День {day}";
        public void SetNextButtonLabel(string text) => NextButtonLabel.text = text;
        public void SetNextButtonEnabled(bool e) => NextButton.interactable = e;

        private void UpdateSurrenderHighlight(GameContext ctx)
        {
            bool noAcid = false;
            int minAcid = int.MaxValue;
            foreach (var i in ctx.MimicGrid.Model.AllItems())
            {
                int a = ctx.LastResolved?.GetAcid(i) ?? i.Data.AcidCost;
                if (a < minAcid) minAcid = a;
            }
            if (minAcid != int.MaxValue && ctx.Resources.CurrentAcid < minAcid) noAcid = true;

            bool noSpace = ctx.MimicGrid.Model.FreeCellsCount < OccupiedCells(ctx.AdventurerGrid);

            bool danger = noAcid || noSpace;
            var img = SurrenderButton.GetComponent<Image>();
            if (img != null) img.color = danger ? Color.red : Color.white;
        }

        private int OccupiedCells(GridView grid)
        {
            int total = grid.Width * grid.Height;
            return total - grid.Model.FreeCellsCount;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/UI/HudView.cs
git commit -m "feat(ui): HudView with resource binding and surrender highlight"
```

---

### Task 4.10: Popup-скрипты (`AdventurerIntroPopup`, `SurrenderConfirmPopup`, `EndOfDayPopup`)

**Files:**
- Create: `Code/UI/AdventurerIntroPopup.cs`
- Create: `Code/UI/SurrenderConfirmPopup.cs`
- Create: `Code/UI/EndOfDayPopup.cs`

- [ ] **Step 1: `AdventurerIntroPopup.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using Mimic.Data;
using System;

namespace Mimic.UI
{
    public class AdventurerIntroPopup : MonoBehaviour
    {
        public Text NameText;
        public Text PhraseText;
        public Button EatButton;

        private Action onEat;

        private void Awake()
        {
            EatButton.onClick.AddListener(() => { onEat?.Invoke(); gameObject.SetActive(false); });
            gameObject.SetActive(false);
        }

        public void Show(AdventurerData data, Action onEatCallback)
        {
            NameText.text = data.Name;
            PhraseText.text = data.Phrase;
            onEat = onEatCallback;
            gameObject.SetActive(true);
        }
    }
}
```

- [ ] **Step 2: `SurrenderConfirmPopup.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Mimic.UI
{
    public class SurrenderConfirmPopup : MonoBehaviour
    {
        public Button YesButton;
        public Button NoButton;

        private Action onYes;

        private void Awake()
        {
            YesButton.onClick.AddListener(() => { onYes?.Invoke(); gameObject.SetActive(false); });
            NoButton.onClick.AddListener(() => gameObject.SetActive(false));
            gameObject.SetActive(false);
        }

        public void Show(Action onYesCallback)
        {
            onYes = onYesCallback;
            gameObject.SetActive(true);
        }
    }
}
```

- [ ] **Step 3: `EndOfDayPopup.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Mimic.UI
{
    public class EndOfDayPopup : MonoBehaviour
    {
        public Text TitleText;
        public Text SubtitleText;
        public Button RetryButton;
        public Button MenuButton;

        private void Awake()
        {
            RetryButton.onClick.AddListener(() => SceneManager.LoadScene("Game"));
            MenuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
            gameObject.SetActive(false);
        }

        public void ShowWin(int gold, int quota)
        {
            TitleText.text = "День спасён!";
            SubtitleText.text = $"Заработано {gold} / {quota}";
            RetryButton.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        public void ShowLose(string reason, int gold, int quota)
        {
            TitleText.text = reason;
            SubtitleText.text = $"Заработано {gold} / {quota}";
            RetryButton.gameObject.SetActive(true);
            gameObject.SetActive(true);
        }
    }
}
```

- [ ] **Step 4: Привязать каждый скрипт к соответствующему префабу**

Открыть префаб → Add Component → перетянуть Text/Button refs в Inspector.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/UI/AdventurerIntroPopup.cs \
        Game/Assets/Code/UI/SurrenderConfirmPopup.cs \
        Game/Assets/Code/UI/EndOfDayPopup.cs \
        Game/Assets/Prefabs/AdventurerIntroPopup.prefab \
        Game/Assets/Prefabs/SurrenderConfirmPopup.prefab \
        Game/Assets/Prefabs/EndOfDayPopup.prefab
git commit -m "feat(ui): popup controllers (intro/surrender/end-of-day)"
```

---

### Task 4.11: `GameFlow` — FSM

**Files:**
- Create: `Code/Game/GameFlow.cs`

- [ ] **Step 1: Создать `Code/Game/GameFlow.cs`**

```csharp
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
            if (queue.Count == 0) { Hud.SetNextButtonLabel("Завершить день"); Hud.SetNextButtonEnabled(true); return; }
            string id = queue.Dequeue();
            current = AdventurerCatalog.Get(id);
            processed++;
            Hud.SetHeroCounter(processed, totalInDay);
            IntroPopup.Show(current, OnEatPressed);
        }

        private void OnEatPressed()
        {
            // Spawn adventurer's loot in adventurer grid
            var ctx = GameContext.Instance;
            int placed = 0;
            foreach (var lootId in current.LootIds)
            {
                var data = LootCatalog.Get(lootId);
                var view = ctx.SpawnLoot(data, ctx.AdventurerGrid.CellsRoot);
                if (!TryPlaceFirstFit(ctx.AdventurerGrid, view))
                {
                    Destroy(view.gameObject); // overflow — drop on floor (MVP cuts cache)
                    continue;
                }
                placed++;
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
            // Allow next only when adventurer grid is empty
            var ctx = GameContext.Instance;
            if (ctx.AdventurerGrid.Model.FreeCellsCount < ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height)
                return;

            if (queue.Count == 0) EndDay();
            else BringNext();
        }

        private void EndDay()
        {
            var ctx = GameContext.Instance;
            int gold = ctx.Resources.CurrentGoldInMimic;
            int quota = ctx.Resources.DayQuota;
            if (gold >= quota) EndPopup.ShowWin(gold, quota);
            else EndPopup.ShowLose("День провален", gold, quota);
        }

        private void EndLose()
        {
            var ctx = GameContext.Instance;
            EndPopup.ShowLose("Вы лопнули от переедания", ctx.Resources.CurrentGoldInMimic, ctx.Resources.DayQuota);
        }

        private void Update()
        {
            // Enable next-button when adventurer grid is empty during sorting
            var ctx = GameContext.Instance;
            if (ctx == null || Hud == null) return;
            bool advEmpty = ctx.AdventurerGrid.Model.FreeCellsCount == ctx.AdventurerGrid.Width * ctx.AdventurerGrid.Height;
            Hud.SetNextButtonEnabled(advEmpty);
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Game/GameFlow.cs
git commit -m "feat(game): GameFlow FSM for day cycle"
```

---

### Task 4.12: `SfxPlayer` (no-op stub)

**Files:**
- Create: `Code/Game/SfxPlayer.cs`

- [ ] **Step 1: Создать**

```csharp
namespace Mimic.Game
{
    public static class SfxPlayer
    {
        public static void Play(string id) { /* MVP: no-op */ }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Game/SfxPlayer.cs
git commit -m "feat(game): SfxPlayer stub (no-op)"
```

---

## Phase 5 — Scenes assembly

### Task 5.1: Сборка `Game.unity`

**Files:**
- Modify: `Game/Assets/Scenes/SampleScene.unity` (переименовать в `Game.unity`) or create new `Game.unity`

- [ ] **Step 1: Создать новую сцену `Game.unity`**

File → New Scene → 2D template → Save As `Assets/Scenes/Game.unity`.

- [ ] **Step 2: Создать UI Root**

GameObject → UI → Canvas (Screen Space - Overlay, Scale With Screen Size 1920×1080). Под Canvas:
- Image `HUDRoot` (full-screen background, optional gray).
- Instantiate `MimicGrid.prefab` под HUDRoot, разместить слева.
- Instantiate `AdventurerGrid.prefab` под HUDRoot, разместить справа.
- Создать `TopBar` (Horizontal Layout Group): тексты GoldInMimic, DayQuota, DayCounter, HeroCounter.
- Создать `BottomBar`: HealthBar (Image filled), AcidBar (Image filled), Button SurrenderButton, Button NextButton + Text NextButtonLabel.
- Instantiate `TooltipPanel.prefab`, `ContextMenuPanel.prefab`, `AdventurerIntroPopup.prefab`, `SurrenderConfirmPopup.prefab`, `EndOfDayPopup.prefab` под Canvas. Все popup'ы выключены по умолчанию.
- Создать `DragLayer` (Image transparent, full-screen, RaycastTarget=false) — поверх всего остального.

- [ ] **Step 3: Добавить EventSystem**

GameObject → UI → Event System (если ещё нет). Заменить `Standalone Input Module` на `Input System UI Input Module` (требуется новым Input System).

- [ ] **Step 4: Создать `_Game` GameObject c компонентами**

GameObject → Create Empty `_Game`. Add Component:
- `GameContext` (Mimic.Game) — заполнить ссылки: MimicGrid, AdventurerGrid, Hud, LootContainer (DragLayer), LootItemPrefab (LootItem.prefab).
- `GameFlow` (Mimic.Game) — IntroPopup, SurrenderPopup, EndPopup, Hud.
- `DragController` (Mimic.Input) — MimicGrid, AdventurerGrid, DragLayer.
- `InputBridge` (Mimic.Input) — RotateCWAction, RotateCCWAction (ссылки на действия из `InputSystem_Actions`).
- `TooltipController` (Mimic.UI) — все Text refs из TooltipPanel.prefab instance.
- `ContextMenuController` (Mimic.UI) — references из ContextMenuPanel.prefab instance.
- `HudView` (Mimic.UI) — все Text/Image/Button refs из HUD.

- [ ] **Step 5: Сохранить сцену**

- [ ] **Step 6: Commit**

```bash
git add Game/Assets/Scenes/Game.unity Game/Assets/Scenes/Game.unity.meta
git commit -m "feat(scene): assemble Game scene with all UI and controllers"
```

---

### Task 5.2: Сборка `MainMenu.unity`

**Files:**
- Create: `Game/Assets/Scenes/MainMenu.unity`
- Create: `Code/UI/MainMenu.cs`

- [ ] **Step 1: Создать `Code/UI/MainMenu.cs`**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mimic.UI
{
    public class MainMenu : MonoBehaviour
    {
        public Button StartButton;
        public Button QuitButton;

        private void Awake()
        {
            StartButton.onClick.AddListener(() => SceneManager.LoadScene("Game"));
            QuitButton.onClick.AddListener(() =>
            {
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #else
                Application.Quit();
                #endif
            });
        }
    }
}
```

- [ ] **Step 2: Создать сцену**

File → New Scene → 2D → Save As `Assets/Scenes/MainMenu.unity`. На Canvas — Text «Is this a mimic?» (заголовок), Button «Новая игра», Button «Выход». На пустом GameObject `_Menu` — компонент `MainMenu` со ссылками.

- [ ] **Step 3: Добавить обе сцены в Build Settings**

File → Build Profiles → Scene List → Add `Scenes/MainMenu.unity` (index 0), `Scenes/Game.unity` (index 1).

- [ ] **Step 4: Commit**

```bash
git add Game/Assets/Code/UI/MainMenu.cs Game/Assets/Scenes/MainMenu.unity Game/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(scene): MainMenu with Start/Quit"
```

---

## Phase 6 — Playtest & balance

### Task 6.1: Smoke playtest

- [ ] **Step 1: Открыть `MainMenu.unity`, нажать Play, нажать «Новая игра»**

- [ ] **Step 2: Проверить базовый цикл:**
  - Появляется попап первого приключенца → жмём «Сожрать» → лут падает в правую сетку.
  - Левая ЛКМ по предмету → поднимается, следует за курсором.
  - Q/E или колесо → вращает.
  - Hover над сеткой мимика — клетки подсвечены зелёным/красным.
  - ЛКМ ещё раз — кладём.
  - ПКМ во время удержания — возврат.
  - ПКМ по предмету в мимике (без удержания) — открывает context menu → «Переварить» → предмет исчезает, счётчик золота меняется.
  - Tooltip появляется при наведении.
  - Кнопка «Следующий!» залочена пока есть лут справа.
  - Когда правая сетка пуста — кнопка активна, жмём → новый приключенец.
  - 5 приключенцев → кнопка меняется на «Завершить день» → жмём → попап win/lose.
  - Кнопка «Сдаться» открывает confirm → подтверждение → lose popup.

- [ ] **Step 3: Записать выявленные баги в `BUGS.md`** (или сразу фиксить и коммитить)

### Task 6.2: Balance pass

- [ ] **Step 1: Скорректировать `loot.csv`/`day.csv`** чтобы было реально, но не тривиально пройти день. Проверить за 3-5 партий.

- [ ] **Step 2: Commit финальный баланс**

```bash
git add Game/Assets/Resources/Configs/
git commit -m "balance: tune day 1 quota and loot values"
```

---

## Self-Review

**Spec coverage:**

- ✅ Сцены `MainMenu` + `Game` → Task 5.1, 5.2
- ✅ FSM `NewDay → AdventurerIntro → Sorting → EndOfDay` → Task 4.11 (`GameFlow`)
- ✅ Префабы из spec §4 → Tasks 3.1, 3.2, 3.3
- ✅ CSV формат loot/adventurers/day → Tasks 2.1, 2.2
- ✅ Grid модель + TryPlace/Remove → Task 1.3
- ✅ Drag&drop с поворотом, подсветка зелёным/красным → Tasks 4.3, 4.4
- ✅ Adjacency пересчёт + clamp + самососедство → Task 1.7
- ✅ Digestion через ContextMenu → Tasks 4.6, 4.8
- ✅ Tooltip → Task 4.5
- ✅ HUD с биндингом ресурсов + подсветка Сдаться по OR двух условий → Task 4.9
- ✅ Popups (intro/surrender/end-of-day) → Tasks 3.3, 4.10
- ✅ Win/Lose триггеры → Task 4.11 (`GameFlow.EndDay`, `EndLose`)
- ✅ Запрет drop в правую сетку из левой → Task 4.3 (`TryDropAt`)

**Placeholder scan:** Просмотрел — все TODO/TBD убраны, код в каждом шаге полный, тесты содержат конкретные ассерты. Один компромисс: задачи 3.1-3.3 описывают создание префабов через Editor UI, не через скрипт — это нормально для Unity, но шаги менее автоматизируемы. Subagent должен будет работать с MCP for Unity для создания префабов.

**Type consistency check:**
- `GridModel<T>` — параметризован. В тестах используется `Token` (с полем `Shape`), в рантайме — `LootView` (с свойством `Shape`). Resolver через duck-typing reflection. ✓
- `AdjacencyResolver.Resolve` — принимает делегаты, не зависит от конкретного типа. ✓
- `GameContext.LastResolved` типизирован как `AdjacencyResult<LootView>`. ✓
- Имена методов: `Rotate(clockwise: bool)` в LootView, `Pick/Cancel/TryDropAt` в DragController — везде согласованы.
- `OnGridChanged()` вызывается из `TryDropAt` и `Digest`. ✓

**Scope:** Реалистично укладывается в 3-5 дней одиночной работы. Phase 1 (логика+тесты) — 1 день, Phase 2-4 (данные, префабы, скрипты) — 2-3 дня, Phase 5-6 (сборка сцен + playtest) — 1 день.

---

**Plan complete and saved to `docs/superpowers/plans/2026-05-27-mimic-game.md`.**

Два варианта выполнения:

1. **Subagent-Driven** (рекомендую) — диспетчу свежего subagent на каждую task, проверяю между tasks, быстрая итерация.
2. **Inline Execution** — выполнение задач в этой сессии через executing-plans, батч с чекпоинтами.

Какой подход?
