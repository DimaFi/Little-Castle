# Task02 — аудит архитектуры биомов и тонкого снега

Дата: 27.10.2026. Итог: PR79 даёт полезный отдельный raster/theme core, но не готов к прямой интеграции. Обычные отрицательные координаты и tile seams проходят тесты; найдены ошибки invalidation, освобождения памяти, малых weather increments и входной валидации. Полноценная интеграция биомов/снега в производственный проект **запрещена до отдельного одобрения**. В этом аудите production source не менялся, новые runtime/editor модули не реализовывались.

## 1. Канонический проект и исследованные кандидаты

По последнему явному запросу пользователя канонический проект — `E:/Games_Develop/Little-Castle`. Его ProjectVersion и manifest повторно прочитаны: Unity **6000.5.5f1**, URP **17.5.0**, test-framework **1.7.0**, Forward — согласованный rendering baseline. Нельзя выбирать другой projectPath только потому, что там удобнее существующая сцена.

| Кандидат | Роль и доказанная версия | Как используется в Task02 |
|---|---|---|
| `E:/Games_Develop/Little-Castle` | Канонический проект. Root функционально объединяет актуальную BrushFoundation `4cc6382` и lighting `42f317c` | Точка будущей согласованной интеграции. Итоги её визуальных/интеграционных проверок root добавляет отдельно; этот отчёт их не подменяет |
| `E:/Games_Develop/Little-Castle_BrushFoundation_20261027` | Unity6000.5.5f1/URP17.5, commit `4cc63820347bab7da3b944fe76875eca00c8f107` | Read-only источник общей системы кистей и актуальных terrain/snow/triplanar функций |
| `E:/Games_Develop/Little-Castle_LightingIntegration_20261025` | Unity6000.5.5f1/URP17.5, lighting baseline `42f317c` по координации root | Read-only сравнение lighting и PackageCache; целиком старый Terrain shader переносить нельзя |
| `E:/Games_Develop/Little-Castle_URP_20261024` | Unity6000.5.5f1/URP17.5, исторический исходник ручного редактора | Архивный кандидат/контекст; не новый canonical project |
| `E:/Games_Develop/Little-Castle_RiverBanks_20261026` | Unity6000.5.5f1/URP17.5, `6a7d58c467fc6f292b359ad14b7d614df41eb6b1` в исходном inventory | Отдельная работа владельца рек; не запускалась/не менялась |
| `E:/Games_Develop/Little-Castle_cliffkit` | Unity6000.5.5f1, URP отсутствует в исходном manifest inventory | Не подходящий baseline для этой URP проверки |
| `E:/Games_Develop/Research/EnvironmentAudit_20261027/Probe` | Минимальный Unity6000.5.5f1/URP17.5 стенд | Только изолированные EditMode CPU tests; не продуктовый проект |

`local-project-inventory.json` — исторический снимок ДО объединения root: старый HEAD и status канонической папки там не являются текущим финальным состоянием. Git commit номера не доказывают состояние незакоммиченных локальных assets. Активные сцены, кадры и результаты функционального объединения должны браться из итогов root.

Исходник [PR79](https://github.com/DimaFi/Little-Castle/pull/79): `origin/feature/biome-thin-snow-foundation-2026-10-26`, точный SHA **1626fcc33c6ba1fb381e070988851265d6e76240**. Через git show прочитаны все 9 source/test файлов: EnvironmentFields.cs, BiomeSnowWorld.cs, BiomeThemeProfile.cs, BiomeSnowfallParticleDriver.cs, SnowMaskTextureBridge.cs, BiomeSnowWorldEditor.cs, LC_ThinSnowURP.shader, LC_ThinSnowShaderGraph.hlsl, EnvironmentBiomeSnowTests.cs. Прочитаны оба handoff и biome-map-authoring-editor.md. Для локальной архитектуры прочитаны MapTools.cs, MapEditSession.Notifications.cs, MapSurfaceQuery.cs и unified-map-tools.md. Полные исходные выгрузки находятся в `raw/`.

## 2. Разделение данных

**BiomeKind — базовая экология.** `Assets/_Game/Scripts/World/Core/BiomeKind.cs`, `Generation/Stages/BiomeClassificationStage.cs`: результат детерминированной генерации, например типы почвы/влажности/растительности. Перекраска темы не должна переписывать seed, исходную экологию или запускать новую генерацию всей карты.

**Mutable theme — редактируемый художественный/фракционный слой.** PR79 использует отдельные stable integer IDs: summer0, enchanted1, elven2, winter3, расширяемые profile assets. Это overlay над экологией. Profile metadata пока содержит ID/name/color/description; полноценные material/vegetation/weather rules ещё отсутствуют. Palette index, имя GameObject и transient asset reference не должны становиться save ID.

**Influence — численные веса темы в world XZ.** Не отдельная физическая сущность и не цвет пикселя: sparse cell-centred grid, bilinear samples, конкурирующие слои и implicit summer. Нужно утвердить invariant сумм весов, разрешённые map bounds, grid resolution и семантику blur. Будущее игровое magic influence может вызывать тот же data core через authority-validated команды; developer UI не становится gameplay authority.

**Weather и snow cover — отдельное состояние.** Winter influence определяет климатическую реакцию, интенсивность snowfall управляет осадками, накопленная coverage остаётся после прекращения снегопада и тает независимо. Авторский instant snow preview, simulation coverage и временные footprints должны иметь разные явные контракты. Частицы и shader mask — presentation, не авторитетное сохранение мира. Update/Time.deltaTime не заменяет проектный world-clock.

## 3. Что сохранять при будущем объединении

| Система | Local | PR79 | Сохранить и почему |
|---|---|---|---|
| **Кисти** | IMapTool/registry/controller, общие radius/strength/softness/presets, bounded15Hz/32dabs/4ms sampling; pending heightfield query; session Undo/Cancel; long drag queue | Собственный Inspector/SceneGUI input, Physic.Raycast любого collider, falloff/preview и JSON Undo; cap512 dabs | Local controller/query/input и presets. Добавить IMapTool адаптер для биома, пользоваться session transactions/events. Это сохраняет одну систему ввода и точное попадание по текущему рельефу. Старый biome mouse loop не подключать параллельно |
| **Биомы** | Ecological BiomeKind, stage pipeline, finite map и stable entities | Отдельные ID/profile, sparse world grid, cell-centred sampling, atomic field restore | Разделение ecology/theme, стабильные IDs и world sampling. Field core доработать, не заменять генерацию или persistent entities |
| **Шейдеры** | Shared LC globals/lighting, foliage wind+shadows, Terrain triplanar, snow normal/AO/flattening/path/wetness | Отдельный Lambert static opaque prototype и albedo HLSL helper | Local shaders и их текущие функции. Позже адаптировать snow inputs в общий материал; shader целиком из PR79 потеряет lighting/wind/art refinements |
| **Снег** | Уже есть authored Terrain coverage `_SnowControl/_SnowMapBounds`, настройки Normal/AO/DetailStrength/SurfaceFlattening; локальные study assets | CPU accumulate/melt, camera particles, global `_LC_SnowField` RGBA32 mask | Не путать существующий authored rendering с готовым runtime snow. Сохранить local coverage и clearance; CPU weather/streamed atlas вводить отдельно. PR globals не подключаются автоматически к `_SnowControl` |
| **Сохранения** | MapEditSession, WorldSaveState.mapEdits/savev5, compact commands, stable tree/road IDs, region notifications | Snapshotv1 и scene authoredStateJson, полные snapshots на Undo | Local save/session как единственную авторитетную точку. Добавить versioned theme deltas/migration, использовать staged atomic restore. Не создавать параллельный game save в компонентном JSON |

Текущий MapChangeKind не имеет отдельного Biome/Snow flags: future integration должна явно расширить consumers и типы изменений, а не выдавать theme repaint за Height и не вызывать общий terrain/lighting rebuild.

## 4. Реальные Unity tests

Стенд содержит только PR79 source/tests, необходимый ChunkCoordinate.cs, asmdef, минимальные packages/settings. Production Unity проекты этим аудитом не запускались. Batchmode/nographics запуски завершены, runtime source побайтно совпадает с `raw/` (`runtime-source-integrity.json`).

| Проверка | Результат |
|---|---|
| C# compile source/editor/tests в isolated probe | **PASS** |
| RAW все тесты | **17 total = 9 PASS / 8 FAIL** |
| RAW восемь оригинальных PR79 | **7 PASS / 1 FAIL** |
| Probe-only corrected fixture, все тесты | **17 total = 10 PASS / 7 FAIL** |
| Восемь оригинальных после коррекции fixture | **8 PASS / 0 FAIL** |
| Девять дополнительных diagnostic contracts | **2 PASS / 7 FAIL** |
| Production PR79 shader GPU compile/visual/Player | **NOT RUN** |
| Live SceneView drag/Undo UX, production save/streaming, 2/8/16-player profiling | **NOT RUN** |

Оригинальный SnapshotRoundTrip ожидает winter=1 в точке(-.5,-.5), но при cellSize2/soft brush это не cell centre: реальный исходный weight **0.8564450145**. Probe-only коррекция сохраняет themeBefore перед export и сравнивает с ним после import. Это проверяет roundtrip без неправильного предположения о величине painted weight. Production/runtime source не исправлялся; оба XML и лога сохранены отдельно. Оставшиеся FAIL намеренно не маскировались, оба запуска завершаются exitcode2.

PASS диагностик: обычные отрицательные tile seams/restore (-17,-16,-9,-8,-1,0,7,8), атомарность Import при повреждённом втором поле. FAIL диагностик см. ниже. Shader отсутствие ошибок в nographics import не является доказательством GPU variant correctness.

## 5. Точные дефекты и ограничения PR79

Пути ниже относительно PR79 `Assets/_Game/`; line numbers относятся к неизменённой `raw/` выгрузке.

| Приоритет | Файл:строка | Воспроизведение/риск | Требуемое изменение после разрешения |
|---|---|---|---|
| P1 | `Scripts/World/Environment/EnvironmentFields.cs:305` | DirtyRect ограничен radius, bilinear support шире: cellSize1,dab(.5,.5),r.1 меняет sample(1.25,.5)=.25 за dirty bounds. Diagnostic **FAIL** | Собирать изменённые cell centres, расширять bounds на bilinear halo, уведомлять соседние regions/tiles |
| P1 | `EnvironmentFields.cs:102`, Capture:121, IsEmpty:53 | Обнулённые tiles не удаляются; erase/melt сохраняют память и JSON; dictionary IsEmpty остаётся false. Diagnostic **FAIL** | Nonzero counts/pruning, компактные snapshots и deltas. Не prune по произвольному epsilon, теряя накопление |
| P2 | `EnvironmentFields.cs:101` | Каждая delta<1e-5 отбрасывается:100 ticks dt.01/rate.0001 дают snow0 вместо.0001. Diagnostic **FAIL** | Точное хранение или remainder; порог отдельно для notification, fixed authoritative ticks |
| P2 | `EnvironmentFields.cs:268`, `:289` | Blur только selected layer: winter→другая theme создаёт Summer=.5, соседние raw суммы могут превысить1. Diagnostic **FAIL будущего joint-blend контракта**, узкий selected-layer API так и документирован | Утвердить vector-weight blur/implicit summer; staged before/after deltas, нормализация |
| P2 | `EnvironmentFields.cs:226`, `:273`, `:188` | HardnessNaN не проверен до Layer creation: исключение есть, но пустой слой уже остаётся. Diagnostic **FAIL** | Finite checks всех входов до mutation; atomic operation/cancel |
| P2/extreme | `EnvironmentFields.cs:68` | int→float floor division при cell=-16777217 даёт IndexOutOfRange. Diagnostic **FAIL**. Обычные конечные карты этим не объявляются сломанными | Integer floor division и bound validation до loops |
| P2 | `Shaders/Effects/LC_ThinSnowURP.shader:106`, `LC_ThinSnowShaderGraph.hlsl:35` | Noise добавлен после cover*slope*acceptance: при acceptance0/vertical positive noise оставляет до EdgeBreakup/2 снега. Static formula defect; GPU **NOT RUN** | Noise внутри coverage, затем slope/acceptance; visual zero-acceptance/vertical tests |
| Limitation | `Scripts/World/Environment/BiomeSnowWorld.cs:140`, `:133` | Export field-only: нет weather driver/settings/bounds/clock; intensity.75→0 не восстанавливается. Diagnostic **FAIL будущего complete-save контракта**; Unity scene settings сохраняются отдельно. Time.deltaTime не world-clock | Versioned world save+clock/authority; ясно отделить fields vs full save |
| Limitation | `BiomeSnowWorld.cs:176`, `:113` | Restore invalidates только SimulationBounds, хотя Paint допускает данные за ним; Inspector grid change не мигрирует initialized fields/authored JSON | Finite-map policy, explicit grid migration/reset, full dirty extent |
| P2/editor | `Editor/BiomeSnowWorldEditor.cs:192`, `:265`, `:290` | Full JSON Undo, любой collider hit, cap512 растягивает spacing, пропуски длинного drag; маленький r.5 при grid2 может не задеть cells | Общий controller/query/Undo, bounded queue и честный preview actual cells |
| Scale limit | `Scripts/World/Environment/SnowMaskTextureBridge.cs:54`, `:88`, `:96`; `EnvironmentFields.cs:418` | Weather full rectangle; bridge full res² sample/upload, один global owner; OnDisable любого bridge сбрасывает globals | Active/dirty tiles, per-tick budgets, tiled/atlas residency, явный owner |

Оценка памяти, не profiler measurement: tile32² float =4096байт без overhead. При карте4096²м/cell2 один полный слой занимает16MiB, четыре explicit слоя+snow —80MiB raw до clones/JSON/Undo. После erase это сейчас не возвращается. Художественная точность ограничена grid, zoom не делает её выше.

## 6. Бесплатные источники и лицензии

Подробности с local hashes и primary links — `third-party-snow-review.md`.

- **Unity Production Ready Weather/Snow**: [первичный документ](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/Documentation~/Shader-Graph-Sample-Production-Ready-Weather.md), [package LICENSE](https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.shadergraph/LICENSE.md), [UCL v1.4](https://unity.com/legal/licenses/unity-companion-license). Лицензия **Unity Companion**, не MIT: использование в рамках Unity Engine License, сохранять notices; у производных изменений Work специальные условия UCL. Семь sample/package/license файлов в local SG17.5 двух рабочих копий идентичны. Предпочтительный референс thin material coverage, без автоматической замены LC lighting.
- **DeepSnowFootprint** head `4380788f0e0aad707d2bdde5056cc2be247e2b85`: [README](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/README.md), [MIT LICENSE](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/LICENSE.md), [shader](https://github.com/ZGeng/DeepSnowFootprint/blob/4380788f0e0aad707d2bdde5056cc2be247e2b85/SnowGroundShader.shader). Copyright2018 Zhen Geng; сохранять copyright/permission notice. Built-in Deferred Unity5.5/target5/hull-domain-geometry: прямой reuse для URP Forward не подходит. Только идеи stamp/fade; bundled Unity Standard Assets требуют собственной проверки notices.
- **SnowShaderGraph** head `c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b`: [README](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/README.md), [MIT LICENSE](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/LICENSE), [actual graph](https://github.com/mert-dev-acc/SnowShaderGraph/blob/c6c51f2eb901ae2a54f2b3f1aaed44ac4aa2508b/Snow.shadergraph). Copyright2020 Mert Kirimgeri; сохранять notices. Legacy PBRMasterNode/NodeVersion0, точная SG package version не задана; Custom CalculateHeight accumulator не инициализирован. Изучать локальный splatmap/blur, исключить deformation из thin-snow scope.

**NOT RUN**: импорт/миграция/компиляция сторонних samples/packages. Никакие внешние assets/code в main или другую производственную копию не добавлялись.

## 7. Будущие модули и план после отдельного одобрения

Сложность относительная: S — локальный контракт/адаптер, M — несколько data/editor consumers, L — persistence/rendering/streaming subsystem. Это не обещание календарных сроков.

| Шаг / модуль | Зависимости и сложность | Критерий приёмки |
|---|---|---|
| 0. Утверждение scope/ADR | Канонический объединённый baseline root; S | Явно разрешены конкретные biome задачи; отдельно перечислен snow/rendering scope. Документированы finite bounds, grid, profile IDs, vector weights, full save vs fields |
| 1. EnvironmentFieldCore | Шаг0; M | Все воспроизведённые core defects закрыты: halo, pruning, tiny increments, NaN atomicity, integer floor. Negative/seam/cancel tests; normalized sums и deterministic samples. Не запускать Unity в чужих copies |
| 2. EnvironmentEditTransaction | Шаг1 + MapEditSession contract; M | Компактные before/after dirty tile deltas; один stroke=одна Undo command; Cancel возвращает точные weights. Region events+halo без whole-map rebuild; отдельные change kinds |
| 3. BiomeMapToolAdapter | Шаг2 + IMapTool/controller/query; M | Одна вкладка/ввод существующего World Editor, Paint/Erase/Smooth, presets и radius metres; near/far hit по pending terrain; длинный drag без дыр/двойного input; original height/grass/tree/river инструменты сохранены |
| 4. VersionedEnvironmentSave | Шаг1–2 + WorldSaveState migration; L | Старые saves открываются по явной схеме; stable entity/theme IDs; exact save/load/Undo; corrupt import atomic; missing profile fallback; weather+clock settings при полном game save явно сохранены |
| 5. BiomeAppearanceResolver | Шаг3–4 + approved profile rules; L | Only affected residents/chunks update; terrain/grass/props appearance меняется без удаления ручных объектов/дорог/ownership; locked overrides; visual overlay честно отделён от final art |
| 6. SnowSimulationScheduler | Отдельное разрешение снега; шаг1,4 и world-clock; L | Fixed authority ticks; cover независимо от snowfall/theme; active tiles/prune; timestep/pause/load tests; measured CPU budget и finite bounds, независимость от camera |
| 7. SnowMaskPresentationAdapter | Шаг6 + existing Terrain/Lit/Foliage contracts; L | Единый tiled/atlas owner; existing _SnowControl/normalAO/pathwetness сохраняются; correct slope/acceptance0; lighting/wind/shadow/depth parity; one shared material family, без material clone/object-per-cell |
| 8. Golden scene / Player gates | Шаг5–7; L | Ground/roof/rock/wall/tree/path при одинаковых near/far cameras и day/night; snow after snowfall stop, melt, seam, save/reload. GPU compile, Player screenshots и profiler, реальные2/8/16-player costs; bootstrap отдельно от streaming |
| 9. OptionalTemporaryFootprints | Только после шага8 и отдельного UX приоритета; M/L | Локальные stamps/decals с fade/pooling/budget; без deep tessellation/deformation и authoritative gameplay state; исчезают без накопления памяти; default persistence не нужна |

До каждого этапа фиксировать exact baseline/source owners и собственный scope; работу рек не перезаписывать. Перед foliage shader integration пройти обязательный реальный mesh/wind/shadow compatibility preflight. Критерий готовности — реальные tests/кадры/профили, а не наличие prototype source или название sample Production Ready.

## 8. Артефакты и полнота Task02

Полное вложение Task02 прочитано основным агентом: C:/Users/Дмитрий/.codex/attachments/de43b274-a947-4c63-9f4d-c44b78fdc939/Вставленный текст.txt. Отчёт дополнен отдельным отчётом фактической интеграции World Editor/освещения в основном проекте.

- `audit-pr79.md` — подробный source audit; `results.json` — machine-readable обе серии.
- `raw-test-results.xml`, `raw-unity.log` — неизменённый первый результат17/9/8.
- `diagnostic-corrected-test-results.xml`, `diagnostic-corrected-unity.log` — fixture correction17/10/7.
- `Probe/Assets/Tests/Editor/DiagnosticEdgeTests.cs`, `raw/`, `runtime-source-integrity.json` — воспроизведение и source integrity.
- `third-party-snow-review.md`, `shadergraph-sample-local-integrity.json` — первичные источники/лицензии/локальные SHA256.

Этот отчёт завершает исследовательскую часть Task02. Он не разрешает и не объявляет завершённой полноценную biome/snow production integration.
