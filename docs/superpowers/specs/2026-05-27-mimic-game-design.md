# Design — «Is this a mimic?»

**Дата:** 2026-05-27
**Тип:** GameJam MVP (бюджет 3-5 дней, один разработчик)
**Стек:** Unity 6000.3.16f1, URP 2D, новый Input System, uGUI
**ТЗ:** `~/Downloads/Is this a mimic_ _ Notion.pdf` (вне репо)

## 1. Цель и скоуп

Пазл-инвентарь в духе DREDGE / Backpack Hero / Save Room. Игрок управляет мимиком, в которого приходят 5 приключенцев за день. Лут каждого приключенца сыпется в правую сетку 6×10; задача — перетащить и уложить лут в сетку мимика 14×8, переваривать предметы за «желудочный сок» (освобождая клетки), и к концу дня иметь в мимике суммарную цену лута ≥ дневной квоты.

**В MVP входит:** drag&drop с поворотом, переваривание + расход ЖС, цикл из 5 приключенцев в течение одного дня, свойства «ЕСЛИ РЯДОМ С» (модификаторы цены/стоимости).

**Не входит:** прогрессия дней (только один день), бой/общение/обмен, сломанные клетки, кэш для не влезшего лута, анимации, звуки, финальный арт, портреты (placeholder-эмодзи/цветные квадраты).

## 2. Сцены

- `MainMenu.unity` — экран старта: «Новая игра», «Выход».
- `Game.unity` — весь игровой день в одной сцене. Win/Lose попапы — оверлеи внутри неё.

## 3. Game flow

FSM на одном MonoBehaviour `GameFlow`:

```
NewDay → AdventurerIntro → Sorting → (loop пока есть приключенцы)
                                       ↓
                              EndOfDay → Win/Lose попап → MainMenu
                                       ↑
                            SurrenderPopup (по кнопке «Сдаться»)
```

- **NewDay** — инициализация ресурсов (HP, ЖС, золото=0, dayQuota из CSV), сброс обеих сеток, генерация очереди из 5 приключенцев на день из CSV.
- **AdventurerIntro** — попап с именем, фразой, кнопкой «Сожрать».
- **Sorting** — основной геймплей: лут падает в правую сетку из шаблона приключенца, игрок таскает / переваривает / жмёт «Следующий!». Кнопка «Следующий!» заблокирована, пока в правой сетке остался лут.
- **EndOfDay** — считаем сумму золота в мимике, сравниваем с квотой, показываем `EndOfDayPopup` в win или lose режиме.
- **SurrenderPopup** — confirm → переход в `EndOfDayPopup` lose-режим.

## 4. Префабы

Всё в `Assets/Prefabs/`:

| Префаб | Назначение |
|---|---|
| `MimicGrid.prefab` | Canvas-панель 14×8 (левая) |
| `AdventurerGrid.prefab` | Canvas-панель 6×10 (правая) |
| `LootItem.prefab` | `RectTransform` с фоном + детьми-клетками по шейпу, скрипт `LootView` |
| `AdventurerIntroPopup.prefab` | попап с именем/фразой/кнопкой «Сожрать» |
| `SurrenderConfirmPopup.prefab` | confirm перед сдачей |
| `EndOfDayPopup.prefab` | win и lose режимы (одна префаб с двумя состояниями) |
| `Tooltip.prefab` | общий тултип для лута и кнопок |
| `ContextMenuPopup.prefab` | меню по ПКМ с одной кнопкой «Переварить» |

## 5. Данные (CSV)

Всё в `Assets/Configs/`. Парсятся при старте `Game.unity` в `List<T>` через простой `string.Split(',')` (экранирование кавычками для свойств с запятыми).

### 5.1. `loot.csv`

```csv
id,name,description,shape,gold,acidCost,healOnDigest,cellsRestoredOnDigest,adjacencyTarget,adjacencyEffect
sword,Меч,"Острый, но тупой",X|X|X|X,10,3,0,0,,
shield,Щит,"Защита от себя",XX|XX,8,4,5,0,sword,gold:+50%
barracuda,Барракуда,"Ещё трепыхается",.X|XX|.X|.X,15,5,0,2,bread,acid:-30%
bread,Хлеб,Несвежий,XX,3,1,2,0,,
gem,Самоцвет,Дешёвый блеск,X,20,2,0,0,gem,gold:+25%
```

- **`shape`** — pattern-string. Строки разделены `|`, `X` = занятая клетка, `.` = пустая. **Все строки в одном шейпе должны быть одинаковой длины** (валидируется при парсинге, иначе ошибка). Пример `.X|XX|.X|.X` = 4 строки × 2 колонки, форма буквы «Г».
- **`adjacencyTarget`** — id предмета. Пусто = нет свойства. Самососедство: указываем тот же id, что у самого предмета — эффект сработает, если **другая** копия этого предмета лежит рядом (предмет не считается соседом самого себя).
- **`adjacencyEffect`** — формат `<тип>:<знак><число>%`. Типы: `gold` (модификатор цены), `acid` (модификатор стоимости переваривания). Несколько эффектов — через `;`.

### 5.2. `adventurers.csv`

```csv
id,name,phrase,lootIds
warrior,Воин,"definitely not a mimic!",sword;shield;bread
rogue,Плут,"я просто посмотрю",gem;gem;bread;barracuda
mage,Маг,"что в этом сундуке?",gem;shield;bread
```

- **`lootIds`** — точные id из `loot.csv`, разделитель `;`. На раунд приключенца лут падает в правую сетку именно этим набором.

### 5.3. `day.csv`

```csv
day,goldQuota,startHp,startAcid,adventurerIds
1,40,3,15,warrior;rogue;mage;warrior;rogue
```

- **`goldQuota`** — сколько суммы золота должно быть в мимике на конец дня для Win.
- **`adventurerIds`** — очередь из 5 приключенцев.
- Файл может содержать несколько строк (для будущей прогрессии), MVP читает только первую.

## 6. Grid модель

```csharp
class GridModel {
    int width, height;
    LootView[,] cells;   // null = пусто, ссылка на LootView = занято
    bool[,] broken;      // сломанные клетки (всегда false в MVP)
    bool TryPlace(LootView item, int x, int y, Rotation rot);
    void Remove(LootView item);
    IEnumerable<LootView> AllItems();
}
```

Шейп предмета — `bool[,]` после парсинга pattern-string, плюс `Rotation` (0/90/180/270) для текущего поворота. При `TryPlace` берём повёрнутый шейп, для каждой занятой клетки шейпа сверяем `cells[x+dx, y+dy] == null && !broken[x+dx, y+dy]`.

Две инстанции: `mimicGrid` (14×8) и `adventurerGrid` (6×10).

## 7. Управление и drag&drop

`InputBridge` MonoBehaviour читает действия нового Input System:

| Action | Биндинг | Эффект |
|---|---|---|
| `Click` | ЛКМ | pick / drop |
| `Cancel` | ПКМ (когда предмет взят) | return to origin |
| `ContextMenu` | ПКМ по item в мимике (когда ничего не взято) | открыть `ContextMenuPopup` |
| `RotateCW` | E, колесо вниз | поворот по часовой |
| `RotateCCW` | Q, колесо вверх | поворот против часовой |

**Цикл pick → hover → drop:**

1. ЛКМ по `LootView` (через `IPointerDownHandler`) → `DragController.Pick(item)`. Item открепляется от своей `GridModel`, паркуется как child под `DragLayer` Canvas (поверх всего), следует за курсором.
2. Hover над клеткой — каждый кадр считаем «под какой клеткой курсор» по `RectTransform.InverseTransformPoint`. Клетки под предметом подсвечиваются зелёным (если `TryPlace` true) или красным (если false).
3. ЛКМ ещё раз — если зелёным, коммитим `TryPlace` и item становится child клетки. Если красным — игнорим (предмет остаётся в руке).
4. ПКМ во время drag — item возвращается на исходную позицию (запоминаем `originGrid`, `originX/Y/Rot` в `Pick`).
5. Q/E/wheel пока несём — мутируем `currentRotation`, пересчитываем подсветку.

**Правила переходов между сетками:**

- Из правой (приключенец) в левую (мимик) — можно всегда.
- Из левой в правую — **запрещено** (упрощение, в ТЗ не оговорено).
- Внутри одной сетки — можно (перекладка).

## 8. Adjacency («ЕСЛИ РЯДОМ С»)

Пересчитывается полностью при каждом изменении мимика (place/remove/digest). Алгоритм:

1. `effectiveGold[item] = item.baseGold`, `effectiveAcid[item] = item.baseAcidCost` для всех item в мимике.
2. Для каждого `item`: для каждой его занятой клетки смотрим 4-соседей по граням, собираем уникальный `Set<LootView>` соседей.
3. Если у `item` есть `adjacencyTarget` и хотя бы один сосед матчит этот id (или `item.id == adjacencyTarget` для самососедства) — применяем `adjacencyEffect` к **самому item**:
   - `gold:+50%` → `effectiveGold[item] *= 1.5`
   - `acid:-30%` → `effectiveAcid[item] *= 0.7`
4. После применения эффектов: `effectiveGold` клампим к `>= 0`, `effectiveAcid` клампим к `>= 1` (не может быть бесплатного переваривания).
5. Сумма `effectiveGold` всех в мимике = «Цена лута в мимике» в HUD.

**Подсветка граней:** grани между item и активирующим соседом подсвечиваются — зелёным если эффект для игрока «хороший» (`gold+` или `acid-`), красным иначе. Если у item несколько эффектов — приоритет красному. Подсветка реализуется как overlay-Image поверх грани клетки.

## 9. Digestion

ПКМ по item в мимике (когда ничего не взято) → `ContextMenuPopup` рядом с курсором с одной кнопкой «Переварить (стоит N сока)», где N = текущий `effectiveAcid[item]`. Если `currentAcid < N` — кнопка disabled.

При нажатии:
- `currentAcid -= effectiveAcid[item]`
- `currentHp += healOnDigest` (без потолка в MVP)
- `cellsRestoredOnDigest` → no-op (broken cells не реализуем)
- Item удаляется из `MimicGrid`, GameObject уничтожается
- Adjacency пересчитывается

«В бездне переваривать нельзя» — `ContextMenuPopup` не открывается для item в `AdventurerGrid`.

## 10. Ресурсы

`Resources` MonoBehaviour на одном объекте:

| Поле | Стартовое | Изменяется |
|---|---|---|
| `currentHp` | `day.startHp` (3) | `+= healOnDigest`. Снижения нет в MVP. |
| `currentAcid` | `day.startAcid` (15) | `-= effectiveAcid` при переваривании |
| `currentGoldInMimic` | 0 | вычисляется: `sum(effectiveGold)` по мимику, пересчёт при каждом adjacency-апдейте |
| `dayQuota` | `day.goldQuota` (40) | не меняется |

## 11. Win / Lose

- **Win** срабатывает в `EndOfDay`: если `currentGoldInMimic >= dayQuota` → `EndOfDayPopup` win-режим («День спасён! Заработано X / нужно Y», кнопка «В меню»).
- **Lose** срабатывает по двум путям:
  - Игрок жмёт «Сдаться» → `SurrenderConfirmPopup` → подтверждение → `EndOfDayPopup` lose-режим («Вы лопнули от переедания», кнопки «Начать день заново» и «В меню»).
  - `EndOfDay` и `currentGoldInMimic < dayQuota` → тот же `EndOfDayPopup` lose-режим.

**Подсветка кнопки «Сдаться»:** каждый кадр чекаем (дёшево), по ТЗ — если **хотя бы одно** из условий true, кнопка меняет цвет на красный (мигание не делаем):
- `currentAcid < min(effectiveAcid[item] for item in mimicGrid)` — нечем переваривать даже самый дешёвый.
- `mimicGrid.freeCells < adventurerGrid.occupiedCells` — даже если перетащить весь оставшийся лут, в мимика не влезет.

Сама сдача — только по клику игрока, не автоматически.

## 12. HUD layout

Главный Canvas в `Game.unity` — одна панель `HUDRoot` с подэлементами:

- **Верхний бар:** «Цена лута: {currentGoldInMimic}», «Нужно: {dayQuota}», «День 1», «Герой {n}/5».
- **Центр-лево:** `MimicGrid` (14×8).
- **Центр-право:** `AdventurerGrid` (6×10) + сверху «Портрет и имя» текущего приключенца.
- **Нижний бар:** `HealthBar`, `AcidBar` (filled images), кнопка «Сдаться», кнопка «Следующий!» / «Завершить день» (последняя — когда приключенцы кончились).

**Тултип** — один общий `Tooltip.prefab` с `TooltipController`-синглтоном. `LootView` дёргает `Tooltip.Show(itemData, effectiveGold, effectiveAcid, adjacencyActive)` на pointer-enter, `Hide()` на exit/pick. Показывает: название, описание, текущую цену в золоте, стоимость переваривания, описание свойства «РЯДОМ С» (подсвечено если активно).

## 13. Список MonoBehaviour (high-level)

| Скрипт | Где живёт | Ответственность |
|---|---|---|
| `GameFlow` | объект в `Game.unity` | FSM состояний дня |
| `Resources` | объект в `Game.unity` | HP/ЖС/золото/квота |
| `GridModel` | поле в `GridView` | 2D массив клеток + TryPlace/Remove |
| `GridView` | префабы `MimicGrid`/`AdventurerGrid` | визуализация сетки, проверка клика по клеткам |
| `LootView` | префаб `LootItem` | визуал предмета, IPointerDownHandler |
| `DragController` | синглтон | состояние «в руке», подсветка валидности |
| `InputBridge` | объект в `Game.unity` | подписки на Input System actions |
| `AdjacencyResolver` | синглтон или static | пересчёт `effectiveGold/Acid` |
| `TooltipController` | синглтон | показ/скрытие тултипа |
| `ContextMenuController` | синглтон | показ/скрытие меню переваривания |
| `CsvLoader` | static | парсинг трёх CSV в `List<T>` |
| `LootCatalog`, `AdventurerCatalog`, `DayConfig` | статика после загрузки | доступ к данным по id |

Никаких asmdef, DI-контейнеров, ивент-систем. `FindObjectOfType` и прямые ссылки через инспектор — допустимы.

## 14. Out of scope (явный список)

| Фича из ТЗ | Решение | Причина |
|---|---|---|
| Сломанные клетки (broken) | поле в модели всегда false | эффектов, ломающих клетки, в MVP нет |
| Кэш для не влезшего лута | гарантируем балансом (`sum(shape) ≤ 6×10`) | edge case, доп. UI |
| Анимации (пыщ, плавные бары) | мгновенный fill | полировка |
| Звуки | хуки `SfxPlayer.Play(name)` → noop | полировка |
| Кнопки бой/общение/обмен в попапе приключенца | только «Сожрать» | систем нет |
| Картинка обблевавшегося мимика на Lose | текст вместо | арт placeholder |
| Direction-aware подсветка граней (стрелочки) | просто заливка цветом грани | усложнение для второстепенной фичи |
| Прогрессия дней (несколько дней) | один день | MVP-решение |
| Сохранения | нет | один день, перезапуск из меню |
