# Система диалогов — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Добавить систему диалогов: цепочки реплик с портретом, запускаемые по триггерам начала/конца дня, через конфиг-CSV.

**Architecture:** Конфиг `dialogs.csv` → статический `DialogCatalog` (чистый `Parse` + `Load` через Resources) → модальный `DialogOverlay` (строит UI в коде под главным Canvas, затемняет экран, клик листает цепочку). `GameFlow` дёргает триггеры `start_day_{N}` / `end_day_{N}`. Портреты грузит общий `PortraitLoader`.

**Tech Stack:** Unity 6 (URP 2D), C#, uGUI (UnityEngine.UI), NUnit EditMode-тесты.

**Важно про пути:** Unity грузит TextAsset по имени без последнего `.txt`. Файл `Resources/Configs/dialogs.csv.txt` загружается как `Resources.Load<TextAsset>("Configs/dialogs.csv")` — точно как существующий `day.csv`.

**Запуск тестов:** EditMode через Unity Test Runner (`Window → General → Test Runner → EditMode → Run Selected`) или MCP `run_tests` (mode=EditMode). После правок скриптов — `read_console` на ошибки компиляции перед использованием новых типов.

---

### Task 1: Тип данных `DialogLine`

**Files:**
- Create: `Game/Assets/Code/Data/DialogLine.cs`

- [ ] **Step 1: Создать тип реплики**

```csharp
namespace Mimic.Data
{
    // Одна реплика диалога: текст и id портрета говорящего (master/mimic/id приключенца).
    public class DialogLine
    {
        public string Text;
        public string Icon;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Game/Assets/Code/Data/DialogLine.cs
git commit -m "feat(dialog): тип DialogLine"
```

---

### Task 2: `DialogCatalog` — парсинг и доступ

**Files:**
- Create: `Game/Assets/Code/Catalogs/DialogCatalog.cs`
- Test: `Game/Assets/Code.Tests/DialogCatalogTests.cs`

- [ ] **Step 1: Написать падающий тест**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Mimic.Catalogs;
using Mimic.Data;

namespace Mimic.Tests
{
    public class DialogCatalogTests
    {
        [Test]
        public void Parse_GroupsLinesByTriggerUntilNextTrigger()
        {
            var csv = "trigger,text,icon\n"
                    + "start_day_1,Привет!,master\n"
                    + ",Пока!,master\n"
                    + "end_day_1,Итог,master\n";
            var chains = DialogCatalog.Parse(csv);

            Assert.IsTrue(chains.ContainsKey("start_day_1"));
            Assert.AreEqual(2, chains["start_day_1"].Count);
            Assert.AreEqual("Привет!", chains["start_day_1"][0].Text);
            Assert.AreEqual("master", chains["start_day_1"][0].Icon);
            Assert.AreEqual("Пока!", chains["start_day_1"][1].Text);

            Assert.IsTrue(chains.ContainsKey("end_day_1"));
            Assert.AreEqual(1, chains["end_day_1"].Count);
            Assert.AreEqual("Итог", chains["end_day_1"][0].Text);
        }

        [Test]
        public void Get_ReturnsNullForUnknownTrigger()
        {
            DialogCatalog.SetForTest(new Dictionary<string, List<DialogLine>>());
            Assert.IsNull(DialogCatalog.Get("nope"));
        }
    }
}
```

- [ ] **Step 2: Запустить тест — убедиться, что не компилируется/падает**

Run: Test Runner (EditMode) → `DialogCatalogTests`.
Expected: компиляция падает — `DialogCatalog` не существует.

- [ ] **Step 3: Реализовать `DialogCatalog`**

```csharp
using System.Collections.Generic;
using Mimic.Logic;
using UnityEngine;

namespace Mimic.Catalogs
{
    public static class DialogCatalog
    {
        private static Dictionary<string, List<Mimic.Data.DialogLine>> _chains = new();

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("Configs/dialogs.csv");
            _chains = ta != null
                ? Parse(ta.text)
                : new Dictionary<string, List<Mimic.Data.DialogLine>>();
        }

        // Колонки: trigger,text,icon. Непустой trigger начинает новую цепочку;
        // пустой trigger добавляет реплику в текущую цепочку. Шапка пропускается CsvLoader.
        public static Dictionary<string, List<Mimic.Data.DialogLine>> Parse(string csvText)
        {
            var rows = CsvLoader.ParseAll(csvText);
            var result = new Dictionary<string, List<Mimic.Data.DialogLine>>();
            List<Mimic.Data.DialogLine> current = null;
            foreach (var r in rows)
            {
                string trigger = r.Length > 0 ? r[0].Trim() : "";
                string text = r.Length > 1 ? r[1] : "";
                string icon = r.Length > 2 ? r[2].Trim() : "";
                if (!string.IsNullOrEmpty(trigger))
                {
                    current = new List<Mimic.Data.DialogLine>();
                    result[trigger] = current;
                }
                if (current == null) continue; // строки до первого триггера игнорируем
                current.Add(new Mimic.Data.DialogLine { Text = text, Icon = icon });
            }
            return result;
        }

        // Цепочка реплик для триггера или null, если такого триггера нет.
        public static IReadOnlyList<Mimic.Data.DialogLine> Get(string trigger)
            => _chains.TryGetValue(trigger, out var list) ? list : null;

        public static void SetForTest(Dictionary<string, List<Mimic.Data.DialogLine>> chains)
            => _chains = chains;
    }
}
```

- [ ] **Step 4: Запустить тест — убедиться, что проходит**

Run: Test Runner (EditMode) → `DialogCatalogTests`.
Expected: оба теста PASS. Проверить `read_console` — нет ошибок компиляции.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Catalogs/DialogCatalog.cs Game/Assets/Code.Tests/DialogCatalogTests.cs
git commit -m "feat(dialog): DialogCatalog с парсингом цепочек"
```

---

### Task 3: Конфиг `dialogs.csv`

**Files:**
- Create: `Game/Assets/Resources/Configs/dialogs.csv.txt`

- [ ] **Step 1: Создать конфиг с примерами под триггеры дня 1**

Содержимое `Game/Assets/Resources/Configs/dialogs.csv.txt`:

```csv
trigger,text,icon
start_day_1,Привет! Добро пожаловать в подземелье.,master
,Сегодня к тебе заглянут приключенцы. Не оплошай.,master
end_day_1,Посмотрим как ты поработал!,master
start_day_2,Надо сбежать от этого узурпатора.,mimic
```

- [ ] **Step 2: Дать Unity сгенерировать .meta**

Run: MCP `refresh_unity` (или фокус на Editor). Проверить, что появился `dialogs.csv.txt.meta`.
Expected: `.meta` создан рядом с файлом.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Resources/Configs/dialogs.csv.txt Game/Assets/Resources/Configs/dialogs.csv.txt.meta
git commit -m "feat(dialog): конфиг dialogs.csv"
```

---

### Task 4: `PortraitLoader` — общий загрузчик портретов

**Files:**
- Create: `Game/Assets/Code/UI/PortraitLoader.cs`

- [ ] **Step 1: Реализовать загрузчик**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mimic.UI
{
    // Общий загрузчик префабов-портретов персонажей (приключенцы, master, mimic).
    // Сначала ищет Art/Portraits/{id}, затем Art/Adventurers/{id} (реюз существующих артов).
    public static class PortraitLoader
    {
        private static readonly Dictionary<string, GameObject> cache = new();

        public static GameObject LoadPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var p)) return p;
            p = Resources.Load<GameObject>("Art/Portraits/" + id)
                ?? Resources.Load<GameObject>("Art/Adventurers/" + id);
            cache[id] = p;
            return p;
        }

        // Инстанцирует портрет в контейнер на весь его размер, гасит raycast.
        // Возвращает инстанс или null (арта нет — вызывающий показывает заглушку).
        public static GameObject Instantiate(string id, RectTransform container)
        {
            var prefab = LoadPrefab(id);
            if (prefab == null || container == null) return null;
            var inst = Object.Instantiate(prefab, container, false);
            if (inst.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            foreach (var g in inst.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            return inst;
        }
    }
}
```

- [ ] **Step 2: Проверить компиляцию**

Run: MCP `read_console` после refresh.
Expected: нет ошибок.

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/PortraitLoader.cs
git commit -m "feat(dialog): общий PortraitLoader"
```

---

### Task 5: `DialogOverlay` — модальный оверлей

**Files:**
- Create: `Game/Assets/Code/UI/DialogOverlay.cs`

- [ ] **Step 1: Реализовать оверлей (строит UI в коде под Canvas)**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mimic.Data;

namespace Mimic.UI
{
    // Модальный оверлей диалогов: затемняет экран, показывает баббл с портретом и текстом,
    // клик по любому месту листает цепочку. UI строится в коде под главным Canvas
    // (паттерн TooltipController.EnsurePanel). Авто-создаётся в GameContext.
    public class DialogOverlay : MonoBehaviour
    {
        public static DialogOverlay Instance { get; private set; }

        private Canvas hostCanvas;
        private GameObject root;            // затемняющая панель — ловит клики
        private RectTransform portraitContainer;
        private GameObject portraitFallback;
        private Text bodyText;
        private GameObject portraitInstance;

        private IList<DialogLine> chain;
        private int index;
        private Action onComplete;

        private void Awake()
        {
            Instance = this;
            EnsureUi();
            if (root != null) root.SetActive(false);
        }

        // Показать цепочку. Пустая/null — сразу зовём onComplete (диалога нет).
        public void Show(IList<DialogLine> lines, Action onCompleteCallback)
        {
            if (lines == null || lines.Count == 0) { onCompleteCallback?.Invoke(); return; }
            if (root == null) { onCompleteCallback?.Invoke(); return; } // нет Canvas — не блокируем игру
            chain = lines;
            index = 0;
            onComplete = onCompleteCallback;
            root.SetActive(true);
            ShowCurrent();
        }

        private void Advance()
        {
            index++;
            if (chain == null || index >= chain.Count) { Close(); return; }
            ShowCurrent();
        }

        private void Close()
        {
            if (root != null) root.SetActive(false);
            var cb = onComplete;
            onComplete = null;
            chain = null;
            cb?.Invoke();
        }

        private void ShowCurrent()
        {
            var line = chain[index];
            if (bodyText != null) bodyText.text = line.Text;
            ShowPortrait(line.Icon);
        }

        private void ShowPortrait(string icon)
        {
            if (portraitInstance != null) Destroy(portraitInstance);
            portraitInstance = PortraitLoader.Instantiate(icon, portraitContainer);
            if (portraitFallback != null) portraitFallback.SetActive(portraitInstance == null);
        }

        private void EnsureUi()
        {
            if (root != null) return;
            if (hostCanvas == null) hostCanvas = FindFirstObjectByType<Canvas>();
            if (hostCanvas == null)
            {
                Debug.LogWarning("[DialogOverlay] Нет Canvas в сцене — диалоги не отрисуются");
                return;
            }

            // Затемняющая панель на весь экран; Button глотает клики и листает диалог.
            var panelGo = new GameObject("DialogOverlay_Auto",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            panelGo.transform.SetParent(hostCanvas.transform, false);
            var panelRt = (RectTransform)panelGo.transform;
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.7f);
            panelImg.raycastTarget = true;
            var btn = panelGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Advance);
            root = panelGo;
            panelRt.SetAsLastSibling(); // поверх всего

            // Баббл внизу по центру (реюзаемый визуал диалога/поп-апа).
            var bubbleGo = new GameObject("Bubble",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bubbleGo.transform.SetParent(panelRt, false);
            var bubbleRt = (RectTransform)bubbleGo.transform;
            bubbleRt.anchorMin = new Vector2(0.5f, 0f);
            bubbleRt.anchorMax = new Vector2(0.5f, 0f);
            bubbleRt.pivot = new Vector2(0.5f, 0f);
            bubbleRt.anchoredPosition = new Vector2(0f, 60f);
            bubbleRt.sizeDelta = new Vector2(760f, 220f);
            var bubbleImg = bubbleGo.GetComponent<Image>();
            bubbleImg.color = new Color(0.97f, 0.96f, 0.92f, 1f);
            bubbleImg.raycastTarget = false;

            const float portW = 180f;

            // Контейнер портрета слева.
            var portGo = new GameObject("Portrait", typeof(RectTransform));
            portGo.transform.SetParent(bubbleRt, false);
            portraitContainer = (RectTransform)portGo.transform;
            portraitContainer.anchorMin = new Vector2(0f, 0f);
            portraitContainer.anchorMax = new Vector2(0f, 1f);
            portraitContainer.pivot = new Vector2(0f, 0.5f);
            portraitContainer.offsetMin = new Vector2(16f, 16f);
            portraitContainer.offsetMax = new Vector2(16f + portW, -16f);

            // Заглушка портрета (если арта нет).
            var fbGo = new GameObject("PortraitFallback",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fbGo.transform.SetParent(portraitContainer, false);
            var fbRt = (RectTransform)fbGo.transform;
            fbRt.anchorMin = Vector2.zero; fbRt.anchorMax = Vector2.one;
            fbRt.offsetMin = Vector2.zero; fbRt.offsetMax = Vector2.zero;
            var fbImg = fbGo.GetComponent<Image>();
            fbImg.color = new Color(0.6f, 0.55f, 0.5f, 1f);
            fbImg.raycastTarget = false;
            portraitFallback = fbGo;

            // Текст реплики справа от портрета.
            var textGo = new GameObject("Body",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(bubbleRt, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.offsetMin = new Vector2(16f + portW + 16f, 40f);
            textRt.offsetMax = new Vector2(-24f, -16f);
            bodyText = textGo.GetComponent<Text>();
            bodyText.font = FontProvider.Default;
            bodyText.fontSize = 26;
            bodyText.color = new Color(0.1f, 0.1f, 0.12f);
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.raycastTarget = false;

            // Маркер ▶ — намёк, что диалог можно скликнуть.
            var markGo = new GameObject("ClickHint",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            markGo.transform.SetParent(bubbleRt, false);
            var markRt = (RectTransform)markGo.transform;
            markRt.anchorMin = new Vector2(1f, 0f);
            markRt.anchorMax = new Vector2(1f, 0f);
            markRt.pivot = new Vector2(1f, 0f);
            markRt.anchoredPosition = new Vector2(-18f, 14f);
            markRt.sizeDelta = new Vector2(40f, 40f);
            var mark = markGo.GetComponent<Text>();
            mark.font = FontProvider.Default;
            mark.fontSize = 30;
            mark.text = "▶";
            mark.color = new Color(0.1f, 0.1f, 0.12f);
            mark.alignment = TextAnchor.MiddleRight;
            mark.raycastTarget = false;
        }
    }
}
```

- [ ] **Step 2: Проверить компиляцию**

Run: MCP `read_console` после refresh.
Expected: нет ошибок. `FontProvider.Default` существует (используется в других попапах).

- [ ] **Step 3: Commit**

```bash
git add Game/Assets/Code/UI/DialogOverlay.cs
git commit -m "feat(dialog): модальный DialogOverlay"
```

---

### Task 6: Подключение в `GameContext` (загрузка каталога + авто-создание оверлея)

**Files:**
- Modify: `Game/Assets/Code/Game/GameContext.cs:37` (после `DayConfig.Load();`)
- Modify: `Game/Assets/Code/Game/GameContext.cs:60-64` (в `EnsureRuntimeControllers`, рядом с `DigestConfirmPopup`)

- [ ] **Step 1: Загрузить каталог диалогов**

В `Awake`, сразу после строки `DayConfig.Load();` добавить:

```csharp
            DialogCatalog.Load();
```

- [ ] **Step 2: Авто-создать `DialogOverlay`**

В методе `EnsureRuntimeControllers`, после блока `DigestConfirmPopup` (перед закрывающей `}` метода), добавить:

```csharp
            if (DialogOverlay.Instance == null)
            {
                gameObject.AddComponent<DialogOverlay>();
                Debug.Log("[GameContext] Auto-added DialogOverlay");
            }
```

- [ ] **Step 3: Проверить компиляцию**

Run: MCP `read_console` после refresh.
Expected: нет ошибок. `Mimic.Catalogs` и `Mimic.UI` уже в using-ах файла.

- [ ] **Step 4: Commit**

```bash
git add Game/Assets/Code/Game/GameContext.cs
git commit -m "feat(dialog): загрузка каталога и авто-создание оверлея"
```

---

### Task 7: Триггеры в `GameFlow`

**Files:**
- Modify: `Game/Assets/Code/Game/GameFlow.cs:57` (конец `BeginDay` — заменить `BringNext();`)
- Modify: `Game/Assets/Code/Game/GameFlow.cs:165-169` (`EnterOverlord`)
- Modify: `Game/Assets/Code/Game/GameFlow.cs` (добавить метод `PlayTrigger`)

- [ ] **Step 1: Добавить хелпер `PlayTrigger`**

Добавить новый метод в класс `GameFlow` (например, сразу после `BeginDay`):

```csharp
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
```

- [ ] **Step 2: Хук start_day в конце `BeginDay`**

В методе `BeginDay` заменить последнюю строку `BringNext();` на:

```csharp
            PlayTrigger($"start_day_{DayConfig.Current.Day}", BringNext);
```

- [ ] **Step 3: Хук end_day в `EnterOverlord`**

Заменить тело метода `EnterOverlord` целиком на:

```csharp
        private void EnterOverlord()
        {
            Phase = DayPhase.Overlord;
            PlayTrigger($"end_day_{DayConfig.Current.Day}",
                () => OverlordPopup.Show(OnSettled));
        }
```

- [ ] **Step 4: Проверить компиляцию**

Run: MCP `read_console` после refresh.
Expected: нет ошибок. `DialogOverlay` в namespace `Mimic.UI` — уже в using-ах `GameFlow.cs`.

- [ ] **Step 5: Commit**

```bash
git add Game/Assets/Code/Game/GameFlow.cs
git commit -m "feat(dialog): триггеры start_day/end_day в GameFlow"
```

---

### Task 8: Ручная проверка в Play mode

**Files:** нет (verification only)

- [ ] **Step 1: Запустить игру и проверить start_day_1**

Войти в Play mode (MCP `manage_editor` play, или из MainMenu). Ожидаемо при старте дня 1:
- экран затемняется и не прокликивается;
- появляется баббл с текстом «Привет! Добро пожаловать…» и заглушкой/портретом `master`;
- клик листает на вторую реплику, затем закрывает оверлей и пускает приключенцев (`BringNext`).

- [ ] **Step 2: Проверить end_day_1**

Завершить день (кнопка «Завершить день»). Ожидаемо: диалог «Посмотрим как ты поработал!» появляется ПЕРЕД экраном Властелина; после проклика открывается `OverlordPopup`.

- [ ] **Step 3: Проверить отсутствие диалога**

Убедиться, что переход на день, для которого нет строк (напр. `end_day_2`), не вешает игру — флоу идёт дальше без задержки.

- [ ] **Step 4: Проверить повтор на RetryDay**

Спровоцировать смерть/лопание и «переиграть день» — `start_day_{N}` показывается снова (повтор каждый раз — ожидаемое поведение).

- [ ] **Step 5: Финальный коммит (если были правки конфига при проверке)**

```bash
git add -A && git commit -m "chore(dialog): правки конфига после ручной проверки" || true
```

---

## Карта файлов

| Файл | Ответственность |
|------|------------------|
| `Code/Data/DialogLine.cs` | DTO реплики (текст + icon). |
| `Code/Catalogs/DialogCatalog.cs` | Парсинг `dialogs.csv` в цепочки, доступ по триггеру. |
| `Resources/Configs/dialogs.csv.txt` | Контент диалогов (trigger,text,icon). |
| `Code/UI/PortraitLoader.cs` | Общая загрузка/инстанс префабов-портретов. |
| `Code/UI/DialogOverlay.cs` | Модальный UI: затемнение + баббл + листание. |
| `Code/Game/GameContext.cs` | Загрузка каталога, авто-создание оверлея. |
| `Code/Game/GameFlow.cs` | Триггеры `start_day_{N}` / `end_day_{N}`. |
