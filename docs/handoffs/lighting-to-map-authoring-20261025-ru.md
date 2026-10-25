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
