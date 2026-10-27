# World Editor + Meadow lighting в основном проекте

Актуальный итог после доработки панели и переноса воды:
[World Editor — что объединено и как посмотреть](world-editor-final-20261027-ru.md).
Ниже сохранена техническая запись первоначального объединения; её test counts
и benchmark относятся к срезу до последующих UI/water исправлений.

27.10.2026. Канонический Unity-проект: **E:/Games_Develop/Little-Castle**, Unity **6000.5.5f1**, URP **17.5.0 Forward**, Linear. Ветка `codex/world-editor-lighting-integration-20261027`, исходный HEAD `e6a02df`. До работы было127 локальных изменений; они не сбрасывались. Полный резервный снимок Assets/Packages/ProjectSettings/docs и binary patch — `E:/Games_Develop/Research/WorldIntegration_20261027/BeforeMain`.

Источники: актуальные кисти BrushFoundation **4cc63820347bab7da3b944fe76875eca00c8f107** (продолжение URP_20261024, Draft PR80), освещение LightingIntegration опубликованное **42f317c49fb12e374a3f85ecc75616673a2c0ad0** с baseline **9e8c126** и последовательностью GI зависимостей. Прочитаны AGENTS.md, lighting handoff, map coordination, оба контракта материалов/растительности, полный Task02 и `E:/Games_Develop/PROJECT_INDEX_RU.md`. Исходные рабочие копии не изменялись. Main и GitHub main не считались автоматически свежейшими.

## Что объединено

- Существующее окно **Little Castle → Map → World Editor**, новая отдельная сцена **Assets/_Game/Scenes/WorldEditor_20261027.unity**. Быстрый вход: **Little Castle → World Editor → Open Integrated Scene**. Существующие сцены Meadow/LightingMeadow/SpatialIndirectStudy сохранены.
- Последняя реальная система IMapTool/registry/controller/context/settings/presets и MapEditSession используется дорогой, лугом, стиранием покрытия, поднятием/опусканием, рощами, отдельными деревьями и control-point draft рек. Отдельных существовавших height Smooth/Restore алгоритмов аудит не нашёл; они не выдумывались. Подробности — [архитектура](../architecture/unified-map-tools.md), [новая кисть](../workflows/add-map-brush-ru.md).
- Радиус в мировых метрах; общий stroke/input, softness/strength, contour по heightfield, regional notifications/Undo/savev5. Свежий grass MPB/tiled surface cache оставлен. Высотные правки не ждут пересоздания collider. Игровая камера сохраняет defaults; только authoring получает расширенный zoom. Presets камеры синхронизируют strategy rig.
- Проверенный Meadow baseline: owned HDR/AO profiles, Neutral, MSAA4,2048 shadowmap/2 cascades/90м, light bias.035/normal.13, palette .95sun/.2moon/readable night; root AO.22 в owned LightingMeadow/Blades.mat. Screen AO: Depth reconstruction, radius.28/intensity.45/direct0, only indirect, min(root/material,screen). Текущие quality profiles основного проекта подключены обратно; они не заменяются целиком экспериментальными настройками.
- Shared шейдеры объединены по функциям: terrain triplanar/локальная snow normal+AO/очистка снега дорогой и влажностью сохранены. Grass/Foliage deformation и Shadow/Depth/Normals sections идентичны свежему map. Terrain и StylizedLit в исходниках не имеют собственного DepthNormals pass; baseline SSAO реконструирует normals по depth. Wind/interaction/UV anchors/instancing/LOD/local keywords сохранены.
- Автоматические сутки360с день/90с ночь, developer buttons День/Закат/Ночь и resume automatic в существующей игровой панели World Editor. Диагностические куб/две стены оставлены в новой сцене, original spatial roof/walls/receivers сохранены отдельно.
- Spatial GI — только opt-in отдельной SpatialIndirectStudy_20261026, не компонент authoring сцены и не включённый keyword. Proxy GI пока не подписан на реальный изменяемый рельеф/объекты: не выдавать его за production GI.

## Изолированные исправления интеграции

1. LightingMeadowReview удалён из новой сцены: disabled MonoBehaviour всё равно выполняет Awake, где старый review замораживал часы и переставлял камеру. Изменены только новая сцена и её builder; исследовательские scenes/drivers сохранены. Добавлен regression test и runtime startup assertion до acceptance overrides.
2. Старая Main Meadow_ScatterLibrary содержала0 grass prototypes; актуальная Map имеет6 и завершённые категории цветов/подлеска. После сравнения перенесены две catalog assets и недостающая library meta с сохранением GUID. Все5 Flower EditMode tests проходят. Старый shader diff не использован для замены material/snow/MPB контрактов.
3. При реальном открытии Editor после BuildPipeline обнаружена потеря URP ссылок: Unity выгружает сохранённые Unity object references. Build теперь восстанавливает assets по persistent paths и исходные байты трёх ProjectSettings, проверяя восстановление в отдельном файле. В открытом Unity выполнен Build verification Player: Succeeded/0errors; default+6quality profiles восстановлены по paths; ProjectSettings/GraphicsSettings/QualitySettings восстановлены побайтно(True/True/True). После build материалы в Editor корректные, pink regression устранён. Служебное предупреждение об uncompiled editor changes записано в логе, затем Assets Refresh завершил компиляцию; проверенная FinalPlayer capture/performance сборка создана ранее из свежих runtime scripts.
4. World Editor повторно подключает выбранную базу после domain reload, чтобы окно не оставалось пустым после компиляции. Перед изменением кода сохраняйте незавершённый авторский документ в JSON; история session не является сохранением Unity scene.

GUID closure: первоначально749 files/180890645bytes,0 GUID conflicts,33 package IDs разрешены PackageCache. Решения/перечень каждого файла: `world-editor-lighting-evidence-20261027/applied-code-assets.json`, shader merge и catalog completion рядом. Публичный source checkpoint не содержит raw licensed meshes/textures: локально они на месте и включены в `local-art-manifest.json`; чистый clone требует восстановления этих fixtures из имеющихся лицензированных библиотек/резервной копии.

## Проверки

| Проверка | Результат |
|---|---|
| Unity real Oak A–D, native grass LOD meshes validator | PASS,0 errors/warnings; листья и кора separate material submeshes, normals/UV/tangents есть; classification COMPATIBLE с этим material split |
| Static функциональное слияние/source/GUID/includes |53/53 PASS; original finding сохранён отдельно |
| Full EditMode |278:277 PASS/1 FAIL |
| Map / Lighting / SpatialIndirect / Flower / new WorldEditor EditMode |74/74;5/5;32/32;5/5;1/1 PASS (группы могут пересекаться) |
| Full PlayMode |15/15 PASS, включая map presentation и sky material lifecycle |
| Release Windows build |Succeeded;0 errors/3 warnings; Linear; DX12 actual player |
| Actual integrated Player startup/clock/road/Undo/Redo/disk save/load |PASS, runtime errors empty; user persistent saves не трогались |
| Real game PNG |18 settled camera views:12/18.5/1ч ×11/35/115м ×AO off/on; плюс20 transition frames |
| Continuous clock trajectory |240 samples, monotonic,19.8h→следующий день6.97h; затем production automatic Update advance PASS |
| Большая multiplayer карта/2/8/16 игроков, другие GPU/API |NOT RUN — эти fixture measurements их не заменяют |
| PR79 production shader GPU/rendering/full integration |NOT RUN, только isolated CPU audit |

Единственный EditMode FAIL: ConceptNaturalVegetationTests.GroundCoverPresetsRespectClearanceWhileMainTreeFlagsStayLegacy:159 ожидает Main ground-cover clearance=true, текущий Main preset=false. Четыре генераторных preset assets совпадают с BeforeMain; поведение/настройки генератора не менялись ради зелёного теста. Это известное несоответствие теста и текущих локальных presets, а не провал кистей. Все raw XML сохранены, полный набор не объявлен зелёным.

Initial preflight выполнился и записал mesh results, но immediate EditorApplication.Exit привёл к native exit crash(-1073741819). Subsequent Setup/build/test exits штатные; helper переключён на существующий deferred idle exit. Никакой утраты assets/scenes не обнаружено.

В Player имеются предупреждения URP о stripped unused LensFlare shaders; lens flares сценой не используются. Это не объявляется отсутствием всех warnings; terrain/grass/foliage/solid rendering и SSAO проверялись настоящими кадрами. Старые obsolete API/LOD importer warnings остаются отдельным техническим долгом.

## Кадры и измерения

[День](world-editor-lighting-evidence-20261027/h12-d35-aoon.png), [Ночь](world-editor-lighting-evidence-20261027/h1-d35-aoon.png), [Дальний вид](world-editor-lighting-evidence-20261027/h12-d115-aoon.png). Визуально ночь читается: видны форма дороги, объекты и растительность; на115м исходный fog заметно съедает детали, shadow distance90м ограничивает дальние тени. Не заявляется наличие одинаковых full shadows на всех дистанциях. Artist approval и оценка shimmer в длительном движении остаются за пользователем; сохранение pass contracts не равно художественной приёмке.

На600 обычных отрисованных кадров после120 warmup: Windows11, RTX4070TiSUPER16GB, Direct3D12,1600×900, release, no PNG/readback во время измерения. Весь measurement loop требует foreground focus. GC recorder недоступен — это не0bytes/frame. GPU timings приходили частично; median только по положительным samples, количество указано. Это маленькая Meadow fixture, не FPS promise всей стратегии.

| Сценарий | Median wall,мс | P95 wall,мс | Median GPU,мс | GPU samples |
|---|---:|---:|---:|---:|
| far-day-ao | 1.39 | 2.51 | 0.52 | 474/600 |
| mid-day-ao | 1.29 | 4.29 | 0.67 | 460/600 |
| mid-day-no-ao | 1.29 | 2.30 | 0.46 | 470/600 |
| mid-night-ao | 1.24 | 2.63 | 0.62 | 478/600 |
| near-day-ao | 1.14 | 2.13 | 0.48 | 488/600 |

Файлы `final-benchmark-summary.json`, CSV в Research/WorldIntegration_20261027/FinalBenchmark, `transition-summary.json` и `views.csv` содержат исходные значения. AO pairs имеют идентичные позиции камеры(max delta0); анимация травы продолжалась, поэтому pixel subtraction не использовалась как количественный AO metric. На дальней камере105 grass patches/12705 clumps/101640 triangles/26grass draws; это только счётчики native grass renderer.

## Как проверить самому и где остановились

Откройте **E:/Games_Develop/Little-Castle** в Unity6000.5.5f1. Меню **Little Castle → World Editor → Open Integrated Scene** открывает сцену и окно World Editor. Вне Play рисуйте в SceneView, включив Инструмент в SceneView. Сохраните JSON, закройте/откройте его, проверьте Undo/Redo, дорожку поверх луга, высоту и рощу. В Play используйте существующую игровую панель World Editor, кнопки времени и strategy camera. Подробный [порядок](../workflows/test-world-editor-ru.md).

[Task02 audit](environment-audit-20261027/report-task02-ru.md) содержит обязательную сравнительную таблицу Кисти/Биомы/Шейдеры/Снег/Сохранения, найденные дефекты PR79 и будущий план. Для расширения достаточно нового IMapTool + runtime data operation/notification/save adapter; developer input не нужно дублировать. Биомы/зима пока требуют отдельных нормализованных theme fields, versioned deltas, scheduler от world-clock и adapter существующего terrain mask. Gameplay construction дополнительно требует authority/прав/стоимости/сети; полноценный игровой редактор не создавался.

**Новые биомы, снег, материалы зимы и этап2 не реализованы.** Следующее действие — пользовательская проверка окна/картинок и отдельное решение по Task02. Старые копии, библиотеки, RiverBanks и Asset Book остаются на местах; этот проход не является разрешением их удалить.

Ручная проверка открытого Main Unity: Play Mode запущен из WorldEditor_20261027, автоматическое время дошло до10:18; кнопка Ночь выставила01:00 и остановила часы, освещение сменилось без ошибок Console. Затем возвращён Edit Mode. Ветер/трава/деревья и диагностические объекты видны; долгая проверка shimmer остаётся NOT RUN.

Source commit: `635b5d028b9edea6ace787fbea0ac830bd47966c`. Последняя Editor-only правка reconnect скомпилирована в открытом Main Unity после точечного Reimport MapAuthoringWindow.cs; окно снова показывает все инструменты и деревья без повторной загрузки карты. Main оставлен открытым в Edit Mode.
