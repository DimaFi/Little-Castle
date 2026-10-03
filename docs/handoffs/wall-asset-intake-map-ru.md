# Карта поставки и интеграции — модульная каменная стена

## Актуальные файлы — 2026-10-03

Текущий полный исходник: `E:/Games_Develop/Little-Castle_Assets/Source/Architecture/Wall_Stone_Modular/v004/`.
`Wall_Fortifications.blend` — мастер; `Meshes/` — 11 FBX с тремя LOD;
`Connections.json` — координаты/направления сокетов, anchors и петель;
`Textures/material-bindings.json` — общие материалы; `Preview/` — 8 рендеров;
`QA/optimization.json` — снижение геометрии; `QA/export_validation.json` —
проверка FBX. Весь комплект зарегистрирован в AssetBook art-репозитория.
Материал стены v002 утверждён. Новые башня, ворота и флаг — технически
проверенный кандидат; Unity-импорта и игрового теста пока нет. Ниже сохраняется
исходный план интеграции; упоминания v002 относятся к предыдущей стадии.

Этот документ отвечает на практический вопрос «какой файл куда попадёт и кем
будет использоваться». Он предназначен для передачи Astra 6 вместе с:

- `wall-astra-6-prompt-ru.md` — задание на моделирование;
- `wall-astra-6-production-brief.md` — полный технический контракт;
- `player-banner-asset-contract-ru.md` — отдельный контракт настраиваемого
  флага игрока;
- пользовательским чертежом `Модульная каменная стена_ чертёж поселения.png`.

## Что Astra получает и что от неё требуется

| Вход для Astra | Назначение | Результат Astra |
| --- | --- | --- |
| Чертёж пользователя | Визуальный стиль, пропорции и состав комплекта | Не является текстурой или частью модели |
| `wall-astra-6-prompt-ru.md` | Самодостаточный краткий промпт | Следовать ему без поиска по Unity-проекту |
| `wall-astra-6-production-brief.md` | Оси, pivots, стыки, LOD, UV, коллизия и приёмка | Использовать при любой неоднозначности |
| `Wall_Stone_Modular.blend` | Редактируемый исходник | Передать вместе с экспортом; не добавлять автоматически в Unity/Git |
| 6 FBX | 4 каменных модели + нейтральные banner cloth/bracket | Каменные модели — с LOD0/LOD1/LOD2 и UCX collision proxies; banner cloth — с полной UV0 |
| `Wall_Modular_Preview.png` | Быстрая визуальная проверка комплекта | Передать вместе с набором |

На Astra **не** возлагаются: создание Unity prefab, LODGroup-компонентов,
ScriptableObject, игрового материала, сокетов `WallConnectionSocket`, правка
C# или размещение файлов в проекте.

## Куда интегратор положит результат

Следующие пути являются целевым планом Unity-интеграции после приёмки FBX.
Они не требуют от Astra доступа к репозиторию.

```text
Assets/_Game/
├── Art/Imported/Wall_Stone_Modular/<accepted-version>/Meshes/
│   ├── SM_Wall_Stone_2m_A.fbx
│   ├── SM_Wall_Stone_1m_A.fbx
│   ├── SM_Wall_Stone_Pillar_A.fbx
│   ├── SM_Wall_Stone_End_A.fbx
│   ├── SM_Wall_Banner_Cloth_A.fbx
│   └── SM_Wall_Banner_Bracket_A.fbx
├── Materials/Buildings/Walls/
│   └── M_Wall_Stone.mat                 ← создаётся/назначается в Unity
├── Materials/Buildings/Banners/
│   └── M_PlayerBanner.mat                ← shared, цвет/PNG меняются без клонов
├── Prefabs/Buildings/Walls/
│   ├── PF_Wall_Stone_2m_A.prefab        ← runtime segmentPrefab
│   ├── PF_Wall_Stone_1m_A.prefab        ← будущий short-segment selector
│   ├── PF_Wall_Stone_Pillar_A.prefab    ← startTowerPrefab/repeatTowerPrefab
│   └── PF_Wall_Stone_End_A.prefab       ← endCapPrefab, будущая endpoint-логика
└── Settings/Building/Walls/
    └── StoneWall_A.asset                ← WallPlacementDefinition
```

`Assets/_Game/Models/Ready/` намеренно **не используется**: это каталог
вариантов процедурно сгенерированных объектов мира (`WorldSpawnCatalog`), а
строящаяся игроком стена имеет собственный `WallPlacementDefinition` и
`WallPathPresenter`.

Исходники созданы в отдельной библиотеке:
`E:/Games_Develop/Little-Castle_Assets/Source/Architecture/Wall_Stone_Modular/v002`.
По правилам этой библиотеки binary art хранится через Git LFS. Unity получает
только проверенную копию из `Little-Castle_Assets/Releases/`; live-связи нет.
v001 отклонена за плоскую кладку. В v002 камни объёмные, текущая фактура —
одна новая общая `T_WallStoneSurface_A_BaseColor.png` без швов. Исходные
`CozySettlement/Mat/StoneWall_A_Unity` скопированы без изменения, но теперь
хранятся как референс: их карты кладки не назначать новым блокам.
Рабочая v002 ещё не опубликована в Releases; сначала визуальная приёмка.

## Как каждый готовый prefab будет использоваться

| Prefab | Текущая роль в коде | Статус |
| --- | --- | --- |
| `PF_Wall_Stone_2m_A` | `WallPlacementDefinition.segmentPrefab`; `WallPathPresenter` создаёт его для каждого `WallSectionPose` | Используется сразу |
| `PF_Wall_Stone_Pillar_A` | `startTowerPrefab`; также `repeatTowerPrefab` через заданный интервал | Используется сразу |
| `PF_Wall_Stone_1m_A` | Короткий остаточный/точный сегмент | Геометрия готова, selector будет добавлен позднее |
| `PF_Wall_Stone_End_A` | `endCapPrefab` | Ссылка уже есть в definition, автоматическая установка cap ещё не реализована |

Первый клик игрока создаёт опорный столб. Затем `WallPathLayoutUtility`
выводит детерминированные позы 2м-сегментов из пути, а `WallPathPresenter`
инстанцирует их. На длинной стене та же модель столба появляется повторно по
`automaticTowerSpacing`. Будущие крупные башни и ворота не используют prefab
столба: они будут самостоятельными структурами с дочерними
`WallConnectionSocket`.

## Unity-сборка после получения FBX

Это выполняет интегратор, не Astra:

1. После приёмки и публикации скопировать FBX из Releases в
   `Art/Imported/Wall_Stone_Modular/<accepted-version>/Meshes` в Unity
   `6000.5.5f1`.
2. Собрать из каждого FBX prefab с identity-root, одним `LODGroup`, дочерними
   renderers `LOD0/LOD1/LOD2` и простым collider. LOD2: shadows off; collider
   остаётся только на корне, не на LOD-рендерах.
3. Создать один instancing-enabled `M_Wall_Stone.mat` на существующем шейдере
   `Little Castle/Surface/LC Stylized Lit` и назначить его всем LOD. Использовать
   выбранный пользователем `StoneWall_A_Unity` по `Textures/material-bindings.json`
   в новом наборе моделей. В сцене Blender карты уже назначены.
4. Создать `StoneWall_A.asset` и назначить: `segmentPrefab = 2m`,
   `startTowerPrefab = pillar`, `repeatTowerPrefab = pillar`,
   `endCapPrefab = end`. Оставить `segmentLength = 2.00`,
   `spacingMultiplier = 0.96`, `automaticTowerSpacing = 14.00` до отдельной
   игровой настройки.
5. Выделить `StoneWall_A.asset` и запустить
   `Little Castle → Building → Validate Selected Wall Definition`.
6. Выделить каждый prefab и запустить
   `Little Castle → Assets → Validate Selected Production Model`; вручную
   проверить прямую стену, плавную кривую, Sharp 90° с pillar, LOD и коллизию.

## Код, который уже использует контракт

| Файл | Что делает |
| --- | --- |
| `Assets/_Game/Scripts/Building/Walls/WallPlacementDefinition.cs` | параметры длины, overlap, prefab-ссылки, башни и end cap |
| `Assets/_Game/Scripts/Building/Walls/WallPathLayoutUtility.cs` | Smooth/Sharp траектория, позиции модулей, повторные башни, исключение модулей в clearance |
| `Assets/_Game/Scripts/Building/Walls/WallPathPresenter.cs` | создаёт prefab-сегменты/столбы в preview и confirmed presentation |
| `Assets/_Game/Scripts/Building/Walls/WallConnectionSocket.cs` | будущие точки присоединения независимых башен/ворот |
| `Assets/_Game/Editor/WallModuleContractValidator.cs` | измеряет +Z-длину, pivot, collider и material instancing |
| `Assets/_Game/Editor/ProductionAssetValidator.cs` | проверяет LOD, материалы, коллизию, тени и import performance |

## Оставшаяся работа — намеренно после моделирования

Нужны selector для 1м, автоматическая установка end cap и баннеров. Проверить
стыки с башнями при centre-based suppression и поведение модулей у Sharp:
столб в каждом углу автоматически пока не появляется. Тестовые сборки в
новом `.blend` показывают совместимость геометрии, но не подменяют будущую
проверку настоящего алгоритма в Unity. Подробнее — README_RU.md исходного набора.
