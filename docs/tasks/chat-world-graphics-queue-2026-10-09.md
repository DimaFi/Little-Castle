# Очередь реализации для Chat: карта и графика v1

Исполнитель: Sol 6 High. Одна задача = один небольшой PR. Перед началом читать
AGENTS.md, текущие исходники и отчёт concept-pass: старые issue/checklist не
доказывают, что задача ещё открыта. Не повторять уже реализованные PR26–30,
исправления routing endpoints/failed desired links, opt-in carve envelope,
bounded natural river fallback и начальную concept-scene integration.

Опубликовано: Q01 — [существующая bootstrap issue5](https://github.com/DimaFi/Little-Castle/issues/5#issuecomment-6081194210).
Q02–Q17 — GitHub issues31–46 соответственно; первая
[Q02](https://github.com/DimaFi/Little-Castle/issues/31), последняя
[Q17](https://github.com/DimaFi/Little-Castle/issues/46).
Q18 — [carving bottleneck](https://github.com/DimaFi/Little-Castle/issues/47).

База: актуальная ветка concept-world-pass после desktop проверки. Не менять
Main, общие production материалы, FBX/Blender releases или чужие файлы. Каждый
исполнитель владеет только перечисленными файлами и своими новыми тестами/meta
и report. Публичные API — additive; изменение контрактов только с согласованием
главного агента. Не закрывать визуальную/Player/FPS приёмку по EditMode tests.
Каждый PR: точный diff, тесты действительно запущенные/не запущенные, ограничения,
команда воспроизведения. Не запускать большие seed/size матрицы в каждом PR.

## Q01 — Кооперативный macro bootstrap без зависания UI

Владение: Scripts/World/Macro/MacroWorldPlanner.cs; новый MacroPlanningSession.cs;
новые MacroPlanningSessionTests. Зависимости: принятые source/route fixes.
Ввести пошаговую сессию с ограниченным бюджетом работы и отменой; старый
синхронный Build должен дать тот же итог. Unity объекты не переносить в Task.Run.
Проверить одинаковые IDs/roads/bridges при разных размерах шага, отмену/повтор.
Не менять WorldStreamer: передать главному агенту точный API для интеграции.

## Q02 — Прогресс старта и безопасная отмена

Владение: Streaming/WorldStartupWarmupController.cs; новый bootstrap adapter;
новые StartupCancellationTests. После Q01 и согласования интеграционного API.
Показывать стадии macro/data/visible-ready, не фиктивный общий процент. Камера
не принимает игровой ввод до usable readiness; отмена освобождает частичный мир.
Изменения WorldStreamer.cs — только после отдельной передачи владения root.

## Q03 — Ограничить подготовку пар для connectivity repair

Владение: Macro/RealizedRoadConnectivityRepair.cs; новые RepairCandidateBudgetTests.
Сохранить nearest/stable-ID порядок и успешные пути на маленьких планах, но
не собирать безлимитную O(N²) таблицу на больших. Явный верхний budget и отчёт
об исчерпании, без скрытия disconnected/min-bridge errors. Синтетические тесты
100/1000 features, deterministic tie-break, budget0, плохие materialized roads.

## Q04 — Единая геометрия river envelope

Владение: новый Core/RiverEnvelopeUtility.cs; Rendering/ChunkTerrainVertexMasks.cs;
новые RiverEnvelopeTests. После source/bend pass. Перенести только вычисление
distance/interpolation/envelope из wetness masks в pure helper, сохранить данные
побитово/в заданной float epsilon. Предложить API остальным владельцам, не править
carving/surface/water одновременно. Острый изгиб, конfluence, repeated points,
negative coords, одинаковые значения по обе стороны границы.

## Q05 — Вода на стыках и у мостового stamp

Владение: Rendering/RiverWaterMeshBuilder.cs, RiverWaterPresenter.cs;
новые RiverWaterJoinTests. После Q04 API. Убрать трещины/дубли на border/bend/
confluence; держать fixed waterline base−0.85, scale1 мост не растягивать.
Никакой fake river/новой gameplay hydrology. Mesh tests + близкий/дальний GPU
кадр; явно отделить mesh PASS от визуального pending.

## Q06 — Спокойный береговой shader воды

Владение: новый Shaders/Terrain/LC_RiverWater.shader/meta; новый isolated
Concept_RiverWater.mat; новые WaterMaterialContractTests. После Q05, shader
файлы не пересекаются с terrain/foliage задачами. Один дешёвый прозрачный слой,
плавный flow и shoreline contrast без дорогой refraction/depth fullscreen stack.
Не менять bridge water material, Main или pipeline. Сравнить день/сумерки/ночь.

## Q07 — SurfaceKind покрытия без дорогого набора слоёв

Владение: Shaders/Terrain/LC_Terrain.shader; isolated Concept_Terrain_Masked.mat;
новые TerrainLayerContractTests. Только после Q04 и передачи shader ownership.
Сначала 3–4 читаемых слоя: луг/земля/тропа/камень, woodland/mud дешёвым оттенком.
Существующие defaults/Main обязаны визуально сохраняться. Проверить authored
BaseColor/sRGB/Normal/mips, крупный масштаб/тайлинг; не создавать текстуры кодом.
Матрица day/sunset/night near/far; число texture samples указать в отчёте.

## Q08 — Каменные уступы по scarp bands

Владение: новый Generation/Stages/ScarpDecorationStage.cs; новый concept stage
asset; новые ScarpDecorationTests. Не менять landform heights или deposits.
Детерминированные декоративные placements на реальных склонах, только approved
rock archetypes, bounded density, исключить дороги/bridge/site/entrances.
Проверить negative borders, стабильность IDs при eviction, отсутствие дублей.
Root отдельно подключает stage и prefab catalog; без полномапных GameObjects.

## Q09 — Береговые растения и чистые проходы

Владение: новый Generation/Stages/RiverbankDecorationStage.cs; новый isolated
stage asset; новые RiverbankDecorationTests. После Q04/Q05. Камыши/кусты/редкие
цветы только по влажности/дистанции/склону, без растений в воде и на переходах.
Не включать несуществующие production assets: data archetypes отдельно,
intake отдельно. Стабильные IDs, border/revisit, декор не дублирует bridge dressing.

## Q10 — Отпечатки зданий и входы как данные

Владение: новый Core/BuildingFootprintDefinition.cs; новый pure
Core/BuildingFootprintValidator.cs; новые FootprintValidatorTests.
После начального village presenter. Центр+углы недостаточны для больших зданий:
bounded сетка support samples, height spread/slope, oriented entrance clearance,
water/roads/buildability masks. Явные причины отказа, без flatten всего мира.
Prefab/rendering независимы от data validator. Root подключает утверждённые
footprint metadata и решает compatibility/generation version.

## Q11 — Village layout с подходами, без случайной physics-зависимости

Владение: новый Macro/VillageLayoutPlanner.cs; новый Core/VillageLayoutData.cs;
новые VillageLayoutTests. После Q10.3–5 домов/колодец/малый двор, non-overlap,
входы к подходам, bounded deterministic alternatives. Не создавать структуры
по времени прибытия collider chunks. Сохранить logical village stableId.
Согласовать API с root; существующий ConceptVillageFootprintPresenter не менять
без переуступки. Unit navigation ещё не считается автоматически готовой.

## Q12 — CPU/GPU forest presentation budget

Владение: новый Rendering/ForestPresentationBudget.cs и новый presenter adapter;
новые ForestBudgetTests. После реального Oak preflight. Данные spawn IDs остаются
авторитетными; presentation/colliders только локально, controlled near/far density.
Не заменять FBX/LC_Foliage, не писать runtime simplification. Integration в
ChunkSpawnPresenter/WorldStreamer делает root после утверждения API. Документировать
pool/instancing eligibility и измерения, не заявлять FPS по синтетике.

## Q13 — Свет поселения и дешёвый ночной силуэт

Владение: новый concept lighting prefab и конфиг; новые NightVillageBudgetTests.
После Q11. Использовать существующий night-light budget/Glow/Pool/LOD hooks,
окна и проходы читаемы, far LOD без realtime Point/Spot. Не повышать глобальный
лимит ламп. Не трогать sky/shared production materials. Day/night GPU proof.

## Q14 — Quality presets с прозрачными trade-offs

Владение: новый Rendering/ConceptQualitySettings.cs; новые QualityPresetTests;
docs/reports/concept-quality-budget.md. После Q12/Q13. Low/Medium/High только
presentation: shadow distance, grass density, local light/LOD budgets. Ни один
preset не меняет gameplay, rivers/roads/resources/start fairness или IDs.
Runtime integration в streamer/camera отдельно у root. Нужны реальные замеры
Player CPU/GPU/frame spikes, не выдумывать целевую слабую машину.

## Q15 — Маленький regression suite для map presets

Владение: новый Editor/ConceptMapRegressionAudit.cs; новые
Tests/Editor/ConceptMapRegressionTests.cs; report. Использовать сохранённые
настройки, не включать скрытые клоны флагов. Бюджет: source-only4seeds, один
small macro run; большие1024/3072/6144 и2/8/16players только отдельным явным
запуском. River failure/graph/geometric paths/start exits/IDs/seams/eviction
отдельными полями. Balance и визуал не объявлять по macro connectivity.

## Q16 — Версии мира, save/revisit и ограниченный кеш

Владение: новый Core/WorldGenerationIdentity.cs и validation adapter;
новые GenerationIdentityTests. После утверждения настроек root. Settings hash
и generation-version в compact identity, несовместимый save отклонять понятно.
Не менять схемы existing save/delta без согласования. Одинаковые seed/version
после reload/eviction дают те же IDs; сохранённые runtime removals/buildings
не воскресают. Resource policy остаётся неизменной.

## Q17 — Бюджет дома и реальный LOD preflight

Владение Chat: новый Editor/HouseAssetBudgetAudit.cs; новые HouseBudgetTests;
docs/reports/house-budget-intake.md. Desktop Prepare измерил house143672/
55764/22794triangles: validator0errors не означает приемлемый бюджет.
Сначала измерить реальную геометрию, draw calls/материалы, screen-size и close/far
силуэт, сформулировать approved art intake. Не менять FBX/Blender/assets release
без отдельного авторского задания и root ownership. Экспорт сниженных authored
LOD затем делает desktop art-agent; не писать runtime simplification и не
скрывать проблему агрессивным исчезновением домов. Это отдельный art gate v1.

## Q18 — Локальные river segments для carving

Владение: Generation/Stages/RiverTerrainCarvingStage.cs; concept carving asset;
новые RiverCarveBroadPhaseTests. Последний настоящий Play после оптимизации
Surface показал новый worst stage:02_RiverTerrainCarving77.224ms.
Default-off chunk-local expanded-segment AABB candidates, opt-in only concept.
Сохранить точные высоты/профиль bank feather/depth и max-envelope при изгибах,
без nearest-segment regression и без изменений FixedBridgeTerrainStage.
Проверить whole-height-array parity, отрицательные чанки/общие края,
острые широкие изгибы, нулевые сегменты и profile depth. Не добавлять persistent
полномапный индекс, Task.Run или height weld. Старый nearest-only Main неизменен.
После Q04 согласовать helper API; Unity timing сравнивает root, не CI threshold.

## Граф и параллелизм

Q01→Q02; Q04→Q05→Q06; Q04→Q07/Q08/Q09; Q10→Q11→Q13;
Q12+Q13→Q14; интеграция всех→Q15/Q16/final desktop Player acceptance.
Можно параллельно Q01/Q03/Q10: разные файлы. Нельзя одновременно два владельца
LC_Terrain, WorldStreamer, catalog или scene. Не запускать генераторы/Unity,
пока другой владелец редактирует source в том же checkout.

Final acceptance делает главный агент: настоящий Player, один hardware baseline,
маршрут camera walk/revisit, close/far foliage/shadows, day/sunset/night,
river/confluence/bridge/building contacts, CPU/GPU/memory и проверка UX.
Water mills сейчас вне scope. Player-colored персонажи/флаги и нейтральный
белый торговец — отдельная будущая персонажная ветка, не перекрашивать ресурсы.
