# [Sol 6 High][P1] Подключить Bridge_Stone_A v002 в отдельный Unity concept-профиль

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Воспроизводимо импортировать конкретный reviewed release из `DimaFi/Little-Castle_Assets` (ветка main), собрать Unity prefab с authored LODs/near collision и зарегистрировать `ENV_Bridge_Stone_A` только в concept catalog. Не подменять модель кубом и не менять текущие Main catalogs/scene автоматически.

## Владение файлами
Новые `Assets/_Game/Art/Imported/Bridge_Stone_A/v002/**`, `Assets/_Game/Models/Concept/Bridge_Stone_A/**`;
новый `Assets/_Game/Editor/BridgeStoneAssetIntegration.cs`;
`Assets/_Game/Settings/World/ConceptWorld_v001/Concept_MainWorldSpawnCatalog.asset`;
новые `Assets/_Game/Scenes/ConceptBridgeTest.unity`, `Assets/_Game/Tests/Editor/BridgeStonePrefabTests.cs`;
при необходимости узкие LFS/ignore правила `.gitattributes`, `.gitignore`;
`docs/reports/bridge-v002-unity-integration.md`. Соответствующие .meta принадлежат задаче.
Публичный контракт: `WorldSpawnCatalog` existing mapping, fixed asset ID, units/pivot/scale 1, shared materials и recolorable faction cloth. Не менять data planner/profile classes.

## Зависимости
После baseline; параллельно с bridge-aware routing. Source release: `Releases/Bridge_Stone_A/v002`, immutable. Art commit: `5075ef3`; последующие exact-byte fixes брать с актуального main. Проверить LFS payload и SHA manifest.

## Приёмка
- [ ] Реальный imported FBX, authored LOD0/1/2, 286-tri static near collider, far shadows/probes/colliders выключены.
- [ ] Флаги на входных cap справа по ходу движения; material tint без material clones.
- [ ] В runtime prefab НЕТ наложенного terrain reference и второй water surface.
- [ ] Foliage mesh/shader preflight; production validator; close/far/day/night Unity screenshots.
- [ ] Correct root elevation/yaw/scale and streamed instance cleanup; no stretch by requiredSpan.
- [ ] Метаданные/материалы/текстуры доступны после чистого checkout, не из ignored local-only Ready.
- [ ] Отчёт и фактические Unity результаты; без Unity — оставить visual acceptance pending.
