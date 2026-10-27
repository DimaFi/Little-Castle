# Освещение → редактор карты: совместный Meadow baseline

Работа явно возобновлена пользователем 25.10.2026. Результаты и замеры дополнены
26.10.2026 (Europe/Saratov). Это первый совместный baseline, не завершённый
аналог освещения Tiny Glade. Пространственный GI ещё не реализован.

## Где результат

- Интеграционная копия: `E:/Games_Develop/Little-Castle_LightingIntegration_20261025`.
  Папка до начала работы отсутствовала. Основа — фактический MAIN с предыдущими
  Linear/SSAO-изменениями, без .git, Library, Builds и SourceAssets.
- Сцена: `Assets/_Game/Scenes/LightingMeadow_20261025.unity`.
- Сборки: `E:/Games_Develop/Research/LightingMeadow_20261025/Player_Linear/`
  и `Player_Gamma/`, `LittleCastle_LightingMeadow.exe`. F1 — HDR/AO, F2 — ракурс,
  F3 — день/закат/ночь. Автотесты Player не загружают и не сохраняют файл правок пользователя.
- Достоверные финальные изображения: `Acceptance/Linear/Captures` и
  `Acceptance/Gamma/Captures` в той же папке Research. Старые `Linear`, `Final`,
  `Verified` — промежуточная диагностика, не финальная приёмка.
- Обзор: `lighting-meadow-day-sunset-night.png`; последовательности реальных кадров:
  `camera-motion.gif`, `oak-wind.gif`. Монтаж уменьшает игровые PNG и добавляет
  подписи, без перекрашивания/дорисовки.
- Полные хеши и diff: `docs/lighting-integration/` интеграционной копии.

История исходного чата «Составить план улучшения освещения» восстановлена через
list_threads/read_thread: `01a173a6-adf9-7601-a0a4-43ded6c61b8b`, все три turn доступны.
Пробела истории нет. Прочитаны семь указанных пользователем документов и актуальная
передача карты. MAIN Assets/Packages/ProjectSettings не редактировались; итоговый
аудит сравнил их с исходным манифестом без расхождений. В MAIN записана эта передача.

## Снимок Meadow и реальные модели

Источник: `Little-Castle_URP_20261024`. Выборочный рекурсивный обход GUID сцены
Meadow и её зависимостей, importer remaps и необходимых runtime-классов:
234 записи файлов/метаданных в `meadow-source-manifest.json`.
Время снимка: `2026-10-25T19:19:05.911036Z`. GUID сохранены. Полный исходный MAIN
зафиксирован отдельно в `main-source-manifest.json`; дополнительные входы шейдеров
заморожены в `source-shaders/`, их хеши — `shader-merge-manifest.json`.
`snapshotIntegrityFailures=[]`: перенесённые snapshot-файлы совпадают с записью.

До изменения vegetation shaders запущен настоящий Unity validator на Oak A/B/C/D
и четырёх meshes Meadow. У дубов 3 LOD, кора/листва в двух submesh с solid/foliage
материалами, normals/tangents/UV0 есть; Vertex Color отсутствует и не нужен для
разделённых material regions. Результат: **COMPATIBLE**, 0 ошибок/предупреждений.
Нативная трава: normals/UV0/UV1 есть, UVY-height correlation 0.938–0.960,
четыре LOD 144/48/20/8 треугольников на clump. `preflight.txt` содержит фактические
числа для каждой модели. Цветы остались статичными. Geometry/LOD/production catalog
не переавторены. Близкие/дальние кадры и последовательность ветра просмотрены;
резинового растяжения/отрыва теней в этом коротком проходе не обнаружено.
Это не длительная приёмка всех zoom, всех деревьев и всей карты.

## Что объединено по функциям

Из MAIN сохранены Linear working-color conversion, общий LC_LightingCommon,
SSAO solid/foliage/grass/terrain и единые глобалы суток. Из Meadow добавлены:

- LC_Grass: `_LC_MEADOW`, `_LC_SURFACE_PAINT`, UV1 XYZ=root/W=density rank,
  coherent wind, distance density/softness, local interaction, whole-clump fade.
  Forward/ShadowCaster/DepthOnly/DepthNormals используют общий LCDeformVertex
  и одинаковые keywords. Instancing и исходный MeadowGrassRenderer сохранены.
- LC_Terrain: общая маска дорожек, surface normal/AO multipliers и fade distances.
  Из посторонних расхождений ветки **не переносились** snow/cliff/water-функции.
- Корневое AO и screen AO соединены один раз через `min(materialAO, screenAO)`.
  Не умножаются друг на друга, direct sunlight, emission или финальный кадр.
  Меши травы получают тени деревьев, сами realtime grass shadows не отбрасывают.
- Общие методы: SetWindPresentation добавлен к MAIN globals без потери Linear;
  DistanceToSegment/SoftCorridor открыты для использования кистями без изменения
  алгоритмов; InnerverseGrassStudy получил max distance и shadow reception.
- LightingPaletteProfile: опциональное окрашивание солнца по реальной высоте
  над горизонтом. Без флага сохраняется прежняя формула. WorldTime не изменён.
  Это даёт тёплый низкий свет в 18:30, когда daylight intensity ещё велика.

Восемь изменённых исходных файлов:
`InnerverseGrassStudy.cs`, `ChunkTerrainVertexMasks.cs`,
`DayNightLightingController.cs`, `LightingPaletteProfile.cs`,
`StylizedLightingGlobals.cs`, `LC_Grass.shader`, `LC_GrassURP.hlsl`,
`LC_Terrain.shader`. Точные project-relative paths и SHA-256 перечислены в
`integration-audit.json`; функциональный diff — `main-to-integration.patch`.
Новые собственные классы: LightingMeadowBuild, LightingMeadowIntegration,
LightingMeadowReview, LightingMeadowPaletteTests. Новые owned profiles/materials
только под `Settings/Rendering/LightingMeadow`; исходные Meadow materials сохранены.
Старая CottageGarden/LightingBenchmark оставлена baseline.

Примеры исходных SHA-256 из URP-снимка:

| Файл | SHA-256 |
|---|---|
| LC_Grass.shader | `4b2e67a71c8acf739837243498c61b99158282b047d25404b612c0b93c001f18` |
| LC_GrassURP.hlsl | `cfce98bfeca4d10f1e29c2695d9f495ea275801a336f0d4260ffed462ea48830` |
| LC_Terrain.shader | `4cd99651179380406061e5a4048709d5757e6caa228100700ed1499a54837b11` |

## Настройки и проверки

Отдельные Gamma и Linear Release Players, HDR на pipeline/camera, один Neutral
tonemapper. Без SoftScene contact AO, DoF и auto exposure. MSAA4, soft sun shadows,
2 cascades, shadowmap2048, distance90m, bias0.035/normalBias0.13.
SSAO: depth reconstructed normals, downsample, radius0.28m, intensity0.45,
direct strength0, falloff75m. Root AO owned grass material0.22 (исходный0.30).
Повышен ночной ambient отдельного профиля. Основные игровые настройки не переключены.

- **178/178 Edit Mode, 6/6 Play Mode**. Новая проверка гарантирует тёплый низкий
  свет без изменения интенсивности суток/часов и прежний дневной цвет в полдень.
- Финальные Gamma/Linear builds Succeeded, shader-errors.txt пуст; последний
  Linear build — 0 ошибок, 0 предупреждений. В ранней компиляции было известное
  deprecated API warning LocalSandboxController.
- **152/152 PNG** (76 на пространство) проверены: размер1920×1080, непустое
  содержимое, metadata, 24 HDR/AO-пары. Высокая strategy camera близка к AO falloff:
  почти одинаковые HDR/AO кадры там ожидаемы, работоспособность подтверждена
  близкими/контактными парами. `capture-validation.json`: PASS.
- Player проверяет общую маску terrain/grass, native instancing+4LOD, дорожку
  и подавление травы, восстановление исходного digest после Undo.
- Записаны rootAO on/off, interaction on/off, 32 кадра перемещения камеры,
  16 кадров ветра на пространство. Локальное изменение пикселей/движение есть.
  На просмотренных кадрах нет заметных чёрных ореолов; численные различия сами
  по себе не доказывают художественную приёмку или полную temporal stability.

Экспорт обнаружил особенность первого URP offscreen render request: первый PNG
был пустым. Исправлено: проверять содержимое и повторять на следующем rendered
frame (до четырёх попыток). Два запроса в одном кадре проблему не решали.
Промежуточные пустые PNG исключены из Acceptance. Captures не считаются FPS.
Runtime log содержит известные сообщения о неиспользуемых LensFlare passes;
ошибок LC shader/исключений в Acceptance и измерениях нет.

## Настоящие замеры — 26.10.2026

Unity6000.5.5f1 / URP17.5, D3D12, RTX4070 Ti SUPER, i7-12700F, 1920×1080,
Release, FrameTimingStats=true, VSync/лимит FPS off. Восемь отдельных видимых
standalone-запусков. После начального ожидания2s и120 обычных кадров собраны
600 интервалов endCameraRendering; PNG/readback/CSV вне измерений. GPU samples
доступны не на каждом интервале; точные количества ниже. Во время измерений
наши Unity/capture jobs закончены; фильтр Unity/Meadow процессов до/после не
вернул записей, остальные пользовательские приложения не изолировались.

| Пространство / время / режим | GPU mean, ms | GPU samples | frame median / p95 / p99, ms |
|---|---:|---:|---:|
| Gamma12 HDR | 0.594 | 567 | 0.899 / 1.201 / 1.363 |
| Gamma12 HDR+AO | 0.892 | 571 | 1.157 / 1.622 / 2.039 |
| Linear12 HDR | 0.588 | 512 | 1.025 / 1.559 / 1.920 |
| Linear12 HDR+AO | 0.893 | 486 | 1.481 / 2.618 / 3.255 |
| Linear18.5 HDR | 0.578 | 505 | 1.005 / 1.427 / 1.617 |
| Linear18.5 HDR+AO | 0.903 | 579 | 1.166 / 1.627 / 1.871 |
| Linear1 HDR | 0.546 | 577 | 0.851 / 1.174 / 1.359 |
| Linear1 HDR+AO | 0.727 | 577 | 1.076 / 1.475 / 1.718 |

Во всех запусках основной слой:13552 clumps,504812 triangles,28 draws, плюс
дубы/акценты/scatter. В benchmark сохраняется стационарная демонстрационная
капсула из сцены; screenshot matrix её скрывает. Геометрические счётчики выше
относятся к основному слою, не ко всей сцене. Предварительная цена SSAO в Linear
примерно0.18–0.33ms GPU. Выбросы CPU/кадра не объявляются эффектом только AO.
DXGI предупреждал о timestamp статистике swapchain; wall интервал измеряется
собственным Stopwatch по рендеру камеры, не выдаётся за точный display latency.
Это маленький стенд на мощном GPU, не гарантия FPS большой карты или слабого ПК.
GC allocation не измерялась. Raw JSON и `performance-summary.json` сохранены.

Первый запуск замеров был отклонён из-за ошибки сервиса автоматической проверки
разрешения (лимит использования), команда не выполнялась. После явного сообщения
пользователя «коммит в github и продолжай» и восстановления доступа замеры выполнены.

## Интеграция с новым редактором карты

После снимка изменились шесть source-файлов: InnerverseGrassStudy, MeadowPreview,
MeadowSurfacePainter, MeadowGrassRenderer, MeadowScatterRenderer, SurfacePaintData.
Их старые snapshot-версии **не переносить поверх нового редактора**. Точные before/
current SHA — sourceDrift в integration-audit.json. Общие shaders/globals/URP входы
по финальному аудиту остались прежними; текущий редактор нового shader API не требует.

1. Сверить свежий sourceDrift, интегрировать shader diff по функциям с сохранением
   новых per-tile MPB `_SurfacePaint/_SurfacePaintBounds`, масок, wind/interaction/LOD.
2. Внести отдельно маленькие изменения палитры/globals. Не копировать целиком
   папку Assets, сцену редактора, brushes, save formats или настройки ProjectSettings.
3. На новой сцене редактора повторить близкий/дальний вид, день/закат/ночь,
   дорожку/Undo/Save/Load, границы плиток с одним halo texel и camera motion.
   Новый tiled MapSurfaceGpuCache с этим светом ещё не проходил совместный Player.

Прочитан актуальный `MapEditSession.Changed(MapRegionChange)`: bounds=Rect XZ,
kind=Surface|Decoration, preview=true во время мазка. Будущий световой кеш получает
только завершённые изменения preview=false, расширяет bounds конечным halo,
объединяет dirty tiles и обновляется по бюджету. У события сейчас нет revision;
адаптер может иметь собственный presentation epoch для отбрасывания устаревшей
работы. authored.revision — версия базы, не счётчик каждого мазка.
Полный контракт — `local-cache-invalidation-ru.md`; это план, не внедрённый GI.

## Следующий шаг и Tiny Glade

Пользователь 26.10 уточнил: максимально ориентироваться на свет и shaders Tiny Glade,
искать исходники. Найдены публичные первичные материалы и код авторов; результаты
в `docs/lighting-integration/tiny-glade-source-research-20261026-ru.md`.
Следующий измеримый эксперимент: упрощённая геометрия стен/крыш/земли, перекрытие
неба, один отражённый вклад, внекадровый объект и локальное обновление после сноса.
Он проводится в этой копии и не блокирует редактор. Текущий ambient по-прежнему
не знает окружающих крыш/стен. Настройка цветов и SSAO этот пробел не закрывают.

Полноценные ночные фонари/освещение тумана, плотное поселение, длинный маршрут
камеры через все каскады и окончательное художественное совпадение с Tiny Glade
остаются отдельными этапами. Вода/снег, Production catalog и рабочая копия карты
не менялись, чужие приложения не управлялись.

По отдельному запросу пользователя результат сохраняется в GitHub как
интеграционный пакет на ветке `codex/lighting-meadow-baseline-2026-10-26`.
Пакет не переключает основной проект. Локальная база URP и сторонние бинарные
ассеты не публикуются автоматически; их точные зависимости записаны хешами.

## Продолжение 26.10: опубликован baseline, реализован ограниченный spatial GI

Baseline-пакет опубликован и побайтно проверен:63/63 Git blobs,
[`9e8c126abd2bf34a15df3bcbf01f7285cfe90843`](https://github.com/DimaFi/Little-Castle/commit/9e8c126abd2bf34a15df3bcbf01f7285cfe90843),
ветка `codex/lighting-meadow-baseline-2026-10-26`. Это отдельный интеграционный
пакет; основная игровая ветка/настройки не переключены. Следующие GI-изменения
локальны и не входят в этот опубликованный commit.

В lighting-копии создана отдельная `SpatialIndirectStudy_20261026.unity` на основе
Meadow-стенда с native grass, дорожкой и Oak A/B/C/D. Пространственное поле
49×9×49 проб, шаг1m,128 лучей/пробу, длина6m,6 упрощённых AABB. Перекрытие неба
и один diffuse bounce от солнца/луны/окружения; геометрический sky transport
кешируется по surface cell0.5m. Собственная C#/HLSL реализация; внешний код
Tiny Glade/kajiya не копировался. Полного renderer Tiny Glade в проверенных
публичных источниках не найдено, художественное совпадение не заявляется.

Изменены по функциям7 существующих shader/include файлов: common, solid,
terrain и grass/foliage пары. Новый include `LC_SpatialIndirect.hlsl` заменяет
только hemisphere ambient до прежнего AO/tint. Target4.5 требуется лишь
`_LC_SPATIAL_INDIRECT` варианту. GI-off использует исходное выражение ambient
без промежуточного half3 преобразования. Depth/Shadow/Normals passes, alpha,
wind, interaction, instancing, материал CBUFFER и маски сохранены. Новый код:
SpatialIndirectTrace/Cache/Volume/StudyReview, editor builder,8 GI Edit-тестов,
отдельная сцена и четыре диагностических материала; с meta25 новых ассет-файлов.

**186/186 Edit Mode,6/6 Play Mode**, финальная сборка0 ошибок/0 предупреждений.
27 реальных PNG в `Research/SpatialIndirect_20261026/FinalPrecision`: день,
закат, ночь, исходный ambient/GI, отражатель вне frustum, крыша/снос/Undo,
тонкая стенка с guard и без,12 кадров движения камеры. Runtime-errors пуст.
Удаление крыши повышает sample RGB с(0.0322,0.0401,0.0501) до
(0.1713,0.2185,0.3045); Undo воспроизводит прежнее поле. Отражатель вне кадра
меняет красный канал стены0.1001→0.1144. Камера не меняет мировые коэффициенты.
Visibility guard исключает query-to-probe сегменты через стену, но не доказывает
отсутствия всех light leaks/тёмных ячеек на сложных моделях.

Старая Meadow-сцена также запущена в новом Player с GI keyword off:76 PNG и
road/Undo/native instancing/LOD assertions прошли. Для контроля отдельно
повторён старый неизменённый Linear Player.9 HDR PNG совпали точно;3 близких
вида дубов отличаются максимум1/255 на17/85/98 пикселях — меньше разброса
повтора старого Player. В64 AO кадрах ошибка сопоставима с URP blue-noise
повтором, до~0.33/255 средней ошибки. JSON содержит сырые ошибки и критерии;
это не76 точных совпадений. Начальные strict-fail отчёты сохранены, подробности
и ограничения проверки указаны в новом GI-отчёте.

Финальный парный benchmark: Release, Linear/HDR/Neutral, D3D12,1920×1080,
RTX4070 Ti SUPER/i7-12700F, SSAO, MSAA4,2048 shadowmap/2cascades/90m.
120 warmup+600 обычных кадров на режим, VSync/FPS limit off; PNG/readback и
пересчёта кеша в замере нет. GPU mean0.840ms off (502 samples),1.184ms on
(557 samples), разница~0.344ms. Frame median/p95/p99:1.187/2.824/3.205ms off,
1.350/2.210/2.940ms on. Это маленький стенд и один парный прогон; выбросы
CPU/p95 не приписывать эффекту GI, не обещать FPS большой карты.

Цена обновления отдельно: initial21609 проб —1815.6ms solve+15.0ms upload,
14.95s до сходимости. Reflector commit8019 проб —691.3+5.6ms,5.64s;
снос крыши9801 проб —709.1+5.9ms,5.79s; Undo6.87s. GPU buffer1382976bytes,
локальные uploads513216/627264bytes, не полный буфер. Бюджет solve2ms
кооперативный, максимум solve+upload одного кадра в этом прогоне2.962ms.
После commit инвалидируются старые/новые bounds с halo=2R+0.52m; preview
игнорируется, очередь dedup, stale revision отбрасывается. Сравнение локального
обновления с полным пересчётом проверяется тестом. Нет production map adapter,
moving volume, ускоряющей структуры, плавного смешивания версий или автоматической
injection при непрерывном ходе суток. Сходимость в секунды не годится для
включения по умолчанию; snapshot surface dictionary пока заменяется целиком,
хотя irradiance/GPU пересчитываются локально. При смене света sky transport
переиспользуется, но обновляется всё активное bounded поле.

Повторно прочитаны свежие передача и MapEditSession в URP-копии. Там уже есть
`Surface|Decoration|Height|Objects` и Save v4; старое описание Surface|Decoration
выше относится к прежнему снимку. Событие по-прежнему содержит XZ Rect/preview,
но не transaction revision. Контракт обновлён в `local-cache-invalidation-ru.md`.
Файлы нового редактора не переносились, его приложения/данные не управлялись.
Плоский GI ground proxy ещё не соответствует его новому редактируемому рельефу;
совместная проверка на hills/pits и новых object records остаётся следующим
интеграционным этапом. Задерживать разработку редактора ожиданием GI не требуется.

Точные исходные/current SHA256 и список изменений:

- `E:/Games_Develop/Little-Castle_LightingIntegration_20261025/docs/lighting-integration/spatial-indirect-manifest.json`.
- Исходный Meadow manifest SHA256: `d70ef3e9b315cbda1f58a736c4a72838924ae7053d8650c7be23958c3cd82049`.
- Frozen pre-GI manifest SHA256: `4fc4e7ca9a57284572ac3c2d56df3e86983e6b70b26e69fc76931060f249069e`.
- `spatial-indirect-audit.json`: frozen/settings/MAIN Assets Packages Settings drift,
  protected passes/functions, snapshot integrity и новые GUID collisions — пусто.
  Шесть старых map source-файлов изменены другим чатом; их snapshot не применять
  поверх редактора. Актуальные map contract/shared shader SHA сохранены read-only.
- Shader delta: `docs/lighting-integration/meadow-baseline-to-spatial-indirect.patch`.
- Отчёт: `docs/reports/spatial-indirect-prototype-20261026-ru.md` и экспериментальное
  `docs/decisions/ADR-lighting-spatial-indirect-experiment-20261026-ru.md` в lighting-копии.
- Локальный пакет: `E:/Games_Develop/Research/SpatialIndirect_20261026/spatial-indirect-integration-20261026.zip`;
  manifest файлов, receipt Player/source и полные evidence рядом. Пакет — локальный
  delta к9e8c126, не автоматический перенос в MAIN/редактор и не новая публикация.

Следующий измеримый шаг — ускорить и разделить geometry transport/injection,
добавить приоритет видимых проб и плавную публикацию; затем согласованно
проверить world-space geometry snapshot нового рельефа/объектов. Производственный
GI этим экспериментом не принят. Мягкий общий Meadow baseline остаётся доступен.

## Публикация GI и следующий план — 26.10.2026

По запросу пользователя GI-пакет этого прохода публикуется отдельно от прежнего
baseline: ветка `codex/lighting-spatial-gi-2026-10-26`, родитель9e8c126.
Каталог GitHub: `docs/integration-packages/spatial-indirect-20261026/packet/`.
Это все82 файла замороженного локального пакета, включая код/шейдеры/GUID,
диагностическую сцену, материалы, патч, source manifests, XML тестов, измерения,
контрольные fail/pass evidence и3 сравнения настоящих Player кадров. Исходный ZIP
SHA256: `b19f88a43334f97d92a42029ff1d5c9b5ab824268b86ef3edc3f65b577d18980`.
Сторонние бинарные модели/текстуры и Unity Player остаются локальными; точные
зависимости и Player hashes включены. Frozen README сохраняет статус снимка на
момент упаковки; текущая публикация описана внешним README и publication manifest.

План следующих этапов: `docs/plans/lighting-next-stages-20261026-ru.md` в lighting
копии и в GitHub. Порядок:1) профиль/ускорение geometry transport и lighting injection;
2) приоритет видимых проб, согласованные версии и плавные сутки;3) актуальные
рельеф/объекты через committed snapshots;4) художественный проход Tiny Glade,
SSAO/rootAO, устойчивые тени и читаемая ночь;5) плотное поселение/память/более
слабое железо и отдельное решение об активации. Целевые latency-пороги в плане
помечены как исследовательские, не достигнутые результаты.

Начать следующую реализацию с этапа1 на той же сцене: сохранить качество reference,
показать before/after для прежних8019/9801-пробных правок, измерить GC/память и
не скрывать ускорение увеличением CPU-бюджета. Новый редактор продолжает работать
независимо. Общие shaders переносить по функциям; production настройки/каталоги
и рабочую URP-копию автоматически не менять.

Точная связь исходного ZIP и Git-путей — `publication-manifest.json` у нового пакета.
Проверка опубликованного commit/tree и каждого blob сохраняется локально в
`publication-spatial-gi/publication-receipt.json` после успешной загрузки.

## Этап1: ускорение кеша GI — 26.10.2026

Родитель — опубликованный100c1745924493d2d8be0f29b5541c695273ed6e.
Разделены geometry transport и lighting injection, сохраняются точные primary
hit links и L1-веса. Colour/light edits не повторяют поиск primary. Geometry
commit сохраняет незатронутый surface cache; primary rebuild вR, secondary replay
в оставшейся2R+0.52m области. Stable IDs переживают compaction массива proxy.
Добавлены exact geometry bounds identity и conservative margin от float roundoff.

Финальная версия:193/193 Edit Mode,6/6 Play Mode;3 Release пары с чередованием
порядка.24 полных поля по21609 проб, max coefficient error5.0664e-07
при tolerance1e-5;27 настоящих image pairs, max MAE0.267/255
с обычным SSAO noise. Все успешные Players exit0, runtime-errors пусты.
Первый парный прогон содержит54 PNG, остальные2 пары без PNG с полными CPU dumps.

Median convergence: initial: 14.183→4.639s; albedo: 5.332→0.031s; roof removal: 5.617→1.515s; Undo: 6.585→1.950s; sunset: 13.385→0.700s; night: 12.951→0.651s; day: 13.410→0.668s.
RTX4070 Ti SUPER/i7-12700F,1920×1080,D3D12,Linear/HDR/MSAA4/SSAO, тот же fixture.
Solve budget2ms, safety cap cached8192/reference256; commit/upload отдельно.
Steady120warmup+600 обычных кадров на каждый режим, без PNG/dumps в измерениях.
Это маленький стенд; точные min/max, CPU, GPU samples/frame percentiles в summary.

Transport payload8786312bytes (~8.38MiB) плюс overhead;
GPU buffer прежний1382976bytes. Release bytes-counter не прошёл positive4096-byte
control и помечен unsupported/-1. Отдельный Development Recorder GC.Alloc main
thread откалиброван: warm colour/time/Undo solver0 calls, cold fill/roof removal
и proxy command выделяют память. Setup/cold/command counts, managed heap phase
boundaries и OS peak working set записаны отдельно. Это не zero-GC всей игры;
VRAM peak отдельно не измерен. Скрытый Development без camera frames исключён,
остановлен только собственный PID; AllocationFinal в видимом окне PASS.

Изменены3 research runtime-файла Trace/Volume/StudyReview. Добавлены5 source с
meta: Profile, Transport, AllocationCounter, Stage1Build, TransportTests. Shared7
shaders, Scene/private materials, старый builder/cache queue и3 ProjectSettings
сохранены; MAIN Assets/Packages/Settings не менялись. Рабочая копия карты не
редактировалась. GUID новых файлов проверены без collisions.
Before snapshot11 файлов: docs/lighting-integration/stage1-baseline-100c174/.
Точные SHA/current и проверки: stage1-manifest.json/stage1-audit.json;
delta: gi-100c174-to-stage1.patch в том же каталоге lighting-копии.

Отчёт: docs/reports/lighting-gi-cache-stage1-20261026-ru.md.
Артефакты: E:/Games_Develop/Research/SpatialIndirectStage1_20261026:
stage1-summary.json, Final01..03*, AllocationFinal, JSON/XML/bin/PNG/Players.
Source/evidence packet: docs/integration-packages/lighting-gi-cache-stage1-20261026/,
ветка codex/lighting-gi-cache-stage1-2026-10-26. Крупные local-only артефакты и
compiled Players индексированы SHA256; их бинарники не публикуются автоматически.
План следующих этапов обновлён фактическим результатом.

Геометрические~1.5–2s и initial~4.6s ещё не соответствуют0.5s цели. Следующий
проход1Б: primary/shadow kernels и приоритет видимых проб; затем плавные версии,
непрерывные сутки и snapshot нового рельефа/объектов. Fixed grid/L1/flat AABB,
canopy/emitters/production activation остаются открытыми. Редактор не блокируется.

## Этап1Б: ускорение kernels GI — 26.10.2026

Ускорены primary intervals и any-hit shadow/sky visibility, precomputed72 sky
directions, exact-zero source gating с корректным zero→nonzero epoch.
197/197 Edit Mode,6/6 Play Mode,3 Release пары;24 full fields совпали побитово
с опубликованным cached Stage1, serial error5.0664e-7. Та же сцена/лучи/2ms budget.
Median:initial4.701→2.765s;roof removal1.532→0.831s;Undo1.955→0.934s;
sunset0.701→0.335s;night0.651→0.300s;day0.684→0.301s. Albedo~31ms как прежде.
Transport payload прежний~8.38MiB плюс overhead; static directions+1728B.
Отчёт:`docs/reports/lighting-gi-kernels-stage1b-20261026-ru.md`.

Следующий шаг1В — **приоритет видимых проб и стандартизированная малая правка**:
измерить first visible response/visible convergence отдельно от полного поля;
сохранить starvation bound для невидимой области и stale-work gate. Полная roof
сходимость ещё выше0.5s; не поднимать2ms budget ради целевого показателя.
После этого этап2:согласованные версии, плавное обновление и continuous WorldTime.

### Следующий визуальный стенд: готовые модели и диагностическая зона

Уточнение пользователя26.10:в будущем поставить часть готовых моделей для живой
оценки света, **оставив куб со стенами** для экспериментов. Добавлять это отдельным
сценовым изменением после текущего A/B, сохранив прежний benchmark без изменений.
Диагностическая зона:куб/тонкая стена/крыша/отражатель. Вторая зона:несколько
согласованных production моделей,разные камень/дерево/крыша и крупные/мелкие формы;
approved Oaks A/B/C/D и Meadow остаются частью окружения. Временный WorldSpawnCatalog
ради стенда не мигрировать. Выбор конкретных ещё не утверждённых моделей фиксировать
перед переносом, с GUID/SHA и source snapshot; реальные сетки проверять по AGENTS.
Общий набор day/sunset/readable night и cameras close/overview;GI/AO toggles,
ветер/LOD и shadow alignment. Не подменять этими моделями чистый кубический тест.
Это план, в пакете1Б новые модели не переносились.


Before/current SHA, audit и patch:stage1b-manifest.json,stage1b-audit.json,
gi-98da069-to-stage1b.patch в docs/lighting-integration lighting-копии.
Новых Meadow/mesh зависимостей нет, GUID существующих C# сохранены,7shared shaders,
сцена, материалы и3ProjectSettings неизменны; MAIN game files без drift.
Raw evidence:E:/Games_Develop/Research/SpatialIndirectStage1B_20261026.
Отдельный пакет:docs/integration-packages/lighting-gi-kernels-stage1b-20261026/;
ветка codex/lighting-gi-kernels-stage1b-2026-10-26. Публикация подтверждается
локальным publication-gi-stage1b/publication-receipt.json после remote проверки.
URP editor не менялся и не ожидает GI. Его новый height/tree snapshot ещё не перенесён.

## Этап1В: приоритет GI — 26.10.2026

Очередь frustum-support priority (запас1.8*spacing+.08m),3видимых:1global FIFO,
без дублей, со старым revision gate; camera refresh не сбрасывает fairness.
Классификация внутри прежнего2ms budget.203 Edit+6Play,3 Release FIFO/Priority пары
на одном Player +serial run.36 fields max FIFO/Priority error0,
serial5.0663948e-07;24 field regressions к1Б без изменения результата.
Куб0.8m,move0.5m: Priority support convergence384.8ms,
полная566.5ms. Roof support551.2ms,
полная898.8ms. Это conservative frustum-support/render metrics,
не pixel/input-to-display latency. Queue delta~281KB; solve quality не снижено.
Отчёт:`docs/reports/lighting-gi-priority-stage1c-20261026-ru.md`.

Далее **этап2: версия результата и плавное обновление**. Buffer сейчас in-place,
разный возраст проб возможен. Проверить commit/Undo/load во время pending work,
camera motion, continuous WorldTime и bounded work без starvation. Не повторять
готовый этап1В и не объявлять решённым pixel/temporal качество по latency queues.
После этого — актуальный terrain/object snapshot и готовые модели в отдельной
визуальной зоне. Диагностические куб,стены,крыша остаются; shaders/палитра в1В прежние.


Before/current SHA+GUID, audit и patch в lighting-копии:stage1c-manifest.json,
stage1c-audit.json,gi-d1d92e0-to-stage1c.patch. Новых Meadow зависимостей нет;
Trace/Transport,7shared shaders,scene/materials,3settings unchanged; MAIN game drift0.
Другая URP копия и apps не изменялись. Source/evidence packet:
docs/integration-packages/lighting-gi-priority-stage1c-20261026/.
Публикация нового пакета подтверждается publication-gi-stage1c/publication-receipt.json
после отдельной remote проверки; исходный1Б опубликованd1d92e0.


## Текущий приоритет после этапа2А — 27.10.2026

Плавные сутки проверены:360s день+90s ночь на общем WorldTimeSystem, без geometry
rebuild. Coherent current/previous snapshots и0.2s fade уменьшают скачки света,
но whole-field gate добавляет задержку, а geometry barrier — возможную тёмную паузу.
Это исследовательский кандидат, не default production activation.210 Edit+6Play,
3core пары,12полей mode error0; отдельные1x/8xcycles и3пары реальной strategy camera.
Отчёт: `docs/reports/lighting-gi-temporal-stage2a-20261027-ru.md`.

Основная приёмка теперь **сверху, close/mid/far, с родными LOD**. Roof/wall interior
остаётся диагностикой утечек. Тест11..190m выявил: средняя дистанция может быть
тяжелее близкой, после115m тени90m не покрывают землю; distance fog вымывает дальний
цвет. Ground-relative fog проверен только как опциональный runtime A/B; не принят.

Следующий конкретный проход:
1. Прочитать актуальную передачу map-authoring и снять выборочный committed
   terrain/object snapshot с GUID/SHA, не меняя другую рабочую копию.
2. Согласовать height sampling + committed old/new bounds/chunks; derived caches
   обновлять локально и асинхронно относительно команд/preview/undo/save/load.
3. Новый живой визуальный стенд: рельеф/дороги/Meadow/Oak A–D, отдельная зона
   выбранных готовых моделей; куб/стены/крыша остаются. WorldSpawnCatalog не менять.
4. A/B дальние тени/туман/палитра и SSAO/rootAO на day/sunset/readable night;
   совместить непрерывные сутки с zoom/pan и локальной правкой. Детали под крышей
   не должны поглощать бюджет основной стратегической картинки.
5. После оценки видимой пользы GI решить, нужна ли chunk/region publication для
   снижения задержки вместо текущего whole-field gate; reference и stale gate сохранить.

Новый пакет не переносит terrain/models и не блокирует инструменты карты.
Ниже исторический план; завершённые этапы1/1Б/1В и проверка2А не повторяются.

Source manifest/audit/patch: lighting integration docs/lighting-integration/stage2a-*.
Core evidence:E:/Games_Develop/Research/SpatialIndirectStage2A_20261026.
Game-view evidence:E:/Games_Develop/Research/LightingGameViews_20261026.
Пакет:docs/integration-packages/lighting-gi-temporal-stage2a-20261027/.
Предыдущий1В опубликован и проверен: f4ae7b19e70bcd9a53f8ce7eea84d97ef195d542.
Статус нового пакета определяется publication-gi-stage2a/publication-receipt.json,
не наличием локального commit. Новых Meadow зависимостей нет; исходный f4ae7b1
заморожен в stage2a-baseline-f4ae7b1; все asset SHA/GUID в stage2a-manifest.json.
MAIN game assets/settings unchanged, другая URP рабочая копия read-only.
