# Бой (Combat) — дизайн фичи

**Дата:** 2026-05-31
**Статус:** утверждён к реализации
**ТЗ:** `Бой _ Notion.pdf` (вне репо)

## Обзор

Пошаговый боевой энкаунтер поверх существующей сцены сортировки. Возникает в двух случаях:

1. **battle-приключенец** — пришёл приключенец с флагом `battle=true`; его лут можно получить только после победы в бою.
2. **Вызов Хозяину** — игрок жмёт «Бросить вызов (КУСЬ)» в конце дня (сейчас это стаб `EndChallengeStub`).

Враг всегда ходит первым. Во время своего хода игрок свободно двигает и переваривает предметы в мимике; **ход завершает только атака** — нажатие «Кусь» ИЛИ бросок предмета с `attack > 0` во врага. После атаки враг отвечает. Бой ведётся накопленным в мимике инвентарём + кнопкой «Кусь».

## Архитектура

Вариант A: логика боя в отдельном `CombatController` (MonoBehaviour-синглтон) поверх текущей сцены. Левая сетка мимика, `DragController` и `TryDigestHeld` переиспользуются как есть. Никаких новых сцен/префабов; боевой UI создаётся рантаймом по паттерну digest-зоны.

Ядро правил выносится в **чистый** класс `Mimic.Logic.CombatResolver` (без зависимостей от Unity) для edit-mode тестов.

### Компоненты

| Компонент | Тип | Ответственность |
|---|---|---|
| `CombatEnemy` | data-класс | Лёгкое описание врага в бою: `Name, MaxHp, Hp, Attack`. Собирается из `AdventurerData` или `DayData`. |
| `CombatResolver` | чистая логика (`Mimic.Logic`) | Применение урона, определение исходов. Тестируемо без Unity. |
| `CombatController` | MonoBehaviour-синглтон (`Mimic.Game`) | Состояние боя, цикл ходов, боевой UI, координация с `GameFlow`/`DragController`/`GameContext`. |
| боевая панель | рантайм-UI | Портрет-плейсхолдер + имя, HP-бар врага, индикатор атаки врага, кнопка «Кусь! N», зона «Атаковать» на месте правой сетки. |

## Модель данных и конфиги

**`AdventurerData`** (`Code/Data/AdventurerData.cs`): добавить `bool Battle`, `int Hp`, `int Attack`.
`AdventurerCatalog.Load` — читать новые колонки опционально (нет колонки → `Battle=false`, `Hp/Attack=0`).
CSV `adventurers.csv.txt`: новые колонки `battle,hp,attack` в конец.

**`LootData`** (`Code/Data/LootData.cs`): добавить `int Attack`, `int AttackOnDigest`.
`LootCatalog.Load` — через существующий `Col(row, i, def)` с дефолтом `0`.
CSV `loot.csv.txt`: новые колонки `attack,attackOnDigest` в конец.

**`DayData`** (`Code/Data/DayData.cs`): добавить `int OverlordHp`, `int OverlordAttack`, `int BiteDamage` (урон «Кусь» — настройка ГД).
`DayConfig.Load` — читать через `r.Length > N` с дефолтами (напр. `BiteDamage=2`, иначе кусь будет нулевым).
CSV `day.csv.txt`: новые колонки `overlordHp,overlordAttack,biteDamage` в конец.

**`CombatEnemy`** (новый, `Code/Data/CombatEnemy.cs`):
```csharp
public class CombatEnemy {
    public string Name;
    public int MaxHp;
    public int Hp;
    public int Attack;
}
```
Фабрики: `FromAdventurer(AdventurerData)` и `FromOverlord(DayData)` (имя Хозяина — константа).

## CombatResolver (чистая логика)

Без состояния Unity. Методы (сигнатуры ориентировочные):

- `ApplyDamageToEnemy(CombatEnemy e, int dmg)` → уменьшает `e.Hp` (clamp ≥ 0).
- `EnemyAttackDamage(CombatEnemy e)` → возвращает `e.Attack` (точка для будущих формул).
- `IsEnemyDead(CombatEnemy e)` → `e.Hp <= 0`.
- `IsPlayerDead(int mimicHp)` → `mimicHp <= 0`.

Урон «всегда арифметически вычитается из здоровья» (ТЗ) — никаких модификаторов брони.

## CombatController (поток боя)

Синглтон-MonoBehaviour. Публичный API:

```csharp
void StartCombat(CombatEnemy enemy, Action onWin, Action onLose);
void Bite();                  // кнопка «Кусь»
bool TryAttackWith(LootView); // бросок предмета в зону атаки
bool IsActive { get; }
```

Состояние: `IsActive`, `CombatEnemy enemy`, `bool playerTurn`, `Action onWin/onLose`.

Цикл (враг ходит первым в обоих триггерах):

1. `StartCombat`: сохранить колбэки, собрать боевой UI, **спрятать** кнопки «Следующий»/«Сдаться» и правую сетку, показать боевую панель → `EnemyTurn()`.
2. `EnemyTurn()`: `Resources.CurrentHp -= CombatResolver.EnemyAttackDamage(enemy)`; `Hud.Refresh()`; короткая корутина-пауза (~0.4 с) + флеш для читаемости; если `IsPlayerDead` → `EndCombat(win:false)`; иначе `playerTurn = true`.
3. Ход игрока: свободно двигает/переваривает (текущий драг не трогаем). Ход завершает только:
   - `Bite()`: `ApplyDamageToEnemy(enemy, day.BiteDamage)`.
   - `TryAttackWith(item)`: если `item.Data.Attack > 0` → `ApplyDamageToEnemy(enemy, item.Data.Attack)`, предмет уничтожается, возвращает `true`; если `Attack == 0` → ничего, предмет остаётся (возврат на место драгом), возвращает `false`.
   - После результативной атаки: если `IsEnemyDead` → `EndCombat(win:true)`; иначе `EnemyTurn()`.
4. `EndCombat(win)`: спрятать боевой UI, вернуть правую сетку и кнопки → вызвать `onWin`/`onLose`.

**`attackOnDigest` (низкий приоритет):** в `GameContext.TryDigestHeld`, если бой активен и `item.Data.AttackOnDigest > 0`, нанести этот урон врагу как **бесплатный бонус** (ход не завершает). Минимальная проводка; при нехватке времени — срезается без последствий.

## Интеграция с DragController

По образцу digest-зоны добавить вторую рантайм-зону **«Атаковать»** (на месте правой сетки), активную только при `CombatController.Instance.IsActive`:

- В `Update`, пока `Held != null`: хит-тест курсора над зоной атаки (как `overDigest`).
- На отпускании над зоной → `CombatController.Instance.TryAttackWith(Held)`. `true` → `Held = null`; `false` → `Cancel()` (предмет возвращается на место — поведение из ТЗ для предметов без атаки).
- Зона прячется вне боя.

Кнопка «Кусь» живёт на боевой панели и вызывает `CombatController.Instance.Bite()`.

## Интеграция с GameFlow

Новая `DayPhase.Combat`. Две точки входа:

- **battle-приключенец:** в `BringNext`, если `current.Battle == true`, `IntroPopup` показывает кнопку **«В бой»** (вместо «Сожрать»). По нажатию → `CombatController.StartCombat(CombatEnemy.FromAdventurer(current), onWin: DropLootAndContinue, onLose: EndDeath)`.
  - `DropLootAndContinue` = текущая логика `OnEatPressed` (лут падает в правую сетку), фаза возвращается в `Adventurers`, далее обычный поток.
- **Вызов Хозяину:** `EnterTransition` → `onChallenge` сейчас зовёт `EndChallengeStub`. Заменить на `CombatController.StartCombat(CombatEnemy.FromOverlord(DayConfig.Current), onWin: () => EndPopup.ShowRansomWin(), onLose: EndDeath)`.

`IntroPopup.Show` получает опциональный параметр текста кнопки (`"В бой"` / `"Сожрать"`).

## Боевой UI (рантайм, без правок сцены)

По паттерну `HudView`/digest-зоны:

- **Боевая панель** на месте правой сетки: плашка-портрет (плейсхолдер — цветной бокс) + имя врага.
- **HP-бар врага** (`Hp/MaxHp`), индикатор атаки врага (`⚔N`).
- **Кнопка «Кусь! N»** (N = `day.BiteDamage`).
- Мимиковский HP-бар — существующий снизу-слева (`HudView`).

Спрайтов портретов нет — используем плейсхолдер (имя + цветной бокс), как принято в проекте.

## Граничные случаи

- Смерть мимика в бою (оба триггера) → `EndOfDayPopup.ShowDeath` (переиграть день) — существующий путь.
- Во время боя нельзя сдаться/звать «Следующего» (кнопки спрятаны).
- Нет атакующих предметов → игрок всё равно бьёт «Кусь» и перевариваться ради хила; победа упирается в баланс (забота ГД).
- Переваривание в бою по-прежнему тратит/восполняет ЖС и лечит (`TryDigestHeld` логику не меняем, только добавляем бонус `attackOnDigest`).
- Предмет без `attack`, брошенный во врага, не двигается в зону и не завершает ход (ТЗ).

## Тестирование

Edit-mode (NUnit, сборка `Code.Tests`, namespace `Mimic.Tests`) на `CombatResolver` + фабрики `CombatEnemy`:

- враг бьёт первым: после `EnemyTurn` HP мимика уменьшается на `enemy.Attack`;
- «Кусь» уменьшает HP врага на `BiteDamage`;
- предмет с `attack>0` наносит урон (через `TryAttackWith`-логику в резолвере);
- предмет с `attack==0` урона не наносит и не «исчезает»;
- победа при `enemy.Hp <= 0` (`IsEnemyDead`);
- поражение при `mimicHp <= 0` (`IsPlayerDead`);
- `CombatEnemy.FromAdventurer/FromOverlord` корректно переносят `Hp/Attack/Name`.

UI, драг, цикл ходов в `CombatController` — ручная проверка в Play mode (оба триггера: победа, поражение, бросок предмета, «Кусь», переваривание во время боя).

## Файлы

**Новые:**
- `Code/Data/CombatEnemy.cs`
- `Code/Logic/CombatResolver.cs`
- `Code/Game/CombatController.cs`
- `Code.Tests/CombatResolverTests.cs`

**Изменяемые:**
- `Code/Data/AdventurerData.cs`, `Code/Catalogs/AdventurerCatalog.cs`, `Resources/Configs/adventurers.csv.txt`
- `Code/Data/LootData.cs`, `Code/Catalogs/LootCatalog.cs`, `Resources/Configs/loot.csv.txt`
- `Code/Data/DayData.cs`, `Code/Catalogs/DayConfig.cs`, `Resources/Configs/day.csv.txt`
- `Code/Game/GameFlow.cs` (новая фаза + две точки входа)
- `Code/Game/GameContext.cs` (`attackOnDigest`-бонус в `TryDigestHeld`)
- `Code/Input/DragController.cs` (зона «Атаковать»)
- `Code/UI/AdventurerIntroPopup.cs` (текст кнопки «В бой»)
