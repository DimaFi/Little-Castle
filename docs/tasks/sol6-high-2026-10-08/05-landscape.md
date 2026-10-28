# [Sol 6 High][P2] Довести concept-рельеф и поверхность до читаемого игрового вида

Исполнитель: **Sol 6 / High** (`gpt-6-sol`, `reasoning_effort=high`). Это параметры для ручного запуска, Issue сам агента не запускает.

Репозиторий: `DimaFi/Little-Castle`. База: ветка **`current-unity-fix`**, не старый `main`; foundation commit `edb9eae5949966f8a0b7a71a4f0354ee7d427a8e`. Сначала прочитать `AGENTS.md`, `docs/handoffs/sol6-high-mobile-2026-10-08.md`, `docs/reports/bridge-world-v002-2026-10-08.md` и архитектуру своего подсистемного участка.

Общие ограничения: конечная карта, детерминизм/negative chunk seams, plain authoritative data отдельно от presentation, Built-in renderer, без watermills/URP/HDRP/зеркального выравнивания ресурсов. Не редактировать чужие файлы без согласованной переуступки. Перед изменениями проверить актуальные commits и работающих исполнителей. При отсутствии Unity/Blender/LFS не объявлять проверки PASS: зафиксировать точную команду и необходимость проверки на ПК.

## Результат
Луга, холмы, выбранные каменные террасы и читаемые долины по направлению двух референсов — не буквальная копия деревни. Heightfield не выдавать за законченные rock outcrops/overhangs.

## Владение файлами
`Assets/_Game/Scripts/World/Generation/{StylizedLandformSettings,StylizedLandformSampler}.cs`;
`Generation/Stages/{LayeredTerrainStage,TerrainSurfaceStage}.cs`;
`Assets/_Game/Settings/World/ConceptWorld_v001/Stages/{01_LayeredTerrain,03_TerrainClassification,07_TerrainSurface}.asset`;
`Assets/_Game/Tests/Editor/StylizedLandformTests.cs`;
новый `Assets/_Game/Editor/ConceptLandscapePreview.cs`;
`Assets/_Game/Scenes/ConceptBridgeTest.unity`;
`docs/architecture/stylized-landforms.md`, `docs/reports/concept-landscape-review.md`.
Публичные контракты: opt-in sampler/settings и presentation-facing masks; сохранить exact legacy branch и seed/version semantics. Main*/macro planner/river settings/shared foliage shaders не принадлежат задаче.

## Зависимости
После crossing presentation: она отдаёт scene ownership. До этого можно только анализ/план без пересекающихся записей. Изменение existing tree/grass meshes/shaders требует AGENTS foliage preflight и переуступки.

## Приёмка
- [ ] Связные полезные площади, естественная неровность, редкие высокие уступы вместо шумной всей карты.
- [ ] Несколько seeds/map sizes; отсутствующие highlands в малом окне не маскировать ложной гарантией.
- [ ] Plains/height/slope masks для травы и камня без texture noise на каждом месте.
- [ ] Никакой готовой автоматически построенной player village, watermills или искусственного равенства ресурсов.
- [ ] Neg seams, finiteness, same/different seed, legacy regression, slope/distribution metrics.
- [ ] Actual Unity gameplay/close/far views; authored cliff/outcrop backlog явно отделён от сделанного.
- [ ] Отчёт, тесты, API/ownership и коммит.
