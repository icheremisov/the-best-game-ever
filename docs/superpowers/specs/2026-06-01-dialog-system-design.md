# Система диалогов — дизайн

**Дата:** 2026-06-01
**ТЗ:** `ТЗ Диалоги _ Notion.pdf` (вне репо)

## Цель

В игре по триггеру запускаются цепочки диалогов. Диалог — цепочка реплик с текстом и
портретом говорящего. Экран затемняется и не прокликивается; клик листает цепочку, после
последней реплики игрока отпускают.

## Решения (согласовано с заказчиком)

- **Баббл**: отдельный компонент `DialogOverlay`, `AdventurerIntroPopup` не трогаем. «Реюз»
  баббла = общий код-билдер визуала, а не общий компонент.
- **Арт спикера**: префаб-портрет, тот же механизм, что у портрета приключенца и прочих
  персонажей (master/mimic).
- **Повтор**: триггер показывает диалог **каждый раз** при срабатывании, включая RetryDay.
  Once-гарда нет.

## 1. Конфиг — `Resources/Configs/dialogs.csv.txt`

Колонки: `trigger,text,icon`.

- Строка с непустым `trigger` начинает новую цепочку с этим ключом.
- Идущие следом строки с пустым `trigger` добавляются в текущую цепочку по порядку.
- `icon` — id портрета говорящего (`master` / `mimic` / id приключенца).

Триггеры (формируются в коде):
- `start_day_{N}` — начался день N (в т.ч. самый первый и RetryDay).
- `end_day_{N}` — игрок нажал «Завершить день» (момент появления экрана Властелина).

Пример:

```csv
trigger,text,icon
start_day_1,Привет!,master
,Пока!,master
,Снова привет!,master
end_day_1,Привет!,master
,Посмотрим как ты поработал!,master
start_day_2,Надо сбежать от этого узурпатора,mimic
```

Парсится существующим `CsvLoader.ParseAll`.

## 2. Данные + каталог

- `Data/DialogLine.cs` — `{ string Text; string Icon; }` (одна реплика).
- `Catalogs/DialogCatalog.cs` — статический класс по образцу `DayConfig`:
  - `Load()` — `Resources.Load<TextAsset>("Configs/dialogs.csv")`, парс в
    `Dictionary<string, List<DialogLine>>` по триггерам (пустой `trigger` = продолжение
    последней цепочки). Текущий ключ держим в локальной переменной при проходе строк.
  - `Get(string trigger)` → `List<DialogLine>` или `null`/пусто, если триггера нет.
  - Вызов в `GameContext.Awake` после `LootCatalog/AdventurerCatalog/DayConfig.Load()`.

## 3. UI — `Code/UI/DialogOverlay.cs`

MonoBehaviour-синглтон (`Instance`), авто-создаётся в
`GameContext.EnsureRuntimeControllers` (как `TooltipController`/`DigestConfirmPopup`).
UI строится в коде под главным Canvas (паттерн `TooltipController.EnsurePanel`):

- **Затемнение**: полноэкранный полупрозрачный чёрный `Image`, `raycastTarget=true` —
  гасит экран и глотает клики. Рисуется поверх всего (последний sibling / высокий порядок).
- **Баббл** (общий код-билдер):
  - контейнер портрета (`PortraitContainer`),
  - `Text` реплики,
  - маркер ▶ — намёк, что можно скликнуть.

API:

```csharp
void Show(IList<DialogLine> chain, Action onComplete);
```

- Активирует оверлей, показывает `chain[0]`.
- Клик по затемнению → следующая реплика. Реплик не осталось → прячем оверлей, зовём
  `onComplete`.

**Портрет** — общий `PortraitLoader.Load(icon)`:
`Resources.Load<GameObject>` сначала `Art/Portraits/{icon}`, затем `Art/Adventurers/{icon}`
(master/mimic кладутся в `Art/Portraits`, приключенцы реюзаются из существующей папки).
Кэш по id + fallback «нет арта» (контейнер пуст/заглушка). Портрет не перехватывает клики
(`raycastTarget=false` на его графиках, как в `AdventurerIntroPopup`).

## 4. Триггеры в `GameFlow`

Хелпер:

```csharp
void PlayTrigger(string key, Action onDone)
{
    var chain = DialogCatalog.Get(key);
    if (chain == null || chain.Count == 0) { onDone(); return; }
    DialogOverlay.Instance.Show(chain, onDone);
}
```

- **start_day**: в конце `BeginDay` вместо `BringNext();` →
  `PlayTrigger($"start_day_{DayConfig.Current.Day}", BringNext);`
- **end_day**: в `EnterOverlord` сначала `Phase = DayPhase.Overlord;` (ввод закрыт), затем
  `PlayTrigger($"end_day_{DayConfig.Current.Day}", () => OverlordPopup.Show(OnSettled));`

Срабатывает каждый раз при срабатывании триггера. Нет строк в конфиге → диалог
пропускается без задержки (`onDone()` сразу).

Во время start_day-диалога `Phase == Adventurers`, но затемняющая панель перекрывает грид и
глотает raycast; `GameFlow.Update` лишь тогглит кнопку Next — конфликта нет.

## 5. Проверка (джем, без CI)

- Manual через Unity MCP: добавить `start_day_1` в `dialogs.csv`, Play, проверить:
  затемнение → баббл с портретом → проклик всей цепочки → отпускание игрока (BringNext);
  `end_day_1` → диалог перед экраном Властелина.
- Опц.: быстрый EditMode-тест парсинга `DialogCatalog` (группировка цепочки по пустому
  `trigger`) — есть `Mimic.Editor.asmdef`.

## Затрагиваемые файлы

Новые:
- `Game/Assets/Resources/Configs/dialogs.csv.txt` (+ `.meta`)
- `Game/Assets/Code/Data/DialogLine.cs`
- `Game/Assets/Code/Catalogs/DialogCatalog.cs`
- `Game/Assets/Code/UI/DialogOverlay.cs`
- `Game/Assets/Code/UI/PortraitLoader.cs` (общий загрузчик портретов)

Правки:
- `Game/Assets/Code/Game/GameContext.cs` — `DialogCatalog.Load()` + авто-создание
  `DialogOverlay`.
- `Game/Assets/Code/Game/GameFlow.cs` — `PlayTrigger` + хуки в `BeginDay`/`EnterOverlord`.
