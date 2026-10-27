# Единая система инструментов карты — этап 1

Дата: 27.10.2026. Unity 6000.5.5f1, URP 17.5.0 в локальном стенде.
Рабочая копия: `Little-Castle_BrushFoundation_20261027`.

## Аудит до изменений

GitHub обновлён через `git fetch origin`; открытые PR и список 43 веток также
проверены через GitHub connector. `origin/main` = `b337687`, исходная ветка
concept = `e6a02df`. Актуальная ручная карта отсутствует в main и remote
lighting baseline. Источник инструментов — `Little-Castle_URP_20261024`;
снимок существует в локальной ветке `codex/river-bank-generation-20261026`.
Там `047ad1e` содержит исходную карту, `6a7d58c` — дальнейшую интеграцию воды.
Эта работа использует исходники URP-копии, не переносит новую воду/берега.

`d2094da` отдельно фиксирует уже существовавшие исходники редактора и их
зависимости перед рефакторингом. Это prerequisite, а не результат новой разработки.
Локальные art fixtures/сцены, купленные библиотеки, бинарные модели и настройки
исследовательского освещения не публикуются этим этапом.

| Система | До изменения | Общая/специальная часть после изменения |
|---|---|---|
| Дорожка / луг / удалить траву | `SurfacePaintStroke`, растеризация маски, собственный ввод в двух UI | Три `MapSurfaceTool`, общие ввод/штрих/настройки; исходная растеризация и ordered commands сохранены |
| Поднять / опустить | `BeginHeightStroke`, плавное затухание, ограничения карты, отдельные таймеры | Два `MapHeightTool`; та же сетка/дельты/защищённые области, общая сила и настраиваемая мягкость |
| Сглаживание / восстановление исходного рельефа | Отдельных инструментов не обнаружено; smooth falloff не является сглаживанием высот | Новые алгоритмы не добавлялись. Реестр допускает дополнительные модули |
| Цветы / камни / подлесок / лесная мелочь | Профили `MeadowScatterProfile`, детерминированный scatter | Параметры поверхностного инструмента; специальные каталог/LOD/хеширование остаются в scatter |
| Роща / пак деревьев | Конечные stable instance records, собственный mouse loop | `MapGroveTool`, общие штрих/радиус/сила/мягкость; размещение и проверки сохраняются в session |
| Дерево: посадить / выбрать / передвинуть | Клики и отдельная обработка луча | `MapClickTool`; один ввод и выбор инструмента. Pick по кроне остаётся в tree presentation |
| Дерево: повернуть / масштаб / удалить | Кнопки вызывают операции session | Сохранены специализированные команды; отдельного mouse handler нет |
| Река / броды / спуски / скальные секции | `MapRiverToolState`, отдельный runtime Update и SceneGUI | Зарегистрированный ControlPoints tool. Draft и явный Apply остаются специализированными |
| Стены / заборы / строительство | Игровые `WallPlacementController` и path/runtime-state | Не являются кистями ручной карты. Сохранены: стоимость/authority и игровой UX относятся к отдельному этапу |
| Процедурный мир | MacroWorldPlan → stages → chunk data → presentation | Не заменён и не перегенерируется от ввода кисти |

До изменения UI дублировали: mouse down/drag/up, 15 Hz таймеры, радиус,
выбор поверхности, bounds-проверки, завершение штриха, переключение режимов,
контуры. Runtime raycast имел предел 300 м, SceneView — 10 000 м.
Контур SceneView был диском в одной плоскости и не повторял рельеф.
Meadow выключал стратегическую камеру при включении рисования.

`MapEditSession` уже содержал права, локальные индексы, транзакции, Undo/Redo,
save v5, stable IDs, plain map data, `MapRegionChange` и negative coordinates.
Вместо второй модели операций сохранён этот контракт.
Основные зависимости: `WorldSaveState.mapEdits`, chunk coordinate/32 м регионы,
sparse height lattice/16×16 samples, Surface CPU/GPU cache, terrain/tree/river views.
Рабочие правила `AGENTS.md`, план координации, world generation, foliage и
material contracts прочитаны. Меши/материалы/ветер не перерабатывались.

## Соседние ветки и PR

- [PR #79](https://github.com/DimaFi/Little-Castle/pull/79),
  `feature/biome-thin-snow-foundation-2026-10-26`, head `1626fcc`:
  изучены реальные `BiomeSnowWorldEditor.cs` и `EnvironmentFields.cs`, не только описание.
  Sparse XZ поля и отдельные theme IDs совместимы с направлением этого API.
  Но там собственные input/falloff/preview и полные Undo snapshots, отдельный
  JSON и global RGBA mask. **Интеграция сознательно отложена по заданию.**
  В будущем: адаптер `IMapTool`, региональные операции/дельты session, tiled
  invalidation, миграция существующих snapshots, отключение старого mouse loop.
  Не переносить снеговой shader и не смешивать themes с `World.BiomeKind`.
- [PR #66](https://github.com/DimaFi/Little-Castle/pull/66) и issue #76:
  направление URP. Remote main ещё не отражает весь проверенный локальный URP.
- Ветки `codex/lighting-meadow-baseline-2026-10-26`, `lighting-spatial-gi`,
  `lighting-gi-cache-stage1`, `lighting-gi-kernels-stage1b`,
  `lighting-gi-priority-stage1c`, `lighting-gi-temporal-stage2a-2026-10-27`:
  отдельное освещение, общие шейдеры и GI не менялись.
- PR #65/#63/#64: процедурные берега/стыки воды/material, отдельные dependencies;
  PR #28/#29: streamed terrain masks/normals; #30: scatter; #57: carving.
  Не заменялись и не объединялись с ручными river drafts.
- Локальная незавершённая ветка рек: новый bank generation остаётся её владельцу.
  Точки/Apply исходной реки подключены к общему API без замены bank algorithms.

## Новые общие компоненты

`MapTools.cs` (runtime, без UnityEditor):

- `IMapTool`: ID/категория, capabilities, Stroke/Click/ControlPoints,
  Begin/Apply/End, требования к поверхности, повтор на месте, предел радиуса.
- `MapToolRegistry`: проверенная регистрация по стабильному ID; зарегистрированы
  существующие механики, а не пустые будущие вкладки.
- `MapBrushSettings`: мировые метры, strength, softness. При начале штриха
  делается одна маленькая копия настроек. Движение мыши не копирует карту.
- `MapBrushPreset.cs`: настоящий ScriptableObject с собственным MonoScript;
  сохранение/применение общих настроек в существующем окне.
- `MapToolController`: единственный lifecycle для обоих UI. Модули не читают
  Input/Event, не создают камеры и не имеют собственного Update.
- `MapBrushFalloff`: общий falloff и контур по высотам.

`MapSurfaceQuery`: slab clipping + traversal ячеек + точное пересечение текущих
треугольников heightfield. Запрос ограничен конечными bounds карты и использует
актуальные pending высоты, поэтому cursor не ждёт cooking коллайдера.
Луч переносится к envelope карты до triangle math, чтобы сохранить точность
с далёкой камеры. Это heightfield surface contract; будущие пещеры/нависающие
поверхности требуют другого surface provider.

`MapEditSession.Notifications`: дополнительный batch-scope над существующим
`Changed`. Внутри scope события режутся по 32 м регионам, повторные события
одного региона объединяются, kinds складываются, committed событие заменяет
preview-флаг. Разнесённые регионы не объединяются в одну огромную территорию.
Commit/Undo/Redo/Cancel из controller выводят dirty regions из отдельных
изменённых samples/segments/instances, сохраняя узкий коридор диагонального
штриха вместо его огромного bounding rectangle. Специальные river-команды
сохраняют существующий bounds contract.
Подписчики terrain/trees/scatter/CPU/GPU mask уже получают эти события.
Прямые вызовы session сохраняют прежнюю синхронную семантику.
Полная карта/освещение/nav не регенерируются. Это готовая точка подписки для
дополнительных кешей, а не новая система генерации.

## Ввод, камера и границы нагрузки

SceneView использует существующее окно `Little Castle → Map → World Editor`.
Registry формирует выбор инструментов; галерея деревьев и river drafts сохранены.
Alt/ПКМ/СКМ/колесо остаются навигацией. Frame Map использует SceneView.Frame.
Радиус никак не вычисляется из pixels, FOV или zoom.
Во время штриха вне SceneView он завершается, Esc откатывает текущую операцию.
Ctrl/Cmd+Z отменяет активный штрих либо последнюю завершённую команду.

В Meadow единственный ввод находится в `MeadowSurfacePainter.Tools`.
У `MapRiverRuntimeTool` больше нет самостоятельного Update с чтением мыши.
Обычная игровая камера сохраняет defaults. Только Meadow вызывает opt-in
`ConfigureAuthoringRange`; рисование не отключает её pan/zoom.

Обычный Tick ограничен 15 Hz, 32 dabs и 4 мс sampling-работы. Лимит времени
проверяется между dabs; уведомления/mesh upload имеют собственную стоимость. Spacing = max(0.15 м, radius/4).
Длинный drag дорабатывается следующими ticks после MouseUp, без потери конца.
Явное сохранение/смена инструмента завершает оставшийся путь перед commit.
Радиус surface/grove до 128 м; terrain до min(128,max(32,64×height spacing)), чтобы
ограничить число grid nodes одного dab. Это текущий бюджет, не обещание
неограниченной кисти на всю карту одним синхронным проходом.
В preview один reused массив контуров, без GameObject на dab/растение.
Настоящего streamer большой ручной карты этот этап не добавляет.

## Совместимость данных

Схема map/save и stable object IDs сохранены. В `SurfacePaintStroke` добавлен
optional `brushSoftness`: 0 означает прежний край 62%/38%; новое значение
равно softness+.001. Это отличает старый формат от действительно жёсткого края.
Сериализованные нулевые поля исключаются из base fingerprint для совместимости
с ранее сохранёнными v2/v3/v4/v5 базами. Иные ID/revision по-прежнему отвергаются.
Старые signatures height/tree brush сохранены optional параметрами с прежними
defaults. Algorithms поднятия, placement validation и ordered masks переиспользованы.
Собственная история кисти не создавалась: один завершённый штрих — одна session command.

## Готовность к следующим этапам

Биомы/зима могут использовать selection/settings/input/query/preview и локальные
events. Их authoritative field deltas, ограничения и версия сохранения ещё
потребуют отдельной реализации. Игровые инструменты могут напрямую вызывать
runtime session operations; стоимость, multiplayer authority и network commands
не реализованы. Полный редактор игрока не создан.

Проверки и оставшиеся ограничения: `docs/reports/unified-map-tools-20261027.md`.
Краткая инструкция: `docs/workflows/add-map-brush-ru.md`.
