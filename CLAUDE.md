# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

GameJam миниигра «Is this a mimic?» — пазл-инвентарь в духе DREDGE. Игрок управляет мимиком: лут приключенцев падает в правую сетку (6×10), нужно перетащить и уложить его в левую сетку мимика (14×8), переваривать предметы за «желудочный сок», получать золото для дневного квота, использовать свойства «ЕСЛИ РЯДОМ С [предмет]» для модификаторов цены/стоимости. ТЗ — в `/Users/cheremisov/Downloads/Is this a mimic_ _ Notion.pdf` (вне репо).

Стек: **Unity 6000.3.16f1** (Unity 6), URP 2D, Input System.

## ⚠️ Это game jam

- **Архитектуры не строим.** Простая реализация на префабах + ScriptableObject/CSV/JSON конфигах.
- **Скорость важнее чистоты.** Хардкод, синглтоны, `FindObjectOfType` — допустимо если экономит время.
- Никаких asmdef-сборок, паттернов, DI-контейнеров, абстракций «на будущее». Будущего у этого проекта нет — есть дедлайн джема.

## Repository Layout

Unity-проект лежит в подпапке **`Game/`**, не в корне репозитория.

```
.                         # репо-корень (git)
├── .gitignore            # Unity excludes + macOS + IDE
├── .gitattributes        # LFS + unityyamlmerge + line endings
├── CLAUDE.md
└── Game/                 # ← Unity project root (открывать в Unity Hub)
    ├── Assets/
    │   ├── Code/         # весь C# код
    │   ├── Art/          # спрайты, материалы, анимации
    │   ├── Audio/        # звуки, музыка
    │   ├── Prefabs/      # префабы
    │   ├── Configs/      # ScriptableObject / CSV / JSON
    │   ├── UI/           # UI префабы, иконки, шрифты
    │   └── Scenes/
    ├── Packages/
    └── ProjectSettings/
```

## Working with this repo

**Открыть проект:** Unity Hub → Add → выбрать папку `Game/` (не корень репо).

**После `git clone`:**
1. `git lfs install` — обязательно, бинарные ассеты лежат в LFS.
2. (Опционально) зарегистрировать `unityyamlmerge` driver для нормального merge YAML-ассетов:
   ```
   git config merge.unityyamlmerge.driver \
     "/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/Tools/UnityYAMLMerge merge -p %O %B %A %A"
   ```

**Build / Run / Tests:** через Unity Editor (Play mode, File → Build Profiles). CI/CLI нет.

## Unity packages of note

- `com.unity.render-pipelines.universal` — URP 2D
- `com.unity.inputsystem` — новый Input System
- `com.coplaydev.unity-mcp` — **MCP for Unity** (OSS bridge для управления Editor извне через MCP-клиенты типа Claude Code). При первом открытии Unity подтянет с git URL.

## MCP integration

После установки `com.coplaydev.unity-mcp` (см. выше) **в Unity Editor**: `Window → MCP for Unity → Configure All Detected Clients` — плагин сам зарегистрирует MCP server в Claude Code и других обнаруженных клиентах. После этого перезапусти Claude Code чтобы tools `mcp__unityMCP__*` подгрузились в сессию.

Не путать с `com.coplaydev.coplay` (`unity-plugin`) — это другой продукт (Coplay AI ассистент внутри Editor), он не нужен.

## Conventions

- **`.meta` файлы — версионировать всегда** (Unity требует, чтобы каждый ассет имел meta с GUID).
- Бинарные ассеты автоматически идут через LFS по расширению (см. `.gitattributes`).
- Line endings нормализуются в LF; исключения — `.bat`/`.cmd`/`.ps1` (CRLF).
